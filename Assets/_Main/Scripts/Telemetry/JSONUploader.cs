using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sube el archivo de telemetría de la sesión al servidor, por POST multipart.
///
/// La subida es manual y opcional (RNF-04): el juego funciona entero sin red, y si no hay
/// conexión el archivo se queda en el visor sin perderse nada. Reintenta un par de veces por
/// si la caída es momentánea.
/// </summary>
public class JSONUploader : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://csv.penginexr.com/upload";
    [SerializeField] private string apiKey = "CodeaVR2YachayTech2026";
    [SerializeField] private int timeoutSeconds = 15;
    [SerializeField] private int maxRetries = 2;

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

        // El nombre imita el de una sesión real: '{pin}_{sessionId}.json', con pin 9999 para
        // reconocerlo. Un nombre libre no serviría de prueba — si el servidor deduce algo del
        // nombre, un formato distinto falla por un motivo que la subida real no tendría.
        string path = Path.Combine(Application.temporaryCachePath,
                                   $"9999_{DateTime.UtcNow:HHmmss}.json");

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
        StartCoroutine(UploadCoroutine(filePath, 0));
    }

    private IEnumerator UploadCoroutine(string filePath, int attempt)
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
                Debug.Log($"[Upload] OK: {request.downloadHandler.text}");
                yield break;
            }

            // El cuerpo de la respuesta se registra además del error: UnityWebRequest.error solo
            // dice "HTTP/1.1 502 Bad Gateway", mientras que el cuerpo trae el motivo real, sea
            // de Flask o la página de diagnóstico de Cloudflare.
            Debug.LogError($"[Upload] Falló ({request.responseCode}): {request.error}\n" +
                           $"Respuesta: {request.downloadHandler.text}");

            if (attempt < maxRetries)
            {
                Debug.Log($"[Upload] Reintentando ({attempt + 1}/{maxRetries})...");
                yield return new WaitForSeconds(2f);
                StartCoroutine(UploadCoroutine(filePath, attempt + 1));
            }
            else
            {
                Debug.LogError("[Upload] Falló tras todos los reintentos. El archivo permanece en el dispositivo.");
            }
        }
    }
}
