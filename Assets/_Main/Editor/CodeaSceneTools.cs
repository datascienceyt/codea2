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
/// 1 - Crear escena Setup: copia la escena de juego, se queda solo con el rig del jugador y los
///     building blocks de interacción, y monta la pantalla del supervisor con su teclado.
/// 2 - Preparar escena de juego abierta: añade lo que falta para el ciclo de sesión
///     (aplicador de dificultad, tiempo agotado, vuelta a Setup).
/// 3 - Unificar dificultades: trae a la escena abierta lo que tenía Intermedio y la deja
///     como única escena de juego, Juego.unity. Se pasa una vez.
/// 4 - Montar piezas del tutorial: las cuatro interacciones y su TutorialController.
/// Ver como básica / intermedia / todo: oculta en la vista de escena las piezas de la otra
///     dificultad, sin tocar la escena.
///
/// Todas son repetibles: lo que ya existe no se duplica.
/// </summary>
public static class CodeaSceneTools
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string GameSceneName = "Juego";
    private const string SetupScene = "Assets/Scenes/Setup.unity";

    // Las dos escenas de antes de unificar. Solo las usa la herramienta 3.
    private const string BasicScene = "Assets/Scenes/Basico.unity";
    private const string IntermediateScene = "Assets/Scenes/Intermedio.unity";
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
        if (!AssetDatabase.CopyAsset(GameScene, SetupScene))
        {
            Debug.LogError($"[Codea] No se pudo copiar {GameScene}.");
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
            new EditorBuildSettingsScene(GameScene, true),
        };

        // Las escenas de antes de unificar no se vuelven a añadir aunque sigan en disco.
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (scenes.All(s => s.path != existing.path) &&
                existing.path != BasicScene && existing.path != IntermediateScene)
                scenes.Add(existing);

        EditorBuildSettings.scenes = scenes.ToArray();

        log.AppendLine($"  Build Settings: Setup (0), {GameSceneName} (1), las dos activas");
    }

    // --- 2. Escena de juego ---

    [MenuItem("Tools/Codea/2 - Preparar escena de juego abierta")]
    private static void PrepareGameScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!EditorUtility.DisplayDialog("Preparar escena de juego",
                $"Escena '{scene.name}'.\n\n" +
                "Se añaden DifficultyApplier, SceneLoader y TimeUpSequence, y se cablean el " +
                "tiempo agotado y la vuelta a Setup.",
                "Preparar", "Cancelar"))
            return;

        PrepareGameScene(scene);
    }

    private static void PrepareGameScene(Scene scene)
    {
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

        GetOrAdd<DifficultyApplier>(session, log);

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
            ("Escenario 4", FindScenario4(), null),
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

    // --- 3. Unificar dificultades ---

    /// <summary>
    /// Deja la escena abierta (Basico) como la única escena de juego, con las dos dificultades.
    /// Lee de Intermedio lo que allí era distinto y lo declara en la propia escena:
    ///
    ///   · Narración: NarrationDifficulty con la lista de cada dificultad, y la frase 0 del
    ///     tutorial también al principio de la lista intermedia
    ///   · Esc. 1: Scenario1Difficulty (nivel y huecos), y los bloques de la paleta que solo
    ///     están en una de las dos con DifficultyOnly, en la posición que tenían en su escena
    ///   · Esc. 2: Scenario2Difficulty con los ModuleData de cada dificultad
    ///   · Esc. 3: Scenario3Difficulty (fila editable y bloques iniciales), y los giros
    ///     literales de intermedia como bloques propios con DifficultyOnly
    ///   · Esc. 4: solo compara las figuras; si difieren, avisa
    ///
    /// Después pasa la herramienta 2, renombra la escena a Juego y deja Build Settings con
    /// Setup y Juego. Intermedio.unity no se borra: se borra a mano tras comprobar el resultado.
    /// Repetible: lo que ya está unificado no se duplica.
    /// </summary>
    [MenuItem("Tools/Codea/3 - Unificar dificultades en esta escena")]
    private static void UnifyDifficultiesMenu()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != BasicScene && scene.path != GameScene)
        {
            EditorUtility.DisplayDialog("Unificar dificultades",
                $"La escena abierta es '{scene.name}'. Abre {BasicScene} (o {GameScene}) y vuelve " +
                "a pasar la herramienta.", "Vale");
            return;
        }

        if (!File.Exists(IntermediateScene))
        {
            EditorUtility.DisplayDialog("Unificar dificultades",
                $"No existe {IntermediateScene}: no hay de dónde leer la intermedia.", "Vale");
            return;
        }

        if (!EditorUtility.DisplayDialog("Unificar dificultades",
                $"'{scene.name}' pasa a tener las dos dificultades, leyendo de Intermedio lo que " +
                "allí era distinto, y se renombra a Juego.\n\nIntermedio no se toca ni se borra.",
                "Unificar", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        UnifyDifficulties(scene);
    }

    /// <summary>
    /// La misma herramienta sin abrir Unity:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod CodeaSceneTools.UnifyDifficultiesBatch
    /// </summary>
    public static void UnifyDifficultiesBatch()
    {
        string path = File.Exists(GameScene) ? GameScene : BasicScene;
        UnifyDifficulties(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
    }

    private static void UnifyDifficulties(Scene scene)
    {
        StringBuilder log = new StringBuilder($"[Codea] Unificando dificultades en '{scene.name}':\n");

        Scene intermediate = EditorSceneManager.OpenScene(IntermediateScene, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        try
        {
            UnifyNarration(scene, intermediate, log);
            UnifyScenario1(scene, intermediate, log);
            UnifyScenario2(scene, intermediate, log);
            UnifyScenario3(scene, intermediate, log);
            CompareScenario4(scene, intermediate, log);
        }
        finally
        {
            // Antes de la herramienta 2: sus búsquedas recorren todas las escenas abiertas.
            EditorSceneManager.CloseScene(intermediate, true);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());

        PrepareGameScene(scene);

        log = new StringBuilder("[Codea] Unificación, cierre:\n");

        if (scene.path == BasicScene)
        {
            string error = AssetDatabase.RenameAsset(BasicScene, GameSceneName);
            log.AppendLine(string.IsNullOrEmpty(error)
                ? $"  {BasicScene} renombrada a {GameScene}"
                : $"  ⚠ No se pudo renombrar la escena: {error}");
        }

        SetBuildScenes(log);
        AssetDatabase.SaveAssets();

        log.AppendLine("  ⚠ Falta: comprobar con Tools → Codea → Ver como básica / intermedia, probar " +
                       "las dos en Play (DifficultyApplier → Editor Difficulty) y borrar Intermedio.unity.");
        Debug.Log(log.ToString());
    }

    private static void UnifyNarration(Scene scene, Scene intermediate, StringBuilder log)
    {
        Narrator narrator = InScene<Narrator>(scene).FirstOrDefault();
        Narrator other = InScene<Narrator>(intermediate).FirstOrDefault();

        if (narrator == null)
        {
            log.AppendLine("  ⚠ Narración: no hay Narrator en la escena");
            return;
        }

        int basicIndex = new SerializedObject(narrator).FindProperty("listIndex").intValue;
        int intermediateIndex = other != null ? new SerializedObject(other).FindProperty("listIndex").intValue : 2;

        NarrationDifficulty variant = GetOrAddOn<NarrationDifficulty>(narrator.gameObject, log);
        Undo.RecordObject(variant, "Narración");
        variant.SetNarrator(narrator);
        variant.SetSettings(new NarrationDifficulty.Settings { listIndex = basicIndex },
                            new NarrationDifficulty.Settings { listIndex = intermediateIndex });
        EditorUtility.SetDirty(variant);
        log.AppendLine($"  Narración: lista {basicIndex} en básica, {intermediateIndex} en intermedia");

        // El Director espera al Narrator una vez por entrada: la lista intermedia tiene que
        // empezar también por la frase del tutorial, o todo lo demás sale desplazado.
        SerializedObject so = new SerializedObject(narrator);
        SerializedProperty lists = so.FindProperty("lists");
        if (basicIndex >= lists.arraySize || intermediateIndex >= lists.arraySize) return;

        SerializedProperty basic = lists.GetArrayElementAtIndex(basicIndex).FindPropertyRelative("entries");
        SerializedProperty inter = lists.GetArrayElementAtIndex(intermediateIndex).FindPropertyRelative("entries");

        bool StartsWithTutorial(SerializedProperty entries) =>
            entries.arraySize > 0 && entries.GetArrayElementAtIndex(0).FindPropertyRelative("textId").stringValue == "0";

        if (StartsWithTutorial(basic) && !StartsWithTutorial(inter))
        {
            SerializedProperty source = basic.GetArrayElementAtIndex(0);
            inter.InsertArrayElementAtIndex(0);
            SerializedProperty added = inter.GetArrayElementAtIndex(0);
            added.FindPropertyRelative("clip").objectReferenceValue = source.FindPropertyRelative("clip").objectReferenceValue;
            added.FindPropertyRelative("textId").stringValue = "0";
            so.ApplyModifiedProperties();
            log.AppendLine("  Narración: frase 0 del tutorial añadida al principio de la lista intermedia");
        }

        log.AppendLine(basic.arraySize == inter.arraySize
            ? $"  Narración: las dos listas tienen {basic.arraySize} entradas"
            : $"  ⚠ Narración: la lista básica tiene {basic.arraySize} entradas y la intermedia " +
              $"{inter.arraySize}. Tienen que coincidir");
    }

    private static void UnifyScenario1(Scene scene, Scene intermediate, StringBuilder log)
    {
        ProgramTrigger trigger = TriggerIn(scene, TelemetryManager.Scenario1Id);
        ProgramTrigger otherTrigger = TriggerIn(intermediate, TelemetryManager.Scenario1Id);
        LevelLoader loader = InScene<LevelLoader>(scene).FirstOrDefault();
        LevelLoader otherLoader = InScene<LevelLoader>(intermediate).FirstOrDefault();

        if (trigger == null || trigger.socketRow == null || loader == null ||
            otherTrigger == null || otherTrigger.socketRow == null || otherLoader == null)
        {
            log.AppendLine("  ⚠ Esc. 1: falta el ProgramTrigger, su fila o el LevelLoader en alguna escena");
            return;
        }

        Scenario1Controller controller = InScene<Scenario1Controller>(scene).FirstOrDefault();
        GameObject host = controller != null ? controller.gameObject : trigger.gameObject;

        Scenario1Difficulty variant = GetOrAddOn<Scenario1Difficulty>(host, log);
        Undo.RecordObject(variant, "Escenario 1");
        variant.SetTargets(loader, trigger.socketRow);
        variant.SetSettings(
            new Scenario1Difficulty.Settings { level = loader.levelJson, sockets = trigger.socketRow.SocketsQuantity },
            new Scenario1Difficulty.Settings { level = otherLoader.levelJson, sockets = otherTrigger.socketRow.SocketsQuantity });
        EditorUtility.SetDirty(variant);

        log.AppendLine($"  Esc. 1: básica '{Name(loader.levelJson)}' con {trigger.socketRow.SocketsQuantity} huecos, " +
                       $"intermedia '{Name(otherLoader.levelJson)}' con {otherTrigger.socketRow.SocketsQuantity}");

        // Paleta. Un bloque que está en las dos escenas con la misma acción y en el mismo sitio
        // es común; uno que solo está en Intermedio se crea aquí, copiando uno de la misma
        // acción, y uno que solo está en Basico pasa a ser de básica.
        List<GridBlock> blocks = InScene<GridBlock>(scene).Where(b => !InTutorial(b)).ToList();
        List<GridBlock> others = InScene<GridBlock>(intermediate).Where(b => !InTutorial(b)).ToList();
        HashSet<GridBlock> used = new HashSet<GridBlock>();
        int shared = 0, created = 0;

        foreach (GridBlock wanted in others)
        {
            GridBlock same = blocks.FirstOrDefault(b => !used.Contains(b) && b.action == wanted.action &&
                                                        OnlyIn(b) != Difficulty.Basica &&
                                                        Vector3.Distance(b.transform.position, wanted.transform.position) < 0.01f);
            if (same != null)
            {
                used.Add(same);
                if (OnlyIn(same) == null) shared++;
                continue;
            }

            GridBlock source = blocks.FirstOrDefault(b => b.action == wanted.action);
            GameObject copy = source != null ? DuplicateAnywhere(source.gameObject) : null;
            if (copy == null)
            {
                log.AppendLine($"  ⚠ Esc. 1: no se pudo crear el bloque '{wanted.name}' ({wanted.action})");
                continue;
            }

            copy.name = wanted.name;
            copy.transform.SetPositionAndRotation(wanted.transform.position, wanted.transform.rotation);
            MarkOnly(copy, Difficulty.Avanzada);

            GridBlock block = copy.GetComponent<GridBlock>();
            used.Add(block);
            blocks.Add(block);
            created++;
        }

        int basicOnly = 0;
        foreach (GridBlock block in blocks)
        {
            if (used.Contains(block) || OnlyIn(block) != null) continue;

            MarkOnly(block.gameObject, Difficulty.Basica);
            basicOnly++;
        }

        log.AppendLine($"  Esc. 1: paleta con {shared} bloques comunes, {created} nuevos de intermedia y " +
                       $"{basicOnly} pasados a solo básica " +
                       $"(básica {blocks.Count(b => OnlyIn(b) != Difficulty.Avanzada)}, " +
                       $"intermedia {blocks.Count(b => OnlyIn(b) != Difficulty.Basica)})");
    }

    private static void UnifyScenario2(Scene scene, Scene intermediate, StringBuilder log)
    {
        Scenario2Controller controller = InScene<Scenario2Controller>(scene).FirstOrDefault();
        if (controller == null)
        {
            log.AppendLine("  ⚠ Esc. 2: no hay Scenario2Controller");
            return;
        }

        List<SystemModule> modules = controller.GetComponentsInChildren<SystemModule>(true).ToList();
        List<SystemModule> others = InScene<SystemModule>(intermediate);

        Scenario2Difficulty.Settings basic = new Scenario2Difficulty.Settings();
        Scenario2Difficulty.Settings inter = new Scenario2Difficulty.Settings();

        foreach (SystemModule module in modules)
        {
            ModuleData data = module.Data;
            ModuleData other = others.Where(m => m.Data != null && data != null && m.Data.moduleId == data.moduleId)
                                     .Select(m => m.Data).FirstOrDefault();

            if (other == null && data != null)
                other = AssetDatabase.LoadAssetAtPath<ModuleData>(AssetDatabase.GetAssetPath(data).Replace("_Basica", "_Intermedia"));

            basic.modules.Add(data);
            inter.modules.Add(other);
            log.AppendLine(other != null
                ? $"  Esc. 2: {module.name} → {Name(data)} / {Name(other)}"
                : $"  ⚠ Esc. 2: {module.name} sin datos de intermedia");
        }

        Scenario2Difficulty variant = GetOrAddOn<Scenario2Difficulty>(controller.gameObject, log);
        Undo.RecordObject(variant, "Escenario 2");
        variant.SetModules(modules);
        variant.SetSettings(basic, inter);
        EditorUtility.SetDirty(variant);
    }

    private static void UnifyScenario3(Scene scene, Scene intermediate, StringBuilder log)
    {
        ProgramTrigger trigger = TriggerIn(scene, TelemetryManager.Scenario3Id);
        ProgramTrigger otherTrigger = TriggerIn(intermediate, TelemetryManager.Scenario3Id);

        if (trigger == null || trigger.socketRow == null || otherTrigger == null || otherTrigger.socketRow == null)
        {
            log.AppendLine("  ⚠ Esc. 3: falta el ProgramTrigger o su fila en alguna escena");
            return;
        }

        SocketRow row = trigger.socketRow;
        List<ArmBlock> blocks = InScene<ArmBlock>(scene).Where(b => !InTutorial(b)).ToList();

        Scenario3Difficulty.Settings basic = new Scenario3Difficulty.Settings
        {
            editable = row.IsEditable,
            initialBlocks = row.InitialBlocks
                               .Select(i => new SocketRow.InitialBlock { block = i?.block, fixedInPlace = i != null && i.fixedInPlace })
                               .ToList(),
        };

        // Cada bloque de la fila intermedia se busca aquí por su acción. Los giros literales no
        // existen en Basico: se crean copiando el bloque del que salieron en Intermedio (mismo
        // nombre, porque Intermedio es un duplicado de Basico), que pasa a ser de básica.
        Scenario3Difficulty.Settings inter = new Scenario3Difficulty.Settings { editable = otherTrigger.socketRow.IsEditable };

        foreach (SocketRow.InitialBlock wanted in otherTrigger.socketRow.InitialBlocks)
        {
            ArmBlock wantedBlock = wanted?.block as ArmBlock;
            ArmBlock block = null;

            if (wantedBlock != null)
            {
                block = blocks.FirstOrDefault(b => b.action == wantedBlock.action && OnlyIn(b) != Difficulty.Basica);

                if (block == null)
                {
                    ArmBlock source = blocks.FirstOrDefault(b => b.name == wantedBlock.name) ??
                                      blocks.FirstOrDefault(b => b.action == wantedBlock.action);
                    GameObject copy = source != null ? DuplicateAnywhere(source.gameObject) : null;

                    if (copy == null)
                    {
                        log.AppendLine($"  ⚠ Esc. 3: no se pudo crear el bloque '{wantedBlock.InstructionLabel}'");
                    }
                    else
                    {
                        block = copy.GetComponent<ArmBlock>();
                        SetArmAction(block, wantedBlock.action);
                        copy.name = $"{source.name} ({block.InstructionLabel})";
                        MarkOnly(copy, Difficulty.Avanzada);
                        blocks.Add(block);

                        if (OnlyIn(source) == null) MarkOnly(source.gameObject, Difficulty.Basica);
                        log.AppendLine($"  Esc. 3: creado '{block.InstructionLabel}' para intermedia; " +
                                       $"'{source.InstructionLabel}' pasa a solo básica");
                    }
                }
            }

            inter.initialBlocks.Add(new SocketRow.InitialBlock { block = block, fixedInPlace = wanted != null && wanted.fixedInPlace });
        }

        Scenario3Controller controller = InScene<Scenario3Controller>(scene).FirstOrDefault();
        Scenario3Difficulty variant = GetOrAddOn<Scenario3Difficulty>(controller != null ? controller.gameObject : trigger.gameObject, log);
        Undo.RecordObject(variant, "Escenario 3");
        variant.SetRow(row);
        variant.SetSettings(basic, inter);
        EditorUtility.SetDirty(variant);

        string Row(Scenario3Difficulty.Settings s) =>
            string.Join(" · ", s.initialBlocks.Select(i => i.block is ArmBlock a ? a.InstructionLabel : "(vacío)")) +
            (s.editable ? ", editable" : ", bloqueada");

        log.AppendLine($"  Esc. 3: básica {Row(basic)}");
        log.AppendLine($"  Esc. 3: intermedia {Row(inter)}");
    }

    private static void CompareScenario4(Scene scene, Scene intermediate, StringBuilder log)
    {
        string Shapes(Scene s) => string.Join(", ", InScene<ShapeSocket>(s)
            .Where(x => !InTutorial(x))
            .Select(x => new SerializedObject(x).FindProperty("expectedShape").objectReferenceValue)
            .Select(o => o != null ? o.name : "(sin figura)")
            .OrderBy(n => n));

        string basic = Shapes(scene), inter = Shapes(intermediate);

        log.AppendLine(basic == inter
            ? $"  Esc. 4: mismas figuras en las dos ({basic}). Para la intermedia propia (pedido 11), " +
              "marca sus huecos y fichas con DifficultyOnly"
            : $"  ⚠ Esc. 4: las figuras difieren. Básica: {basic}. Intermedia: {inter}. Monta las de " +
              "intermedia aquí y márcalas con DifficultyOnly");
    }

    // --- Vista previa por dificultad ---

    [MenuItem("Tools/Codea/Ver como básica")]
    private static void ShowBasic() => ShowDifficulty(Difficulty.Basica);

    [MenuItem("Tools/Codea/Ver como intermedia")]
    private static void ShowIntermediate() => ShowDifficulty(Difficulty.Avanzada);

    [MenuItem("Tools/Codea/Ver todas las piezas")]
    private static void ShowAllPieces() => ShowDifficulty(null);

    /// <summary>
    /// Oculta en la vista de escena las piezas de la otra dificultad. Es solo visibilidad del
    /// editor (el ojo de la jerarquía): no cambia la escena ni lo que pasa en Play.
    /// </summary>
    private static void ShowDifficulty(Difficulty? difficulty)
    {
        SceneVisibilityManager visibility = SceneVisibilityManager.instance;
        int shown = 0, hidden = 0;

        foreach (DifficultyOnly only in Object.FindObjectsByType<DifficultyOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (difficulty == null || only.Difficulty == difficulty)
            {
                visibility.Show(only.gameObject, true);
                shown++;
            }
            else
            {
                visibility.Hide(only.gameObject, true);
                hidden++;
            }
        }

        Debug.Log(difficulty == null
            ? $"[Codea] Vista: todas las piezas ({shown} de una sola dificultad)"
            : $"[Codea] Vista: {DifficultyApplier.Label(difficulty.Value)} · {shown} piezas propias visibles, " +
              $"{hidden} de la otra ocultas. Para probarla en Play: DifficultyApplier → Editor Difficulty");
    }

    // --- Utilidades de la unificación ---

    private static List<T> InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToList();

    private static ProgramTrigger TriggerIn(Scene scene, string challengeId) =>
        InScene<ProgramTrigger>(scene).FirstOrDefault(t => t.ChallengeId == challengeId);

    private static bool InTutorial(Component component) =>
        component.GetComponentInParent<TutorialController>(true) != null ||
        TutorialRoomNames.Contains(component.transform.root.name);

    private static Difficulty? OnlyIn(Component component) =>
        component.TryGetComponent(out DifficultyOnly only) ? only.Difficulty : (Difficulty?)null;

    private static void MarkOnly(GameObject target, Difficulty difficulty)
    {
        if (!target.TryGetComponent(out DifficultyOnly only)) only = Undo.AddComponent<DifficultyOnly>(target);

        Undo.RecordObject(only, "Dificultad");
        only.Difficulty = difficulty;
        EditorUtility.SetDirty(only);
    }

    /// <summary>GetOrAdd en un objeto concreto, sin buscar en las demás escenas abiertas.</summary>
    private static T GetOrAddOn<T>(GameObject host, StringBuilder log) where T : Component
    {
        if (host.TryGetComponent(out T existing)) return existing;

        log.AppendLine($"  añadido {typeof(T).Name} en '{host.name}'");
        return Undo.AddComponent<T>(host);
    }

    /// <summary>
    /// Duplica conservando el enlace al prefab. El portapapeles del editor no siempre está en
    /// modo batch: entonces se instancia el mismo prefab con los mismos ajustes, o se clona.
    /// </summary>
    private static GameObject DuplicateAnywhere(GameObject source)
    {
        GameObject copy = Application.isBatchMode ? null : Duplicate(source);
        if (copy != null) return copy;

        if (PrefabUtility.IsOutermostPrefabInstanceRoot(source))
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
            copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, source.transform.parent);
            PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(source));
        }
        else
        {
            copy = Object.Instantiate(source, source.transform.parent);
        }

        copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(copy, "Duplicar");
        return copy;
    }

    private static string Name(Object asset) => asset != null ? asset.name : "(nada)";

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

    // --- 4. Tutorial ---

    private const string ChipPrefab = "Assets/_Main/Prefabs/Blocks/ShapeChip.prefab";
    private const string SocketPrefab = "Assets/_Main/Prefabs/Blocks/ShapeSocket.prefab";
    private const string BlockSocketPrefab = "Assets/_Main/Prefabs/Blocks/socket.prefab";
    private const string ProgramButtonPrefab = "Assets/_Main/Prefabs/Scenario3/Button.prefab";
    private const string OptionButtonPrefab = "Assets/_Main/Prefabs/Scenario2/Button.prefab";
    private const string ShapesSheet = "Assets/_Main/Scripts/Scenario 4/Figuras/symbols.png";

    // El cuarto se llamó Start hasta que pasó a ser el tutorial.
    private static readonly string[] TutorialRoomNames = { "Tutorial", "Start" };
    private const string TutorialDesk = "Desk";
    private const string TutorialProgramButton = "Boton del Escenario 1";
    private const string TutorialOptionButton = "Boton del Escenario 2";

    // Una figura que no sale en el Escenario 4: aquí se aprende el gesto de encajar, no a
    // distinguir figuras.
    private const string TutorialShape = "flecha_sola";

    /// <summary>
    /// Deja el cuarto del tutorial con sus cuatro interacciones, el TutorialController que
    /// las vigila y el arranque del Director al terminarlas:
    ///
    ///   · un botón como el de ejecutar del Escenario 1
    ///   · un botón como los de los módulos del Escenario 2
    ///   · un bloque y un hueco del Escenario 1
    ///   · un hueco y su ficha del Escenario 4
    ///
    /// Lo que ya esté en el cuarto se respeta tal cual —ni se mueve ni cambia de padre— y
    /// solo se cablea; lo que falte se crea en fila sobre la mesa. No guarda la escena.
    /// </summary>
    [MenuItem("Tools/Codea/4 - Montar piezas del tutorial")]
    private static void BuildTutorial()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject room = FindTutorialRoom(scene);

        if (room == null)
        {
            EditorUtility.DisplayDialog("Tutorial",
                $"'{scene.name}' no tiene un objeto raíz llamado 'Tutorial' (ni 'Start'), que es " +
                "el cuarto del tutorial.", "Vale");
            return;
        }

        StringBuilder log = new StringBuilder($"[Codea] Tutorial en '{scene.name}/{room.name}':\n");

        // Lo nuevo sale en fila sobre la mesa. Es orientativo.
        Transform desk = room.transform.Find(TutorialDesk);
        Vector3 rowStart = desk != null
            ? desk.localPosition + new Vector3(-0.3f, 0.5f, -0.3f)
            : new Vector3(0f, 1f, 0f);
        int slot = 0;
        Vector3 NextSlot() => rowStart + new Vector3(0f, 0f, -0.2f * slot++);

        ShapeSocket shapeSocket = BuildTutorialShapes(room, NextSlot, log);
        Socket blockSocket = BuildTutorialBlock(room, NextSlot, log);
        InteractableUnityEventWrapper programButton = BuildTutorialProgramButton(room.transform, NextSlot, log);
        ModuleOptionButton optionButton = BuildTutorialOptionButton(room, NextSlot, log);

        if (!room.TryGetComponent(out TutorialController tutorial))
        {
            tutorial = Undo.AddComponent<TutorialController>(room);
            log.AppendLine("  añadido TutorialController");
        }

        SetField(tutorial, "programButton", programButton);
        SetField(tutorial, "optionButton", optionButton);
        SetField(tutorial, "blockSocket", blockSocket);
        SetField(tutorial, "shapeSocket", shapeSocket);

        ConnectTutorial(room, log);

        Selection.activeGameObject = room;
        EditorSceneManager.MarkSceneDirty(scene);

        log.AppendLine("  ⚠ Falta a mano: recolocar sobre la mesa lo que se haya creado (si se creó " +
                       "algo) y guardar la escena.");
        Debug.Log(log.ToString(), room);
    }

    private static GameObject FindTutorialRoom(Scene scene) =>
        scene.GetRootGameObjects().FirstOrDefault(r => TutorialRoomNames.Contains(r.name));

    /// <summary>
    /// Deja el cuarto listo para funcionar en su escena:
    ///
    ///   · El Director sin arranque automático: lo arranca TutorialController al terminar,
    ///     buscándolo en la escena. No se cablea, y si hubiera un cable de antes se quita.
    ///   · Un BlockResetter en la raíz, que es a donde vuelve sola una ficha que se cae al
    ///     suelo. Va en la raíz y no en un contenedor para no tocar la jerarquía; nadie llama
    ///     a su ResetBlocks, así que la sala y la mesa no se ven afectadas.
    ///   · El Scenario4Controller sin telemetría SOLO si en el cuarto hay alguna ficha que no
    ///     encaja: su único papel aquí es expulsarla. Sin fichas incorrectas, sobra.
    /// </summary>
    private static void ConnectTutorial(GameObject room, StringBuilder log)
    {
        if (!room.TryGetComponent(out BlockResetter resetter))
        {
            resetter = Undo.AddComponent<BlockResetter>(room);
            log.AppendLine("  añadido BlockResetter: una ficha caída vuelve sola a su sitio");
        }

        ShapeSocket shapeSocket = room.GetComponentInChildren<ShapeSocket>(true);
        ShapeChip[] chips = room.GetComponentsInChildren<ShapeChip>(true);
        bool hasWrongChip = shapeSocket != null && chips.Any(c => c.Shape != shapeSocket.Expected);

        if (shapeSocket != null && chips.Length > 0 && !chips.Any(c => c.Shape == shapeSocket.Expected))
            log.AppendLine("  ⚠ Ninguna ficha del cuarto lleva la figura del hueco: no se puede acertar");

        Scenario4Controller rejecter = room.GetComponent<Scenario4Controller>();

        if (hasWrongChip)
        {
            if (rejecter == null)
            {
                rejecter = Undo.AddComponent<Scenario4Controller>(room);
                Scenario4Controller scenario4 = FindScenario4();

                if (scenario4 != null)
                {
                    // Mismo rechazo que en el escenario, para que el tutorial enseñe lo que va a pasar.
                    SerializedObject from = new SerializedObject(scenario4);
                    SerializedObject to = new SerializedObject(rejecter);

                    foreach (string field in new[] { "rejectDelay", "rejectSpeed", "rejectLocalDirection" })
                        to.CopyFromSerializedProperty(from.FindProperty(field));

                    to.ApplyModifiedProperties();
                }

                log.AppendLine("  añadido Scenario4Controller sin telemetría: expulsa la ficha incorrecta");
            }

            SetField(rejecter, "chipResetter", resetter);
            SetString(rejecter, "challengeId", string.Empty);
        }
        else if (rejecter != null && string.IsNullOrEmpty(rejecter.ChallengeId))
        {
            Undo.DestroyObjectImmediate(rejecter);
            log.AppendLine("  quitado Scenario4Controller: no hay ficha incorrecta que expulsar");
        }

        TutorialController tutorial = room.GetComponent<TutorialController>();
        Director director = Find<Director>(log);

        if (tutorial == null)
        {
            log.AppendLine("  ⚠ El cuarto no tiene TutorialController: pásale Tools → Codea → 4");
            return;
        }

        if (director == null) return;

        if (new SerializedObject(director).FindProperty("playOnStart").boolValue)
        {
            SetBool(director, "playOnStart", false);
            log.AppendLine("  Director: playOnStart desactivado, arranca al terminar el tutorial");
        }

        // TutorialController arranca el Director él solo. Un cable además lo arrancaría dos
        // veces, con dos recorridos en paralelo.
        if (RemoveListener(tutorial, tutorial.OnTutorialFinished, director, nameof(Director.Play)))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial);
            log.AppendLine("  quitado OnTutorialFinished → Director.Play: ya lo hace el propio TutorialController");
        }
    }

    /// <summary>El hueco de figura y sus dos fichas. Devuelve el hueco.</summary>
    private static ShapeSocket BuildTutorialShapes(GameObject room, System.Func<Vector3> nextSlot,
                                                   StringBuilder log)
    {
        Sprite[] sheet = AssetDatabase.LoadAllAssetsAtPath(ShapesSheet).OfType<Sprite>().ToArray();
        Sprite shape = sheet.FirstOrDefault(s => s.name == TutorialShape);

        Scenario4Controller scenario4 = FindScenario4();
        ShapeChip sampleChip = scenario4 != null ? scenario4.GetComponentInChildren<ShapeChip>(true) : null;
        ShapeSocket sampleSocket = scenario4 != null ? scenario4.GetComponentInChildren<ShapeSocket>(true) : null;

        ShapeSocket socket = room.GetComponentInChildren<ShapeSocket>(true);
        if (socket == null)
        {
            socket = PlacePiece<ShapeSocket>(SocketPrefab, room.transform, "Hueco de figura", nextSlot(),
                sampleSocket != null ? sampleSocket.transform : null, log);

            if (socket != null)
            {
                SetField(socket, "expectedShape", shape);
                socket.ApplyShapeToChild();
                log.AppendLine($"  creado el hueco de figura ({TutorialShape})");
            }
        }

        // Una sola ficha, la que encaja. Si el usuario añade otra que no encaje, ConnectTutorial
        // pone lo necesario para expulsarla.
        if (room.GetComponentsInChildren<ShapeChip>(true).Length == 0)
        {
            AddTutorialChip(room.transform, "Ficha correcta", nextSlot(), shape, sampleChip, log);
            log.AppendLine($"  creada la ficha ({TutorialShape})");
        }

        return socket;
    }

    private static void AddTutorialChip(Transform parent, string name, Vector3 localPosition, Sprite shape,
                                        ShapeChip sample, StringBuilder log)
    {
        ShapeChip chip = PlacePiece<ShapeChip>(ChipPrefab, parent, name, localPosition,
            sample != null ? sample.transform : null, log);

        if (chip == null) return;

        SetField(chip, "shape", shape);
        chip.ApplyShapeToChild();

        // La física se copia de una ficha del escenario: en el prefab viene sin gravedad.
        if (sample != null && sample.TryGetComponent(out Rigidbody from) && chip.TryGetComponent(out Rigidbody to))
        {
            Undo.RecordObject(to, "Física de la ficha");
            to.useGravity = from.useGravity;
            to.isKinematic = from.isKinematic;
            PrefabUtility.RecordPrefabInstancePropertyModifications(to);
        }
    }

    /// <summary>Un bloque del Escenario 1 y un hueco suelto donde encajarlo. Devuelve el hueco.</summary>
    private static Socket BuildTutorialBlock(GameObject room, System.Func<Vector3> nextSlot,
                                             StringBuilder log)
    {
        GridBlock block = room.GetComponentInChildren<GridBlock>(true);

        if (block == null)
        {
            // Se duplica uno del escenario: hereda rótulo, tamaño, prefab y eventos.
            GridBlock[] all = Object.FindObjectsByType<GridBlock>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GridBlock source = all.FirstOrDefault(b => b.action == GridActionType.MoveForward) ?? all.FirstOrDefault();
            GameObject copy = source != null ? Duplicate(source.gameObject) : null;

            if (copy == null)
            {
                log.AppendLine("  ⚠ No hay ningún bloque del Escenario 1 que duplicar: falta el bloque del tutorial");
            }
            else
            {
                Undo.SetTransformParent(copy.transform, room.transform, "Tutorial");
                copy.name = "Bloque";
                copy.transform.localPosition = nextSlot();
                log.AppendLine($"  creado el bloque '{source.InstructionLabel}'");
            }
        }

        // Los huecos de figura también llevan Socket: el de bloque es el que no es ShapeSocket.
        Socket socket = room.GetComponentsInChildren<Socket>(true)
                            .FirstOrDefault(s => s.GetComponent<ShapeSocket>() == null);

        if (socket == null)
        {
            // El mismo prefab con el que la fila del Escenario 1 genera sus huecos al arrancar.
            string path = BlockSocketPrefab;
            ProgramTrigger trigger = FindTrigger(TelemetryManager.Scenario1Id);

            if (trigger != null && trigger.socketRow != null)
            {
                Object rowPrefab = new SerializedObject(trigger.socketRow).FindProperty("socketPrefab").objectReferenceValue;
                if (rowPrefab != null) path = AssetDatabase.GetAssetPath(rowPrefab);
            }

            socket = PlacePiece<Socket>(path, room.transform, "Hueco de bloque", nextSlot(), null, log);
            if (socket != null) log.AppendLine("  creado el hueco de bloque");
        }

        return socket;
    }

    /// <summary>
    /// Copia del botón de ejecutar del Escenario 1, sin su cable: en el tutorial no ejecuta
    /// nada, solo lo escucha el TutorialController.
    /// </summary>
    private static InteractableUnityEventWrapper BuildTutorialProgramButton(Transform room,
        System.Func<Vector3> nextSlot, StringBuilder log)
    {
        Transform existing = room.Find(TutorialProgramButton);
        if (existing != null)
            return existing.GetComponentInChildren<InteractableUnityEventWrapper>(true);

        ProgramTrigger trigger = FindTrigger(TelemetryManager.Scenario1Id);
        InteractableUnityEventWrapper source = trigger == null ? null :
            Object.FindObjectsByType<InteractableUnityEventWrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .FirstOrDefault(w => Calls(w.WhenSelect, trigger));

        GameObject copy = null;

        if (source != null)
        {
            GameObject sourceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(source.gameObject);
            copy = Duplicate(sourceRoot != null ? sourceRoot : source.gameObject);
        }

        if (copy != null)
        {
            Undo.SetTransformParent(copy.transform, room, "Tutorial");
        }
        else
        {
            // Sin el del escenario a mano, el prefab tal cual: mismo botón, sin sus ajustes.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProgramButtonPrefab);
            if (prefab == null)
            {
                log.AppendLine($"  ⚠ No encuentro el botón del Escenario 1 ni {ProgramButtonPrefab}");
                return null;
            }

            copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            Undo.RegisterCreatedObjectUndo(copy, "Tutorial");
            log.AppendLine("  ⚠ No encuentro el botón de ejecutar del Escenario 1: se usa el prefab sin ajustar");
        }

        copy.name = TutorialProgramButton;
        copy.transform.localPosition = nextSlot();

        InteractableUnityEventWrapper wrapper = copy.GetComponentInChildren<InteractableUnityEventWrapper>(true);

        // Imprescindible: la copia hereda el cable al ProgramTrigger, y pulsarla en el
        // tutorial ejecutaría el programa del Escenario 1.
        if (wrapper != null && trigger != null)
        {
            while (RemoveListener(wrapper, wrapper.WhenSelect, trigger, nameof(ProgramTrigger.OnPlayPressed))) { }
            PrefabUtility.RecordPrefabInstancePropertyModifications(wrapper);

            if (Calls(wrapper.WhenSelect, trigger))
                log.AppendLine($"  ⚠ '{TutorialProgramButton}' sigue cableado al ProgramTrigger del " +
                               "Escenario 1: quítale ese evento a mano");
        }

        log.AppendLine("  creado el botón del Escenario 1");
        return wrapper;
    }

    /// <summary>Un botón de módulo en modo 'standalone': se marca solo, sin avería detrás.</summary>
    private static ModuleOptionButton BuildTutorialOptionButton(GameObject room,
        System.Func<Vector3> nextSlot, StringBuilder log)
    {
        ModuleOptionButton button = room.GetComponentInChildren<ModuleOptionButton>(true);

        if (button == null)
        {
            ModuleOptionButton sample = Object.FindAnyObjectByType<ModuleOptionButton>(FindObjectsInactive.Include);
            Transform sampleRoot = sample == null ? null :
                (PrefabUtility.GetNearestPrefabInstanceRoot(sample.gameObject) ?? sample.gameObject).transform;

            button = PlacePiece<ModuleOptionButton>(OptionButtonPrefab, room.transform, TutorialOptionButton,
                nextSlot(), sampleRoot, log);

            if (button == null) return null;

            // El rótulo solo al crearlo: si ya existía, el texto es del usuario.
            SerializedProperty label = new SerializedObject(button).FindProperty("label");
            if (label != null && label.objectReferenceValue is TMP_Text text)
            {
                Undo.RecordObject(text, "Tutorial");
                text.text = "Púlsame";
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }

            log.AppendLine("  creado el botón del Escenario 2");
        }

        SetField(button, "module", null);
        SetBool(button, "standalone", true);
        return button;
    }

    private static bool Calls(UnityEvent unityEvent, Object target)
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            if (unityEvent.GetPersistentTarget(i) == target) return true;

        return false;
    }

    /// <summary>Duplica en la escena conservando el enlace al prefab y sus ajustes.</summary>
    private static GameObject Duplicate(GameObject source)
    {
        Selection.activeGameObject = source;
        Unsupported.DuplicateGameObjectsUsingPasteboard();
        GameObject copy = Selection.activeGameObject;

        return copy != null && copy != source ? copy : null;
    }

    /// <summary>Instancia el prefab enlazado, con el giro y el tamaño de una pieza ya ajustada.</summary>
    private static T PlacePiece<T>(string prefabPath, Transform parent, string name, Vector3 localPosition,
                                   Transform sample, StringBuilder log) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            log.AppendLine($"  ⚠ No encuentro {prefabPath}: falta '{name}'");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Tutorial");
        instance.name = name;
        instance.transform.localPosition = localPosition;

        if (sample != null)
        {
            instance.transform.localRotation = sample.localRotation;
            instance.transform.localScale = sample.localScale;
        }

        return instance.GetComponentInChildren<T>(true);
    }

    private static void SetBool(Object owner, string field, bool value)
    {
        SerializedObject so = new SerializedObject(owner);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// El controlador del Escenario 4 de verdad. Desde que el tutorial tiene el suyo hay dos
    /// en la escena, y una búsqueda por tipo puede devolver cualquiera.
    /// </summary>
    private static Scenario4Controller FindScenario4() =>
        Object.FindObjectsByType<Scenario4Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .FirstOrDefault(c => c.ChallengeId == TelemetryManager.Scenario4Id);

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
