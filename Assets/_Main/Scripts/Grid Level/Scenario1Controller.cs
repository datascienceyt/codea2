using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 1 con el Director y con la telemetría.
/// </summary>
public class Scenario1Controller : MonoBehaviour, IStepAction
{
    [Header("Acciones al finalizar el nivel")]
    public UnityEvent OnLevelFinished;

    [Header("Nivel")]
    [Tooltip("Si queda vacío se resuelve solo con FindAnyObjectByType.")]
    [SerializeField] LevelManager levelManager;

    [Header("Reintento automático")]
    [Tooltip("Si queda vacío se busca en este mismo objeto.")]
    [SerializeField] ProgramRunner runner;

    [Tooltip("Margen entre que la secuencia termina y el reinicio, para que el jugador vea " +
             "dónde falló antes de que todo vuelva a su sitio.")]
    [SerializeField] float retryDelay = 1.5f;

    [Tooltip("Se dispara cuando la secuencia termina SIN resolver el nivel. Aquí van " +
             "RegisterFailedAttempt, LevelLoader.ReloadLevel, LevelManager.ResetLevel " +
             "y ProgramRunner.ResetRunner.")]
    public UnityEvent OnAttemptFailed;

    [Header("Telemetría")]
    [Tooltip("Debe coincidir con el challengeId del ProgramTrigger de este escenario.")]
    [SerializeField] string challengeId = "escenario1";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento: dos llamadas " +
             "reinician el cronómetro del escenario.")]
    [SerializeField] bool startChallengeOnStart = true;

    bool levelFinished;

    void Awake()
    {
        if (levelManager == null)
            levelManager = FindAnyObjectByType<LevelManager>();

        // Se escucha a LevelManager y NO a Exit.OnLevelFinished a propósito.
        //
        // ReloadLevel() destruye todos los hijos del loader e instancia un Exit nuevo, así que
        // una suscripción al Exit se quedaba apuntando a un objeto destruido: tras un reinicio
        // el bot llegaba a la meta y no pasaba nada. LevelManager sobrevive a la recarga.
        if (levelManager != null)
            levelManager.OnLevelCompleted += HandleLevelFinished;
        else
            Debug.LogError("[Escenario1] No hay ningún LevelManager en la escena.");

        if (runner == null)
            runner = GetComponent<ProgramRunner>();

        if (runner != null)
            runner.OnRunFinished.AddListener(HandleRunFinished);
        else
            Debug.LogWarning("[Escenario1] Sin ProgramRunner: no habrá reintento automático.", this);
    }

    void OnDestroy()
    {
        if (levelManager != null)
            levelManager.OnLevelCompleted -= HandleLevelFinished;

        if (runner != null)
            runner.OnRunFinished.RemoveListener(HandleRunFinished);
    }

    void Start()
    {
        //OVRInput.DisableSimultaneousHandsAndControllers();

        if (startChallengeOnStart && TelemetryManager.Instance != null)
            TelemetryManager.Instance.StartChallenge(challengeId);
    }

    void HandleLevelFinished()
    {
        levelFinished = true;

        // Antes de invocar el evento: si algo cableado ahí falla, la traza ya se vio.
        Debug.Log($"[Escenario 1] COMPLETADO · challengeId '{challengeId}'", this);

        OnLevelFinished?.Invoke();
    }

    /// <summary>
    /// La secuencia terminó de ejecutarse. Si el bot no llegó a la meta, el intento falló y
    /// el nivel se reinicia solo: el jugador no tiene que pulsar nada para volver a probar.
    /// </summary>
    void HandleRunFinished()
    {
        // CompleteLevel() ocurre DENTRO de la ejecución, cuando el bot usa la salida, así que
        // al llegar aquí el estado ya es definitivo.
        if (IsLevelCompleted) return;

        StartCoroutine(RetryAfterDelay());
    }

    IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSeconds(retryDelay);

        // Se revalida por si algo completó el nivel durante la espera.
        if (IsLevelCompleted) yield break;

        OnAttemptFailed?.Invoke();
    }

    bool IsLevelCompleted => levelManager != null && levelManager.IsCompleted;

    public IEnumerator Execute()
    {
        levelFinished = false;
        yield return new WaitUntil(() => levelFinished);
    }
}
