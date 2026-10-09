using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Tools → Codea → Probar las dos dificultades en Play: juega Juego.unity en básica y en
/// intermedia sin visor y comprueba lo que de verdad queda montado.
///
/// Por cada dificultad: carga la escena forzando la dificultad, enciende las salas para que
/// despierten, comprueba huecos, bloques de la fila, módulos, figuras, narración y telemetría,
/// y recorre el Director entero saltando pasos (con eso se ejecuta cada evento de cada paso).
/// Se detiene ANTES del paso que sube el JSON o vuelve a Setup.
///
/// No deja rastro: la subida apunta a una dirección local mientras dura la prueba, las
/// PlayerPrefs del editor (PIN, contador de sesiones, dificultad) se restauran al terminar y
/// los JSON que escribe la prueba se borran. El informe va a Logs/Codea_PruebaPlay.txt.
/// Sin Unity abierto (sin -quit: la prueba sale sola):
/// Unity.exe -batchmode -projectPath . -executeMethod CodeaPlayCheck.RunBatch
/// </summary>
[InitializeOnLoad]
public static class CodeaPlayCheck
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string ReportPath = "Logs/Codea_PruebaPlay.txt";

    private const string KeyQueue = "CodeaPlayCheck.Queue";
    private const string KeyStage = "CodeaPlayCheck.Stage";
    private const string KeyCurrent = "CodeaPlayCheck.Current";
    private const string KeyReport = "CodeaPlayCheck.Report";
    private const string KeyBatch = "CodeaPlayCheck.Batch";
    private const string KeyErrors = "CodeaPlayCheck.Errors";
    private const string KeyPrefs = "CodeaPlayCheck.Prefs";
    private const string KeyFiles = "CodeaPlayCheck.Files";

    private static readonly string[] PrefKeys = { "telemetry_session_counter", "telemetry_pin_counter", "telemetry_difficulty" };

    // Estado de la pasada en curso. Se pierde al recargar el dominio, por eso las fases se
    // deciden por tiempo y lo que hay que conservar va a SessionState.
    private static double playStarted = -1;
    private static int phase;
    private static double nextSkip;
    private static readonly StringBuilder pass = new StringBuilder();
    private static readonly List<string> logs = new List<string>();
    private static int errors;

    static CodeaPlayCheck()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(KeyStage, ""))) return;

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Codea/Probar las dos dificultades en Play")]
    private static void RunMenu()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Begin(false);
    }

    public static void RunBatch() => Begin(true);

    private static void Begin(bool batch)
    {
        SessionState.SetString(KeyQueue, $"{(int)Difficulty.Basica},{(int)Difficulty.Avanzada}");
        SessionState.SetString(KeyReport, $"Prueba en Play de {GameScene} · {DateTime.Now:dd/MM/yyyy HH:mm}\n");
        SessionState.SetBool(KeyBatch, batch);
        SessionState.SetInt(KeyErrors, 0);
        SessionState.SetString(KeyFiles, "");
        SessionState.SetString(KeyPrefs, string.Join("\n", PrefKeys.Select(k => PlayerPrefs.HasKey(k) ? $"{k}={PlayerPrefs.GetInt(k)}" : $"{k}=")));

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Next();
    }

    private static void Next()
    {
        List<string> queue = SessionState.GetString(KeyQueue, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (queue.Count == 0)
        {
            Finish();
            return;
        }

        Difficulty difficulty = (Difficulty)int.Parse(queue[0]);
        SessionState.SetString(KeyQueue, string.Join(",", queue.Skip(1)));
        SessionState.SetInt(KeyCurrent, (int)difficulty);

        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        DifficultyApplier applier = Object.FindAnyObjectByType<DifficultyApplier>(FindObjectsInactive.Include);
        if (applier == null)
        {
            Append($"\n== {Label(difficulty)}\n  ✗ No hay DifficultyApplier en la escena\n");
            SessionState.SetInt(KeyErrors, SessionState.GetInt(KeyErrors, 0) + 1);
            Next();
            return;
        }

        // Solo en memoria: la escena no se guarda, y Finish la vuelve a abrir desde disco.
        SerializedObject so = new SerializedObject(applier);
        so.FindProperty("editorDifficulty").enumValueIndex = difficulty == Difficulty.Basica ? 1 : 2;
        so.ApplyModifiedPropertiesWithoutUndo();

        SessionState.SetString(KeyStage, "entering");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        string stage = SessionState.GetString(KeyStage, "");

        if (stage == "entering" && EditorApplication.isPlaying)
        {
            SessionState.SetString(KeyStage, "playing");
            playStarted = EditorApplication.timeSinceStartup;
            phase = 0;
            pass.Clear();
            errors = 0;
            return;
        }

        if (stage == "playing" && EditorApplication.isPlaying)
        {
            if (playStarted < 0) playStarted = EditorApplication.timeSinceStartup;
            Play(EditorApplication.timeSinceStartup - playStarted);
            return;
        }

        if (stage == "exiting" && !EditorApplication.isPlaying)
        {
            SessionState.SetString(KeyStage, "next");
            EditorApplication.delayCall += Next;
        }
    }

    // --- Lo que pasa dentro de Play ---

    private static void Play(double seconds)
    {
        Difficulty difficulty = (Difficulty)SessionState.GetInt(KeyCurrent, 0);

        try
        {
            if (phase == 0 && seconds > 1.5)
            {
                phase = 1;
                pass.AppendLine($"\n== {Label(difficulty)}");
                CheckLoaded(difficulty);
                WakeRooms();
            }
            else if (phase == 1 && seconds > 3.5)
            {
                phase = 2;
                CheckRooms(difficulty);

                if (!StartArmRun(difficulty))
                {
                    phase = 3;
                    StartDirector();
                }
            }
            else if (phase == 2)
            {
                if (ArmRunFinished())
                {
                    phase = 3;
                    StartDirector();
                }
            }
            else if (phase == 3)
            {
                if (DriveDirector()) EndPass("recorrido completo del Director");
                else if (seconds > 300) EndPass("✗ el Director no llegó al final en 300 s");
            }
        }
        catch (Exception e)
        {
            Fail($"la prueba se interrumpió: {e.GetType().Name}: {e.Message}");
            EndPass("interrumpida");
        }
    }

    private static void CheckLoaded(Difficulty expected)
    {
        Expect(DifficultyApplier.Current == expected, $"DifficultyApplier aplicó {Label(DifficultyApplier.Current)}");

        TelemetryManager telemetry = TelemetryManager.Instance;
        if (telemetry != null)
        {
            Expect(telemetry.GetCurrentDifficulty() == expected, $"la telemetría registra {Label(telemetry.GetCurrentDifficulty())}");

            string file = telemetry.GetCurrentFilePath();
            if (!string.IsNullOrEmpty(file))
                SessionState.SetString(KeyFiles, SessionState.GetString(KeyFiles, "") + file + "\n");
        }
        else Fail("no hay TelemetryManager");

        Narrator narrator = Object.FindObjectsByType<Narrator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        NarrationDifficulty narration = narrator != null ? narrator.GetComponent<NarrationDifficulty>() : null;
        if (narrator != null && narration != null)
        {
            int index = (int)typeof(Narrator).GetField("listIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(narrator);
            int wanted = (expected == Difficulty.Basica ? narration.Basica : narration.Intermedia).listIndex;
            Expect(index == wanted, $"el Narrator usa la lista {index}");
        }

        // Ninguna pieza de la otra dificultad encendida, y todas las de esta como estaban.
        DifficultyOnly[] pieces = Object.FindObjectsByType<DifficultyOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int wrong = pieces.Count(p => p.Difficulty != expected && p.gameObject.activeSelf);
        int own = pieces.Count(p => p.Difficulty == expected && p.gameObject.activeSelf);
        Expect(wrong == 0, $"{wrong} pieza(s) de la otra dificultad encendidas; {own} propias encendidas");
    }

    /// <summary>Enciende las salas para que sus componentes despierten como lo harían al llegar.</summary>
    private static void WakeRooms()
    {
        foreach (MonoBehaviour controller in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                                    .Where(m => m is Scenario1Controller || m is Scenario2Controller ||
                                                                m is Scenario3Controller || m is Scenario4Controller))
            for (Transform t = controller.transform; t != null; t = t.parent)
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
    }

    private static void CheckRooms(Difficulty expected)
    {
        ProgramTrigger[] triggers = Object.FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Escenario 1
        Scenario1Difficulty s1 = Object.FindAnyObjectByType<Scenario1Difficulty>(FindObjectsInactive.Include);
        ProgramTrigger t1 = triggers.FirstOrDefault(t => t.ChallengeId == TelemetryManager.Scenario1Id && t.GetComponentInParent<TutorialController>(true) == null);
        if (s1 != null && t1 != null)
        {
            Scenario1Difficulty.Settings settings = expected == Difficulty.Basica ? s1.Basica : s1.Intermedia;
            List<Socket> row = Chain(t1.socketRow.FirstSocket);
            Expect(row.Count == settings.sockets, $"Esc. 1: la fila tiene {row.Count} huecos");

            LevelLoader loader = Object.FindAnyObjectByType<LevelLoader>(FindObjectsInactive.Include);
            Expect(loader != null && loader.levelJson == settings.level, $"Esc. 1: nivel '{(loader != null && loader.levelJson != null ? loader.levelJson.name : "-")}'");

            List<GridBlock> palette = Object.FindObjectsByType<GridBlock>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                            .Where(b => b.GetComponentInParent<TutorialController>(true) == null).ToList();
            pass.AppendLine($"  · Esc. 1: {palette.Count} bloques activos en la paleta");
        }
        else Fail("Esc. 1: falta Scenario1Difficulty o su botón");

        // Escenario 2
        foreach (SystemModule module in Object.FindObjectsByType<SystemModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string data = module.Data != null ? module.Data.name : "(nada)";
            bool right = module.Data != null && module.Data.name.EndsWith(expected == Difficulty.Basica ? "_Basica" : "_Intermedia");
            Expect(right, $"Esc. 2: {module.name} con {data}");
        }

        // Escenario 3
        Scenario3Difficulty s3 = Object.FindAnyObjectByType<Scenario3Difficulty>(FindObjectsInactive.Include);
        ProgramTrigger t3 = triggers.FirstOrDefault(t => t.ChallengeId == TelemetryManager.Scenario3Id);
        if (s3 != null && t3 != null)
        {
            Scenario3Difficulty.Settings settings = expected == Difficulty.Basica ? s3.Basica : s3.Intermedia;
            List<Socket> row = Chain(t3.socketRow.FirstSocket);

            string got = string.Join(" · ", row.Select(s => s.CurrentBlock != null ? s.CurrentBlock.InstructionLabel : "(vacío)"));
            string want = string.Join(" · ", settings.initialBlocks.Select(b => b?.block != null ? b.block.InstructionLabel : "(vacío)")
                                                                   .Concat(Enumerable.Repeat("(vacío)", Math.Max(0, row.Count - settings.initialBlocks.Count))));
            Expect(got == want, $"Esc. 3: la fila arranca con {got}");

            bool open = row.Any(s => s.AcceptsBlocks || (s.CurrentBlock != null && s.CurrentBlock.IsInteractable));
            Expect(open == settings.editable, $"Esc. 3: fila {(open ? "editable" : "bloqueada")}");

            int shown = t3.socketRow.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy);
            Expect((shown > 0) == settings.showRow, $"Esc. 3: fila {(shown > 0 ? $"visible ({shown} piezas a la vista)" : "oculta")}");
        }
        else Fail("Esc. 3: falta Scenario3Difficulty o su botón");

        // Escenario 4
        Scenario4Controller s4 = Object.FindObjectsByType<Scenario4Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                       .FirstOrDefault(c => c.ChallengeId == TelemetryManager.Scenario4Id);
        if (s4 != null)
        {
            var sockets = (ShapeSocket[])typeof(Scenario4Controller).GetField("sockets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s4);
            int excluded = sockets.Count(s => DifficultyOnly.IsExcluded(s.gameObject));
            Expect(sockets.Length > 0 && excluded == 0, $"Esc. 4: {sockets.Length} huecos cuentan para terminar, {excluded} de la otra dificultad");
        }
        else Fail("Esc. 4: no hay Scenario4Controller");
    }

    // --- Escenario 3 en básica: la fila oculta tiene que funcionar sola ---

    private static ProgramTrigger armTrigger;
    private static ArmItemType armType;
    private static ArmSlot armDestination;
    private static int armRequired, armBefore;
    private static double armDeadline;

    /// <summary>
    /// Pone una ficha de tipo, fija tantas repeticiones como objetos de ese tipo y pulsa
    /// Ejecutar, como haría el niño. Solo en básica: la fila de intermedia viene desordenada a
    /// propósito y fallar es lo correcto. False si no hay nada que probar.
    /// </summary>
    private static bool StartArmRun(Difficulty difficulty)
    {
        if (difficulty != Difficulty.Basica) return false;

        armTrigger = Object.FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                           .FirstOrDefault(t => t.ChallengeId == TelemetryManager.Scenario3Id);
        ArmTypeSocket typeSocket = Object.FindAnyObjectByType<ArmTypeSocket>(FindObjectsInactive.Include);
        Scenario3Controller controller = Object.FindAnyObjectByType<Scenario3Controller>(FindObjectsInactive.Include);
        ArmTypeChip chip = Object.FindObjectsByType<ArmTypeChip>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

        if (armTrigger == null || typeSocket == null || controller == null || chip == null)
        {
            Fail("Esc. 3: no se pudo preparar la ejecución (falta el botón, el socket de tipo, el controlador o una ficha)");
            return false;
        }

        var goals = (Scenario3Controller.SortingGoal[])typeof(Scenario3Controller)
            .GetField("goals", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
        Scenario3Controller.SortingGoal goal = goals.FirstOrDefault(g => g.type == chip.Type);
        if (goal == null || goal.destination == null)
        {
            Fail($"Esc. 3: no hay meta para el tipo {chip.Type}");
            return false;
        }

        armType = chip.Type;
        armDestination = goal.destination;
        armRequired = goal.required;
        armBefore = armDestination.CountOf(armType);

        chip.AttachToSocket(typeSocket.GetComponent<Socket>());
        armTrigger.socketRow.SetRepetitions(armRequired);
        armTrigger.OnPlayPressed();

        armDeadline = EditorApplication.timeSinceStartup + 120;
        pass.AppendLine($"  · Esc. 3: ejecutando la fila oculta con la ficha {armType} y {armRequired} repeticiones");
        return true;
    }

    private static bool ArmRunFinished()
    {
        bool running = armTrigger.runner != null && armTrigger.runner.IsRunning;
        if (running && EditorApplication.timeSinceStartup < armDeadline) return false;

        int moved = armDestination.CountOf(armType) - armBefore;
        Expect(!running && moved == armRequired,
               $"Esc. 3: la fila oculta llevó {moved} de {armRequired} objetos {armType} a su destino" +
               (running ? " (no terminó en 120 s)" : ""));
        return true;
    }

    // --- Recorrido del Director ---

    private static Director director;
    private static int stopScenario, stopStep;

    private static void StartDirector()
    {
        director = Object.FindAnyObjectByType<Director>(FindObjectsInactive.Include);
        if (director == null)
        {
            Fail("no hay Director");
            return;
        }

        // Nada sale a la red aunque algo llegara a llamar a la subida.
        foreach (JSONUploader uploader in Object.FindObjectsByType<JSONUploader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            typeof(JSONUploader).GetField("serverUrl", BindingFlags.NonPublic | BindingFlags.Instance)
                                .SetValue(uploader, "http://127.0.0.1:9/prueba-sin-red");

        // Se para en el paso anterior al primero que sube el JSON o cambia de escena.
        stopScenario = director.scenarios.Count;
        stopStep = 0;
        for (int i = 0; i < director.scenarios.Count && stopScenario == director.scenarios.Count; i++)
            for (int j = 0; j < director.scenarios[i].steps.Count; j++)
            {
                UnityEngine.Events.UnityEvent events = director.scenarios[i].steps[j].instantEvents;
                bool leaves = Enumerable.Range(0, events?.GetPersistentEventCount() ?? 0)
                                        .Any(k => events.GetPersistentMethodName(k) == nameof(JSONUploader.UploadTelemetry) ||
                                                  events.GetPersistentMethodName(k) == nameof(SceneLoader.Load));
                if (!leaves) continue;

                stopScenario = i;
                stopStep = j;
                break;
            }

        pass.AppendLine($"  · Director: se recorre hasta antes de '{(stopScenario < director.scenarios.Count ? director.scenarios[stopScenario].steps[stopStep].message : "el final")}'");

        // El paso 0 apaga el botón de inicio: se arranca como lo haría ese botón.
        director.Play();
        nextSkip = EditorApplication.timeSinceStartup + 0.3;
    }

    /// <summary>Salta pasos hasta el anterior al de salida. True al terminar.</summary>
    private static bool DriveDirector()
    {
        if (director == null) return true;

        int scenario = (int)typeof(Director).GetField("currentScenarioIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);
        int step = (int)typeof(Director).GetField("currentStepIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);

        bool beforeExit = scenario < stopScenario || (scenario == stopScenario && step < stopStep);
        bool lastBeforeExit = (scenario == stopScenario && step == stopStep - 1) ||
                              (stopStep == 0 && scenario == stopScenario - 1 && step == director.scenarios[scenario].steps.Count - 1);

        if (!director.IsPlaying || !beforeExit)
        {
            if (!beforeExit) Fail($"el Director entró en {scenario}.{step} antes de poder pararlo");
            director.Stop();
            return true;
        }

        if (lastBeforeExit)
        {
            director.Stop();
            pass.AppendLine($"  · Director parado en {scenario}.{step} '{director.scenarios[scenario].steps[step].message}'");
            return true;
        }

        if (EditorApplication.timeSinceStartup < nextSkip) return false;

        nextSkip = EditorApplication.timeSinceStartup + 0.3;
        typeof(Director).GetMethod("SkipStep", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
        return false;
    }

    private static void EndPass(string how)
    {
        pass.AppendLine($"  · {how}");

        List<string> problems = logs.Where(l => l.StartsWith("✗")).ToList();
        pass.AppendLine(problems.Count == 0
            ? "  ✓ ningún error ni excepción en consola durante la pasada"
            : $"  ✗ {problems.Count} error(es) en consola:");
        foreach (string line in problems.Distinct().Take(40)) pass.AppendLine($"      {line}");

        foreach (string line in logs.Where(l => l.StartsWith("⚠") || l.StartsWith("·")).Distinct().Take(40)) pass.AppendLine($"      {line}");

        errors += problems.Count;
        SessionState.SetInt(KeyErrors, SessionState.GetInt(KeyErrors, 0) + errors);
        Append(pass.ToString());

        logs.Clear();
        phase = 9;
        director = null;
        SessionState.SetString(KeyStage, "exiting");
        EditorApplication.ExitPlaymode();
    }

    private static void Finish()
    {
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;

        // PlayerPrefs del editor como estaban antes de la prueba.
        foreach (string line in SessionState.GetString(KeyPrefs, "").Split('\n'))
        {
            string[] pair = line.Split('=');
            if (pair.Length != 2) continue;

            if (pair[1] == "") PlayerPrefs.DeleteKey(pair[0]);
            else PlayerPrefs.SetInt(pair[0], int.Parse(pair[1]));
        }
        PlayerPrefs.Save();

        int deleted = 0;
        foreach (string file in SessionState.GetString(KeyFiles, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct())
            if (File.Exists(file))
            {
                File.Delete(file);
                deleted++;
            }

        int failures = SessionState.GetInt(KeyErrors, 0);
        string report = $"[Codea] Prueba en Play: {failures} fallo(s). PlayerPrefs restauradas, {deleted} JSON de prueba borrados.\n" +
                        SessionState.GetString(KeyReport, "");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));

        bool batch = SessionState.GetBool(KeyBatch, false);
        foreach (string key in new[] { KeyQueue, KeyStage, KeyCurrent, KeyReport, KeyErrors, KeyPrefs, KeyFiles })
            SessionState.EraseString(key);
        SessionState.EraseInt(KeyCurrent);
        SessionState.EraseInt(KeyErrors);
        SessionState.EraseBool(KeyBatch);

        if (batch)
        {
            Debug.Log(report);
            EditorApplication.Exit(failures > 0 ? 1 : 0);
            return;
        }

        // Descarta el cambio en memoria del DifficultyApplier.
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        if (failures > 0) Debug.LogError(report);
        else Debug.Log(report);
    }

    // --- Utilidades ---

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!EditorApplication.isPlaying) return;

        string first = message.Split('\n')[0];
        if (first.Length > 220) first = first.Substring(0, 220) + "…";

        if (first.StartsWith("[Step "))
        {
            logs.Add($"· {first}");
            return;
        }

        // Sin visor conectado, el runtime de OpenXR no encuentra un casco. En el visor no pasa.
        if (first.Contains("xrGetSystem") || first.Contains("ErrorFormFactorUnavailable"))
        {
            logs.Add($"· (sin visor, se ignora) {first}");
            return;
        }

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string at = stack?.Split('\n').FirstOrDefault(l => l.Contains("Assets/")) ?? "";
            logs.Add($"✗ [{type}] {first}{(at != "" ? $"  ({at.Trim()})" : "")}");
        }
        else if (type == LogType.Warning && (first.StartsWith("[Dificultad]") || first.StartsWith("[SocketRow]") ||
                                             first.StartsWith("[Narrator]") || first.StartsWith("[Telemetry]") ||
                                             first.StartsWith("[Escenario") || first.StartsWith("[Tutorial]")))
        {
            logs.Add($"⚠ {first}");
        }
    }

    private static List<Socket> Chain(Socket first)
    {
        var chain = new List<Socket>();
        for (Socket s = first; s != null && chain.Count < 64; s = s.Next) chain.Add(s);
        return chain;
    }

    private static void Expect(bool condition, string what)
    {
        if (condition) pass.AppendLine($"  ✓ {what}");
        else Fail(what);
    }

    private static void Fail(string what)
    {
        errors++;
        pass.AppendLine($"  ✗ {what}");
    }

    private static void Append(string text) => SessionState.SetString(KeyReport, SessionState.GetString(KeyReport, "") + text);

    private static string Label(Difficulty difficulty) => DifficultyApplier.Label(difficulty);
}
