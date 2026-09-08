using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga una escena por nombre desde un UnityEvent, comprobando antes que esté en Build
/// Settings.
///
/// Cierra el círculo del piloto: al terminar una sesión hay que volver a la pantalla del
/// supervisor para que entre el siguiente participante.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Tooltip("Escena a cargar. Debe estar en File > Build Settings.")]
    [SerializeField] private string sceneName = "Setup";

    [Tooltip("Margen antes de cargar, para que dé tiempo al fundido.")]
    [SerializeField] private float loadDelay = 0.5f;

    [Tooltip("Cierra la run y vuelca lo pendiente antes de cambiar de escena.\n\n" +
             "Déjalo activado para volver a la pantalla del supervisor: si no, los contadores " +
             "de escritura diferida que todavía no se hayan guardado se pierden al descargar " +
             "la escena. Desactívalo solo si cargas otra escena a mitad de la misma sesión.")]
    [SerializeField] private bool closeRunBeforeLoading = true;

    [Header("Eventos")]
    [Tooltip("Justo antes de cargar: fundido, sonido...")]
    public UnityEvent OnLoading;

    private bool loading;

    [ContextMenu("Cargar")]
    public void Load() => Load(sceneName);

    /// <summary>Para cargar otra escena desde un UnityEvent con argumento string.</summary>
    public void Load(string scene)
    {
        // Una segunda pulsación mientras se funde a negro cargaría dos veces.
        if (loading) return;

        if (string.IsNullOrWhiteSpace(scene))
        {
            Debug.LogError($"[Escenas] {name} no tiene ninguna escena configurada.", this);
            return;
        }

        // Se comprueba antes de fundir: si no está en Build Settings, LoadScene lanza una
        // excepción y el jugador se queda en negro sin salida.
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[Escenas] La escena {scene} no está en File > Build Settings.", this);
            return;
        }

        if (closeRunBeforeLoading && TelemetryManager.Instance != null)
        {
            // Flush primero y EndRun después: EndRun sella endedUtc y guarda, así que la
            // última escritura del archivo se lleva dentro también lo diferido.
            TelemetryManager.Instance.Flush();
            TelemetryManager.Instance.EndRun();
        }

        loading = true;
        OnLoading?.Invoke();

        StartCoroutine(LoadAfterDelay(scene));
    }

    private IEnumerator LoadAfterDelay(string scene)
    {
        yield return new WaitForSeconds(loadDelay);

        SceneManager.LoadScene(scene);
    }
}
