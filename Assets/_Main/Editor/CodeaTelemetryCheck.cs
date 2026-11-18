using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools → Codea → Validar un JSON de telemetría: comprueba un archivo contra las reglas del
/// formato 1 (TELEMETRIA.md, sección "Reglas que se cumplen siempre"). La usa también
/// CodeaPlayCheck al final de cada pasada.
///
/// Comprueba lo que el dashboard va a comprobar, para verlo aquí antes:
///   · formatVersion = 1, appVersion, endReason con un valor válido
///   · seq: único en toda la sesión, creciente en cada lista
///   · totalSeconds = endedUtc − challengeStartedUtc − pausedSeconds (±0,1 s)
///   · ningún evento antes de challengeStartedUtc ni después de endedUtc
///   · ningún durationSeconds mayor que el totalSeconds de su escenario
///   · failedAttempts, wrongSelections y wrongPlacements cuadran con sus listas
///   · failReason vacío solo en las ejecuciones resueltas; moved y phaseCompleted coherentes
///   · modulesRequired, itemsRequired y socketsRequired presentes en los escenarios empezados
/// </summary>
public static class CodeaTelemetryCheck
{
    private const double Tolerance = 0.1;

    private static readonly string[] RunReasons = { EndReasons.Completed, EndReasons.Quit, EndReasons.AppClosed };

    private static readonly string[] ScenarioReasons =
        { EndReasons.NotStarted, EndReasons.Completed, EndReasons.TimedOut, EndReasons.Abandoned, EndReasons.AppClosed };

    private static readonly string[] FailReasonValues =
        { FailReasons.Collision, FailReasons.InvalidUse, FailReasons.IncompleteSequence, FailReasons.Other };

    private static readonly string[] PauseReasonValues =
        { PauseReasons.HeadsetRemoved, PauseReasons.Menu, PauseReasons.FocusLost };

    // Campos que el contrato pide siempre presentes. JsonUtility rellena los que falten con su
    // valor por defecto al leer, así que la presencia se mira en el texto.
    private static readonly string[] RootKeys =
        { "formatVersion", "appVersion", "pin", "deviceId", "sessionId", "difficulty", "startedUtc", "endedUtc", "endReason", "totalSeconds" };

    private static readonly string[] ScenarioKeys =
        { "started", "completed", "timedOut", "endReason", "startedUtc", "challengeStartedUtc", "endedUtc", "totalSeconds", "pausedSeconds", "pauses" };

