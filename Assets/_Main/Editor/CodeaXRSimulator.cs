using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Activa el Meta XR Simulator antes de entrar en Play, para no tener que acordarse.
///
/// El SDK de Meta lo activa solo para el proceso de Unity abierto (variables XR_RUNTIME_JSON y
/// XR_SELECTED_RUNTIME_JSON), así que se pierde cada vez que se cierra Unity. Si se olvida,
/// Play usa el runtime de Meta Horizon y, sin casco conectado, OpenXR falla con
/// XR_ERROR_FORM_FACTOR_UNAVAILABLE y Unity se cierra de golpe.
///
/// Tools → Codea → Usar el Meta XR Simulator en Play: activado por defecto, se guarda por
/// equipo (EditorPrefs). Desmarcarlo para probar con el casco por Link; entonces el simulador
/// también se desactiva. Vale igual en modo batch (allí el SDK usa su configuración "ci").
/// </summary>
[InitializeOnLoad]
public static class CodeaXRSimulator
{
    private const string MenuPath = "Tools/Codea/Usar el Meta XR Simulator en Play";
    private const string PrefKey = "Codea.UseXRSimulator";
    private const string ActivateMenu = "Meta/Meta XR Simulator/Activate";
    private const string DeactivateMenu = "Meta/Meta XR Simulator/Deactivate";

    static CodeaXRSimulator()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    /// <summary>Lo mismo que mira el SDK: el runtime elegido es el JSON del simulador.</summary>
    private static bool IsActive() =>
        string.Equals(Path.GetFileName(Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON") ?? ""),
                      "meta_openxr_simulator.json", StringComparison.OrdinalIgnoreCase);

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || !Enabled || IsActive()) return;

        // Por su menú y no por su clase, que es interna del SDK.
        if (!EditorApplication.ExecuteMenuItem(ActivateMenu))
        {
            Debug.LogWarning($"[Codea] No encuentro '{ActivateMenu}': ¿está el SDK de Meta? Play irá sin simulador.");
            return;
        }

        Debug.Log(IsActive()
            ? "[Codea] Meta XR Simulator activado para este Play (Tools → Codea → Usar el Meta XR Simulator en Play)."
            : "[Codea] No se pudo activar el Meta XR Simulator: mira el aviso de Meta. ¿Está instalado?");
    }

    [MenuItem(MenuPath, false, 200)]
    private static void Toggle()
    {
        Enabled = !Enabled;

        if (!Enabled && IsActive()) EditorApplication.ExecuteMenuItem(DeactivateMenu);

        Debug.Log(Enabled
            ? "[Codea] Play usará el Meta XR Simulator."
            : "[Codea] Play usará el runtime del sistema (casco por Link). Sin casco, Unity se cerrará al pulsar Play.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
