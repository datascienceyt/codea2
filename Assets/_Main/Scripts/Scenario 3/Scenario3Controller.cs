using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 3 con el Director y con la telemetría, igual que sus equivalentes de
/// los escenarios 1 y 2.
///
/// El escenario se completa cuando cada tipo está entero en su destino: barriles a un
/// lado, cajas al otro. Hacen falta al menos dos ejecuciones, una por tipo.
/// </summary>
public class Scenario3Controller : MonoBehaviour, IStepAction
{
    [System.Serializable]
    public class SortingGoal
    {
        [Tooltip("Tipo que hay que clasificar.")]
        public ArmItemType type = ArmItemType.Barril;

        [Tooltip("Dónde tienen que acabar todos los objetos de ese tipo.")]
        public ArmSlot destination;

        /// <summary>Cuántos hay de este tipo en toda la escena. Se calcula en Start.</summary>
        [System.NonSerialized] public int required;
    }

    [Header("Meta")]
    [Tooltip("Un destino por tipo: barriles a la izquierda, cajas a la derecha. El escenario " +
             "se completa cuando TODOS los tipos están clasificados.")]
    [SerializeField] private SortingGoal[] goals;

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

    [Tooltip("Se dispara cuando una ejecución SÍ clasificó objetos pero aún falta trabajo: " +
             "típicamente al terminar con un tipo y quedar el otro. Solo para el aviso visual; " +
             "el botón de ejecutar ya se rearma desde código.")]
    public UnityEvent OnAttemptAdvanced;

    [Header("Telemetría")]
    [SerializeField] private string challengeId = "escenario3";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento: dos llamadas " +
             "reinician el cronómetro del escenario.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Acciones al finalizar")]
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

    /// <summary>
    /// Objetos ya bien clasificados al terminar la última ejecución. Es lo que permite
    /// distinguir "esta pasada no sirvió de nada" de "esta pasada avanzó pero aún falta el
    /// otro tipo", que con dos pasadas son cosas muy distintas.
    /// </summary>
    private int lastDeliveredTotal;

    private void Awake()
    {
        if (arm == null) arm = FindAnyObjectByType<RoboticArm>();

        if (arm != null)
        {
            arm.OnActionCompleted += CheckCompletion;
            arm.OnInvalidAction += HandleInvalidAction;

            // El brazo necesita saber adónde va cada tipo para el bloque "Girar al destino" de
            // la básica, y esa tabla es esta: se le presta en vez de copiarla en su inspector.
            arm.SetDestinationResolver(DestinationFor);
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

        // Cuántos hay de cada tipo en TODAS las posiciones del brazo, no solo en el montón de
        // origen: así da igual que el decorado reparta los barriles entre varios sitios, y el
        // dato sigue siendo del nivel y no del estado de la partida.
        foreach (SortingGoal goal in goals)
        {
            if (goal == null) continue;

            goal.required = arm != null ? arm.CountOf(goal.type) : 0;

            if (goal.destination == null)
                Debug.LogError($"[Escenario3] El objetivo '{goal.type}' no tiene destino " +
                               "asignado: nunca se dará por clasificado.", this);
        }

        lastDeliveredTotal = DeliveredTotal();
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

        // El criterio de fallo NO puede ser "el escenario no está completo".
        //
        // Hacen falta al menos dos ejecuciones, una por tipo, así que la pasada de barriles
        // perfecta terminaría con las cajas sin mover y se contaría como fallida: failedAttempts
        // saldría inflado midiendo el diseño del reto en vez del error del niño. Falla la pasada
        // que no clasificó NADA; la que avanzó y se quedó corta es simplemente menos óptima, y
        // eso ya se lee en el número de intentos y sus repeticiones.
        int delivered = DeliveredTotal();
        bool progressed = delivered > lastDeliveredTotal;

        lastDeliveredTotal = delivered;

        if (progressed)
        {
            // Red de seguridad, no el mecanismo principal: lo que de verdad permite las varias
            // pasadas es 'allowRepeatedRuns' en el ProgramTrigger. Esto cubre el caso de que
            // alguien lo deje sin marcar, porque entonces el botón se quedaría muerto a mitad
            // de partida y el escenario sin salida.
            if (runner != null) runner.ResetRunner();

            OnAttemptAdvanced?.Invoke();
            return;
        }

        // El registro va ANTES de la espera, y desde código, por las dos razones de siempre:
        // una corrutina muere si el Director desactiva la estación durante retryDelay, y
        // cablearlo por UnityEvent significa que olvidarlo rompe los datos en silencio.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterFailedAttempt();

        StartCoroutine(NotifyFailureAfterDelay());
    }

    /// <summary>Adónde va ese tipo. Null si nadie lo ha declarado en goals.</summary>
    private ArmSlot DestinationFor(ArmItemType type)
    {
        foreach (SortingGoal goal in goals)
            if (goal != null && goal.type == type) return goal.destination;

        return null;
    }

    /// <summary>Objetos ya colocados en el destino que les toca, sumando todos los tipos.</summary>
    private int DeliveredTotal()
    {
        int total = 0;

        foreach (SortingGoal goal in goals)
            if (goal != null && goal.destination != null)
                total += goal.destination.CountOf(goal.type);

        return total;
    }

    private IEnumerator NotifyFailureAfterDelay()
    {
        yield return new WaitForSeconds(retryDelay);

        // Se revalida por si algo resolvió el escenario durante la espera.
        if (scenarioFinished) yield break;

        OnAttemptFailed?.Invoke();
    }

    /// <summary>
    /// Se mide contando lo que hay EN CADA DESTINO, no lo que falta en el origen: Take() saca
    /// el objeto de la pila en el propio Recoger, así que el montón de salida queda "vacío" con
    /// el objeto todavía en la pinza. Medir por el destino evita completar el nivel antes de que
    /// el Soltar final ocurra de verdad.
    /// </summary>
    private void CheckCompletion()
    {
        if (scenarioFinished || goals == null || goals.Length == 0) return;

        int total = 0;

        foreach (SortingGoal goal in goals)
        {
            if (goal == null || goal.destination == null) return;
            if (goal.destination.CountOf(goal.type) < goal.required) return;

            total += goal.required;
        }

        scenarioFinished = true;

        Debug.Log($"[Escenario 3] COMPLETADO · {total} objeto(s) clasificados en " +
                  $"{goals.Length} destino(s) · challengeId '{challengeId}'", this);

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

        // Después del reset, no antes: DeliveredTotal lee el estado ya devuelto a su sitio.
        lastDeliveredTotal = DeliveredTotal();
    }
}
