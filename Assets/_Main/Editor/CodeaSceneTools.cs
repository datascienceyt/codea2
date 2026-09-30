using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Montaje automático de lo que antes había que cablear a mano, en Tools → Codea.
///
/// 1 - Crear escena Setup: copia Basico, se queda solo con el rig del jugador y los building
///     blocks de interacción, y monta la pantalla del supervisor con su teclado ya cableado.
/// 2 - Preparar escena de juego abierta: añade a Basico o Intermedio lo que falta para el
///     ciclo de sesión (dificultad, tiempo agotado, vuelta a Setup).
///
/// Las dos son repetibles: lo que ya existe no se duplica.
/// </summary>
public static class CodeaSceneTools
{
    private const string BasicScene = "Assets/Scenes/Basico.unity";
    private const string IntermediateScene = "Assets/Scenes/Intermedio.unity";
    private const string SetupScene = "Assets/Scenes/Setup.unity";
    private const string ButtonPrefab = "Assets/_Main/Prefabs/Buttons/TextButton.prefab";

    private const float KeySpacing = 0.12f;

    // --- 1. Escena Setup ---

    [MenuItem("Tools/Codea/1 - Crear escena Setup")]
    private static void CreateSetupScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefab);
        if (buttonPrefab == null)
        {
            Debug.LogError($"[Codea] No encuentro el botón en {ButtonPrefab}.");
            return;
        }

        if (File.Exists(SetupScene) &&
            !EditorUtility.DisplayDialog("Crear escena Setup",
                "Ya existe Setup.unity. ¿Reemplazarla?", "Reemplazar", "Cancelar"))
            return;

        AssetDatabase.DeleteAsset(SetupScene);
        if (!AssetDatabase.CopyAsset(BasicScene, SetupScene))
        {
            Debug.LogError($"[Codea] No se pudo copiar {BasicScene}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(SetupScene, OpenSceneMode.Single);
        StringBuilder log = new StringBuilder("[Codea] Escena Setup creada.\n");

        StripToRig(scene, log);

        OVRCameraRig rig = Object.FindAnyObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        if (rig == null)
        {
            Debug.LogError("[Codea] La copia no tiene OVRCameraRig: no sé dónde poner la pantalla.");
            return;
        }

        BuildSupervisorPanel(rig.transform, buttonPrefab, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        SetBuildScenes(log);

        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Borra toda raíz que no sea el rig, la interacción o la luz. Lo que se queda y lo que se
    /// va sale en el log, para revisarlo si algo del juego se hubiera colado en el rig.
    /// </summary>
    private static void StripToRig(Scene scene, StringBuilder log)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (IsRigRoot(root))
            {
                log.AppendLine($"  conservado: {root.name}");
                continue;
            }

            Object.DestroyImmediate(root);
        }

        // Nada de la sesión de juego puede quedarse en la pantalla del supervisor: un
        // TelemetryManager abriría una run con el PIN anterior (ver SessionSetup).
        RemoveAll<TelemetryManager>(log);
        RemoveAll<JSONUploader>(log);
        RemoveAll<Director>(log);
        RemoveAll<Timer>(log);
        RemoveAll<TimerDisplay>(log);
        RemoveAll<Narrator>(log);
    }

    private static bool IsRigRoot(GameObject root)
    {
        if (root.name.StartsWith("[BuildingBlock]")) return true;
        if (root.GetComponentInChildren<OVRCameraRig>(true) != null) return true;
        if (root.GetComponentInChildren<OVRManager>(true) != null) return true;

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            if (light.type == LightType.Directional) return true;

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;

            System.Type type = behaviour.GetType();
            if (type.Namespace != null && type.Namespace.StartsWith("Oculus.Interaction") &&
                type.Name.Contains("Interactor"))
                return true;
        }

        return false;
    }

    private static void RemoveAll<T>(StringBuilder log) where T : Component
    {
        foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            log.AppendLine($"  quitado {typeof(T).Name} de '{component.name}'");
            Object.DestroyImmediate(component);
        }
    }

    private static void BuildSupervisorPanel(Transform rig, GameObject buttonPrefab, StringBuilder log)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = Vector3.forward;

        GameObject panel = new GameObject("PantallaSupervisor");
        panel.transform.SetPositionAndRotation(rig.position + forward * 0.45f + Vector3.up * 1.1f,
                                               Quaternion.LookRotation(forward));

        SessionSetup setup = panel.AddComponent<SessionSetup>();

        GameObject pinObject = new GameObject("PIN");
        pinObject.transform.SetParent(panel.transform, false);
        PinEntry pin = pinObject.AddComponent<PinEntry>();

        TextMeshPro pinText = CreateLabel(panel.transform, "PantallaPIN", new Vector3(0f, 0.36f, 0f), 0.7f, "____");
        TextMeshPro statusText = CreateLabel(panel.transform, "Estado", new Vector3(0f, 0.26f, 0f), 0.35f, "");

        SetField(pin, "displayTmp", pinText);
        SetField(setup, "pinEntry", pin);
        SetField(setup, "statusText", statusText);

        // Teclado 3x4 a la izquierda; dificultades y empezar en una columna a la derecha.
        string[,] keypad =
        {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" },
            { "Borrar", "0", "Siguiente" },
        };

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string label = keypad[row, col];
                Vector3 position = new Vector3((col - 2) * KeySpacing, 0.12f - row * KeySpacing, 0f);
                InteractableUnityEventWrapper wrapper = CreateButton(panel.transform, buttonPrefab, label, position);

                if (wrapper == null) continue;

                if (int.TryParse(label, out int digit))
                    UnityEventTools.AddIntPersistentListener(wrapper.WhenSelect, pin.AppendDigit, digit);
                else if (label == "Borrar")
                    UnityEventTools.AddPersistentListener(wrapper.WhenSelect, pin.DeleteLast);
                else
                    UnityEventTools.AddPersistentListener(wrapper.WhenSelect, pin.UseNextPin);

                PrefabUtility.RecordPrefabInstancePropertyModifications(wrapper);
            }
        }

        AddAction(panel.transform, buttonPrefab, "Básica", new Vector3(2f * KeySpacing, 0.12f, 0f), setup.SelectBasic);
        AddAction(panel.transform, buttonPrefab, "Intermedia", new Vector3(2f * KeySpacing, 0.12f - KeySpacing, 0f), setup.SelectIntermediate);
        AddAction(panel.transform, buttonPrefab, "EMPEZAR", new Vector3(2f * KeySpacing, 0.12f - 3f * KeySpacing, 0f), setup.StartSession);

        Selection.activeGameObject = panel;

        log.AppendLine("  pantalla del supervisor montada: 12 teclas + Básica, Intermedia, EMPEZAR");
    }

    private static void AddAction(Transform parent, GameObject prefab, string label, Vector3 position, UnityAction action)
    {
        InteractableUnityEventWrapper wrapper = CreateButton(parent, prefab, label, position);
        if (wrapper == null) return;

        UnityEventTools.AddPersistentListener(wrapper.WhenSelect, action);
        PrefabUtility.RecordPrefabInstancePropertyModifications(wrapper);
    }

    private static InteractableUnityEventWrapper CreateButton(Transform parent, GameObject prefab, string label, Vector3 position)
    {
        GameObject button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        button.name = $"Tecla {label}";
        button.transform.localPosition = position;
        button.transform.localRotation = Quaternion.identity;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }

        InteractableUnityEventWrapper wrapper = button.GetComponentInChildren<InteractableUnityEventWrapper>(true);
        if (wrapper == null)
            Debug.LogError($"[Codea] El botón '{label}' no tiene InteractableUnityEventWrapper.", button);

        return wrapper;
    }

    private static TextMeshPro CreateLabel(Transform parent, string name, Vector3 position, float height, string text)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(0.6f, height * 0.25f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = text;

        return tmp;
    }

    private static void SetBuildScenes(StringBuilder log)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(SetupScene, true),
            new EditorBuildSettingsScene(BasicScene, true),
            new EditorBuildSettingsScene(IntermediateScene, true),
        };

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (scenes.All(s => s.path != existing.path))
                scenes.Add(existing);

        EditorBuildSettings.scenes = scenes.ToArray();

        log.AppendLine("  Build Settings: Setup (0), Basico (1), Intermedio (2), las tres activas");
    }

    // --- 2. Escena de juego ---

    [MenuItem("Tools/Codea/2 - Preparar escena de juego abierta")]
    private static void PrepareGameScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        bool intermediate = scene.name.ToLowerInvariant().Contains("intermed");
        Difficulty difficulty = intermediate ? Difficulty.Avanzada : Difficulty.Basica;

        if (!EditorUtility.DisplayDialog("Preparar escena de juego",
                $"Escena '{scene.name}' → dificultad {(intermediate ? "INTERMEDIA" : "BÁSICA")}.\n\n" +
                "Se añaden DifficultyScene, SceneLoader y TimeUpSequence, y se cablean el " +
                "tiempo agotado y la vuelta a Setup.",
                "Preparar", "Cancelar"))
            return;

        StringBuilder log = new StringBuilder($"[Codea] Preparando '{scene.name}':\n");

        Director director = Find<Director>(log);
        Narrator narrator = Find<Narrator>(log);
        Timer timer = Find<Timer>(log);
        JSONUploader uploader = Find<JSONUploader>(log);

        GameObject session = GameObject.Find("Sesion");
        if (session == null)
        {
            session = new GameObject("Sesion");
            Undo.RegisterCreatedObjectUndo(session, "Sesion");
        }

        DifficultyScene difficultyScene = GetOrAdd<DifficultyScene>(session, log);
        SetEnum(difficultyScene, "difficulty", (int)difficulty);

        SceneLoader loader = GetOrAdd<SceneLoader>(session, log);
        SetString(loader, "sceneName", "Setup");

        TimeUpSequence timeUp = GetOrAdd<TimeUpSequence>(session, log);
        SetField(timeUp, "director", director);
        SetField(timeUp, "narrator", narrator);
        SetField(timeUp, "uploader", uploader);
        SetField(timeUp, "sceneLoader", loader);

        if (timer != null && AddOnce(timer, timer.OnTimeUp, timeUp, nameof(TimeUpSequence.Begin), timeUp.Begin))
            log.AppendLine("  Timer.OnTimeUp → TimeUpSequence.Begin");

        if (director != null && timer != null)
            AddReturnStep(director, timer, loader, log);

        if (director != null)
            StartChallengesOnArrival(director, log);

        Scenario3Controller scenario3 = Object.FindAnyObjectByType<Scenario3Controller>(FindObjectsInactive.Include);
        RoboticArm arm = Object.FindAnyObjectByType<RoboticArm>(FindObjectsInactive.Include);

        // Se QUITA si existe: ResetArm devuelve todas las pilas al inicio y borraba lo ya
        // clasificado en pasadas anteriores. Lo que había que resolver —un objeto olvidado en
        // la pinza— lo hace ahora Scenario3Controller al terminar cada ejecución.
        if (scenario3 != null && arm != null &&
            RemoveListener(scenario3, scenario3.OnAttemptFailed, arm, nameof(RoboticArm.ResetArm)))
            log.AppendLine("  Escenario 3: quitado OnAttemptFailed → RoboticArm.ResetArm (ya no hace falta)");

        FixPreconditions(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("  Escena guardada.");

        SerializedProperty timeUpClip = new SerializedObject(timeUp).FindProperty("timeUpLine.clip");
        if (timeUpClip != null && timeUpClip.objectReferenceValue == null)
            log.AppendLine("  ⚠ Falta a mano: el clip de tiempo agotado en TimeUpSequence → Time Up Line");
        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Paso final del Director: parar el reloj y volver a Setup. Va después de "Subir JSON";
    /// SceneLoader ya espera a que la subida termine antes de cargar.
    /// </summary>
    private static void AddReturnStep(Director director, Timer timer, SceneLoader loader, StringBuilder log)
    {
        const string message = "Volver a Setup";

        if (director.scenarios == null || director.scenarios.Count == 0)
        {
            log.AppendLine("  ⚠ El Director no tiene escenarios: no se añade la vuelta a Setup");
            return;
        }

        Scenario last = director.scenarios[director.scenarios.Count - 1];
        if (last.steps.Any(s => s.message == message))
        {
            log.AppendLine($"  ya existía el paso '{message}'");
            return;
        }

        Undo.RecordObject(director, message);

        Step step = new Step
        {
            message = message,
            completionType = StepCompletionType.All,
            instantEvents = new UnityEvent(),
            waitActions = new List<GameObject>(),
        };

        UnityEventTools.AddPersistentListener(step.instantEvents, timer.Pause);
        UnityEventTools.AddPersistentListener(step.instantEvents, new UnityAction(loader.Load));

        last.steps.Add(step);
        EditorUtility.SetDirty(director);

        log.AppendLine($"  Director: paso final '{message}' (Timer.Pause + SceneLoader.Load)");
    }

    /// <summary>
    /// Deja en cada ProgramTrigger solo las condiciones que lo son, y pone el socket de tipo en
    /// el del escenario 3. En Basico la lista apuntaba a las fichas: el socket ya se registra
    /// solo en ejecución, pero así la escena dice la verdad y no avisa en cada Play.
    /// </summary>
    private static void FixPreconditions(StringBuilder log)
    {
        ArmTypeSocket typeSocket = Object.FindAnyObjectByType<ArmTypeSocket>(FindObjectsInactive.Include);

        foreach (ProgramTrigger trigger in Object.FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty list = so.FindProperty("preconditions");
            bool changed = false;

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                Object entry = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (entry is IRunPrecondition) continue;

                log.AppendLine($"  {trigger.name}: quitado '{(entry != null ? entry.name : "vacío")}' de preconditions");

                // En listas de referencias, la primera llamada puede solo vaciar el hueco.
                int before = list.arraySize;
                list.DeleteArrayElementAtIndex(i);
                if (list.arraySize == before) list.DeleteArrayElementAtIndex(i);
                changed = true;
            }

            bool isScenario3 = trigger.ChallengeId == TelemetryManager.Scenario3Id;
            bool listed = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == typeSocket) listed = true;

            if (isScenario3 && typeSocket != null && !listed)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = typeSocket;
                log.AppendLine($"  {trigger.name}: añadido '{typeSocket.name}' (ArmTypeSocket) a preconditions");
                changed = true;
            }

            if (changed) so.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Abre los retos 2, 3 y 4 en el PRIMER paso de su escenario del Director, antes de la
    /// narración. El niño ya manipula mientras escucha, y con el reto abierto al acabar la
    /// narración (como estaba) lo que hacía antes se perdía o se atribuía a la sala anterior:
    /// en las pruebas del 30/09 un intento del esc. 3 quedó antes del inicio del escenario y
    /// un agarre del esc. 4 se contó en el 3. La llamada de después no hace nada:
    /// StartChallenge es idempotente. El 1 no se toca: arranca con el temporizador.
    /// </summary>
    private static void StartChallengesOnArrival(Director director, StringBuilder log)
    {
        var starts = new (string scenario, MonoBehaviour controller, UnityAction start)[]
        {
            ("Escenario 2", Object.FindAnyObjectByType<Scenario2Controller>(FindObjectsInactive.Include), null),
            ("Escenario 3", Object.FindAnyObjectByType<Scenario3Controller>(FindObjectsInactive.Include), null),
            ("Escenario 4", Object.FindAnyObjectByType<Scenario4Controller>(FindObjectsInactive.Include), null),
        };

        foreach (var (scenarioName, controller, _) in starts)
        {
            if (controller == null) continue;

            Scenario scenario = director.scenarios.Find(s => s.name == scenarioName);
            if (scenario == null || scenario.steps.Count == 0)
            {
                log.AppendLine($"  ⚠ No hay escenario '{scenarioName}' en el Director");
                continue;
            }

            Step first = scenario.steps[0];
            if (first.instantEvents == null) first.instantEvents = new UnityEvent();

            UnityAction action = controller switch
            {
                Scenario2Controller c => c.StartScenario,
                Scenario3Controller c => c.StartScenario,
                Scenario4Controller c => c.StartScenario,
                _ => null,
            };

            if (action != null && AddOnce(director, first.instantEvents, controller, "StartScenario", action))
                log.AppendLine($"  {scenarioName}: el reto se abre al llegar, en '{first.message}'");
        }
    }

    // --- 3. Conversión a intermedia ---

    private const string IntermediateLevel = "Assets/_Main/Levels/escenario1_intermedio.json";

    /// <summary>Huecos de la fila del escenario 1 en intermedia: los 11 de la solución.</summary>
    private const int IntermediateSockets = 11;

    /// <summary>
    /// Paleta del escenario 1 en intermedia: 13 bloques. Los 11 de la solución (el camino de
    /// arriba) y un Girar Derecha y un Avanzar 2 de sobra, que tientan hacia el camino de
    /// abajo: simétrico al bueno, pero de 14 bloques, así que no cabe en la fila.
    /// </summary>
    private static readonly Dictionary<GridActionType, int> IntermediatePalette = new Dictionary<GridActionType, int>
    {
        { GridActionType.RotateLeft, 4 },
        { GridActionType.RotateRight, 2 },
        { GridActionType.MoveForwardTwice, 4 },
        { GridActionType.MoveForward, 2 },
        { GridActionType.Use, 1 },
    };

    /// <summary>
    /// Deja la escena abierta (una copia de Basico) en dificultad intermedia: todo lo que
    /// cambia entre las dos salvo las figuras del escenario 4, que son una elección de diseño.
    /// Repetible: lo que ya está en intermedia no se toca.
    /// </summary>
    [MenuItem("Tools/Codea/3 - Convertir escena abierta a Intermedia")]
    private static void ConvertToIntermediate()
    {
        Scene scene = SceneManager.GetActiveScene();

        // Protección: convertir Basico por error la dejaría en intermedia sin avisar.
        if (!scene.name.ToLowerInvariant().Contains("intermed"))
        {
            EditorUtility.DisplayDialog("Convertir a Intermedia",
                $"La escena abierta es '{scene.name}'. Solo se convierte una escena cuyo nombre " +
                "contenga 'Intermed'. Duplica Basico, renómbrala a Intermedio y ábrela.", "Vale");
            return;
        }

        if (!EditorUtility.DisplayDialog("Convertir a Intermedia",
                $"Se va a pasar '{scene.name}' a dificultad intermedia:\n\n" +
                "• Narrador: lista 2\n• Esc. 2: módulos *_Intermedia\n" +
                "• Esc. 1: nivel intermedio, 11 huecos y paleta de 13 bloques\n" +
                "• Esc. 3: fila editable con los bloques desordenados\n\n" +
                "Las figuras del escenario 4 se cambian a mano.", "Convertir", "Cancelar"))
            return;

        StringBuilder log = new StringBuilder($"[Codea] Convirtiendo '{scene.name}' a intermedia:\n");

        Narrator narrator = Object.FindAnyObjectByType<Narrator>(FindObjectsInactive.Include);
        if (narrator != null)
        {
            SetInt(narrator, "listIndex", 2);
            log.AppendLine("  Narrador: lista 2 (Intermedia)");
        }

        ConvertModules(log);
        ConvertScenario1(log);
        ConvertScenario3(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("  Escena guardada.");
        log.AppendLine("  ⚠ Falta a mano: las figuras del escenario 4 (huecos y fichas) y colocar en la " +
                       "mesa los bloques nuevos del escenario 1, que salen apilados sobre su original.");
        Debug.Log(log.ToString());
    }

    private static void ConvertModules(StringBuilder log)
    {
        foreach (SystemModule module in Object.FindObjectsByType<SystemModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(module);
            SerializedProperty data = so.FindProperty("data");
            if (data == null || data.objectReferenceValue == null) continue;

            string path = AssetDatabase.GetAssetPath(data.objectReferenceValue);
            if (!path.Contains("_Basica")) continue;

            string intermediatePath = path.Replace("_Basica", "_Intermedia");
            ModuleData intermediate = AssetDatabase.LoadAssetAtPath<ModuleData>(intermediatePath);
            if (intermediate == null)
            {
                log.AppendLine($"  ⚠ Esc. 2: no existe {intermediatePath}");
                continue;
            }

            data.objectReferenceValue = intermediate;
            so.ApplyModifiedProperties();
            log.AppendLine($"  Esc. 2: {module.name} → {intermediate.name}");
        }
    }

    private static void ConvertScenario1(StringBuilder log)
    {
        ProgramTrigger trigger = FindTrigger(TelemetryManager.Scenario1Id);
        if (trigger != null && trigger.socketRow != null)
        {
            SetInt(trigger.socketRow, "socketsQuantity", IntermediateSockets);
            log.AppendLine($"  Esc. 1: {IntermediateSockets} huecos en la fila");
        }

        LevelLoader loader = Object.FindAnyObjectByType<LevelLoader>(FindObjectsInactive.Include);
        TextAsset level = AssetDatabase.LoadAssetAtPath<TextAsset>(IntermediateLevel);
        if (loader != null && level != null)
        {
            SetField(loader, "levelJson", level);
            log.AppendLine($"  Esc. 1: nivel {level.name}");
        }

        // Los bloques nuevos se duplican de uno de la misma acción: así heredan el rótulo, el
        // prefab y los eventos, y cuelgan del mismo BlockResetter que los devuelve a su sitio.
        List<GridBlock> blocks = Object.FindObjectsByType<GridBlock>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        foreach (KeyValuePair<GridActionType, int> wanted in IntermediatePalette)
        {
            List<GridBlock> same = blocks.Where(b => b.action == wanted.Key).ToList();
            if (same.Count == 0)
            {
                log.AppendLine($"  ⚠ Esc. 1: no hay ningún bloque '{wanted.Key}' que duplicar");
                continue;
            }

            for (int copy = 0; same.Count < wanted.Value; copy++)
            {
                GridBlock source = same[0];

                Selection.activeGameObject = source.gameObject;
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                GameObject duplicate = Selection.activeGameObject;

                if (duplicate == null || duplicate == source.gameObject) break;

                duplicate.transform.position = source.transform.position + Vector3.up * (0.08f * (copy + 1));
                same.Add(duplicate.GetComponent<GridBlock>());
                log.AppendLine($"  Esc. 1: añadido bloque '{source.InstructionLabel}'");
            }
        }
    }

    private static void ConvertScenario3(StringBuilder log)
    {
        ProgramTrigger trigger = FindTrigger(TelemetryManager.Scenario3Id);
        if (trigger == null || trigger.socketRow == null)
        {
            log.AppendLine("  ⚠ Esc. 3: no encuentro su ProgramTrigger o su fila");
            return;
        }

        SerializedObject row = new SerializedObject(trigger.socketRow);
        row.FindProperty("editable").boolValue = true;

        SerializedProperty initial = row.FindProperty("initialBlocks");
        List<ArmBlock> armBlocks = new List<ArmBlock>();

        for (int i = 0; i < initial.arraySize; i++)
            if (initial.GetArrayElementAtIndex(i).FindPropertyRelative("block").objectReferenceValue is ArmBlock armBlock)
                armBlocks.Add(armBlock);

        // Los giros automáticos de básica pasan a giros literales: en intermedia el niño decide
        // hacia dónde gira el brazo. Se cambia también el rótulo, que es un texto puesto a mano.
        foreach (ArmBlock block in armBlocks)
        {
            ArmActionType before = block.action;
            if (before == ArmActionType.RotateToDestination) SetArmAction(block, ArmActionType.RotateLeft);
            if (before == ArmActionType.RotateToOrigin) SetArmAction(block, ArmActionType.RotateRight);

            if (block.action != before)
                log.AppendLine($"  Esc. 3: bloque '{block.name}' pasa a '{block.InstructionLabel}'");
        }

        // Desordenada a propósito: la solución de una pasada es Recoger · Girar · Soltar · Girar
        // de vuelta, y el niño tiene que encontrar ese orden.
        ArmActionType[] shuffled = { ArmActionType.Drop, ArmActionType.RotateRight, ArmActionType.Pick, ArmActionType.RotateLeft };
        List<ArmBlock> ordered = shuffled
            .Select(a => armBlocks.FirstOrDefault(b => b.action == a))
            .Where(b => b != null)
            .ToList();

        if (ordered.Count == armBlocks.Count)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                SerializedProperty element = initial.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("block").objectReferenceValue = ordered[i];
                element.FindPropertyRelative("fixedInPlace").boolValue = false;
            }

            log.AppendLine("  Esc. 3: fila editable, bloques desordenados (Soltar · Girar Der · Recoger · Girar Izq)");
        }
        else
        {
            log.AppendLine("  ⚠ Esc. 3: la fila no tiene los 4 bloques esperados; solo se marcó editable");
        }

        row.ApplyModifiedProperties();
    }

    private static void SetArmAction(ArmBlock block, ArmActionType action)
    {
        SerializedObject so = new SerializedObject(block);
        so.FindProperty("action").enumValueIndex = (int)action;
        so.ApplyModifiedProperties();

        string label = block.InstructionLabel;

        foreach (TMP_Text text in block.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(text, "Rótulo");
            text.text = label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }

        foreach (UnityEngine.UI.Text text in block.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            Undo.RecordObject(text, "Rótulo");
            text.text = label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
    }

    private static ProgramTrigger FindTrigger(string challengeId) =>
        Object.FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .FirstOrDefault(t => t.ChallengeId == challengeId);

    private static void SetInt(Object owner, string field, int value)
    {
        SerializedObject so = new SerializedObject(owner);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedProperties();
    }

    // --- Utilidades ---

    private static T Find<T>(StringBuilder log) where T : Object
    {
        T found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (found == null) log.AppendLine($"  ⚠ No hay {typeof(T).Name} en la escena");
        return found;
    }

    private static T GetOrAdd<T>(GameObject host, StringBuilder log) where T : Component
    {
        T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
        {
            log.AppendLine($"  ya existía {typeof(T).Name} en '{existing.name}'");
            return existing;
        }

        log.AppendLine($"  añadido {typeof(T).Name} en '{host.name}'");
        return Undo.AddComponent<T>(host);
    }

    private static bool AddOnce(Object owner, UnityEvent unityEvent, Object target, string method, UnityAction action)
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == method)
                return false;

        Undo.RecordObject(owner, method);
        UnityEventTools.AddPersistentListener(unityEvent, action);
        EditorUtility.SetDirty(owner);
        return true;
    }

    private static bool RemoveListener(Object owner, UnityEvent unityEvent, Object target, string method)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (unityEvent.GetPersistentTarget(i) != target || unityEvent.GetPersistentMethodName(i) != method)
                continue;

            Undo.RecordObject(owner, method);
            UnityEventTools.RemovePersistentListener(unityEvent, i);
            EditorUtility.SetDirty(owner);
            return true;
        }

        return false;
    }

    private static void SetField(Object owner, string field, Object value)
    {
        if (owner == null) return;

        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[Codea] {owner.GetType().Name} no tiene el campo '{field}'.");
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetString(Object owner, string field, string value)
    {
        SerializedObject so = new SerializedObject(owner);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetEnum(Object owner, string field, int value)
    {
        SerializedObject so = new SerializedObject(owner);
        so.FindProperty(field).enumValueIndex = value;
        so.ApplyModifiedProperties();
    }
}
