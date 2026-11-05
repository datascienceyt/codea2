using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Todo lo del servidor de datos en el editor:
///
///   · Tools → Codea → Servidor de datos: selecciona Assets/_Main/Resources/ServidorDatos.asset
///     (o lo crea). Es el único sitio donde está la URL.
///   · El inspector de ese asset avisa si la URL es http:// y Android no la dejaría pasar, con un
///     botón para poner Allow downloads over HTTP en "Always allowed". Con https:// no toca nada.
///   · Antes de compilar para Android, el build se para si la URL es http:// sin ese permiso, o
///     si no hay URL: un APK así no sube nada y se descubre en el aula.
///   · Tools → Codea → Probar el servidor (Ping y JSON de prueba): entra en Play en una escena
///     vacía con un JSONUploader con la clave de Juego, hace el Ping (GET /health) y sube el
///     JSON de prueba ('9999_0_...json', queda en el servidor). Informe en Logs/Codea_PruebaServidor.txt.
///     Sin Unity abierto (sale solo):
///       Unity.exe -batchmode -projectPath . -executeMethod CodeaServerCheck.RunBatch
///     y para probar otro servidor sin tocar el asset (el de Yachay antes de cambiar):
///       ... -executeMethod CodeaServerCheck.RunBatch -servidor http://IP:PUERTO/upload
/// </summary>
[InitializeOnLoad]
public static class CodeaServerCheck
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string ReportPath = "Logs/Codea_PruebaServidor.txt";
    private const double TimeoutSeconds = 120;

    private const string KeyStage = "CodeaServerCheck.Stage";
    private const string KeyBatch = "CodeaServerCheck.Batch";
    private const string KeyKey = "CodeaServerCheck.ApiKey";
    private const string KeyTimeout = "CodeaServerCheck.Timeout";
    private const string KeyUrl = "CodeaServerCheck.Url";
    private const string KeyReport = "CodeaServerCheck.Report";
    private const string KeyScene = "CodeaServerCheck.Scene";
    private const string KeyResult = "CodeaServerCheck.Result";

    private static JSONUploader uploader;
    private static double stageStarted;

    static CodeaServerCheck()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(KeyStage, ""))) return;

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // --- El asset ---

    [MenuItem("Tools/Codea/Servidor de datos")]
    private static void SelectConfig()
    {
        UploadServerConfig config = AssetDatabase.LoadAssetAtPath<UploadServerConfig>(UploadServerConfig.AssetPath);

        if (config == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UploadServerConfig.AssetPath));
            config = ScriptableObject.CreateInstance<UploadServerConfig>();
            AssetDatabase.CreateAsset(config, UploadServerConfig.AssetPath);
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[Codea] Creado {UploadServerConfig.AssetPath}: pon la URL de subida.", config);
        }

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    /// <summary>Qué le pasa a una URL para Android, o null si nada.</summary>
    public static string Problem(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return $"No hay URL de subida en {UploadServerConfig.AssetPath}: el APK no subiría nada.";

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return $"'{url}' no es una URL http:// o https:// válida.";

        if (UploadServerConfig.IsInsecure(url) && PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
            return $"La URL es http:// (sin cifrar) y Android la rechaza: Project Settings → Player → " +
                   $"Android → Other Settings → Allow downloads over HTTP está en " +
                   $"'{Label(PlayerSettings.insecureHttpOption)}' y tiene que estar en 'Always allowed'.";

        return null;
    }

    private static string Label(InsecureHttpOption option) => option switch
    {
        InsecureHttpOption.NotAllowed => "Not allowed",
        InsecureHttpOption.DevelopmentOnly => "Allowed in development builds",
        _ => "Always allowed"
    };

    [CustomEditor(typeof(UploadServerConfig))]
    private class ConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uploadUrl"), new GUIContent("Upload Url"));
            serializedObject.ApplyModifiedProperties();

            string url = ((UploadServerConfig)target).Url;
            string health = UploadServerConfig.HealthUrlFor(url);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Ping (GET)", health ?? "");

            EditorGUILayout.Space();

            if (url.Length > 0 && !url.TrimEnd('/').EndsWith("/upload", StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.HelpBox("La URL debería terminar en /upload: es la ruta de subida de Codea2Server.", MessageType.Warning);

            string problem = Problem(url);

            if (problem != null)
            {
                EditorGUILayout.HelpBox(problem + " El build de Android se para hasta arreglarlo.", MessageType.Error);

                if (UploadServerConfig.IsInsecure(url) &&
                    GUILayout.Button("Poner Allow downloads over HTTP en 'Always allowed'"))
                {
                    PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Codea] Allow downloads over HTTP = Always allowed (la URL del servidor es http://).");
                }
            }
            else if (UploadServerConfig.IsInsecure(url))
            {
                EditorGUILayout.HelpBox("http:// sin cifrar: permitido porque Allow downloads over HTTP está en " +
                                        "'Always allowed'. La API key viaja en claro.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("https://: no hace falta permitir HTTP en Player Settings.", MessageType.Info);
            }

            EditorGUILayout.HelpBox("La usan los JSONUploader de Setup y Juego. Al cambiarla: Tools → Codea → " +
                                    "Probar el servidor, y recompilar el APK.", MessageType.None);
        }
    }

    // --- Antes del build ---

    private class BuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            UploadServerConfig config = AssetDatabase.LoadAssetAtPath<UploadServerConfig>(UploadServerConfig.AssetPath);
            string problem = Problem(config != null ? config.Url : null);

            if (problem != null)
                throw new BuildFailedException($"[Codea] Servidor de datos: {problem} Tools → Codea → Servidor de datos.");

            Debug.Log($"[Codea] Servidor de datos del APK: {config.Url}");
        }
    }

    // --- Prueba en Play: Ping y JSON de prueba ---

    [MenuItem("Tools/Codea/Probar el servidor (Ping y JSON de prueba)")]
    private static void RunMenu()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Begin(false, null);
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-servidor");
        Begin(true, i >= 0 && i + 1 < args.Length ? args[i + 1] : null);
    }

    private static void Begin(bool batch, string url)
    {
        SessionState.SetBool(KeyBatch, batch);
        SessionState.SetString(KeyUrl, url ?? "");
        SessionState.SetString(KeyScene, EditorSceneManager.GetActiveScene().path);
        SessionState.SetString(KeyResult, "");

        // La clave y el timeout, de Juego: son los que lleva el APK.
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        JSONUploader game = Object.FindAnyObjectByType<JSONUploader>(FindObjectsInactive.Include);
        if (game == null)
        {
            Debug.LogError($"[Codea] {GameScene} no tiene JSONUploader: no sé qué clave usar.");
            if (batch) EditorApplication.Exit(1);
            return;
        }

        SerializedObject so = new SerializedObject(game);
        SessionState.SetString(KeyKey, so.FindProperty("apiKey").stringValue);
        SessionState.SetInt(KeyTimeout, so.FindProperty("timeoutSeconds").intValue);

        string target = string.IsNullOrEmpty(url) ? UploadServerConfig.UploadUrl : url;
        SessionState.SetString(KeyReport,
            $"Prueba del servidor · {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
            $"Subida: {target ?? "(sin URL)"}" + (string.IsNullOrEmpty(url) ? $" (de {UploadServerConfig.AssetPath})" : " (-servidor)") + "\n" +
            $"Ping:   {UploadServerConfig.HealthUrlFor(target) ?? "(sin URL)"}\n" +
            $"Allow downloads over HTTP: {Label(PlayerSettings.insecureHttpOption)}\n" +
            (Problem(target) is string problem ? $"⚠ {problem}\n" : "") + "\n");

        // Escena vacía: Setup subiría de verdad las sesiones pendientes del editor, y Juego
        // arrancaría la partida.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        SessionState.SetString(KeyStage, "entering");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        string stage = SessionState.GetString(KeyStage, "");
        double now = EditorApplication.timeSinceStartup;

        try
        {
            if (stage == "entering" && EditorApplication.isPlaying)
            {
                string url = SessionState.GetString(KeyUrl, "");
                if (url.Length > 0) JSONUploader.UrlOverride = url;

                GameObject go = new GameObject("Prueba del servidor");
                uploader = go.AddComponent<JSONUploader>();
                Set("apiKey", SessionState.GetString(KeyKey, ""));
                Set("timeoutSeconds", SessionState.GetInt(KeyTimeout, 15));

                SetStage("ping");
                Invoke("PingServer");
            }
            else if (stage == "ping" && EditorApplication.isPlaying)
            {
                // El ping deja un solo mensaje [Ping]; OnLog pasa a la subida al verlo.
                if (now - stageStarted > TimeoutSeconds) End("✗ el ping no terminó a tiempo");
            }
            else if (stage == "upload-start" && EditorApplication.isPlaying)
            {
                SetStage("upload");
                Invoke("UploadTestFile");
            }
            else if (stage == "upload" && EditorApplication.isPlaying)
            {
                if (JSONUploader.PendingUploads == 0) End(null);
                else if (now - stageStarted > TimeoutSeconds) End("✗ la subida no terminó a tiempo");
            }
            else if (stage == "exiting" && !EditorApplication.isPlaying)
            {
                Finish();
            }
        }
        catch (Exception e)
        {
            Append($"✗ La prueba se interrumpió: {e.GetType().Name}: {e.Message}\n");
            End(null);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        string stage = SessionState.GetString(KeyStage, "");
        if (stage != "ping" && stage != "upload") return;
        if (!message.StartsWith("[Ping]") && !message.StartsWith("[Upload]")) return;

        bool error = type == LogType.Error || type == LogType.Exception;
        Append($"{(error ? "✗" : "✓")} {message.TrimEnd().Replace("\n", "\n    ")}\n");

        if (message.StartsWith("[Ping]"))
        {
            Record(error ? "ping-falla" : "ping-ok");
            SetStage("upload-start");
        }
        else if (message.StartsWith("[Upload] OK")) Record("subida-ok");
        else if (message.StartsWith("[Upload] Falló tras todos")) Record("subida-falla");
    }

    private static void End(string note)
    {
        if (note != null) Append(note + "\n");
        SetStage("exiting");
        EditorApplication.ExitPlaymode();
    }

    private static void Finish()
    {
        SessionState.EraseString(KeyStage);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;

        string result = SessionState.GetString(KeyResult, "");
        bool ok = result.Contains("ping-ok") && result.Contains("subida-ok");

        Append($"\n{(ok ? "✓ Ping y subida de prueba correctos" : "✗ Algo falló: mira las líneas con ✗")}\n");

        string report = SessionState.GetString(KeyReport, "");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report);

        if (ok) Debug.Log("[Codea] " + report);
        else Debug.LogWarning("[Codea] " + report);

        bool batch = SessionState.GetBool(KeyBatch, false);
        if (batch)
        {
            EditorApplication.Exit(ok ? 0 : 1);
            return;
        }

        string scene = SessionState.GetString(KeyScene, "");
        if (!string.IsNullOrEmpty(scene) && File.Exists(scene))
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
    }

    private static void SetStage(string stage)
    {
        SessionState.SetString(KeyStage, stage);
        stageStarted = EditorApplication.timeSinceStartup;
    }

    private static void Record(string what) =>
        SessionState.SetString(KeyResult, SessionState.GetString(KeyResult, "") + what + ";");

    private static void Append(string text) =>
        SessionState.SetString(KeyReport, SessionState.GetString(KeyReport, "") + text);

    private static void Set(string field, object value) =>
        typeof(JSONUploader).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(uploader, value);

    private static void Invoke(string method) =>
        typeof(JSONUploader).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(uploader, null);
}
