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
/// 3 - Poner al día Setup y Juego: lo que una escena ya montada necesita para seguir al
///     código. En Setup, el botón de subir datos; en Juego, cada reto abierto una sola vez,
///     fuera los ajustes de prefab que ya no se aplican y el nivel de la básica por defecto.
/// 4 - Montar piezas del tutorial: las cuatro interacciones y su TutorialController.
/// Asignar la voz del narrador: pone en el Narrator los audios de la carpeta de voz, cada
///     uno en la frase que se llama como él.
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

        log.AppendLine("  pantalla del supervisor montada: 12 teclas + Básica, Intermedia, EMPEZAR");

        EnsureUploadButton(panel.transform, log);

        Selection.activeGameObject = panel;
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

        // Las demás se conservan, salvo las que ya no existen en disco.
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (scenes.All(s => s.path != existing.path) && File.Exists(existing.path))
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
                "Se añaden DifficultyApplier, SceneLoader, TimeUpSequence y ScenarioTimeLimit " +
                "(15 minutos de sesión, 5 por sala), y se cablean el tiempo agotado y la vuelta a Setup.",
                "Preparar", "Cancelar"))
            return;

        PrepareGameScene(scene);
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaSceneTools.PrepareGameSceneBatch</summary>
    public static void PrepareGameSceneBatch() =>
        PrepareGameScene(EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single));

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

        // Tiempo límite por sala (09/10/2026). El reloj ya no cierra la sesión directamente: lo
        // decide ScenarioTimeLimit, que pasa a la sala siguiente y solo en la última llama a
        // TimeUpSequence. Si quedara el cable antiguo, la primera sala agotada cerraría la sesión.
        ScenarioTimeLimit limit = GetOrAdd<ScenarioTimeLimit>(session, log);
        SetField(limit, "timer", timer);
        SetField(limit, "narrator", narrator);
        SetField(limit, "sessionTimeUp", timeUp);

        // Frase 21 del CSV ("¡Se acabó el tiempo en esta sala!"). Sin clip solo se escribe en
        // pantalla; el audio se asigna a mano cuando esté grabado. No pisa una frase ya puesta.
        SerializedObject limitSo = new SerializedObject(limit);
        SerializedProperty lineText = limitSo.FindProperty("timeUpLine.textId");
        if (string.IsNullOrEmpty(lineText.stringValue))
        {
            lineText.stringValue = "21";
            limitSo.ApplyModifiedProperties();
            log.AppendLine("  ScenarioTimeLimit: frase de tiempo agotado por sala = textId 21 (sin audio todavía)");
        }

        if (timer != null && RemoveListener(timer, timer.OnTimeUp, timeUp, nameof(TimeUpSequence.Begin)))
            log.AppendLine("  quitado Timer.OnTimeUp → TimeUpSequence.Begin (ahora lo decide ScenarioTimeLimit)");

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
    /// un agarre del esc. 4 se contó en el 3. El 1 no se toca: arranca con el temporizador.
    ///
    /// Es la ÚNICA apertura: las llamadas a StartScenario que hubiera en pasos posteriores se
    /// quitan. Eran inofensivas mientras StartChallenge fuera idempotente, pero dejaban el
    /// inicio del reto dependiendo de eso.
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

            foreach (Scenario other in director.scenarios)
            {
                foreach (Step step in other.steps)
                {
                    if (step == first || step.instantEvents == null) continue;

                    while (RemoveListener(director, step.instantEvents, controller, "StartScenario"))
                        log.AppendLine($"  {scenarioName}: quitada la segunda apertura del reto, en '{step.message}'");
                }
            }
        }
    }

    // --- 3. Poner al día las escenas ---

    private const string UploadKeyLabel = "Subir datos";
    private const string UploadStatusName = "EstadoSubida";
    private const string UpdateReport = "Logs/Codea_PuestaAlDia.txt";

    /// <summary>
    /// Lleva las dos escenas del build a lo que el código espera hoy, sin rehacerlas: lo que
    /// ya está puesto se respeta. Repetible; pasarla tras actualizar el proyecto.
    ///
    ///   · Setup: el JSONUploader que sube solo las sesiones pendientes al abrir la pantalla,
    ///     su botón "Subir datos" y la etiqueta que dice cuántas faltan
    ///   · Juego: cada reto se abre una sola vez (al llegar a su sala), fuera los ajustes de
    ///     instancia de prefab que ya no corresponden a nada, y el LevelLoader apuntando por
    ///     defecto al nivel de la básica
    /// </summary>
    [MenuItem("Tools/Codea/3 - Poner al día Setup y Juego")]
    private static void UpdateScenesMenu()
    {
        if (!EditorUtility.DisplayDialog("Poner al día las escenas",
                "Se abren Setup y Juego, se les añade o corrige lo que falte y se guardan.\n\n" +
                "Setup: botón de subir datos. Juego: una sola apertura por reto y limpieza de " +
                "ajustes de prefab sin uso.", "Poner al día", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string opened = SceneManager.GetActiveScene().path;

        UpdateScenes();

        if (!string.IsNullOrEmpty(opened) && File.Exists(opened))
            EditorSceneManager.OpenScene(opened, OpenSceneMode.Single);
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaSceneTools.UpdateScenesBatch</summary>
    public static void UpdateScenesBatch() => UpdateScenes();

    private static void UpdateScenes()
    {
        StringBuilder log = new StringBuilder("[Codea] Puesta al día de las escenas:\n");

        // El servidor y la clave se leen del uploader de Juego, que es el que se prueba: el de
        // Setup se quedó con los de un servidor antiguo y las sesiones pendientes no subían.
        gameUploader = ReadGameUploader();

        if (File.Exists(SetupScene))
        {
            Scene setup = EditorSceneManager.OpenScene(SetupScene, OpenSceneMode.Single);
            log.AppendLine($" {setup.name}");
            UpdateSetupScene(log);
            EditorSceneManager.MarkSceneDirty(setup);
            EditorSceneManager.SaveScene(setup);
        }
        else
        {
            log.AppendLine($"  ⚠ No existe {SetupScene}: créala con Tools → Codea → 1");
        }

        Scene game = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        log.AppendLine($" {game.name}");
        UpdateGameScene(game, log);
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game);

        SetBuildScenes(log);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(Path.GetDirectoryName(UpdateReport));
        File.WriteAllText(UpdateReport, log.ToString());

        Debug.Log(log.ToString());
    }

    private static (string url, string key, int timeout)? gameUploader;

    private static (string url, string key, int timeout)? ReadGameUploader()
    {
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        JSONUploader uploader = Object.FindAnyObjectByType<JSONUploader>(FindObjectsInactive.Include);
        if (uploader == null) return null;

        SerializedObject so = new SerializedObject(uploader);
        return (so.FindProperty("serverUrl").stringValue, so.FindProperty("apiKey").stringValue,
                so.FindProperty("timeoutSeconds").intValue);
    }

    private static void UpdateSetupScene(StringBuilder log)
    {
        SessionSetup setup = Object.FindAnyObjectByType<SessionSetup>(FindObjectsInactive.Include);

        if (setup == null)
        {
            log.AppendLine("  ⚠ No hay SessionSetup: no sé dónde poner el botón de subir datos");
            return;
        }

        EnsureUploadButton(setup.transform, log);
    }

    /// <summary>
    /// El uploader de la pantalla del supervisor, su botón y su etiqueta de estado. El botón
    /// va en el hueco libre de la columna de la derecha, encima de EMPEZAR.
    /// </summary>
    private static void EnsureUploadButton(Transform panel, StringBuilder log)
    {
        JSONUploader uploader = Object.FindAnyObjectByType<JSONUploader>(FindObjectsInactive.Include);

        if (uploader == null)
        {
            uploader = Undo.AddComponent<JSONUploader>(panel.gameObject);
            log.AppendLine($"  añadido JSONUploader en '{panel.name}'");
        }

        SetBool(uploader, "uploadPendingOnStart", true);

        if (gameUploader is (string serverUrl, string serverKey, int serverTimeout) && !string.IsNullOrEmpty(serverUrl))
        {
            SerializedObject so = new SerializedObject(uploader);
            bool changed = so.FindProperty("serverUrl").stringValue != serverUrl ||
                           so.FindProperty("apiKey").stringValue != serverKey;

            so.FindProperty("serverUrl").stringValue = serverUrl;
            so.FindProperty("apiKey").stringValue = serverKey;
            so.FindProperty("timeoutSeconds").intValue = serverTimeout;
            so.ApplyModifiedPropertiesWithoutUndo();

            // La clave no va al log: el informe se guarda en Logs/.
            log.AppendLine(changed
                ? $"  JSONUploader: servidor y clave copiados de Juego ({serverUrl}); antes eran otros"
                : $"  JSONUploader: mismo servidor y clave que Juego ({serverUrl})");
        }
        else
        {
            log.AppendLine("  ⚠ Juego no tiene JSONUploader: el de Setup conserva su servidor y su clave");
        }

        Transform startKey = panel.Find("Tecla EMPEZAR");
        Transform key = panel.Find($"Tecla {UploadKeyLabel}");
        InteractableUnityEventWrapper wrapper;

        if (key == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefab);
            if (prefab == null)
            {
                log.AppendLine($"  ⚠ No encuentro {ButtonPrefab}: falta el botón '{UploadKeyLabel}'");
                return;
            }

            Vector3 position = startKey != null
                ? startKey.localPosition + Vector3.up * KeySpacing
                : new Vector3(2f * KeySpacing, 0.12f - 2f * KeySpacing, 0f);

            wrapper = CreateButton(panel, prefab, UploadKeyLabel, position);
            if (wrapper == null) return;

            key = panel.Find($"Tecla {UploadKeyLabel}");

            if (startKey != null)
            {
                key.localRotation = startKey.localRotation;
                key.localScale = startKey.localScale;
            }

            log.AppendLine($"  creado el botón '{UploadKeyLabel}' en {position}");
        }
        else
        {
            wrapper = key.GetComponentInChildren<InteractableUnityEventWrapper>(true);
            if (wrapper == null)
            {
                log.AppendLine($"  ⚠ 'Tecla {UploadKeyLabel}' no tiene InteractableUnityEventWrapper");
                return;
            }
        }

        if (AddOnce(wrapper, wrapper.WhenSelect, uploader, nameof(JSONUploader.UploadPending), uploader.UploadPending))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(wrapper);
            log.AppendLine($"  '{UploadKeyLabel}' → JSONUploader.UploadPending");
        }

        Transform status = panel.Find(UploadStatusName);
        TextMeshPro label = status != null ? status.GetComponent<TextMeshPro>() : null;

        if (label == null)
        {
            // Debajo de todas las teclas, centrada como la etiqueta de estado de la sesión.
            float lowest = 0f;
            foreach (Transform child in panel)
                if (child.name.StartsWith("Tecla ")) lowest = Mathf.Min(lowest, child.localPosition.y);

            Transform sessionStatus = panel.Find("Estado");
            float x = sessionStatus != null ? sessionStatus.localPosition.x : 0f;

            label = CreateLabel(panel, UploadStatusName, new Vector3(x, lowest - KeySpacing, 0f), 0.35f, "");
            Undo.RegisterCreatedObjectUndo(label.gameObject, UploadStatusName);

            if (sessionStatus != null)
            {
                label.transform.localRotation = sessionStatus.localRotation;
                label.transform.localScale = sessionStatus.localScale;
            }

            log.AppendLine($"  creada la etiqueta '{UploadStatusName}' en {label.transform.localPosition}");
        }

        SetField(uploader, "statusTextTmp", label);
    }

    private static void UpdateGameScene(Scene scene, StringBuilder log)
    {
        Director director = Find<Director>(log);
        if (director != null) StartChallengesOnArrival(director, log);

        RemoveUnusedPrefabOverrides(scene, log);
        PointLevelLoaderAtBasicLevel(log);
        SplitScenario3Panel(log);
        AddEarthApproach(scene, log);
        CodeaSolutions.Apply(log);
    }

    private const string EarthView = "Pos_earth";
    private const string EarthName = "Earth";

    /// <summary>
    /// El acercamiento de la Tierra al completar la misión: EarthApproach en la vista exterior
    /// de la Tierra (Pos_earth), apuntando a su Tierra. Arranca solo desde código al terminar el
    /// último escenario, así que no hay nada que cablear en el Director.
    /// </summary>
    private static void AddEarthApproach(Scene scene, StringBuilder log)
    {
        // Pos_earth está apagado al cargar: GameObject.Find no lo vería.
        GameObject view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                               .Select(t => t.gameObject).FirstOrDefault(g => g.name == EarthView);

        if (view == null)
        {
            log.AppendLine($"  ⚠ No hay '{EarthView}' en la escena: no se monta el acercamiento de la Tierra");
            return;
        }

        Transform earth = view.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != view.transform && t.name == EarthName);

        if (earth == null)
        {
            log.AppendLine($"  ⚠ '{EarthView}' no tiene ningún hijo llamado '{EarthName}'");
            return;
        }

        if (!view.TryGetComponent(out EarthApproach approach))
        {
            approach = Undo.AddComponent<EarthApproach>(view);
            log.AppendLine($"  añadido EarthApproach en '{EarthView}': la Tierra se acerca al completar la misión");
        }

        SetField(approach, "body", earth);
    }

    private const string BasicBackground = "Fondo (Basica)";
    private const string IntermediateBackground = "Fondo (Intermedia)";

    /// <summary>
    /// El panel del Escenario 3 con un fondo por dificultad. En intermedia el fondo oscuro
    /// cubre el contador de repeticiones y la columna de instrucciones; en básica la fila no
    /// se ve, y ese mismo fondo dejaba un rectángulo vacío debajo del contador. Ahora cada
    /// dificultad tiene el suyo (DifficultyOnly) y el de básica acaba donde empezaban las
    /// instrucciones.
    ///
    /// Los rótulos y el número de repeticiones siguen siendo únicos y comunes a las dos: la
    /// fila escribe en una sola etiqueta. Lo ya partido no se toca.
    /// </summary>
    private static void SplitScenario3Panel(StringBuilder log)
    {
        Scenario3Controller controller = Object.FindAnyObjectByType<Scenario3Controller>(FindObjectsInactive.Include);
        Transform found = controller != null ? controller.transform.Find("Canvas/Panel") : null;

        if (!(found is RectTransform panel) || !panel.TryGetComponent(out UnityEngine.UI.Image background))
        {
            log.AppendLine("  ⚠ Esc. 3: no encuentro 'Canvas/Panel' con su imagen de fondo: el panel no se parte");
            return;
        }

        if (panel.Find(BasicBackground) != null || panel.Find(IntermediateBackground) != null)
        {
            log.AppendLine("  Esc. 3: el panel ya tiene un fondo por dificultad");
            return;
        }

        // Dónde empieza la columna de instrucciones: el borde de arriba de la pieza de
        // intermedia que quede más alta (su fondo o su rótulo), medido desde lo alto del panel.
        float instructionsTop = float.MaxValue;

        foreach (RectTransform child in panel)
        {
            if (!child.TryGetComponent(out DifficultyOnly only) || only.Difficulty != Difficulty.Intermedia) continue;

            Vector3[] corners = new Vector3[4];
            child.GetLocalCorners(corners);

            // corners[1] es la esquina de arriba a la izquierda, en ejes del hijo.
            float top = panel.InverseTransformPoint(child.TransformPoint(corners[1])).y;
            instructionsTop = Mathf.Min(instructionsTop, panel.rect.yMax - top);
        }

        if (instructionsTop == float.MaxValue || instructionsTop <= 0f || instructionsTop >= panel.rect.height)
        {
            log.AppendLine("  ⚠ Esc. 3: el panel no tiene piezas de solo intermedia de las que sacar dónde " +
                           "empiezan las instrucciones: no se parte");
            return;
        }

        RectTransform intermediate = CreateBackground(panel, IntermediateBackground, background, Difficulty.Intermedia);
        intermediate.anchorMin = Vector2.zero;
        intermediate.anchorMax = Vector2.one;
        intermediate.offsetMin = Vector2.zero;
        intermediate.offsetMax = Vector2.zero;

        RectTransform basic = CreateBackground(panel, BasicBackground, background, Difficulty.Basica);
        basic.anchorMin = new Vector2(0f, 1f);
        basic.anchorMax = new Vector2(1f, 1f);
        basic.pivot = new Vector2(0.5f, 1f);
        basic.anchoredPosition = Vector2.zero;
        basic.sizeDelta = new Vector2(0f, instructionsTop);

        // Detrás de los rótulos: los primeros hijos se pintan primero.
        basic.SetAsFirstSibling();
        intermediate.SetAsFirstSibling();

        Undo.RecordObject(background, "Fondo del panel");
        background.enabled = false;
        EditorUtility.SetDirty(background);

        log.AppendLine($"  Esc. 3: panel con un fondo por dificultad. El de básica mide {instructionsTop:0} de " +
                       $"alto (el de intermedia, {panel.rect.height:0})");
    }

    private static RectTransform CreateBackground(RectTransform panel, string name, UnityEngine.UI.Image source, Difficulty difficulty)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.layer = panel.gameObject.layer;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(panel, false);

        UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
        image.sprite = source.sprite;
        image.type = source.type;
        image.material = source.material;
        image.color = source.color;
        image.raycastTarget = false;

        go.AddComponent<DifficultyOnly>().Difficulty = difficulty;

        return rect;
    }

    /// <summary>
    /// Quita de las instancias de prefab los ajustes que ya no se aplican a nada: campos que
    /// el script dejó de tener, o llamadas de UnityEvent en un hueco de la lista que el prefab
    /// no tiene. Unity los ignora, pero se quedan en la escena y reviven si el prefab vuelve a
    /// tener ese hueco: así, los bloques arrastraban llamadas viejas a RegisterBlockGrabbed
    /// que habrían contado cada agarre dos veces.
    /// </summary>
    private static void RemoveUnusedPrefabOverrides(Scene scene, StringBuilder log)
    {
        GameObject[] instances = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject)
            .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
            .ToArray();

        int Count() => instances.Sum(i => PrefabUtility.GetPropertyModifications(i)?.Length ?? 0);

        int before = Count();
        PrefabUtility.RemoveUnusedOverrides(instances, InteractionMode.AutomatedAction);
        int after = Count();

        log.AppendLine($"  {instances.Length} instancias de prefab: {before - after} ajuste(s) sin uso quitados " +
                       $"(quedan {after})");
    }

    /// <summary>
    /// El nivel que el LevelLoader tiene guardado es solo el de reserva: al cargar manda
    /// Scenario1Difficulty. Se deja apuntando al de la básica para que no dependa de un
    /// archivo que ya no esté.
    /// </summary>
    private static void PointLevelLoaderAtBasicLevel(StringBuilder log)
    {
        Scenario1Difficulty variant = Object.FindAnyObjectByType<Scenario1Difficulty>(FindObjectsInactive.Include);
        LevelLoader loader = Object.FindAnyObjectByType<LevelLoader>(FindObjectsInactive.Include);

        if (variant == null || loader == null || variant.Basica.level == null)
        {
            log.AppendLine("  ⚠ Esc. 1: falta Scenario1Difficulty, el LevelLoader o el nivel de la básica");
            return;
        }

        if (loader.levelJson == variant.Basica.level) return;

        Undo.RecordObject(loader, "Nivel por defecto");
        loader.levelJson = variant.Basica.level;
        PrefabUtility.RecordPrefabInstancePropertyModifications(loader);
        EditorUtility.SetDirty(loader);

        log.AppendLine($"  Esc. 1: LevelLoader apunta por defecto a '{variant.Basica.level.name}'");
    }

    // --- Voz del narrador ---

    private const string VoiceFolder = "Assets/_Main/Sounds/VoiceLines/Camila";
    private const string VoiceErrorFolder = VoiceFolder + "/Error";
    private const string VoiceReport = "Logs/Codea_Voz.txt";

    /// <summary>
    /// Pone en la escena de juego la voz de la carpeta VoiceFolder. Cada audio se llama como
    /// el ID_Texto de su frase en el CSV ("9.1.wav"), así que no hay nada que arrastrar:
    ///
    ///   · Listas del Narrator: a cada entrada, el audio que se llama como su textId. Solo se
    ///     quedan las dos listas que usa NarrationDifficulty (básica e intermedia); las demás
    ///     se quitan
    ///   · Frases de error: Error/e1.wav, e2.wav..., una entrada por archivo
    ///   · Las dos frases de tiempo agotado (ScenarioTimeLimit y TimeUpSequence), por su textId
    ///
    /// Repetible: pasarla cada vez que se regraben o se añadan audios a la carpeta.
    /// </summary>
    [MenuItem("Tools/Codea/Asignar la voz del narrador")]
    private static void AssignVoiceMenu()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != GameScene)
        {
            EditorUtility.DisplayDialog("Voz del narrador", $"Abre {GameScene} y vuelve a pasar la herramienta.", "Vale");
            return;
        }

        AssignVoice(scene);
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaSceneTools.AssignVoiceBatch</summary>
    public static void AssignVoiceBatch()
    {
        AssetDatabase.Refresh();
        AssignVoice(EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single));
    }

    private static void AssignVoice(Scene scene)
    {
        string voice = Path.GetFileName(VoiceFolder);
        StringBuilder log = new StringBuilder($"[Codea] Voz del narrador: '{voice}' ({VoiceFolder})\n");

        Narrator narrator = Find<Narrator>(log);
        NarrationDifficulty variant = Find<NarrationDifficulty>(log);

        if (narrator != null && variant != null)
        {
            AssignNarratorLists(narrator, variant, voice, log);
            AssignErrorLines(narrator, log);
        }

        AssignLine(Object.FindAnyObjectByType<ScenarioTimeLimit>(FindObjectsInactive.Include), "tiempo agotado en una sala", log);
        AssignLine(Object.FindAnyObjectByType<TimeUpSequence>(FindObjectsInactive.Include), "tiempo agotado de la sesión", log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Directory.CreateDirectory(Path.GetDirectoryName(VoiceReport));
        File.WriteAllText(VoiceReport, log.ToString());

        Debug.Log(log.ToString());
    }

    private static AudioClip VoiceClip(string folder, string name) =>
        string.IsNullOrWhiteSpace(name) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{name.Trim()}.wav");

    private static void AssignNarratorLists(Narrator narrator, NarrationDifficulty variant, string voice, StringBuilder log)
    {
        SerializedObject so = new SerializedObject(narrator);
        SerializedProperty lists = so.FindProperty("lists");

        int basic = variant.Basica.listIndex, intermediate = variant.Intermedia.listIndex;

        if (basic < 0 || basic >= lists.arraySize || intermediate < 0 || intermediate >= lists.arraySize || basic == intermediate)
        {
            log.AppendLine($"  ⚠ NarrationDifficulty apunta a las listas {basic} y {intermediate}, y el Narrator tiene " +
                           $"{lists.arraySize}: no se toca nada");
            return;
        }

        // De atrás hacia delante, para que borrar no descoloque los índices pendientes.
        for (int i = lists.arraySize - 1; i >= 0; i--)
        {
            if (i == basic || i == intermediate) continue;

            log.AppendLine($"  quitada la lista {i} '{lists.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue}': " +
                           "no la usa ninguna dificultad");
            lists.DeleteArrayElementAtIndex(i);

            if (i < basic) basic--;
            if (i < intermediate) intermediate--;
        }

        AssignList(lists.GetArrayElementAtIndex(basic), $"{voice} (Basica)", basic, log);
        AssignList(lists.GetArrayElementAtIndex(intermediate), $"{voice} (Intermedia)", intermediate, log);

        // La lista que el Narrator tiene guardada es solo la de reserva: al cargar manda la dificultad.
        so.FindProperty("listIndex").intValue = basic;
        so.ApplyModifiedProperties();

        Undo.RecordObject(variant, "Voz del narrador");
        variant.SetSettings(new NarrationDifficulty.Settings { listIndex = basic },
                            new NarrationDifficulty.Settings { listIndex = intermediate });
        EditorUtility.SetDirty(variant);

        log.AppendLine($"  NarrationDifficulty: básica = lista {basic}, intermedia = lista {intermediate}");
    }

    private static void AssignList(SerializedProperty list, string name, int index, StringBuilder log)
    {
        list.FindPropertyRelative("name").stringValue = name;

        SerializedProperty entries = list.FindPropertyRelative("entries");
        List<string> missing = new List<string>();
        int assigned = 0;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("textId").stringValue;
            AudioClip clip = VoiceClip(VoiceFolder, id);

            // Sin audio nuevo se vacía: dejar el de la voz anterior mezclaría dos voces.
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;

            if (clip != null) assigned++;
            else missing.Add(string.IsNullOrEmpty(id) ? $"(entrada {i} sin textId)" : id);
        }

        log.AppendLine($"  lista {index} '{name}': {assigned} de {entries.arraySize} frases con audio" +
                       (missing.Count > 0 ? $" · ⚠ sin audio: {string.Join(", ", missing)}" : ""));
    }

    private static void AssignErrorLines(Narrator narrator, StringBuilder log)
    {
        List<AudioClip> clips = new List<AudioClip>();

        for (int i = 1; VoiceClip(VoiceErrorFolder, $"e{i}") is AudioClip clip; i++)
            clips.Add(clip);

        if (clips.Count == 0)
        {
            log.AppendLine($"  ⚠ No hay e1.wav, e2.wav... en {VoiceErrorFolder}: las frases de error se quedan como están");
            return;
        }

        SerializedObject so = new SerializedObject(narrator);
        SerializedProperty entries = so.FindProperty("errorLines.entries");

        entries.arraySize = clips.Count;

        for (int i = 0; i < clips.Count; i++)
            entries.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clips[i];

        so.ApplyModifiedProperties();

        log.AppendLine($"  frases de error: {clips.Count} ({string.Join(", ", clips.Select(c => c.name))})");
    }

    /// <summary>La frase 'timeUpLine' de un componente, por su textId.</summary>
    private static void AssignLine(Object owner, string what, StringBuilder log)
    {
        if (owner == null) return;

        SerializedObject so = new SerializedObject(owner);
        string id = so.FindProperty("timeUpLine.textId").stringValue;
        AudioClip clip = VoiceClip(VoiceFolder, id);

        so.FindProperty("timeUpLine.clip").objectReferenceValue = clip;
        so.ApplyModifiedProperties();

        log.AppendLine(clip != null
            ? $"  {what}: frase {id} con audio ({owner.GetType().Name})"
            : $"  ⚠ {what}: no hay audio para la frase '{id}' ({owner.GetType().Name})");
    }

    // --- Sonidos del brazo robótico ---

    private const string ArmRotateSound = "Assets/_Main/Sounds/Robot/RobotMovement MON01_87_2.mp3";
    private const string ArmPickSound = "Assets/_Main/Sounds/Effects/PlugInOut/in.mp3";
    private const string ArmDropSound = "Assets/_Main/Sounds/Effects/PlugInOut/out.mp3";

    /// <summary>
    /// Pone al brazo del Escenario 3 sus tres sonidos (girar, recoger, soltar) y le apaga el
    /// Play On Awake del AudioSource. No guarda la escena.
    /// </summary>
    [MenuItem("Tools/Codea/Asignar sonidos del brazo robótico")]
    private static void AssignArmSoundsMenu()
    {
        AssignArmSounds();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaSceneTools.AssignArmSoundsBatch. Guarda la escena.</summary>
    public static void AssignArmSoundsBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        AssignArmSounds();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AssignArmSounds()
    {
        StringBuilder log = new StringBuilder("[Codea] Sonidos del brazo robótico:\n");
        RoboticArm arm = Find<RoboticArm>(log);

        if (arm != null)
        {
            foreach ((string field, string path) in new[]
                     {
                         ("rotateSound", ArmRotateSound), ("pickSound", ArmPickSound), ("dropSound", ArmDropSound),
                     })
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                if (clip == null)
                {
                    log.AppendLine($"  ⚠ No encuentro {path}: '{field}' se queda como está");
                    continue;
                }

                SetField(arm, field, clip);
                log.AppendLine($"  {field} = {clip.name}");
            }

            if (arm.TryGetComponent(out AudioSource source) && source.playOnAwake)
            {
                Undo.RecordObject(source, "Play On Awake");
                source.playOnAwake = false;
                EditorUtility.SetDirty(source);
                log.AppendLine("  AudioSource: Play On Awake apagado");
            }
        }

        Debug.Log(log.ToString(), arm);
    }

    // --- Vista previa por dificultad ---

    [MenuItem("Tools/Codea/Ver como básica")]
    private static void ShowBasic() => ShowDifficulty(Difficulty.Basica);

    [MenuItem("Tools/Codea/Ver como intermedia")]
    private static void ShowIntermediate() => ShowDifficulty(Difficulty.Intermedia);

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

        // Las filas que se ven, ANTES que las piezas sueltas: mostrar la fila enciende también
        // a sus hijos, y entre ellos están los bloques de la otra dificultad, que se ocultan
        // justo después.
        string rows = ShowRowsFor(difficulty, visibility, hiding: false);

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

        // Las filas que NO se ven, DESPUÉS: el bucle de arriba acaba de mostrar las piezas de
        // esta dificultad, y entre ellas están los bloques propios de la fila (en básica,
        // "Girar al destino" y "Volver"), que tienen que quedar ocultos con ella.
        rows += ShowRowsFor(difficulty, visibility, hiding: true);

        Debug.Log(difficulty == null
            ? $"[Codea] Vista: todas las piezas ({shown} de una sola dificultad)"
            : $"[Codea] Vista: {DifficultyApplier.Label(difficulty.Value)} · {shown} piezas propias visibles, " +
              $"{hidden} de la otra ocultas{rows}. Para probarla en Play: DifficultyApplier → Editor Difficulty");
    }

    /// <summary>
    /// La fila de instrucciones del Escenario 3 con sus bloques, que en básica no se enseña
    /// (Scenario3Difficulty → Show Row). En Play la oculta la propia fila; aquí se oculta en la
    /// vista de escena, para ver el panel como lo verá el niño.
    /// </summary>
    /// <param name="hiding">
    /// Qué filas se tratan en esta pasada: las que se ocultan o las que se muestran. Van en
    /// pasadas distintas porque unas tienen que ir después de las piezas sueltas y otras antes.
    /// </param>
    private static string ShowRowsFor(Difficulty? difficulty, SceneVisibilityManager visibility, bool hiding)
    {
        string summary = "";

        foreach (Scenario3Difficulty variant in Object.FindObjectsByType<Scenario3Difficulty>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(new SerializedObject(variant).FindProperty("row").objectReferenceValue is SocketRow row)) continue;

            Scenario3Difficulty.Settings settings = difficulty == Difficulty.Basica ? variant.Basica : variant.Intermedia;
            bool show = difficulty == null || settings.showRow;

            if (show == hiding) continue;

            // La fila y todo lo que cuelga de ella, y además los bloques con los que arranca
            // en cualquiera de las dos dificultades, por si alguno no es hijo suyo.
            List<GameObject> pieces = new List<GameObject> { row.gameObject };

            foreach (SocketRow.InitialBlock initial in variant.Basica.initialBlocks.Concat(variant.Intermedia.initialBlocks))
                if (initial != null && initial.block != null) pieces.Add(initial.block.gameObject);

            foreach (GameObject piece in pieces.Distinct())
            {
                if (show) visibility.Show(piece, true);
                else visibility.Hide(piece, true);
            }

            if (difficulty != null)
                summary += show ? " · fila de instrucciones del Esc. 3 a la vista" : " · fila de instrucciones del Esc. 3 y sus bloques ocultos";
        }

        return summary;
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
