using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 3 con el Director y con la telemetría, igual que sus equivalentes de
/// los escenarios 1 y 2.
///
/// El escenario se completa cuando todos los objetos han llegado a destinationSlot.
/// </summary>
public class Scenario3Controller : MonoBehaviour, IStepAction
{
    [Header("Meta")]
    [Tooltip("Posición que empieza bloqueando el paso: el montón de barriles a trasladar. " +
             "Solo se usa para calcular cuántos objetos hay que mover, no para revisar la meta.")]
    [SerializeField] private ArmSlot slotToClear;

    [Tooltip("Adónde deben llegar los objetos. La meta se cumple cuando aquí hay tantos " +
             "objetos como al principio sumaban origen + destino.")]
    [SerializeField] private ArmSlot destinationSlot;

    [Tooltip("Si queda vacío se resuelve solo con FindAnyObjectByType.")]
    [SerializeField] private RoboticArm arm;

    [Header("Intento fallido")]
    [Tooltip("El ProgramRunner de ESTE escenario. Deliberadamente no se adivina: hay uno por " +
             "escenario y coger el equivocado registraría los fallos en el escenario de al lado.")]
    [SerializeField] private ProgramRunner runner;

    [Tooltip("Margen entre que la secuencia termina y el aviso, para que el niño vea dónde " +
             "quedó el brazo antes de que todo vuelva a su sitio.")]
    [SerializeField] private float retryDelay = 1.5f;

    [Tooltip("Se dispara cuando la secuencia termina SIN resolver el escenario. Aquí van el " +
             "reinicio (SocketRow.PlaceInitialBlocks, RoboticArm.ResetArm, " +
             "ProgramRunner.ResetRunner) y los avisos visuales.\n\n" +
             "NO cablees aquí RegisterFailedAttempt: ya se registra desde código y contarlo " +
             "aquí además lo duplicaría.")]
    public UnityEvent OnAttemptFailed;

    [Header("Telemetría")]
    [SerializeField] private string challengeId = "escenario3";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento: dos llamadas " +
             "reinician el cronómetro del escenario.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Acciones al finalizar")]
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

    /// <summary>
    /// Objetos que tienen que terminar en destinationSlot. Se calcula UNA vez en Start, con
    /// el montaje inicial de la escena, y no se toca en los resets: es un dato del nivel, no
    /// del estado de la partida.
    /// </summary>
    private int requiredCount;

    private void Awake()
    {
        if (arm == null) arm = FindAnyObjectByType<RoboticArm>();

        if (arm != null)
        {
            arm.OnActionCompleted += CheckCompletion;
            arm.OnInvalidAction += HandleInvalidAction;
        }
        else
        {
            Debug.LogError("[Escenario3] No hay ningún RoboticArm en la escena.", this);
        }

        if (runner != null)
            runner.OnRunFinished.AddListener(HandleRunFinished);
        else
            Debug.LogError($"[Escenario3] 'runner' sin asignar en '{name}': los intentos " +
                           "fallidos no se registrarán y failedAttempts saldrá siempre a 0.", this);
    }

    private void OnDestroy()
    {
        if (runner != null)
            runner.OnRunFinished.RemoveListener(HandleRunFinished);

        if (arm == null) return;

        arm.OnActionCompleted -= CheckCompletion;
        arm.OnInvalidAction -= HandleInvalidAction;
    }

    private void Start()
    {
        if (startChallengeOnStart) StartScenario();

        int fromOrigin = slotToClear != null ? slotToClear.Count : 0;
        int fromDestination = destinationSlot != null ? destinationSlot.Count : 0;
        requiredCount = fromOrigin + fromDestination;
    }

    /// <summary>Cablear al Director si se prefiere lanzarlo desde un Step.</summary>
    public void StartScenario()
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.StartChallenge(challengeId);
    }

    private void HandleInvalidAction()
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);
    }

    /// <summary>
    /// La secuencia terminó de ejecutarse. Si el escenario no quedó resuelto, fue un intento
    /// fallido.
    /// </summary>
    private void HandleRunFinished()
    {
        // El escenario se resuelve DENTRO de la ejecución, en el último Soltar, así que al
        // llegar aquí el estado ya es definitivo.
        if (scenarioFinished) return;

        // El registro va ANTES de la espera, y desde código, por las dos razones de siempre:
        // una corrutina muere si el Director desactiva la estación durante retryDelay, y
        // cablearlo por UnityEvent significa que olvidarlo rompe los datos en silencio.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterFailedAttempt();

        StartCoroutine(NotifyFailureAfterDelay());
    }

    private IEnumerator NotifyFailureAfterDelay()
    {
        yield return new WaitForSeconds(retryDelay);

        // Se revalida por si algo resolvió el escenario durante la espera.
        if (scenarioFinished) yield break;

        OnAttemptFailed?.Invoke();
    }

    /// <summary>
    /// Se mide por destinationSlot, no por slotToClear.IsEmpty: Take() saca el objeto de la
    /// pila en el propio Recoger, así que el origen queda "vacío" con el objeto todavía en la
    /// pinza. Medir por el destino evita completar el nivel antes de que el Soltar final
    /// ocurra de verdad.
    /// </summary>
    private void CheckCompletion()
    {
        if (scenarioFinished || destinationSlot == null || requiredCount <= 0) return;
        if (destinationSlot.Count < requiredCount) return;

        scenarioFinished = true;

        Debug.Log($"[Escenario 3] COMPLETADO · '{destinationSlot.name}' con {requiredCount} objeto(s) · " +
                  $"challengeId '{challengeId}'", this);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.CompleteChallenge(challengeId);

        OnScenarioFinished?.Invoke();
    }

    /// <summary>
    /// Bloquea el Step del Director hasta despejar la posición. Como en Scenario2Controller,
    /// no se reinicia el flag: si ya estaba resuelto, el paso se da por cumplido en vez de
    /// colgarse esperando algo que ya ocurrió.
    /// </summary>
    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => scenarioFinished);
    }

    [ContextMenu("Reset Scenario")]
    public void ResetScenario()
    {
        scenarioFinished = false;

        if (arm != null) arm.ResetArm();
    }
}
