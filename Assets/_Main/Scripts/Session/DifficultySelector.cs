using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de selección de dificultad (RI-03). La fija el supervisor antes de entregar el
/// visor y queda fija para toda la sesión: el jugador no puede cambiarla.
///
/// Va en la primera escena. Guarda la dificultad en TelemetryManager (que persiste entre
/// escenas) y carga la escena de juego correspondiente.
/// </summary>
public class DifficultySelector : MonoBehaviour
{
    [Header("Escenas de destino")]
    [Tooltip("Deben estar añadidas en File > Build Settings, o LoadScene falla.")]
    [SerializeField] private string basicSceneName = "Basica";

    [Tooltip("Si un día unificas las dos dificultades en una sola escena, pon aquí el mismo " +
             "nombre que arriba: la dificultad seguirá registrándose bien.")]
    [SerializeField] private string intermediateSceneName = "Intermedia";

    [Header("Transición")]
    [Tooltip("Margen antes de cargar, para que dé tiempo al fundido o al sonido del botón.")]
    [SerializeField] private float loadDelay = 0.5f;

    [Header("Eventos")]
    [Tooltip("Se dispara al elegir, antes de cargar. Útil para el Fader o un sonido.")]
    public UnityEvent OnDifficultySelected;

    private bool selecting;

    /// <summary>Cablear al WhenSelect del PokeInteractable del botón de dificultad básica.</summary>
    [ContextMenu("Seleccionar básica")]
    public void SelectBasic() => Apply(Difficulty.Basica, basicSceneName);

    /// <summary>Cablear al WhenSelect del PokeInteractable del botón de dificultad intermedia.</summary>
    [ContextMenu("Seleccionar intermedia")]
    public void SelectIntermediate() => Apply(Difficulty.Avanzada, intermediateSceneName);

    /// <summary>0 = básica, 1 = intermedia. Para cablear por UnityEvent con argumento int.</summary>
    public void SelectByIndex(int difficultyIndex)
    {
        if (difficultyIndex == (int)Difficulty.Basica) SelectBasic();
        else SelectIntermediate();
    }

    private void Apply(Difficulty difficulty, string sceneName)
    {
        // Una segunda pulsación mientras se funde a negro cargaría la otra escena encima.
        if (selecting) return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"[Dificultad] No hay escena configurada para {difficulty}.");
            return;
        }

        // Se comprueba antes de fundir a negro: si la escena no está en Build Settings,
        // LoadScene lanza una excepción y el jugador se queda en una pantalla negra sin salida.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[Dificultad] La escena '{sceneName}' no está en File > Build Settings.");
            return;
        }

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.SetDifficulty(difficulty);
        else
            Debug.LogWarning("[Dificultad] No hay TelemetryManager: la sesión se registrará " +
                             "con la dificultad que hubiera guardada de antes.");

        selecting = true;
        OnDifficultySelected?.Invoke();

        StartCoroutine(LoadAfterDelay(sceneName));
    }

    private IEnumerator LoadAfterDelay(string sceneName)
    {
        yield return new WaitForSeconds(loadDelay);

        SceneManager.LoadScene(sceneName);
    }
}
