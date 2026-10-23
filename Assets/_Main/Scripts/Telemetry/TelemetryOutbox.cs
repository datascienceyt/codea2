using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Lleva la cuenta de qué JSON de sesión ya están en el servidor y cuáles faltan por subir.
///
/// Existe porque una sesión puede terminar sin subirse: sin red, con el servidor caído, o con
/// la aplicación cerrada a mitad de partida (en Quest, cerrar es matar el proceso: no hay
/// momento fiable para subir nada). El archivo se queda en el visor, y lo que hace falta es que
/// alguien lo suba después. Aquí se decide cuáles son esos archivos; quien los sube es
/// JSONUploader.
///
/// Los JSON no se mueven, no se renombran y no se borran nunca: esta clase solo apunta, en
/// 'subidos.txt', el nombre de cada archivo subido junto a la huella de su contenido. Un
/// archivo está pendiente si no figura ahí con el contenido que tiene ahora. Así una sesión que
/// se subió a medias y luego siguió vuelve a estar pendiente sola, y una ya subida no se
/// vuelve a enviar por mucho que se pulse el botón.
/// </summary>
public static class TelemetryOutbox
{
    private const string IndexFileName = "subidos.txt";

    // {pin}_{sessionId}_{deviceId}.json, el nombre que pone TelemetryManager.
    private static readonly Regex SessionFile = new Regex(@"^\d+_\d+_[A-Za-z0-9]+\.json$");

    private static string Folder => Application.persistentDataPath;
    private static string IndexPath => Path.Combine(Folder, IndexFileName);

    /// <summary>
    /// Sesiones del visor que el servidor todavía no tiene, de la más antigua a la más reciente.
    /// </summary>
    /// <param name="exceptPath">
    /// La de la run abierta, si la hay: sigue cambiando y la sube su propio fin de sesión.
    /// </param>
    public static List<string> PendingFiles(string exceptPath = null)
    {
        List<string> pending = new List<string>();

        if (!Directory.Exists(Folder)) return pending;

        HashSet<string> uploaded = ReadIndex();

        foreach (string path in Directory.GetFiles(Folder, "*.json"))
        {
            if (!SessionFile.IsMatch(Path.GetFileName(path))) continue;
            if (exceptPath != null && SamePath(path, exceptPath)) continue;

            try
            {
                if (!uploaded.Contains(Entry(Path.GetFileName(path), File.ReadAllBytes(path))))
                    pending.Add(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Upload] No se pudo leer '{path}': {e.Message}");
            }
        }

        pending.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
        return pending;
    }

    /// <summary>
    /// Apunta que el servidor ya tiene ese archivo con ese contenido. Se pasan los bytes que se
    /// enviaron, no los del disco: el archivo puede haber cambiado mientras se subía, y
    /// entonces lo que hay ahora sigue pendiente.
    /// </summary>
    public static void MarkUploaded(string filePath, byte[] sentBytes)
    {
        // Solo las sesiones del visor: el JSON de prueba del uploader vive en otra carpeta.
        if (!SessionFile.IsMatch(Path.GetFileName(filePath)) ||
            !SamePath(Path.GetDirectoryName(filePath), Folder))
            return;

        try
        {
            File.AppendAllText(IndexPath, Entry(Path.GetFileName(filePath), sentBytes) + "\n");
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // No pasa nada grave: el archivo se volverá a subir la próxima vez.
            Debug.LogWarning($"[Upload] No se pudo apuntar la subida en '{IndexPath}': {e.Message}");
        }
    }

    private static HashSet<string> ReadIndex()
    {
        HashSet<string> entries = new HashSet<string>();

        try
        {
            if (File.Exists(IndexPath))
                foreach (string line in File.ReadAllLines(IndexPath))
                    if (line.Length > 0) entries.Add(line);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // Sin índice todo cuenta como pendiente: se sube de más, nunca de menos.
            Debug.LogWarning($"[Upload] No se pudo leer '{IndexPath}': {e.Message}");
        }

        return entries;
    }

    private static string Entry(string fileName, byte[] content)
    {
        using (MD5 md5 = MD5.Create())
            return fileName + "\t" + BitConverter.ToString(md5.ComputeHash(content)).Replace("-", string.Empty);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
