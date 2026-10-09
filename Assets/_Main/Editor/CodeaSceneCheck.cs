using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Tools → Codea → Comprobar escena de juego: informe de la escena abierta, sin tocarla.
///
///   · Integridad: scripts perdidos y TODAS las llamadas de UnityEvent de la escena, con su
///     objeto y su método. Un método renombrado o un objeto borrado dejan la llamada muerta
///     sin que Unity avise, y esto es lo que más se rompe al cambiar código.
///   · El Director paso a paso: eventos, esperas y cuántas veces espera al Narrator.
///   · Cada reto en cada dificultad, como quedará al cargar: datos y piezas.
///   · La escena Setup y Build Settings.
///
/// Sin sorpresas: ✗ es un fallo que rompe algo, ⚠ algo que revisar, ✓ comprobado. El informe
/// sale en consola y en Logs/Codea_Comprobacion.txt. Sin Unity abierto:
/// Unity.exe -batchmode -quit -projectPath . -executeMethod CodeaSceneCheck.CheckBatch
/// </summary>
public static class CodeaSceneCheck
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string SetupScene = "Assets/Scenes/Setup.unity";
    private const string ReportPath = "Logs/Codea_Comprobacion.txt";

    // Diseño de los niveles del Escenario 1 (Context.md, sección 5). Si cambia un nivel,
    // cambia aquí: el informe avisa si la paleta de la escena no es esta.
    private static readonly Dictionary<GridActionType, int> BasicPalette = new Dictionary<GridActionType, int>
    {
        { GridActionType.MoveForward, 2 }, { GridActionType.RotateRight, 2 }, { GridActionType.MoveForwardTwice, 2 },
        { GridActionType.RotateLeft, 1 }, { GridActionType.Use, 1 },
    };

    private static readonly Dictionary<GridActionType, int> IntermediatePalette = new Dictionary<GridActionType, int>
    {
        { GridActionType.RotateLeft, 4 }, { GridActionType.RotateRight, 2 }, { GridActionType.MoveForwardTwice, 4 },
        { GridActionType.MoveForward, 2 }, { GridActionType.Use, 1 },
    };

    private static readonly Difficulty[] Difficulties = { Difficulty.Basica, Difficulty.Avanzada };

    [MenuItem("Tools/Codea/Comprobar escena de juego")]
    private static void CheckMenu()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (AllComponents<Director>(scene).Count == 0)
        {
            EditorUtility.DisplayDialog("Comprobar escena de juego",
                $"'{scene.name}' no tiene Director: abre {GameScene} y vuelve a pasarla.", "Vale");
            return;
        }

        Write(Check(scene));
    }

    /// <summary>Sin abrir Unity. Con '-codeaScene ruta.unity' revisa otra escena en vez de Juego.</summary>
    public static void CheckBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-codeaScene");
        string path = at >= 0 && at + 1 < args.Length ? args[at + 1] : GameScene;

        Write(Check(EditorSceneManager.OpenScene(path, OpenSceneMode.Single)));
    }

    // --- Informe ---

    private class Report
    {
        public readonly StringBuilder Text = new StringBuilder();
        public int Errors, Warnings;

        public void Title(string text) => Text.AppendLine().AppendLine($"== {text}");
        public void Line(string text) => Text.AppendLine(text);
        public void Ok(string text) => Text.AppendLine($"  ✓ {text}");

        public void Warn(string text)
        {
            Warnings++;
            Text.AppendLine($"  ⚠ {text}");
        }

        public void Error(string text)
        {
            Errors++;
            Text.AppendLine($"  ✗ {text}");
        }
    }

    private static void Write(Report report)
    {
        string summary = $"[Codea] Comprobación: {report.Errors} fallo(s), {report.Warnings} aviso(s). " +
                         $"Informe completo en {ReportPath}";

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, summary + "\n" + report.Text, new UTF8Encoding(false));

        if (report.Errors > 0) Debug.LogError(summary + "\n" + report.Text);
        else Debug.Log(summary + "\n" + report.Text);
    }

    private static Report Check(Scene scene)
    {
        Report report = new Report();
        report.Line($"Escena '{scene.path}' · {DateTime.Now:dd/MM/yyyy HH:mm}");

        CheckBuildSettings(report);
        CheckIntegrity(scene, report);
        CheckSingletons(scene, report);
        CheckDirector(scene, report);

        foreach (Difficulty difficulty in Difficulties)
            CheckDifficulty(scene, difficulty, report);

        CheckDifficultyOnly(scene, report);
        CheckSession(scene, report);
        CheckDesign(scene, report);
        CheckSetupScene(report);

        return report;
    }

    // --- Diseño: datos para la lista de pendientes. Solo informa, no falla ---

    private static void CheckDesign(Scene scene, Report report)
    {
        report.Title("Diseño (informativo)");

        foreach (ProgramTrigger trigger in AllComponents<ProgramTrigger>(scene).Where(t => !InTutorial(t) && t.socketRow != null))
        {
            SerializedObject row = new SerializedObject(trigger.socketRow);
            Transform start = row.FindProperty("start").objectReferenceValue as Transform;
            Transform end = row.FindProperty("end").objectReferenceValue as Transform;
            if (start == null || end == null) continue;

            Vector3 along = end.position - start.position;
            float tilt = Mathf.Abs(Mathf.Asin(Mathf.Clamp(along.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
            report.Line($"  Fila '{trigger.ChallengeId}': {along.magnitude:0.00} m de largo, " +
                        $"{(tilt < 10f ? "horizontal" : tilt > 80f ? "VERTICAL" : "inclinada")} ({tilt:0}° respecto al suelo)");
        }

        foreach (SystemModule module in AllComponents<SystemModule>(scene))
        {
            SerializedObject so = new SerializedObject(module);
            var missing = new[] { "statusIcon", "brokenIcon", "repairedIcon", "brokenLight", "repairedLight" }
                .Where(f => so.FindProperty(f).objectReferenceValue == null).ToList();
            report.Line(missing.Count == 0
                ? $"  Esc. 2 · {module.name}: icono y luces de estado asignados"
                : $"  Esc. 2 · {module.name}: sin asignar {string.Join(", ", missing)}");
        }

        var buttons = AllComponents<MonoBehaviour>(scene).Where(m => m != null && m.GetType().Name == "InteractableUnityEventWrapper").ToList();
        var silent = buttons.Where(b =>
        {
            Transform root = b.transform.parent != null ? b.transform.parent : b.transform;
            return !root.GetComponentsInChildren<Component>(true).Any(c => c is AudioSource || (c != null && c.GetType().Name == "AudioTrigger"));
        }).ToList();
        report.Line($"  Botones (InteractableUnityEventWrapper): {buttons.Count}, sin sonido propio: {silent.Count}" +
                    (silent.Count > 0 ? " · " + string.Join(", ", silent.Select(b => PathOf(b)).Take(12)) + (silent.Count > 12 ? ", …" : "") : ""));

        report.Line($"  Relojes en pantalla (TimerDisplay): {AllComponents<TimerDisplay>(scene).Count} · " +
                    string.Join(", ", AllComponents<TimerDisplay>(scene).Select(t => PathOf(t))));
    }

    // --- Build Settings ---

    private static void CheckBuildSettings(Report report)
    {
        report.Title("Build Settings");

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        string[] enabled = scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        report.Line("  " + string.Join(" · ", scenes.Select(s => $"{(s.enabled ? "" : "(apagada) ")}{s.path}")));

        if (enabled.Length >= 2 && enabled[0] == SetupScene && enabled[1] == GameScene)
            report.Ok("Setup primera y Juego después, las dos activas");
        else
            report.Error($"Build Settings debería ser {SetupScene}, {GameScene}");

        foreach (string path in enabled.Skip(2))
            report.Warn($"Escena de más en el build: {path}");

        foreach (string path in enabled.Where(p => !File.Exists(p)))
            report.Error($"Escena del build que no existe: {path}");
    }

    // --- Integridad: scripts perdidos y UnityEvents ---

    private static void CheckIntegrity(Scene scene, Report report)
    {
        report.Title("Integridad de la escena");

        int missing = 0;
        foreach (Transform t in AllTransforms(scene))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (count == 0) continue;

            missing += count;
            report.Error($"{count} script(s) perdido(s) en '{PathOf(t)}'");
        }

        if (missing == 0) report.Ok("Ningún script perdido");

        CheckEvents(scene, report, "");
    }

    /// <summary>
    /// Todas las llamadas de todos los UnityEvent de la escena. Las que fallarían son fallos;
    /// las muertas (sin objeto o sin método) no rompen nada y se agrupan como aviso.
    /// </summary>
    private static void CheckEvents(Scene scene, Report report, string prefix)
    {
        int calls = 0, broken = 0;
        var dead = new Dictionary<string, List<string>>();

        foreach (MonoBehaviour behaviour in AllComponents<MonoBehaviour>(scene))
        {
            if (behaviour == null) continue;

            foreach (EventCall call in EventCalls(behaviour))
            {
                calls++;
                string where = $"{PathOf(behaviour)} · {behaviour.GetType().Name}.{call.EventPath}";

                if (call.IsDead)
                {
                    if (!dead.TryGetValue(call.DeadReason, out List<string> list)) dead[call.DeadReason] = list = new List<string>();
                    list.Add(where);
                    continue;
                }

                string problem = call.Problem();
                if (problem == null) continue;

                broken++;
                report.Error($"{prefix}{where}: {problem}");
            }
        }

        if (broken == 0) report.Ok($"{prefix}{calls} llamadas de UnityEvent; ninguna apunta a un método que no exista");

        foreach (KeyValuePair<string, List<string>> group in dead)
            report.Warn($"{prefix}{group.Value.Count} × {group.Key}. En: " +
                        string.Join(", ", group.Value.Take(4)) + (group.Value.Count > 4 ? ", …" : ""));
    }

    // --- Componentes únicos ---

    private static void CheckSingletons(Scene scene, Report report)
    {
        report.Title("Componentes de la sesión");

        void ExactlyOne<T>(string why) where T : Component
        {
            List<T> found = AllComponents<T>(scene);
            if (found.Count == 1) report.Ok($"{typeof(T).Name} en '{PathOf(found[0])}'");
            else report.Error($"{found.Count} {typeof(T).Name} en la escena; debe haber uno. {why}");
        }

        ExactlyOne<TelemetryManager>("Uno por escena: los UnityEvent apuntan al suyo");
        ExactlyOne<DifficultyApplier>("Sin él los retos no se montan para la dificultad elegida");
        ExactlyOne<Director>("");
        ExactlyOne<JSONUploader>("");
        ExactlyOne<TimeUpSequence>("");
        ExactlyOne<SceneLoader>("");
        ExactlyOne<ScenarioTimeLimit>("Sin él no hay límite de tiempo por sala");

        DifficultyApplier applier = AllComponents<DifficultyApplier>(scene).FirstOrDefault();
        if (applier != null)
        {
            if (!applier.gameObject.activeInHierarchy || !applier.enabled)
                report.Error($"DifficultyApplier en '{PathOf(applier)}' está apagado: su Awake no correría y " +
                             "la escena se jugaría siempre como está guardada");

            SerializedProperty forced = new SerializedObject(applier).FindProperty("editorDifficulty");
            if (forced != null && forced.enumValueIndex != 0)
                report.Warn($"DifficultyApplier → Editor Difficulty está en '{forced.enumDisplayNames[forced.enumValueIndex]}'. " +
                            "En el visor no cuenta, pero en el editor ignora la dificultad de Setup");
        }
    }

    // --- Director ---

    private static void CheckDirector(Scene scene, Report report)
    {
        report.Title("Director");

        Director director = AllComponents<Director>(scene).FirstOrDefault();
        if (director == null) return;

        report.Line($"  playOnStart: {director.playOnStart}");

        List<string> starters = new List<string>();
        foreach (MonoBehaviour behaviour in AllComponents<MonoBehaviour>(scene))
            foreach (EventCall call in EventCalls(behaviour))
                if (call.Target == director && call.Method == nameof(Director.Play) && !call.IsDead && call.State != UnityEventCallState.Off)
                    starters.Add($"{PathOf(behaviour)} · {behaviour.GetType().Name}.{call.EventPath}");

        if (starters.Count > 0) report.Ok("Arranca el Director: " + string.Join("; ", starters));
        else if (!director.playOnStart) report.Error("Nada llama a Director.Play y playOnStart está apagado: la partida no empezaría nunca");

        CheckStartButton(scene, director, report);

        foreach (GameObject root in scene.GetRootGameObjects().Where(r => !r.activeSelf))
            report.Line($"  Raíz apagada al cargar: {root.name}");

        foreach (Scenario1Controller controller in AllComponents<Scenario1Controller>(scene))
        {
            bool onStart = new SerializedObject(controller).FindProperty("startChallengeOnStart").boolValue;
            if (onStart && controller.gameObject.activeInHierarchy)
                report.Warn($"Scenario1Controller abre su reto en Start y '{PathOf(controller)}' está activo al cargar: " +
                            "el Escenario 1 empezaría a contar durante el tutorial");
            else
                report.Ok($"Scenario1Controller: startChallengeOnStart {onStart.ToString().ToLower()}, activo al cargar: " +
                          $"{controller.gameObject.activeInHierarchy.ToString().ToLower()}");
        }

        SerializedObject so = new SerializedObject(director);
        SerializedProperty scenarios = so.FindProperty("scenarios");
        Narrator narrator = AllComponents<Narrator>(scene).FirstOrDefault(n => !InTutorial(n));

        int narratorWaits = 0, starts = 0, stepNumber = 0;
        var startsByController = new Dictionary<Object, int>();

        for (int i = 0; i < scenarios.arraySize; i++)
        {
            SerializedProperty scenario = scenarios.GetArrayElementAtIndex(i);
            SerializedProperty steps = scenario.FindPropertyRelative("steps");
            report.Line($"  [{i}] {scenario.FindPropertyRelative("name").stringValue}");

            for (int j = 0; j < steps.arraySize; j++, stepNumber++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(j);
                SerializedProperty mode = step.FindPropertyRelative("completionType");
                string message = step.FindPropertyRelative("message").stringValue;

                report.Line($"    {i}.{j} \"{message}\" · {mode.enumNames[mode.enumValueIndex]}");

                foreach (EventCall call in CallsOf(step.FindPropertyRelative("instantEvents"), "instantEvents"))
                {
                    report.Line($"        → {call.Describe()}");

                    string problem = call.Problem();
                    if (problem != null) report.Error($"{i}.{j} \"{message}\": {problem}");

                    if (call.Method == "StartScenario" && call.Target != null)
                    {
                        starts++;
                        startsByController[call.Target] = startsByController.TryGetValue(call.Target, out int n) ? n + 1 : 1;
                    }

                    GameObject activated = call.Method == "SetActive" ? call.ObjectArgument as GameObject ?? (call.Target as GameObject) : null;
                    if (activated != null && call.BoolArgument != false && activated.GetComponentInParent<DifficultyOnly>(true) != null)
                        report.Warn($"{i}.{j} \"{message}\" activa '{PathOf(activated.transform)}', que es de una sola " +
                                    "dificultad: en la otra volvería a aparecer");
                }

                SerializedProperty waits = step.FindPropertyRelative("waitActions");
                for (int k = 0; k < waits.arraySize; k++)
                {
                    GameObject wait = waits.GetArrayElementAtIndex(k).objectReferenceValue as GameObject;
                    if (wait == null)
                    {
                        report.Warn($"{i}.{j} \"{message}\": espera vacía en la posición {k}");
                        continue;
                    }

                    IStepAction action = wait.GetComponent<IStepAction>();
                    report.Line($"        ⏳ {PathOf(wait.transform)} ({(action != null ? action.GetType().Name : "SIN IStepAction")})");

                    if (action == null)
                        report.Error($"{i}.{j} \"{message}\": '{wait.name}' no tiene ningún IStepAction; el paso no lo espera");

                    if (wait.GetComponentInParent<DifficultyOnly>(true) != null)
                        report.Error($"{i}.{j} \"{message}\": espera a '{wait.name}', que es de una sola dificultad");

                    if (narrator != null && action is Narrator && wait == narrator.gameObject) narratorWaits++;
                }
            }
        }

        report.Line($"  {stepNumber} pasos · {narratorWaits} esperas al Narrator · {starts} llamadas a StartScenario");

        foreach (KeyValuePair<Object, int> pair in startsByController.Where(p => p.Value > 1))
            report.Warn($"El reto de '{pair.Key.name}' se abre {pair.Value} veces (pendiente conocido: debería ser una)");

        CheckNarratorLists(narrator, narratorWaits, report);
    }

    /// <summary>
    /// El botón de inicio: la partida no debe empezar hasta que el niño tenga el visor puesto.
    /// Tiene que estar encendido al cargar, ser lo único que arranca el Director, y las piezas
    /// del tutorial tienen que esperar apagadas a que se pulse.
    /// </summary>
    private static void CheckStartButton(Scene scene, Director director, Report report)
    {
        report.Title("Botón de inicio");

        var buttons = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in AllComponents<MonoBehaviour>(scene))
            foreach (EventCall call in EventCalls(behaviour))
                if (call.Target == director && call.Method == nameof(Director.Play) && !call.IsDead)
                    buttons.Add(behaviour);

        if (director.playOnStart)
            report.Warn("Director.playOnStart está encendido: la partida empieza sola al cargar, sin esperar al botón");

        foreach (MonoBehaviour button in buttons.Distinct())
        {
            Transform root = button.transform;
            while (root.parent != null && root.name != "StartButton" && root.parent.GetComponent<TutorialController>() == null)
                root = root.parent;

            string labels = string.Join(" / ", root.GetComponentsInChildren<TMPro.TMP_Text>(true)
                                                   .Select(t => t.text.Trim()).Where(t => t.Length > 0));
            report.Line($"  '{PathOf(root)}' · rótulo: {(labels.Length > 0 ? labels : "(sin texto)")} · " +
                        $"activo al cargar: {root.gameObject.activeInHierarchy.ToString().ToLower()}");

            if (!root.gameObject.activeInHierarchy)
                report.Error("El botón de inicio está apagado al cargar: no se podría empezar");
        }

        TutorialController tutorial = AllComponents<TutorialController>(scene).FirstOrDefault();
        if (tutorial != null)
        {
            SerializedObject so = new SerializedObject(tutorial);
            foreach (string field in new[] { "programButton", "optionButton", "blockSocket", "shapeSocket" })
                if (so.FindProperty(field).objectReferenceValue is Component piece)
                {
                    if (piece.gameObject.activeInHierarchy)
                        report.Warn($"Pieza del tutorial '{PathOf(piece)}' encendida al cargar: se puede tocar antes de pulsar el botón de inicio");
                }

            report.Line($"  TutorialController.startDirectorOnFinish: {so.FindProperty("startDirectorOnFinish").boolValue.ToString().ToLower()}");
        }
    }

    private static void CheckNarratorLists(Narrator narrator, int narratorWaits, Report report)
    {
        report.Title("Narración");

        if (narrator == null)
        {
            report.Error("No hay Narrator");
            return;
        }

        SerializedObject so = new SerializedObject(narrator);
        SerializedProperty lists = so.FindProperty("lists");
        TextAsset csv = so.FindProperty("csv").objectReferenceValue as TextAsset;
        HashSet<string> ids = CsvIds(csv);

        report.Line($"  CSV: {(csv != null ? AssetDatabase.GetAssetPath(csv) : "ninguno")} · {ids.Count} IDs");

        NarrationDifficulty variant = narrator.GetComponent<NarrationDifficulty>();
        if (variant == null)
        {
            report.Error("El Narrator no tiene NarrationDifficulty: sonaría siempre la misma lista");
            return;
        }

        foreach (Difficulty difficulty in Difficulties)
        {
            int index = (difficulty == Difficulty.Basica ? variant.Basica : variant.Intermedia).listIndex;
            if (index < 0 || index >= lists.arraySize)
            {
                report.Error($"{Label(difficulty)}: lista {index} fuera de rango (hay {lists.arraySize})");
                continue;
            }

            SerializedProperty list = lists.GetArrayElementAtIndex(index);
            SerializedProperty entries = list.FindPropertyRelative("entries");
            var line = new List<string>();

            for (int e = 0; e < entries.arraySize; e++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                string id = entry.FindPropertyRelative("textId").stringValue;
                Object clip = entry.FindPropertyRelative("clip").objectReferenceValue;
                line.Add(id);

                if (clip == null) report.Warn($"{Label(difficulty)}: la entrada {e} ('{id}') no tiene audio");
                if (!string.IsNullOrEmpty(id) && ids.Count > 0 && !ids.Contains(id))
                    report.Error($"{Label(difficulty)}: la entrada {e} pide el textId '{id}', que no está en el CSV");
                if (clip != null && !string.IsNullOrEmpty(id) && clip.name != id)
                    report.Warn($"{Label(difficulty)}: la entrada {e} tiene el audio '{clip.name}' y el texto '{id}'");
            }

            report.Line($"  {Label(difficulty)}: lista {index} '{list.FindPropertyRelative("name").stringValue}' · " +
                        $"{entries.arraySize} entradas: {string.Join(" ", line)}");

            if (entries.arraySize == narratorWaits)
                report.Ok($"{Label(difficulty)}: tantas entradas como esperas del Director ({narratorWaits})");
            else
                report.Error($"{Label(difficulty)}: {entries.arraySize} entradas y {narratorWaits} esperas del Director. " +
                             "Las frases saldrían desplazadas o faltarían");
        }
    }

    private static HashSet<string> CsvIds(TextAsset csv)
    {
        var ids = new HashSet<string>();
        if (csv == null) return ids;

        // El mismo parser que usa el Narrator: el CSV tiene comillas y saltos de línea dentro.
        MethodInfo parse = typeof(Narrator).GetMethod("ParseCsv", BindingFlags.NonPublic | BindingFlags.Static);
        if (parse == null) return ids;

        var rows = (List<List<string>>)parse.Invoke(null, new object[] { csv.text });
        foreach (List<string> row in rows.Skip(1))
            if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
                ids.Add(row[0].Trim());

        return ids;
    }

    // --- Cada dificultad, como quedará al cargar ---

    private static void CheckDifficulty(Scene scene, Difficulty difficulty, Report report)
    {
        report.Title($"Dificultad {Label(difficulty)}");

        bool Excluded(Component c) => c.GetComponentsInParent<DifficultyOnly>(true).Any(o => o.Difficulty != difficulty);

        // Escenario 1
        Scenario1Difficulty s1 = AllComponents<Scenario1Difficulty>(scene).FirstOrDefault();
        ProgramTrigger t1 = AllComponents<ProgramTrigger>(scene).FirstOrDefault(t => t.ChallengeId == TelemetryManager.Scenario1Id && !InTutorial(t));
        if (s1 == null) report.Error("Esc. 1: no hay Scenario1Difficulty");
        else
        {
            SerializedObject so = new SerializedObject(s1);
            Object loader = so.FindProperty("levelLoader").objectReferenceValue;
            Object row = so.FindProperty("row").objectReferenceValue;
            Scenario1Difficulty.Settings settings = difficulty == Difficulty.Basica ? s1.Basica : s1.Intermedia;

            if (loader == null || row == null) report.Error("Esc. 1: Scenario1Difficulty sin LevelLoader o sin fila");
            if (t1 != null && row != t1.socketRow) report.Error("Esc. 1: la fila de Scenario1Difficulty no es la del botón de ejecutar");
            if (settings.level == null) report.Error("Esc. 1: sin nivel");

            report.Line($"  Esc. 1: nivel '{(settings.level != null ? settings.level.name : "-")}' · {settings.sockets} huecos");
        }

        List<GridBlock> palette = AllComponents<GridBlock>(scene).Where(b => !InTutorial(b) && !Excluded(b)).ToList();
        var counts = palette.GroupBy(b => b.action).ToDictionary(g => g.Key, g => g.Count());
        Dictionary<GridActionType, int> expected = difficulty == Difficulty.Basica ? BasicPalette : IntermediatePalette;

        report.Line($"  Esc. 1: paleta de {palette.Count}: " +
                    string.Join(", ", counts.OrderBy(p => p.Key).Select(p => $"{p.Key} ×{p.Value}")));

        bool samePalette = expected.Count == counts.Count && expected.All(p => counts.TryGetValue(p.Key, out int n) && n == p.Value);
        if (samePalette) report.Ok("Esc. 1: la paleta es la del diseño del nivel");
        else report.Warn("Esc. 1: la paleta no es la del diseño (" +
                         string.Join(", ", expected.Select(p => $"{p.Key} ×{p.Value}")) + ")");

        if (s1 != null)
        {
            int sockets = (difficulty == Difficulty.Basica ? s1.Basica : s1.Intermedia).sockets;
            int solution = difficulty == Difficulty.Basica ? 8 : 11;
            if (sockets != solution) report.Warn($"Esc. 1: {sockets} huecos; la solución del diseño ocupa {solution}");
        }

        foreach (GridBlock block in palette.Where(b => b.GetComponentInParent<BlockResetter>(true) == null))
            report.Warn($"Esc. 1: '{PathOf(block)}' no cuelga de ningún BlockResetter: Reiniciar no lo devolvería");

        // Escenario 2
        Scenario2Difficulty s2 = AllComponents<Scenario2Difficulty>(scene).FirstOrDefault();
        if (s2 == null) report.Error("Esc. 2: no hay Scenario2Difficulty");
        else
        {
            var modules = (List<SystemModule>)typeof(Scenario2Difficulty)
                .GetField("modules", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s2);
            List<ModuleData> data = (difficulty == Difficulty.Basica ? s2.Basica : s2.Intermedia).modules;
            int correct = difficulty == Difficulty.Basica ? 1 : 2;

            if (modules.Count != data.Count) report.Error($"Esc. 2: {modules.Count} módulos y {data.Count} datos");

            for (int i = 0; i < modules.Count && i < data.Count; i++)
            {
                ModuleData d = data[i];
                ModuleData other = (difficulty == Difficulty.Basica ? s2.Intermedia : s2.Basica).modules.ElementAtOrDefault(i);

                if (modules[i] == null || d == null)
                {
                    report.Error($"Esc. 2: módulo o datos vacíos en la posición {i}");
                    continue;
                }

                report.Line($"  Esc. 2: {modules[i].name} → {d.name} · \"{d.moduleName}\" · {d.options.Count} opciones, " +
                            $"{d.CorrectCount} correcta(s)");

                if (!d.IsValid) report.Error($"Esc. 2: '{d.name}' no es válido (sin opciones o sin correctas)");
                if (d.CorrectCount != correct) report.Warn($"Esc. 2: '{d.name}' tiene {d.CorrectCount} correctas; el diseño pide {correct}");
                if (other != null && other.moduleId != d.moduleId) report.Error($"Esc. 2: '{d.name}' y '{other.name}' tienen distinto moduleId");
                if (Excluded(modules[i])) report.Error($"Esc. 2: '{modules[i].name}' está apagado en esta dificultad");
            }
        }

        // Escenario 3
        Scenario3Difficulty s3 = AllComponents<Scenario3Difficulty>(scene).FirstOrDefault();
        ProgramTrigger t3 = AllComponents<ProgramTrigger>(scene).FirstOrDefault(t => t.ChallengeId == TelemetryManager.Scenario3Id);
        if (s3 == null) report.Error("Esc. 3: no hay Scenario3Difficulty");
        else
        {
            Object row = new SerializedObject(s3).FindProperty("row").objectReferenceValue;
            Scenario3Difficulty.Settings settings = difficulty == Difficulty.Basica ? s3.Basica : s3.Intermedia;

            if (t3 == null || row != t3.socketRow) report.Error("Esc. 3: la fila de Scenario3Difficulty no es la del botón de ejecutar");

            if (!settings.showRow && settings.editable)
                report.Error("Esc. 3: fila oculta y editable: se podrían agarrar bloques sin verlos");
            if (difficulty == Difficulty.Basica && settings.showRow)
                report.Warn("Esc. 3: en básica la fila debería estar oculta (las instrucciones no se enseñan)");
            if (difficulty == Difficulty.Avanzada && !settings.showRow)
                report.Error("Esc. 3: en intermedia la fila tiene que verse: el niño la reordena");

            report.Line($"  Esc. 3: fila {(settings.showRow ? "visible" : "OCULTA")}, {(settings.editable ? "editable" : "bloqueada")}: " +
                        string.Join(" · ", settings.initialBlocks.Select(b =>
                            (b?.block is ArmBlock a ? a.InstructionLabel : "(vacío)") + (b != null && b.fixedInPlace ? " [fijo]" : ""))));

            if (t3 != null && t3.socketRow != null && settings.initialBlocks.Count > t3.socketRow.SocketsQuantity)
                report.Error($"Esc. 3: {settings.initialBlocks.Count} bloques para {t3.socketRow.SocketsQuantity} huecos");

            foreach (SocketRow.InitialBlock initial in settings.initialBlocks)
            {
                if (initial?.block == null) continue;

                if (Excluded(initial.block))
                    report.Error($"Esc. 3: la fila usa '{PathOf(initial.block)}', que está apagado en esta dificultad");
                if (InTutorial(initial.block))
                    report.Error($"Esc. 3: la fila usa '{initial.block.name}', que es del tutorial");
            }

            List<BlockNode> used = settings.initialBlocks.Where(b => b?.block != null).Select(b => b.block).ToList();
            if (used.Count != used.Distinct().Count()) report.Error("Esc. 3: la fila usa el mismo bloque dos veces");

            foreach (ArmBlock loose in AllComponents<ArmBlock>(scene).Where(b => !InTutorial(b) && !Excluded(b) && !used.Contains(b)))
                report.Warn($"Esc. 3: '{PathOf(loose)}' está activo y no está en la fila");

            string labels = string.Join(" ", settings.initialBlocks.Select(b => b?.block is ArmBlock a ? a.action.ToString() : "-"));
            if (difficulty == Difficulty.Basica && (settings.editable || labels != "Pick RotateToDestination Drop RotateToOrigin"))
                report.Warn("Esc. 3: la básica no es la fila bloqueada Recoger · Girar al destino · Soltar · Volver");
            if (difficulty == Difficulty.Avanzada && (!settings.editable || labels.Contains("RotateTo")))
                report.Warn("Esc. 3: la intermedia debería ser editable y con giros literales");
        }

        // Escenario 4
        Scenario4Controller s4 = AllComponents<Scenario4Controller>(scene).FirstOrDefault(c => c.ChallengeId == TelemetryManager.Scenario4Id);
        if (s4 == null) report.Error("Esc. 4: no hay Scenario4Controller con challengeId escenario4");
        else
        {
            List<ShapeSocket> sockets = s4.GetComponentsInChildren<ShapeSocket>(true).Where(s => !Excluded(s)).ToList();
            var serialized = new SerializedObject(s4).FindProperty("sockets");
            if (serialized.arraySize > 0)
                sockets = Enumerable.Range(0, serialized.arraySize)
                                    .Select(i => serialized.GetArrayElementAtIndex(i).objectReferenceValue as ShapeSocket)
                                    .Where(s => s != null && !Excluded(s)).ToList();

            BlockResetter resetter = new SerializedObject(s4).FindProperty("chipResetter").objectReferenceValue as BlockResetter;
            List<ShapeChip> chips = (resetter != null ? resetter.GetComponentsInChildren<ShapeChip>(true).ToList() : new List<ShapeChip>())
                                    .Where(c => !Excluded(c)).ToList();

            report.Line($"  Esc. 4: {sockets.Count} huecos, {chips.Count} fichas: " +
                        string.Join(", ", sockets.Select(s => Name(new SerializedObject(s).FindProperty("expectedShape").objectReferenceValue))));

            if (resetter == null) report.Error("Esc. 4: Scenario4Controller sin chipResetter");

            foreach (ShapeSocket socket in sockets)
            {
                Object shape = new SerializedObject(socket).FindProperty("expectedShape").objectReferenceValue;
                if (shape == null) report.Error($"Esc. 4: el hueco '{socket.name}' no tiene figura");
                else if (!chips.Any(c => new SerializedObject(c).FindProperty("shape").objectReferenceValue == shape))
                    report.Error($"Esc. 4: ninguna ficha tiene la figura '{shape.name}' del hueco '{socket.name}': el reto no se podría terminar");
            }

            if (chips.Count <= sockets.Count) report.Warn("Esc. 4: no hay fichas de sobra; el último hueco se resuelve por eliminación");
        }

        // Tutorial
        TutorialController tutorial = AllComponents<TutorialController>(scene).FirstOrDefault();
        if (tutorial == null) report.Warn("No hay TutorialController");
        else
        {
            SerializedObject so = new SerializedObject(tutorial);
            foreach (string field in new[] { "programButton", "optionButton", "blockSocket", "shapeSocket" })
            {
                Object piece = so.FindProperty(field).objectReferenceValue;
                if (piece == null) report.Error($"Tutorial: falta '{field}'");
                else if (piece is Component c && Excluded(c)) report.Error($"Tutorial: '{field}' está apagado en esta dificultad");
            }
        }
    }

    private static void CheckDifficultyOnly(Scene scene, Report report)
    {
        report.Title("Piezas de una sola dificultad");

        List<DifficultyOnly> pieces = AllComponents<DifficultyOnly>(scene);
        foreach (IGrouping<Difficulty, DifficultyOnly> group in pieces.GroupBy(p => p.Difficulty))
            report.Line($"  Solo {Label(group.Key)}: {group.Count()} · " +
                        string.Join(", ", group.Select(p => p.name).Distinct()));

        foreach (DifficultyOnly piece in pieces.Where(p => p.GetComponentsInParent<DifficultyOnly>(true).Select(o => o.Difficulty).Distinct().Count() > 1))
            report.Error($"'{PathOf(piece)}' es de una dificultad y cuelga de otra de la contraria: no saldría en ninguna");

        foreach (DifficultyOnly piece in pieces.Where(p => !p.gameObject.activeSelf))
            report.Warn($"'{PathOf(piece)}' está guardado apagado: tampoco saldrá en su dificultad salvo que algo lo encienda");
    }

    // --- Ciclo de sesión ---

    private static void CheckSession(Scene scene, Report report)
    {
        report.Title("Ciclo de sesión");

        Timer timer = AllComponents<Timer>(scene).FirstOrDefault();
        TimeUpSequence timeUp = AllComponents<TimeUpSequence>(scene).FirstOrDefault();
        SceneLoader loader = AllComponents<SceneLoader>(scene).FirstOrDefault();

        // Tiempo por sala: el reloj no debe cerrar la sesión por su cuenta.
        if (timer != null && timeUp != null &&
            CallsOf(new SerializedObject(timer).FindProperty("OnTimeUp"), "OnTimeUp")
                .Any(c => c.Target == timeUp && c.Method == nameof(TimeUpSequence.Begin)))
            report.Error("Timer.OnTimeUp sigue llamando a TimeUpSequence.Begin: la primera sala que agote su tiempo " +
                         "cerraría la sesión entera. Pasa Tools → Codea → 2");

        ScenarioTimeLimit limit = AllComponents<ScenarioTimeLimit>(scene).FirstOrDefault();
        if (limit != null)
        {
            SerializedObject so = new SerializedObject(limit);
            foreach (string field in new[] { "timer", "narrator", "sessionTimeUp" })
                if (so.FindProperty(field).objectReferenceValue == null) report.Error($"ScenarioTimeLimit sin {field}");

            int seconds = so.FindProperty("secondsPerScenario").intValue;
            bool hasLine = so.FindProperty("timeUpLine.clip").objectReferenceValue != null ||
                           !string.IsNullOrEmpty(so.FindProperty("timeUpLine.textId").stringValue);
            string lineId = so.FindProperty("timeUpLine.textId").stringValue;
            Narrator storyNarrator = AllComponents<Narrator>(scene).FirstOrDefault();
            if (!string.IsNullOrEmpty(lineId) && storyNarrator != null)
            {
                HashSet<string> csvIds = CsvIds(new SerializedObject(storyNarrator).FindProperty("csv").objectReferenceValue as TextAsset);
                if (csvIds.Count > 0 && !csvIds.Contains(lineId))
                    report.Error($"ScenarioTimeLimit pide la frase '{lineId}', que no está en el CSV de narración");
            }
            if (!string.IsNullOrEmpty(lineId) && so.FindProperty("timeUpLine.clip").objectReferenceValue == null)
                report.Warn($"La frase de tiempo agotado por sala ('{lineId}') no tiene audio: solo se escribirá en pantalla");

            report.Line($"  Tiempo por sala: {seconds / 60f:0.#} min · frase al agotarse (salas 1-3): " +
                        (hasLine ? "asignada" : "ninguna (se omite la felicitación y se sigue)") +
                        " · última sala: cierre de sesión con la frase de TimeUpSequence");

            var ids = AllComponents<MonoBehaviour>(scene).OfType<ITimeLimitedChallenge>()
                                                          .Select(c => c.ChallengeId).Where(id => !string.IsNullOrEmpty(id)).ToList();
            foreach (string id in new[] { TelemetryManager.Scenario1Id, TelemetryManager.Scenario2Id, TelemetryManager.Scenario3Id, TelemetryManager.Scenario4Id })
                if (!ids.Contains(id)) report.Error($"Ningún controlador con límite de tiempo para {id}");
            if (ids.Count == ids.Distinct().Count()) report.Ok($"Límite de tiempo en {ids.Count} salas: {string.Join(", ", ids)}");
            else report.Error("Hay dos controladores con el mismo challengeId: el límite de tiempo solo vigilaría uno");
        }

        if (timeUp != null)
        {
            SerializedObject so = new SerializedObject(timeUp);
            foreach (string field in new[] { "director", "narrator", "uploader", "sceneLoader" })
                if (so.FindProperty(field).objectReferenceValue == null) report.Error($"TimeUpSequence sin '{field}'");
            if (so.FindProperty("timeUpLine.clip").objectReferenceValue == null) report.Warn("TimeUpSequence sin audio de tiempo agotado");
        }

        if (loader != null)
        {
            string target = new SerializedObject(loader).FindProperty("sceneName").stringValue;
            if (target == "Setup") report.Ok("SceneLoader vuelve a Setup");
            else report.Error($"SceneLoader carga '{target}' en vez de Setup");
        }

        // Quién llama a cada botón de ejecutar
        var callers = new Dictionary<Object, int>();
        foreach (MonoBehaviour behaviour in AllComponents<MonoBehaviour>(scene))
            foreach (EventCall call in EventCalls(behaviour))
                if (call.Target != null && call.Method == nameof(ProgramTrigger.OnPlayPressed))
                    callers[call.Target] = callers.TryGetValue(call.Target, out int n) ? n + 1 : 1;

        foreach (ProgramTrigger trigger in AllComponents<ProgramTrigger>(scene))
        {
            callers.TryGetValue(trigger, out int count);
            string where = $"ProgramTrigger '{trigger.ChallengeId}' en '{PathOf(trigger)}'";

            if (InTutorial(trigger)) continue;
            if (count == 0) report.Error($"{where}: ningún botón llama a OnPlayPressed");
            else report.Ok($"{where}: {count} llamada(s) a OnPlayPressed");
        }

        foreach (MonoBehaviour behaviour in AllComponents<MonoBehaviour>(scene))
            foreach (EventCall call in EventCalls(behaviour))
            {
                if (call.Method == nameof(RoboticArm.ResetArm))
                    report.Warn($"{PathOf(behaviour)}.{call.EventPath} llama a ResetArm: borraría lo ya clasificado");
                if (behaviour is Scenario3Controller && call.Method == nameof(TelemetryManager.RegisterFailedAttempt))
                    report.Warn("Scenario3Controller tiene RegisterFailedAttempt cableado: el fallo se contaría dos veces");
            }
    }

    private static void CheckSetupScene(Report report)
    {
        report.Title("Escena Setup");

        if (!File.Exists(SetupScene))
        {
            report.Error($"No existe {SetupScene}");
            return;
        }

        Scene setup = SceneManager.GetSceneByPath(SetupScene);
        bool opened = !setup.isLoaded;
        if (opened) setup = EditorSceneManager.OpenScene(SetupScene, OpenSceneMode.Additive);

        try
        {
            SessionSetup session = AllComponents<SessionSetup>(setup).FirstOrDefault();
            if (session == null) report.Error("Setup sin SessionSetup");
            else
            {
                string target = new SerializedObject(session).FindProperty("gameSceneName").stringValue;
                if (target == "Juego") report.Ok("SessionSetup carga 'Juego'");
                else report.Error($"SessionSetup carga '{target}'");
            }

            if (AllComponents<TelemetryManager>(setup).Count > 0)
                report.Warn("Setup tiene TelemetryManager: abriría un JSON vacío por sesión");
            if (AllComponents<DifficultyApplier>(setup).Count > 0)
                report.Warn("Setup tiene DifficultyApplier: no hace nada ahí");

            CheckEvents(setup, report, "Setup: ");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(setup, true);
        }
    }

    // --- UnityEvents ---

    private class EventCall
    {
        public string EventPath;
        public Object Target;
        public string Method;
        public PersistentListenerMode Mode;
        public Object ObjectArgument;
        public string ObjectArgumentType;
        public int IntArgument;
        public float FloatArgument;
        public string StringArgument;
        public bool? BoolArgument;
        public UnityEventCallState State;

        public string Describe()
        {
            string target = Target == null ? "(nada)"
                : Target is Component c ? $"{PathOf(c)} · {c.GetType().Name}"
                : Target is GameObject g ? $"{PathOf(g.transform)} · GameObject"
                : Target.name;

            string argument = Mode switch
            {
                PersistentListenerMode.Object => ObjectArgument != null
                    ? ObjectArgument is GameObject g2 ? PathOf(g2.transform) : ObjectArgument.name
                    : "null",
                PersistentListenerMode.Int => IntArgument.ToString(),
                PersistentListenerMode.Float => FloatArgument.ToString("0.###"),
                PersistentListenerMode.String => $"\"{StringArgument}\"",
                PersistentListenerMode.Bool => BoolArgument == true ? "true" : "false",
                _ => "",
            };

            string state = State == UnityEventCallState.Off ? " [APAGADA]" : "";
            return $"{target}.{Method}({argument}){state}";
        }

        /// <summary>
        /// Una llamada sin objeto o sin método no hace nada al dispararse: Unity se la salta en
        /// silencio. No rompe, pero lo que se quería que pasara no pasa.
        /// </summary>
        public bool IsDead => State != UnityEventCallState.Off && (Target == null || string.IsNullOrEmpty(Method));

        public string DeadReason => Target == null
            ? $"llamada muerta a '{Method}': su objeto no existe (borrado, de otra escena o de un prefab de ejemplo)"
            : "llamada vacía (No Function)";

        /// <summary>Null si la llamada funcionará o está muerta (ver IsDead); si no, por qué falla.</summary>
        public string Problem()
        {
            if (State == UnityEventCallState.Off || IsDead) return null;

            Type[] arguments = Mode switch
            {
                PersistentListenerMode.Void => Type.EmptyTypes,
                PersistentListenerMode.Int => new[] { typeof(int) },
                PersistentListenerMode.Float => new[] { typeof(float) },
                PersistentListenerMode.String => new[] { typeof(string) },
                PersistentListenerMode.Bool => new[] { typeof(bool) },
                PersistentListenerMode.Object => new[] { Type.GetType(ObjectArgumentType ?? "") ?? typeof(Object) },
                _ => null,
            };

            Type type = Target.GetType();

            // EventDefined: recibe los argumentos del propio evento (ninguno en un UnityEvent
            // simple). Es el modo que dejan las llamadas añadidas desde código.
            if (arguments == null)
                return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                           .Any(m => m.Name == Method && m.GetParameters().Length <= 1)
                    ? null
                    : $"'{type.Name}' no tiene ningún método público '{Method}'";

            if (UnityEventBase.GetValidMethodInfo(Target, Method, arguments) != null) return null;

            // Un argumento de objeto puede ser de un tipo derivado del declarado.
            if (Mode == PersistentListenerMode.Object &&
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(m => m.Name == Method && m.GetParameters().Length == 1 &&
                              typeof(Object).IsAssignableFrom(m.GetParameters()[0].ParameterType)))
                return null;

            return $"'{type.Name}' no tiene '{Method}({string.Join(", ", arguments.Select(a => a.Name))})'";
        }
    }

    /// <summary>Todas las llamadas persistentes de todos los UnityEvent de un componente.</summary>
    private static IEnumerable<EventCall> EventCalls(MonoBehaviour behaviour)
    {
        SerializedObject so = new SerializedObject(behaviour);
        SerializedProperty it = so.GetIterator();
        var found = new List<EventCall>();
        bool enter = true;

        while (it.Next(enter))
        {
            enter = true;

            if (it.name == "m_PersistentCalls")
            {
                string eventPath = it.propertyPath.Replace(".m_PersistentCalls", "");
                found.AddRange(CallsOf(it.serializedObject.FindProperty(eventPath), eventPath));
                enter = false;
                continue;
            }

            // Las mallas de ProBuilder y similares tienen miles de elementos sin eventos dentro.
            if (it.propertyType == SerializedPropertyType.String || (it.isArray && it.arraySize > 256))
                enter = false;
        }

        return found;
    }

    private static IEnumerable<EventCall> CallsOf(SerializedProperty unityEvent, string eventPath)
    {
        if (unityEvent == null) yield break;

        SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
        if (calls == null) yield break;

        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            SerializedProperty args = call.FindPropertyRelative("m_Arguments");
            var mode = (PersistentListenerMode)call.FindPropertyRelative("m_Mode").intValue;

            yield return new EventCall
            {
                EventPath = eventPath,
                Target = call.FindPropertyRelative("m_Target").objectReferenceValue,
                Method = call.FindPropertyRelative("m_MethodName").stringValue,
                Mode = mode,
                ObjectArgument = args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue,
                ObjectArgumentType = args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue,
                IntArgument = args.FindPropertyRelative("m_IntArgument").intValue,
                FloatArgument = args.FindPropertyRelative("m_FloatArgument").floatValue,
                StringArgument = args.FindPropertyRelative("m_StringArgument").stringValue,
                BoolArgument = mode == PersistentListenerMode.Bool ? args.FindPropertyRelative("m_BoolArgument").boolValue : (bool?)null,
                State = (UnityEventCallState)call.FindPropertyRelative("m_CallState").intValue,
            };
        }
    }

    // --- Utilidades ---

    private static IEnumerable<Transform> AllTransforms(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));

    private static List<T> AllComponents<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToList();

    private static bool InTutorial(Component component) =>
        component.GetComponentInParent<TutorialController>(true) != null ||
        component.transform.root.name == "Tutorial" || component.transform.root.name == "Start";

    private static string PathOf(Component component) => component != null ? PathOf(component.transform) : "(nada)";

    private static string PathOf(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Name(Object asset) => asset != null ? asset.name : "(nada)";

    private static string Label(Difficulty difficulty) => DifficultyApplier.Label(difficulty);
}
