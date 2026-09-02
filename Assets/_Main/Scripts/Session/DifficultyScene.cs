using UnityEngine;

/// <summary>
/// Declara a qué dificultad pertenece la escena en la que está, y la impone en la telemetría
/// si no coincide.
///
/// Existe para proteger los datos: con una escena por dificultad es cuestión de tiempo que
/// alguien abra "Intermedia" directamente desde el editor, o que la escena se lance sin pasar
/// por el selector. Sin esto, el JSON diría "básica" mientras el estudiante juega la intermedia,
/// y ese error es indetectable al analizar.
/// </summary>
public class DifficultyScene : MonoBehaviour
{
    [Tooltip("La dificultad que representa ESTA escena.")]
    [SerializeField] private Difficulty difficulty = Difficulty.Basica;

    // Start y no Awake: TelemetryManager abre la run en su propio Awake.
    private void Start()
    {
        if (TelemetryManager.Instance == null)
        {
            Debug.LogWarning("[Dificultad] No hay TelemetryManager en la escena; no se registra nada.");
            return;
        }

        Difficulty current = TelemetryManager.Instance.GetCurrentDifficulty();
        if (current == difficulty) return;

        Debug.LogWarning($"[Dificultad] La telemetría decía '{current}' pero esta escena es " +
                         $"'{difficulty}'. Se corrige. ¿Se saltó la pantalla de selección?");

        TelemetryManager.Instance.SetDifficulty(difficulty);
    }
}
