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
/// Herramientas de escena de Tools → Codea:
///
/// Poner al día Setup y Juego: lo que las escenas necesitan para seguir al código. En Setup, el
///     botón de subir datos y la clave del servidor copiada de Juego; en Juego, cada reto abierto
///     una sola vez, fuera los ajustes de prefab que ya no se aplican, el nivel de la básica por
///     defecto y las soluciones de cada reto para el porcentaje de solución.
/// Asignar la voz del narrador: pone en el Narrator los audios de la carpeta de voz, cada
///     uno en la frase que se llama como él.
/// Ver como básica / intermedia / todo: oculta en la vista de escena las piezas de la otra
///     dificultad, sin tocar la escena.
///
/// Todas son repetibles: lo que ya existe no se duplica. Las de montaje inicial (crear Setup,
/// preparar Juego, tutorial, sonidos del brazo) se retiraron el 09/11/2026: ya estaban aplicadas
/// y siguen en el historial de git.
/// </summary>
public static class CodeaSceneTools
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string GameSceneName = "Juego";
    private const string SetupScene = "Assets/Scenes/Setup.unity";

    private const string ButtonPrefab = "Assets/_Main/Prefabs/Buttons/TextButton.prefab";

    private const float KeySpacing = 0.12f;

    // --- Piezas comunes: botones, rótulos y Build Settings ---

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

    // --- Apertura de los retos (la usa la puesta al día) ---

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

    // --- Poner al día las escenas ---

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
    [MenuItem("Tools/Codea/Poner al día Setup y Juego", false, 20)]
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

        // La clave y el timeout se leen del uploader de Juego, que es el que se prueba: el de
        // Setup se quedó una vez con los de un servidor antiguo y las pendientes no subían. La
        // URL ya no va en las escenas: es la de ServidorDatos.asset, común a las dos.
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
            log.AppendLine($"  ⚠ No existe {SetupScene}: la escena del supervisor tiene que existir (está en git)");
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

    private static (string key, int timeout)? gameUploader;

    private static (string key, int timeout)? ReadGameUploader()
    {
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        JSONUploader uploader = Object.FindAnyObjectByType<JSONUploader>(FindObjectsInactive.Include);
        if (uploader == null) return null;

        SerializedObject so = new SerializedObject(uploader);
        return (so.FindProperty("apiKey").stringValue, so.FindProperty("timeoutSeconds").intValue);
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

        if (gameUploader is (string serverKey, int serverTimeout) && !string.IsNullOrEmpty(serverKey))
        {
            SerializedObject so = new SerializedObject(uploader);
            bool changed = so.FindProperty("apiKey").stringValue != serverKey ||
                           so.FindProperty("timeoutSeconds").intValue != serverTimeout;

            so.FindProperty("apiKey").stringValue = serverKey;
            so.FindProperty("timeoutSeconds").intValue = serverTimeout;
            so.ApplyModifiedPropertiesWithoutUndo();

            // La clave no va al log: el informe se guarda en Logs/.
            log.AppendLine(changed
                ? "  JSONUploader: clave y timeout copiados de Juego; antes eran otros"
                : "  JSONUploader: misma clave y timeout que Juego");
        }
        else
        {
            log.AppendLine("  ⚠ Juego no tiene JSONUploader con clave: el de Setup conserva la suya");
        }

        string url = AssetDatabase.LoadAssetAtPath<UploadServerConfig>(UploadServerConfig.AssetPath)?.Url;
        log.AppendLine(string.IsNullOrEmpty(url)
            ? $"  ⚠ Falta la URL del servidor en {UploadServerConfig.AssetPath}: no subirá nada"
            : $"  Servidor (de {UploadServerConfig.AssetPath}, común a Setup y Juego): {url}");

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
    /// de la Tierra (Pos_earth), apuntando a su Tierra. Es una acción del Director: el paso
    /// "Completar reto y cambiar escenario" espera a Pos_earth (y a la frase 19) antes de volver
    /// a Setup.
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
    [MenuItem("Tools/Codea/Asignar la voz del narrador", false, 21)]
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

    // --- Vista previa por dificultad ---

    [MenuItem("Tools/Codea/Ver como básica", false, 40)]
    private static void ShowBasic() => ShowDifficulty(Difficulty.Basica);

    [MenuItem("Tools/Codea/Ver como intermedia", false, 41)]
    private static void ShowIntermediate() => ShowDifficulty(Difficulty.Intermedia);

    [MenuItem("Tools/Codea/Ver todas las piezas", false, 42)]
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

    // --- Utilidades ---

    private static T Find<T>(StringBuilder log) where T : Object
    {
        T found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (found == null) log.AppendLine($"  ⚠ No hay {typeof(T).Name} en la escena");
        return found;
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

}
