using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Tools → Codea → Capturas para informes: fotografía cada sala de Juego.unity en Play, en
/// básica y en intermedia, desde donde la ve el jugador, y la pantalla de Setup. Las imágenes
/// van a Docs/Entregable-Mes2/capturas (JPG, 1600×900).
///
/// Se hace en Play y no en el editor porque media escena se monta al arrancar: la rejilla y el
/// robot de la sala 1, los huecos de las filas y lo que cambia con la dificultad.
///
/// No deja rastro: restaura las PlayerPrefs del editor y borra los JSON de telemetría que se
/// escriban. Sin Unity abierto (con gráficos: SIN -nographics, y sin -quit):
/// Unity.exe -batchmode -projectPath . -executeMethod CodeaCaptures.RunBatch
/// </summary>
[InitializeOnLoad]
public static class CodeaCaptures
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string SetupScene = "Assets/Scenes/Setup.unity";
    private const string OutputFolder = "Docs/Entregable-Mes2/capturas";
    private const int Width = 1600, Height = 900;

    private const string KeyStage = "CodeaCaptures.Stage";
    private const string KeyQueue = "CodeaCaptures.Queue";
    private const string KeyCurrent = "CodeaCaptures.Current";
    private const string KeyBatch = "CodeaCaptures.Batch";
    private const string KeyPrefs = "CodeaCaptures.Prefs";
    private const string KeyFiles = "CodeaCaptures.Files";
    private const string KeyLog = "CodeaCaptures.Log";

    private static readonly string[] PrefKeys = { "telemetry_session_counter", "telemetry_pin_counter", "telemetry_difficulty" };

    private static double playStarted = -1;
    private static int phase;

    /// <summary>Una toma: nombre del archivo, dónde va la cámara y adónde mira.</summary>
    private struct Shot
    {
        public string file;
        public Vector3 position, target;
        public float fov;

        public Shot(string file, Vector3 position, Vector3 target, float fov = 60f)
        {
            this.file = file;
            this.position = position;
            this.target = target;
            this.fov = fov;
        }
    }

    // Puntos de vista, sacados de los puntos de teletransporte de cada sala (a la altura de los
    // ojos) y de dónde están la mesa o el panel de cada reto.
    private static IEnumerable<Shot> ShotsFor(Difficulty difficulty)
    {
        string d = difficulty == Difficulty.Basica ? "basica" : "intermedia";

        if (difficulty == Difficulty.Basica)
        {
            yield return new Shot("tutorial_inicio", new Vector3(-25.9f, 1.45f, -17.5f), new Vector3(-24.2f, 1.0f, -17.5f), 70f);
            yield return new Shot("sala1_general", new Vector3(-6.9f, 5.6f, -3.0f), new Vector3(2.5f, 3.4f, -2.2f), 75f);
            yield return new Shot("sala2_general", new Vector3(-3.4f, 1.75f, 24.0f), new Vector3(-8.9f, 1.45f, 24.0f), 66f);
            yield return new Shot("sala2_modulo_sin_resolver", new Vector3(-6.3f, 1.7f, 24.0f), new Vector3(-8.9f, 1.42f, 24.0f), 50f);
            yield return new Shot("sala2_modulo_resuelto", new Vector3(-6.3f, 1.7f, 20.0f), new Vector3(-8.9f, 1.42f, 20.0f), 50f);
            yield return new Shot("sala4_panel", new Vector3(-0.49f, 1.55f, 99.85f), new Vector3(-0.46f, 0.85f, 100.95f), 65f);
        }

        yield return new Shot($"sala1_mesa_{d}", new Vector3(-6.95f, 5.25f, -2.9f), new Vector3(-5.95f, 4.4f, -2.9f), 60f);
        yield return new Shot($"sala3_{d}", new Vector3(0.75f, 4.45f, 47.1f), new Vector3(-0.85f, 3.95f, 47.35f), 65f);
    }

    [MenuItem("Tools/Codea/Capturas para informes")]
    private static void RunMenu()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Begin(false);
    }

    public static void RunBatch() => Begin(true);

    static CodeaCaptures()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(KeyStage, ""))) return;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Begin(bool batch)
    {
        Directory.CreateDirectory(OutputFolder);

        SessionState.SetString(KeyQueue, $"{(int)Difficulty.Basica},{(int)Difficulty.Avanzada}");
        SessionState.SetBool(KeyBatch, batch);
        SessionState.SetString(KeyFiles, "");
        SessionState.SetString(KeyLog, "");
        SessionState.SetString(KeyPrefs, string.Join("\n", PrefKeys.Select(k => PlayerPrefs.HasKey(k) ? $"{k}={PlayerPrefs.GetInt(k)}" : $"{k}=")));

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
            return;
        }

        if (stage == "playing" && EditorApplication.isPlaying)
        {
            if (playStarted < 0) playStarted = EditorApplication.timeSinceStartup;
            double seconds = EditorApplication.timeSinceStartup - playStarted;
            Difficulty difficulty = (Difficulty)SessionState.GetInt(KeyCurrent, 0);

            try
            {
                if (phase == 0 && seconds > 2)
                {
                    phase = 1;
                    Prepare(difficulty);
                }
                else if (phase == 1 && seconds > 6)
                {
                    phase = 2;
                    foreach (Shot shot in ShotsFor(difficulty)) Capture(shot);

                    TelemetryManager telemetry = TelemetryManager.Instance;
                    if (telemetry != null && !string.IsNullOrEmpty(telemetry.GetCurrentFilePath()))
                        SessionState.SetString(KeyFiles, SessionState.GetString(KeyFiles, "") + telemetry.GetCurrentFilePath() + "\n");

                    SessionState.SetString(KeyStage, "exiting");
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception e)
            {
                Log($"✗ {e.GetType().Name}: {e.Message}");
                SessionState.SetString(KeyStage, "exiting");
                EditorApplication.ExitPlaymode();
            }
            return;
        }

        if (stage == "exiting" && !EditorApplication.isPlaying)
        {
            SessionState.SetString(KeyStage, "next");
            EditorApplication.delayCall += Next;
        }
    }

    /// <summary>Enciende las salas y deja resuelto el primer módulo de la sala 2.</summary>
    private static void Prepare(Difficulty difficulty)
    {
        foreach (MonoBehaviour controller in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                                    .Where(m => m is Scenario1Controller || m is Scenario2Controller ||
                                                                m is Scenario3Controller || m is Scenario4Controller))
            for (Transform t = controller.transform; t != null; t = t.parent)
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

        if (difficulty != Difficulty.Basica) return;

        SystemModule cooling = Object.FindObjectsByType<SystemModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                     .FirstOrDefault(m => m.name == "CoolingModule");
        if (cooling == null || cooling.Data == null) return;

        for (int i = 0; i < cooling.Data.options.Count; i++)
            if (cooling.Data.IsCorrectAt(i)) cooling.ToggleOption(i);
    }

    private static void Capture(Shot shot)
    {
        GameObject holder = new GameObject("CodeaCaptureCamera");
        try
        {
            Camera camera = holder.AddComponent<Camera>();
            camera.fieldOfView = shot.fov;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 5000f;
            camera.transform.position = shot.position;
            camera.transform.LookAt(shot.target);

            RenderTexture texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4
            };
            camera.targetTexture = texture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(OutputFolder, shot.file + ".jpg");
            File.WriteAllBytes(path, image.EncodeToJPG(90));
            Log($"✓ {path}");

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;

        // La pantalla del supervisor no cambia en Play: se fotografía en el editor.
        try
        {
            EditorSceneManager.OpenScene(SetupScene, OpenSceneMode.Single);
            SessionSetup setup = Object.FindAnyObjectByType<SessionSetup>(FindObjectsInactive.Include);
            if (setup != null)
            {
                Transform panel = setup.transform;
                Vector3 front = panel.position - panel.forward * 1.3f + Vector3.up * 0.05f;
                Capture(new Shot("setup_supervisor", front, panel.position, 60f));
            }
        }
        catch (Exception e)
        {
            Log($"✗ Setup: {e.Message}");
        }

        foreach (string line in SessionState.GetString(KeyPrefs, "").Split('\n'))
        {
            string[] pair = line.Split('=');
            if (pair.Length != 2) continue;
            if (pair[1] == "") PlayerPrefs.DeleteKey(pair[0]);
            else PlayerPrefs.SetInt(pair[0], int.Parse(pair[1]));
        }
        PlayerPrefs.Save();

        foreach (string file in SessionState.GetString(KeyFiles, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct())
            if (File.Exists(file)) File.Delete(file);

        string log = SessionState.GetString(KeyLog, "");
        File.WriteAllText("Logs/Codea_Capturas.txt", log);

        bool batch = SessionState.GetBool(KeyBatch, false);
        foreach (string key in new[] { KeyStage, KeyQueue, KeyPrefs, KeyFiles, KeyLog }) SessionState.EraseString(key);
        SessionState.EraseInt(KeyCurrent);
        SessionState.EraseBool(KeyBatch);

        AssetDatabase.Refresh();
        Debug.Log("[Codea] Capturas:\n" + log);

        if (batch) EditorApplication.Exit(0);
        else EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
    }

    private static void Log(string line) => SessionState.SetString(KeyLog, SessionState.GetString(KeyLog, "") + line + "\n");
}
