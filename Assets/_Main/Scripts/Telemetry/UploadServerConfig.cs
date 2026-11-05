using System;
using UnityEngine;

/// <summary>
/// La dirección del servidor de datos, en un solo sitio: Assets/_Main/Resources/ServidorDatos.asset.
///
/// La leen los JSONUploader de Juego y de Setup, así que cambiar de servidor es cambiar
/// 'Upload Url' en ese asset y recompilar el APK: ni código ni escenas. La API key y el timeout
/// siguen en cada JSONUploader (la herramienta 3 copia los de Juego a Setup).
///
/// El ping usa '/health' en el mismo servidor: se deduce cambiando el '/upload' final.
///
/// Si la URL es http:// (servidor por IP pública, sin dominio ni certificado), Android la
/// rechaza salvo con Project Settings → Player → Android → Other Settings → Allow downloads
/// over HTTP en "Always allowed". El inspector de este asset avisa y el build de Android se
/// para si falta (CodeaServerCheck). Con https:// no hay que tocar nada.
/// </summary>
[CreateAssetMenu(menuName = "Codea/Servidor de datos", fileName = ResourceName)]
public class UploadServerConfig : ScriptableObject
{
    public const string ResourceName = "ServidorDatos";
    public const string AssetPath = "Assets/_Main/Resources/" + ResourceName + ".asset";

    [Tooltip("Dirección completa de subida, terminada en /upload. Con dominio: " +
             "https://DOMINIO/upload. Sin dominio: http://IP-PÚBLICA:PUERTO/upload (y entonces " +
             "hay que permitir HTTP en Player Settings, ver el aviso de abajo).")]
    [SerializeField] private string uploadUrl = "";

    private static UploadServerConfig loaded;
    private static bool warned;

    /// <summary>El asset, o null si no existe (se avisa una vez).</summary>
    public static UploadServerConfig Instance
    {
        get
        {
            if (loaded == null)
            {
                loaded = Resources.Load<UploadServerConfig>(ResourceName);
                if (loaded == null && !warned)
                {
                    warned = true;
                    Debug.LogError($"[Upload] Falta {AssetPath}: no hay a qué servidor subir. " +
                                   "Créalo con Tools → Codea → Servidor de datos.");
                }
            }
            return loaded;
        }
    }

    /// <summary>La URL de subida tal cual está en el asset, sin espacios. Vacía si no hay asset.</summary>
    public string Url => (uploadUrl ?? "").Trim();

    /// <summary>La URL de subida del asset; null si falta el asset o está vacía.</summary>
    public static string UploadUrl
    {
        get
        {
            string url = Instance != null ? Instance.Url : "";
            return url.Length > 0 ? url : null;
        }
    }

    /// <summary>
    /// '/health' en el mismo servidor que la subida: 'https://x/upload' → 'https://x/health'.
    /// Si la URL no termina en '/upload' (mal puesta), se cuelga '/health' detrás.
    /// </summary>
    public static string HealthUrlFor(string uploadUrl)
    {
        if (string.IsNullOrEmpty(uploadUrl)) return null;

        const string tail = "/upload";
        string url = uploadUrl.Trim().TrimEnd('/');

        return url.EndsWith(tail, StringComparison.OrdinalIgnoreCase)
            ? url.Substring(0, url.Length - tail.Length) + "/health"
            : url + "/health";
    }

    /// <summary>http:// sin cifrar: Android lo rechaza si Player Settings no lo permite.</summary>
    public static bool IsInsecure(string url) =>
        !string.IsNullOrEmpty(url) && url.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase);
}
