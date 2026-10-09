using UnityEngine;

/// <summary>
/// Pone la escena de juego en la dificultad de la sesión. Hay una sola escena para las dos
/// dificultades: lo que cambia de un reto a otro lo declaran sus componentes de dificultad
/// (DifficultyVariant) y las piezas que solo existen en una (DifficultyOnly).
///
/// La dificultad sale de la pantalla del supervisor, la misma que lee TelemetryManager, así
/// que la escena y el JSON no pueden discrepar. Antes había una escena por dificultad y este
/// componente (DifficultyScene) solo corregía la telemetría cuando no coincidían.
///
/// Corre antes que cualquier otro script (orden -1000): los retos usan sus datos en su propio
/// Awake o Start —SocketRow crea los huecos en Awake, LevelLoader carga el nivel en Start— y
/// tienen que encontrárselos ya puestos.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DifficultyApplier : MonoBehaviour
{
    public enum EditorDifficulty
    {
        DeLaPantallaDeSetup,
        Basica,
        Intermedia
    }

    [Tooltip("Solo en el editor, para probar sin pasar por Setup. En el visor se usa siempre la " +
             "dificultad elegida en la pantalla del supervisor.")]
    [SerializeField] private EditorDifficulty editorDifficulty = EditorDifficulty.DeLaPantallaDeSetup;

    private static Difficulty? applied;

    /// <summary>
    /// Dificultad con la que se montó la escena. Antes de que el aplicador corra, la que dejó
    /// preparada la pantalla del supervisor.
    /// </summary>
    public static Difficulty Current => applied ?? TelemetryManager.GetPreparedDifficulty();

    private void Awake()
    {
        Difficulty difficulty = Resolve();
        applied = difficulty;

        int variants = 0, hidden = 0;

        foreach (DifficultyVariant variant in FindObjectsByType<DifficultyVariant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Un reto mal configurado no debe dejar sin montar a los demás.
            try
            {
                variant.Apply(difficulty);
                variants++;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, variant);
                Debug.LogError($"[Dificultad] '{variant.name}' ({variant.GetType().Name}) no se pudo aplicar; " +
                               "ese reto queda como está guardado en la escena.", variant);
            }
        }

        foreach (DifficultyOnly only in FindObjectsByType<DifficultyOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (only.Difficulty == difficulty) continue;

            only.gameObject.SetActive(false);
            hidden++;
        }

        Debug.Log($"[Dificultad] Escena en {Label(difficulty)}: {variants} reto(s) configurados, " +
                  $"{hidden} pieza(s) de la otra dificultad apagadas.", this);
    }

    private void OnDestroy()
    {
        // Al volver a Setup la escena se descarga; la siguiente sesión puede ser de la otra.
        applied = null;
    }

    // Start y no Awake: TelemetryManager abre la run en su propio Awake.
    private void Start()
    {
        if (TelemetryManager.Instance == null)
        {
            Debug.LogWarning("[Dificultad] No hay TelemetryManager en la escena; no se registra nada.");
            return;
        }

        // Solo discrepan si en el editor se fuerza una dificultad distinta de la de Setup. El
        // JSON tiene que decir la que de verdad se jugó, sin cambiar la que dejó elegida Setup:
        // forzar una prueba no debe arrastrarse a la siguiente.
        if (TelemetryManager.Instance.GetCurrentDifficulty() != Current)
            TelemetryManager.Instance.SetRunDifficulty(Current);
    }

    private Difficulty Resolve()
    {
        if (Application.isEditor && editorDifficulty != EditorDifficulty.DeLaPantallaDeSetup)
        {
            Difficulty forced = editorDifficulty == EditorDifficulty.Basica ? Difficulty.Basica : Difficulty.Avanzada;
            Debug.LogWarning($"[Dificultad] Forzada a {Label(forced)} desde el inspector (solo en el editor).", this);
            return forced;
        }

        return TelemetryManager.GetPreparedDifficulty();
    }

    public static string Label(Difficulty difficulty) =>
        difficulty == Difficulty.Basica ? "básica" : "intermedia";
}