    [MenuItem("Tools/Codea/Validar un JSON de telemetría", false, 40)]
    private static void ValidateMenu()
    {
        string path = EditorUtility.OpenFilePanel("JSON de telemetría", Application.persistentDataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        List<string> problems = Check(File.ReadAllText(path));

        if (problems.Count == 0)
            Debug.Log($"[Codea] {Path.GetFileName(path)}: cumple el formato 1.");
        else
            Debug.LogError($"[Codea] {Path.GetFileName(path)}: {problems.Count} problema(s) con el formato 1:\n  · " +
                           string.Join("\n  · ", problems));
    }

    /// <summary>Lista de incumplimientos del formato 1. Vacía si el JSON es correcto.</summary>
    public static List<string> Check(string json)
    {
        var problems = new List<string>();

        RunRecord run;
        try { run = JsonUtility.FromJson<RunRecord>(json); }
        catch (Exception e)
        {
            problems.Add($"no se puede leer el JSON: {e.Message}");
            return problems;
        }

        if (json.Contains(": null")) problems.Add("hay valores null");

        foreach (string key in RootKeys.Concat(ScenarioKeys))
            if (!json.Contains($"\"{key}\"")) problems.Add($"falta el campo {key}");

        if (run.formatVersion != TelemetryManager.FormatVersion) problems.Add($"formatVersion {run.formatVersion}");
        if (string.IsNullOrEmpty(run.appVersion)) problems.Add("appVersion vacío");

        bool closed = !string.IsNullOrEmpty(run.endedUtc);
        if (closed && !RunReasons.Contains(run.endReason)) problems.Add($"endReason de la raíz '{run.endReason}'");
        if (!closed && run.endReason != EndReasons.Open) problems.Add($"raíz sin endedUtc y con endReason '{run.endReason}'");

        // seq único en toda la sesión
        var seqs = new List<int>();
        seqs.AddRange(run.escenario1.attempts.Select(e => e.seq));
        seqs.AddRange(run.escenario2.selections.Select(e => e.seq));
        seqs.AddRange(run.escenario2.attempts.Select(e => e.seq));
        seqs.AddRange(run.escenario3.attempts.Select(e => e.seq));
        seqs.AddRange(run.escenario4.placements.Select(e => e.seq));

        if (seqs.Any(s => s <= 0)) problems.Add("hay eventos con seq ≤ 0");
        int repeated = seqs.Count - seqs.Distinct().Count();
        if (repeated > 0) problems.Add($"{repeated} seq repetido(s)");

        CheckScenario(problems, "escenario1", run.escenario1, closed,
                      run.escenario1.attempts.Select(e => (e.seq, e.timestamp, e.durationSeconds)));
        CheckScenario(problems, "escenario2", run.escenario2, closed,
                      run.escenario2.selections.Select(e => (e.seq, e.timestamp, e.durationSeconds))
                         .Concat(run.escenario2.attempts.Select(e => (e.seq, e.timestamp, e.durationSeconds))));
        CheckScenario(problems, "escenario3", run.escenario3, closed,
                      run.escenario3.attempts.Select(e => (e.seq, e.timestamp, e.durationSeconds)));
        CheckScenario(problems, "escenario4", run.escenario4, closed,
                      run.escenario4.placements.Select(e => (e.seq, e.timestamp, e.durationSeconds)));

        CheckIncreasing(problems, "escenario1.attempts", run.escenario1.attempts.Select(e => e.seq));
        CheckIncreasing(problems, "escenario2.selections", run.escenario2.selections.Select(e => e.seq));
        CheckIncreasing(problems, "escenario2.attempts", run.escenario2.attempts.Select(e => e.seq));
        CheckIncreasing(problems, "escenario3.attempts", run.escenario3.attempts.Select(e => e.seq));
        CheckIncreasing(problems, "escenario4.placements", run.escenario4.placements.Select(e => e.seq));

        // Escenario 1
        Scenario1Record s1 = run.escenario1;
        int failed1 = s1.attempts.Count(a => !a.solved);
        if (s1.failedAttempts != failed1) problems.Add($"escenario1: failedAttempts {s1.failedAttempts} y {failed1} ejecuciones sin resolver");
        foreach (AttemptRecord a in s1.attempts)
        {
            if (a.solved && a.failReason != FailReasons.None) problems.Add($"escenario1 seq {a.seq}: resuelta con failReason '{a.failReason}'");
            if (!a.solved && !FailReasonValues.Contains(a.failReason)) problems.Add($"escenario1 seq {a.seq}: failReason '{a.failReason}'");
        }
        if (s1.attempts.Count(a => a.solved) > 1) problems.Add("escenario1: más de una ejecución resuelta");

        // Escenario 2
        Scenario2Record s2 = run.escenario2;
        int wrong2 = s2.selections.Count(e => e.selected == 1 && !e.correct);
        if (s2.wrongSelections != wrong2) problems.Add($"escenario2: wrongSelections {s2.wrongSelections} y {wrong2} selecciones incorrectas");
        int failed2 = s2.attempts.Count(a => !a.solved);
        if (s2.failedAttempts != failed2) problems.Add($"escenario2: failedAttempts {s2.failedAttempts} y {failed2} Probar sin reparar");
        if (s2.selections.Any(e => e.selected != 0 && e.selected != 1)) problems.Add("escenario2: selected distinto de 0 y 1");
        if (s2.attempts.Any(a => a.selection.Count == 0)) problems.Add("escenario2: Probar sin nada marcado");
        if (s2.started && s2.modulesRequired.Count == 0) problems.Add("escenario2: modulesRequired vacío");

        // Escenario 3
        Scenario3Record s3 = run.escenario3;
        if (json.Contains("\"failedAttempts\"") && CountKey(json, "failedAttempts") != 2)
            problems.Add("failedAttempts aparece fuera de los escenarios 1 y 2");
        if (s3.started && s3.itemsRequired.Count == 0) problems.Add("escenario3: itemsRequired vacío");
        foreach (LoopAttemptRecord a in s3.attempts)
        {
            bool typed = !string.IsNullOrEmpty(a.itemType);
            if (typed && a.remaining < 0) problems.Add($"escenario3 seq {a.seq}: con tipo y remaining {a.remaining}");
            if (!typed && (a.remaining != -1 || a.moved != -1)) problems.Add($"escenario3 seq {a.seq}: sin tipo y remaining/moved distintos de -1");
            if (typed && a.remaining >= 0 && (a.moved < 0 || a.moved > a.remaining)) problems.Add($"escenario3 seq {a.seq}: moved {a.moved} con remaining {a.remaining}");
            if (a.phaseCompleted != 0 && a.phaseCompleted != 1) problems.Add($"escenario3 seq {a.seq}: phaseCompleted {a.phaseCompleted}");
            if (a.phaseCompleted == 1 && a.moved != a.remaining) problems.Add($"escenario3 seq {a.seq}: phaseCompleted sin mover todo lo que quedaba");
        }
        if (s3.attempts.Count(a => a.solved) > 1) problems.Add("escenario3: más de una ejecución resuelta");
        if (s3.completed && s3.attempts.Count > 0 && !s3.attempts.Last().solved) problems.Add("escenario3: completado y la última ejecución sin solved");

        // Escenario 4
        Scenario4Record s4 = run.escenario4;
        int wrong4 = s4.placements.Count(e => !e.correct);
        if (s4.wrongPlacements != wrong4) problems.Add($"escenario4: wrongPlacements {s4.wrongPlacements} y {wrong4} colocaciones incorrectas");
        if (s4.chipsReleased > s4.chipsGrabbed) problems.Add($"escenario4: chipsReleased {s4.chipsReleased} > chipsGrabbed {s4.chipsGrabbed}");
        if (s4.started && s4.socketsRequired.Count == 0) problems.Add("escenario4: socketsRequired vacío");

        return problems;
    }

    private static void CheckScenario(List<string> problems, string id, ScenarioRecord s, bool runClosed,
                                      IEnumerable<(int seq, string timestamp, float duration)> events)
    {
        bool open = s.endReason == EndReasons.Open;

        if (!open && !ScenarioReasons.Contains(s.endReason)) problems.Add($"{id}: endReason '{s.endReason}'");
        if (open && (!s.started || runClosed)) problems.Add($"{id}: sin endReason {(runClosed ? "con la run cerrada" : "sin haber empezado")}");
        if (!s.started && s.endReason != EndReasons.NotStarted) problems.Add($"{id}: no empezado y con endReason '{s.endReason}'");
        if (s.completed && s.endReason != EndReasons.Completed) problems.Add($"{id}: completed con endReason '{s.endReason}'");
        if (s.timedOut && s.endReason != EndReasons.TimedOut) problems.Add($"{id}: timedOut con endReason '{s.endReason}'");
        if (s.completed && s.timedOut) problems.Add($"{id}: completed y timedOut a la vez");

        DateTime? started = Parse(s.startedUtc), challenge = Parse(s.challengeStartedUtc), ended = Parse(s.endedUtc);
        if (s.started && !started.HasValue) problems.Add($"{id}: empezado sin startedUtc");
        if (challenge.HasValue && started.HasValue && challenge < started) problems.Add($"{id}: challengeStartedUtc antes de startedUtc");
        if (!open && s.started && !ended.HasValue) problems.Add($"{id}: cerrado sin endedUtc");

        if (s.pauses.Any(p => !PauseReasonValues.Contains(p.reason))) problems.Add($"{id}: pausa con reason desconocido");

        double paused = 0d;
        foreach (PauseRecord p in s.pauses)
        {
            DateTime? a = Parse(p.startUtc), b = Parse(p.endUtc);
            if (!a.HasValue) { problems.Add($"{id}: pausa sin startUtc"); continue; }
            if (b.HasValue) paused += (b.Value - a.Value).TotalSeconds;
            else if (!open) problems.Add($"{id}: cerrado con una pausa sin endUtc");
        }
        if (s.pauses.All(p => Parse(p.endUtc).HasValue) && Math.Abs(paused - s.pausedSeconds) > Tolerance)
            problems.Add($"{id}: pausedSeconds {s.pausedSeconds:0.00} y las pausas suman {paused:0.00}");

        if (challenge.HasValue && ended.HasValue)
        {
            double formula = (ended.Value - challenge.Value).TotalSeconds - s.pausedSeconds;
            if (Math.Abs(formula - s.totalSeconds) > Tolerance)
                problems.Add($"{id}: totalSeconds {s.totalSeconds:0.00} y endedUtc − challengeStartedUtc − pausedSeconds = {formula:0.00}");
        }

        foreach ((int seq, string timestamp, float duration) in events)
        {
            DateTime? at = Parse(timestamp);
            if (!at.HasValue) { problems.Add($"{id} seq {seq}: sin timestamp"); continue; }
            if (!challenge.HasValue) problems.Add($"{id} seq {seq}: evento sin reto abierto");
            else if (at < challenge) problems.Add($"{id} seq {seq}: {(challenge.Value - at.Value).TotalSeconds:0.000} s antes de challengeStartedUtc");
            if (ended.HasValue && at > ended) problems.Add($"{id} seq {seq}: después de endedUtc");
            if (duration < 0f || duration > s.totalSeconds + Tolerance) problems.Add($"{id} seq {seq}: durationSeconds {duration:0.00} con totalSeconds {s.totalSeconds:0.00}");
        }
    }

    private static void CheckIncreasing(List<string> problems, string list, IEnumerable<int> seqs)
    {
        int last = 0;
        foreach (int seq in seqs)
        {
            if (seq <= last) problems.Add($"{list}: seq {seq} después de {last}");
            last = seq;
        }
    }

    private static int CountKey(string json, string key)
    {
        int count = 0, index = 0;
        string token = $"\"{key}\"";
        while ((index = json.IndexOf(token, index, StringComparison.Ordinal)) >= 0) { count++; index += token.Length; }
        return count;
    }

    private static DateTime? Parse(string utc) =>
        !string.IsNullOrEmpty(utc) && DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value)
            ? value.ToUniversalTime()
            : (DateTime?)null;
}
