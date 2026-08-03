// CsvUploader.cs
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

    public void UploadTelemetry()
    {
        string path = TelemetryManager.Instance.GetCurrentFilePath();
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
            Debug.LogError($"CSV no encontrado: {filePath}");
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("Sin conexión de red, no se puede subir el CSV.");
            yield break;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName, "text/csv");

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, form))
        {
            request.SetRequestHeader("X-API-Key", apiKey);
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Upload OK: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"Upload falló ({request.responseCode}): {request.error}");

                if (attempt < maxRetries)
                {
                    Debug.Log($"Reintentando upload ({attempt + 1}/{maxRetries})...");
                    yield return new WaitForSeconds(2f);
                    StartCoroutine(UploadCsvCoroutine(filePath, attempt + 1));
                }
                else
                {
                    Debug.LogError("Upload falló tras todos los reintentos. El CSV permanece en el dispositivo.");
                }
            }
        }
    }
}