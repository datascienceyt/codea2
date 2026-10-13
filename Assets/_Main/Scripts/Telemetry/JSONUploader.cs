using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Sube los archivos de telemetría al servidor, por POST multipart.
///
/// La subida es opcional (RNF-04): el juego funciona entero sin red, y si no hay conexión el
/// archivo se queda en el visor sin perderse nada. Reintenta un par de veces por si la caída es
/// momentánea.
///
/// Dos usos:
///   · En la escena de juego, UploadTelemetry sube la sesión que acaba de terminar.
///   · En la pantalla del supervisor, UploadPending sube todo lo que se quedó sin subir: sin
///     red, con el servidor caído o con la aplicación cerrada a mitad de partida. Se lanza sola
///     al abrir la pantalla y también desde su botón. Qué está pendiente lo sabe TelemetryOutbox.
/// </summary>
public class JSONUploader : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://codea2.penginexr.com/upload";
    [SerializeField] private string apiKey;
    [SerializeField] private int timeoutSeconds = 15;
    [SerializeField] private int maxRetries = 2;

    [Header("Sesiones pendientes")]
    [Tooltip("Al arrancar la escena, sube lo que se quedó sin subir. Actívalo en la pantalla del " +
             "supervisor; en la escena de juego no, o competiría con la partida por la red.")]
    [SerializeField] private bool uploadPendingOnStart = false;

    [Tooltip("Opcional. Dónde se escribe cuántas sesiones faltan y cómo va la subida.")]
    [SerializeField] private Text statusText;

    [Tooltip("Igual, pero en TextMeshPro. Rellena solo el que uses.")]
    [SerializeField] private TMP_Text statusTextTmp;

    [Tooltip("Si al subir las pendientes falla la conexión, segundos hasta volver a intentarlo solo. " +
             "Al encender el visor la red tarda en estar lista, y sin esto el aviso de fallo se " +
             "quedaba en pantalla aunque los datos acabaran subiendo.")]
    [SerializeField] private float retryPendingSeconds = 20f;

    [Tooltip("Cuántas veces se reintenta solo antes de dejarlo para el botón o la próxima vez.")]
    [SerializeField] private int maxPendingRetries = 6;

    [Tooltip("Al terminar de subir las pendientes, haya ido bien o mal: sonido, luz...")]
    public UnityEvent OnPendingUploadFinished;

    private bool uploadingPending;
    private int pendingRetriesLeft;
    private Coroutine pendingRetry;

    private void Start()
    {
        if (uploadPendingOnStart) UploadPending();
        else ShowPendingCount();
    }

    /// <summary>
    /// Sube todas las sesiones que el servidor todavía no tiene. Cablear al botón "Subir datos"
    /// de la pantalla del supervisor.
    ///
    /// No toca ningún archivo del visor y no reenvía lo que ya se subió, así que se puede
    /// pulsar las veces que haga falta. La run abierta, si la hay, se salta: la sube su propio
    /// fin de sesión.
    /// </summary>
    [ContextMenu("Subir sesiones pendientes")]
    public void UploadPending()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Upload] Entra en Play Mode primero: UnityWebRequest necesita corrutinas.");
            return;
        }

        // Una segunda pulsación con la subida en marcha enviaría los mismos archivos dos veces.
        if (uploadingPending) return;

        // Pedida a mano o al arrancar: empieza de cero la cuenta de reintentos automáticos.
        if (pendingRetry != null) StopCoroutine(pendingRetry);
        pendingRetry = null;
        pendingRetriesLeft = maxPendingRetries;

        StartCoroutine(PendingCoroutine());
    }

    private IEnumerator PendingCoroutine()
    {
        uploadingPending = true;

        List<string> pending = PendingFiles();

        if (pending.Count == 0)
        {
            ShowStatus("Datos: todo subido");
            uploadingPending = false;
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowStatus($"Datos: {Sessions(pending.Count)} por subir. Sin conexión");
            Debug.LogWarning($"[Upload] {Sessions(pending.Count)} por subir y el visor no tiene red.");
            uploadingPending = false;
            yield break;
        }

        inFlight++;
        PendingUploads++;

        int uploaded = 0, tried = 0;
        bool noConnection = false;
        long rejectedCode = 0;

        foreach (string path in pending)
        {
            tried++;
            ShowStatus($"Datos: subiendo {tried} de {pending.Count}...");

            UploadResult result = new UploadResult();
            yield return UploadCoroutine(path, 0, result);

            if (result.Succeeded)
            {
                uploaded++;
                continue;
            }

            // Sin conexión con el servidor, las demás fallarían igual y cada una costaría sus
            // tres esperas: se deja aquí. Si el servidor contestó y rechazó ESTE archivo, el
            // problema es del archivo y se sigue con los demás.
            if (result.ResponseCode == 0)
            {
                noConnection = true;
                break;
            }

            rejectedCode = result.ResponseCode;
        }

        inFlight--;
        PendingUploads--;
        uploadingPending = false;

        // Se vuelve a contar en vez de restar: es lo que de verdad queda sin subir, también si
        // algún archivo llegó al servidor por otro camino mientras tanto.
        int left = PendingFiles().Count;
        bool willRetry = left > 0 && noConnection && pendingRetriesLeft > 0 && retryPendingSeconds > 0f;

        string reason = noConnection ? "Sin respuesta del servidor"
                      : rejectedCode != 0 ? $"El servidor respondió {rejectedCode}"
                      : "Quedan por subir";

        ShowStatus(left == 0
            ? "Datos: todo subido"
            : $"Datos: {Sessions(left)} por subir. {reason}" + (willRetry ? "; reintentando..." : ""));

        Debug.Log($"[Upload] Pendientes: {uploaded} subida(s) ahora, {left} por subir" +
                  (left > 0 ? $" ({reason})" : "") + ".", this);

        OnPendingUploadFinished?.Invoke();

        if (willRetry)
        {
            pendingRetriesLeft--;
            pendingRetry = StartCoroutine(RetryPendingLater());
        }
    }

    private IEnumerator RetryPendingLater()
    {
        yield return new WaitForSecondsRealtime(retryPendingSeconds);

        pendingRetry = null;

        if (!uploadingPending) StartCoroutine(PendingCoroutine());
    }

    private void ShowPendingCount()
    {
        if (!UiText.Any(statusText, statusTextTmp)) return;

        int count = PendingFiles().Count;

        ShowStatus(count == 0 ? "Datos: todo subido" : $"Datos: {Sessions(count)} por subir");
    }

    private static List<string> PendingFiles() =>
        TelemetryOutbox.PendingFiles(TelemetryManager.Instance != null
            ? TelemetryManager.Instance.GetCurrentFilePath()
            : null);

    private static string Sessions(int count) => count == 1 ? "1 sesión" : $"{count} sesiones";

    private void ShowStatus(string message) => UiText.Set(statusText, statusTextTmp, message);

    /// <summary>Cómo acabó una subida, para quien la espera desde otra corrutina.</summary>
    private class UploadResult
    {
        public bool Succeeded;

        /// <summary>Código HTTP del último intento. 0 si no hubo respuesta del servidor.</summary>
        public long ResponseCode;
    }

    /// <summary>
    /// Comprueba si el servidor responde, sin subir nada.
    ///
    /// Existe porque un fallo de subida puede venir de cuatro sitios muy distintos —el visor
    /// sin red, el DNS, el túnel de Cloudflare, o Flask— y el mensaje de error de una subida
    /// fallida no los distingue. Aquí cada resultado se traduce a cuál de los cuatro fue.
    /// </summary>
    [ContextMenu("Ping al servidor")]
    private void PingServer()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Ping] Entra en Play Mode primero: UnityWebRequest necesita corrutinas.");
            return;
        }

        StartCoroutine(PingCoroutine());
    }

    private IEnumerator PingCoroutine()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogError("[Ping] El visor no tiene red. No es problema del servidor.");
            yield break;
        }

        float started = Time.realtimeSinceStartup;

        // GET contra la misma URL de subida. Un 405 aquí es buena señal: significa que la
        // petición llegó hasta Flask y solo se quejó del método.
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl))
        {
            request.SetRequestHeader("X-API-Key", apiKey);
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000f);
            long code = request.responseCode;

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError($"[Ping] No se pudo conectar con '{serverUrl}' ({ms} ms): " +
                               $"{request.error}. Suele ser DNS, o el túnel caído.");
                yield break;
            }

            string veredicto = code switch
            {
                200 or 405 => "el servidor responde. Si la subida falla, el problema está en " +
                              "la petición, no en la conexión",
                401 or 403 => "llegó hasta Flask, pero rechazó la API key",
                404 => "la URL no existe en el servidor: revisa la ruta de 'serverUrl'",
                413 => "el archivo es demasiado grande para el servidor",
                502 or 503 or 504 => "Cloudflare está en pie pero NO alcanza el origen. Es el " +
                                     "fallo conocido: 'raspberrypi' no se resuelve desde el " +
                                     "contenedor de cloudflared, o Flask no está levantado",
                523 => "Cloudflare no encuentra el origen: la Raspberry está apagada o sin túnel",
                _ => "respuesta inesperada"
            };

            Debug.Log($"[Ping] {code} en {ms} ms · {veredicto}\n" +
                      $"Respuesta: {request.downloadHandler.text}");
        }
    }

    /// <summary>
    /// Sube un JSON mínimo de prueba, sin necesidad de haber jugado una sesión.
    ///
    /// Escribe el archivo y lo pasa por UploadFile a propósito, en vez de armar aquí su propia
    /// petición: así recorre EXACTAMENTE el mismo camino que una subida real —mismo formulario,
    /// mismo nombre de campo, mismas cabeceras, mismos reintentos— y si falla, falla igual. Una
    /// prueba que use otro código puede pasar mientras la de verdad sigue rota.
    ///
    /// Deja un archivo en el servidor. Va con nombre 'test_' para poder distinguirlo y borrarlo.
    /// </summary>
    [ContextMenu("Subir JSON de prueba")]
    private void UploadTestFile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Upload] Entra en Play Mode primero: UnityWebRequest necesita corrutinas.");
            return;
        }

        // El nombre imita el de una sesión real: '{pin}_{sessionId}_{deviceId}.json', con pin
        // 9999 y sesión 0 para reconocerlo. Un nombre libre no serviría de prueba — si el servidor deduce algo del
        // nombre, un formato distinto falla por un motivo que la subida real no tendría.
        string path = Path.Combine(Application.temporaryCachePath,
                                   $"9999_0_{TelemetryManager.GetDeviceId()}.json");

        File.WriteAllText(path, $"{{\"test\":true,\"sentUtc\":\"{DateTime.UtcNow:o}\"}}");

        Debug.Log($"[Upload] Prueba: enviando '{Path.GetFileName(path)}'. " +
                  "Si llega, quedará guardado en el servidor.", this);

        UploadFile(path);
    }

    /// <summary>Sube el archivo de la sesión actual. Cablear al botón del supervisor.</summary>
    public void UploadTelemetry()
    {
        if (TelemetryManager.Instance == null)
        {
            Debug.LogError("[Upload] No hay TelemetryManager: no hay nada que subir.");
            return;
        }

        // Vacía la escritura diferida antes de leer de disco, o se subiría una versión vieja.
        TelemetryManager.Instance.Flush();

        string path = TelemetryManager.Instance.GetCurrentFilePath();

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[Upload] La sesión todavía no tiene archivo asignado.");
            return;
        }

        UploadFile(path);
    }

    /// <summary>
    /// Sube un archivo cualquiera por su ruta. El servidor acepta .json y .csv, así que el
    /// nombre no presupone formato.
    /// </summary>
    public void UploadFile(string filePath)
    {
        StartCoroutine(TrackedUpload(filePath));
    }

    /// <summary>
    /// Subidas en vuelo entre todos los uploaders. SceneLoader espera a que llegue a cero antes
    /// de cambiar de escena: descargarla destruye este componente y mata la corrutina a mitad
    /// de la petición, y el JSON de la sesión que acaba de terminar no llegaba nunca.
    /// </summary>
    public static int PendingUploads { get; private set; }

    // Sin recarga de dominio al entrar en Play, el estático arrastraría la cuenta de la vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => PendingUploads = 0;

    /// <summary>Las de este componente, para descontarlas si se destruye con ellas en vuelo.</summary>
    private int inFlight;

    private IEnumerator TrackedUpload(string filePath)
    {
        inFlight++;
        PendingUploads++;

        yield return UploadCoroutine(filePath, 0, new UploadResult());

        inFlight--;
        PendingUploads--;
    }

    private void OnDestroy()
    {
        // Una corrutina cortada por la destrucción no llega a descontarse sola.
        PendingUploads = Mathf.Max(0, PendingUploads - inFlight);
        inFlight = 0;
    }

    private IEnumerator UploadCoroutine(string filePath, int attempt, UploadResult result)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[Upload] Archivo no encontrado: {filePath}");
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("[Upload] Sin conexión de red. El archivo permanece en el dispositivo.");
            yield break;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        WWWForm form = new WWWForm();
        // El servidor Flask valida por extensión del nombre de archivo, no por este MIME.
        form.AddBinaryData("file", fileData, fileName, "application/json");

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, form))
        {
            request.SetRequestHeader("X-API-Key", apiKey);
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Upload] OK '{fileName}': {request.downloadHandler.text}");

                // Con los bytes enviados: si el archivo cambió mientras subía, lo nuevo sigue pendiente.
                TelemetryOutbox.MarkUploaded(filePath, fileData);
                result.Succeeded = true;
                yield break;
            }

            // El cuerpo de la respuesta se registra además del error: UnityWebRequest.error solo
            // dice "HTTP/1.1 502 Bad Gateway", mientras que el cuerpo trae el motivo real, sea
            // de Flask o la página de diagnóstico de Cloudflare.
            Debug.LogError($"[Upload] Falló '{fileName}' ({request.responseCode}): {request.error}\n" +
                           $"Respuesta: {request.downloadHandler.text}");

            result.ResponseCode = request.result == UnityWebRequest.Result.ProtocolError ? request.responseCode : 0;

            if (attempt < maxRetries)
            {
                Debug.Log($"[Upload] Reintentando ({attempt + 1}/{maxRetries})...");
                yield return new WaitForSeconds(2f);

                // Anidado y no con StartCoroutine: así el reintento cuenta dentro de la misma
                // subida pendiente y nadie cambia de escena entre un intento y el siguiente.
                yield return UploadCoroutine(filePath, attempt + 1, result);
            }
            else
            {
                Debug.LogError("[Upload] Falló tras todos los reintentos. El archivo permanece en el dispositivo.");
            }
        }
    }
}
