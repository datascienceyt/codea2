using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 3 con el Director y con la telemetría, igual que sus equivalentes de
/// los escenarios 1 y 2.
///
/// El escenario se completa cuando la posición bloqueada queda despejada.
/// </summary>
public class Scenario3Controller : MonoBehaviour, IStepAction
{
    [Header("Meta")]
    [Tooltip("Posición que debe quedar vacía: el montón de barriles que tapa el paso.")]
    [SerializeField] private ArmSlot slotToClear;

    [Tooltip("Si queda vacío se resuelve solo con FindAnyObjectByType.")]
    [SerializeField] private RoboticArm arm;

    [Header("Telemetría")]
    [SerializeField] private string challengeId = "escenario3";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento: dos llamadas " +
             "reinician el cronómetro del escenario.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Acciones al finalizar")]
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

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
    }

    private void OnDestroy()
    {
        if (arm == null) return;

        arm.OnActionCompleted -= CheckCompletion;
        arm.OnInvalidAction -= HandleInvalidAction;
    }

    private void Start()
    {
        if (startChallengeOnStart) StartScenario();
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

    private void CheckCompletion()
    {
        if (scenarioFinished || slotToClear == null) return;
        if (!slotToClear.IsEmpty) return;

        scenarioFinished = true;

        Debug.Log($"[Escenario 3] COMPLETADO · '{slotToClear.name}' despejado · " +
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
