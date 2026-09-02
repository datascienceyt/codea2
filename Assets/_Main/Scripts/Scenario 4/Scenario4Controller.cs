using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 4 con el Director y con la telemetría. El escenario termina cuando
/// todos los huecos del panel tienen la ficha correcta.
/// </summary>
public class Scenario4Controller : MonoBehaviour, IStepAction
{
    [Header("Panel")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private ShapeSocket[] sockets;

    [Tooltip("Básica empareja por figura; intermedia, por número de lados. Con una escena por " +
             "dificultad, basta con dejar cada escena en su modo.")]
    [SerializeField] private ShapeMatchMode matchMode = ShapeMatchMode.Shape;

    [Header("Rechazo")]
    [Tooltip("Quien devuelve las fichas mal colocadas a su sitio.")]
    [SerializeField] private BlockResetter chipResetter;

    [Tooltip("Margen antes de devolver la ficha, para que el jugador vea que no encajó.")]
    [SerializeField] private float rejectDelay = 0.6f;

    [Header("Telemetría")]
    [SerializeField] private string challengeId = "escenario4";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Eventos")]
    public UnityEvent OnCorrectPlacement;
    public UnityEvent OnWrongPlacement;
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

    private void Awake()
    {
        if (sockets == null || sockets.Length == 0)
            sockets = GetComponentsInChildren<ShapeSocket>(true);

        if (chipResetter == null)
            chipResetter = FindAnyObjectByType<BlockResetter>();

        foreach (ShapeSocket socket in sockets)
        {
            if (socket == null) continue;

            socket.SetMode(matchMode);
            socket.OnChipEvaluated += HandleChipEvaluated;
        }
    }

    private void OnDestroy()
    {
        if (sockets == null) return;

        foreach (ShapeSocket socket in sockets)
            if (socket != null) socket.OnChipEvaluated -= HandleChipEvaluated;
    }

    private void Start()
    {
        if (startChallengeOnStart) StartScenario();
    }

    /// <summary>Cablear al Director si se prefiere lanzarlo desde un Step.</summary>
    public void StartScenario()
    {
        if (TelemetryManager.Instance == null) return;

        TelemetryManager.Instance.StartChallenge(challengeId);

        // Sin el modo, dos sesiones con la misma tasa de acierto no son comparables:
        // emparejar por figura y por número de lados son retos muy distintos.
        TelemetryManager.Instance.SetMatchMode(
            challengeId,
            matchMode == ShapeMatchMode.Shape ? "forma" : "lados");
    }

    private void HandleChipEvaluated(ShapeSocket socket, ShapeChip chip, bool correct)
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterPlacement(
                challengeId,
                socket.SocketId,
                chip.ShapeId,
                chip.Sides,
                socket.ExpectedSides,
                correct);

        if (correct)
        {
            OnCorrectPlacement?.Invoke();
            CheckCompletion();
            return;
        }

        OnWrongPlacement?.Invoke();
        StartCoroutine(RejectChip(socket, chip));
    }

    /// <summary>
    /// Devuelve la ficha a su sitio, pero NO en el acto.
    ///
    /// El aviso llega desde Socket.Occupy(), y BlockNode.AttachTo todavía tiene que terminar
    /// de enlazar el bloque a ese socket: desacoplarlo ahí dejaría al chip apuntando a un
    /// hueco que ya no ocupa. Esperar también da un momento para que se vea el rechazo.
    /// </summary>
    private IEnumerator RejectChip(ShapeSocket socket, ShapeChip chip)
    {
        yield return new WaitForSeconds(rejectDelay);

        socket.ResetSocket();

        if (chipResetter == null || !chipResetter.ReturnBlock(chip))
            Debug.LogWarning($"[Escenario4] No se pudo devolver '{chip.name}' a su sitio: " +
                             "¿cuelga del objeto con BlockResetter?", chip);
    }

    private void CheckCompletion()
    {
        if (scenarioFinished) return;

        foreach (ShapeSocket socket in sockets)
            if (socket != null && !socket.IsSolved) return;

        scenarioFinished = true;

        Debug.Log($"[Escenario 4] COMPLETADO · {sockets.Length} huecos encajados · " +
                  $"modo '{matchMode}' · challengeId '{challengeId}'", this);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.CompleteChallenge(challengeId);

        OnScenarioFinished?.Invoke();
    }

    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => scenarioFinished);
    }

    [ContextMenu("Reset Scenario")]
    public void ResetScenario()
    {
        scenarioFinished = false;

        foreach (ShapeSocket socket in sockets)
            if (socket != null) socket.ResetSocket();

        if (chipResetter != null) chipResetter.ResetBlocks();
    }
}
