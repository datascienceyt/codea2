// CsvUploader.cs
//
// El nombre es un residuo histórico: la telemetría pasó de CSV a JSON, pero renombrar la
// clase rompería las referencias de la escena, que Unity resuelve por nombre de tipo.
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class CsvUploader : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://csv.penginexr.com/upload";
    [SerializeField] private string apiKey = "CodeaVR2YachayTech2026";
    [SerializeField] private int timeoutSeconds = 15;
    [SerializeField] private int maxRetries = 2;

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

        UploadCsv(path);
    }

    public void UploadCsv(string filePath)
    {
        StartCoroutine(UploadCsvCoroutine(filePath, 0));
    }

    private IEnumerator UploadCsvCoroutine(string filePath, int attempt)
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

            Debug.LogError($"[Upload] Falló ({request.responseCode}): {request.error}");

            if (attempt < maxRetries)
            {
                Debug.Log($"[Upload] Reintentando ({attempt + 1}/{maxRetries})...");
                yield return new WaitForSeconds(2f);
                StartCoroutine(UploadCsvCoroutine(filePath, attempt + 1));
            }
            else
            {
                Debug.LogError("[Upload] Falló tras todos los reintentos. El archivo permanece en el dispositivo.");
            }
        }
    }
}
