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
public class Scenario3Controller : MonoBehaviour, IStepAction, ITimeLimitedChallenge, IRemainingCounter
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

    [Tooltip("Llama a StartScenario (entrada en la sala) en Start. Desactivado: lo llama el " +
             "Director al empezar la narración. El reto se abre en Execute.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Bloques")]
    [Tooltip("Fichas y bloques no se pueden agarrar hasta que termina la narración de la sala.")]
    [SerializeField] private bool lockPiecesDuringNarration = true;

    [Header("Indicaciones")]
    [Tooltip("Rótulos sobre los dos destinos: 'IZQUIERDA · barriles rojos' y 'DERECHA · cajas " +
             "azules' (Scenario3Hints).")]
    [SerializeField] private bool showHints = true;

    [Header("Acciones al finalizar")]
    public UnityEvent OnScenarioFinished;

    private Scenario3Hints hints;
    private bool scenarioFinished;
    private bool timedOut;

    public string ChallengeId => challengeId;
    public bool IsCompleted => scenarioFinished;

    // Mientras el brazo ejecuta la fila, el intento sigue vivo: puede ser el que clasifica lo
    // último que faltaba.
    public bool IsBusy => runner != null && runner.IsRunning;

    /// <summary>Se agotó el tiempo de la sala: el Director sigue sin esperar a la clasificación.</summary>
    public void TimeOut() => timedOut = true;

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
            Debug.LogError($"[Escenario3] 'runner' sin asignar en '{name}': tras un intento " +
                           "fallido no se reiniciará nada.", this);
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

        SetPiecesPaused(lockPiecesDuringNarration);

        EnsureRequired();

        lastDeliveredTotal = DeliveredTotal();

        if (showHints && arm != null)
        {
            var destinations = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<ArmItemType, ArmSlot>>();

            foreach (SortingGoal goal in goals)
                if (goal != null && goal.destination != null)
                    destinations.Add(new System.Collections.Generic.KeyValuePair<ArmItemType, ArmSlot>(goal.type, goal.destination));

            hints = gameObject.AddComponent<Scenario3Hints>();
            hints.Build(destinations, arm.transform);
        }
    }

    private bool requiredCounted;

    /// <summary>
    /// Cuántos hay de cada tipo en TODAS las posiciones del brazo, no solo en el montón de
    /// origen: así da igual que el decorado reparta los barriles entre varios sitios, y el dato
    /// sigue siendo del nivel y no del estado de la partida. Una vez, antes de mover nada: lo
    /// piden Start y StartScenario, y no se sabe cuál llega antes.
    /// </summary>
    private void EnsureRequired()
    {
        if (requiredCounted || goals == null) return;
        requiredCounted = true;

        foreach (SortingGoal goal in goals)
        {
            if (goal == null) continue;

            goal.required = arm != null ? arm.CountOf(goal.type) : 0;

            if (goal.destination == null)
                Debug.LogError($"[Escenario3] El objetivo '{goal.type}' no tiene destino " +
                               "asignado: nunca se dará por clasificado.", this);
        }
    }

    /// <summary>
    /// Entrada en la sala, al empezar su narración (cableado en el Director). El reto no se
    /// abre aquí sino en Execute, cuando las piezas se pueden agarrar.
    /// </summary>
    public void StartScenario()
    {
        if (TelemetryManager.Instance == null) return;

        EnsureRequired();

        TelemetryManager.Instance.EnterRoom(challengeId);

        var items = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
        foreach (SortingGoal goal in goals)
            if (goal != null) items.Add(new System.Collections.Generic.KeyValuePair<string, int>(goal.type.ToString(), goal.required));

        TelemetryManager.Instance.SetItemsRequired(challengeId, items);
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
        // Lo primero, y antes de cualquier return: un objeto olvidado en la pinza bloquea
        // todas las ejecuciones siguientes. Va en código y no cableado por la misma razón que
        // el registro de fallos: olvidarlo deja el escenario sin salida.
        if (arm != null) arm.ReturnHeldToOrigin();

        // moved y phaseCompleted de la ejecución que acaba, y fin de su recuento.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.FinishLoopRun(challengeId, Remaining);

        // El escenario se resuelve DENTRO de la ejecución, en el último Soltar, así que al
        // llegar aquí el estado ya es definitivo.
        if (scenarioFinished) return;

        // El criterio de fallo NO puede ser "el escenario no está completo".
        //
        // Hacen falta al menos dos ejecuciones, una por tipo, así que la pasada de barriles
        // perfecta terminaría con las cajas sin mover y se reiniciaría como fallida. Falla la
        // pasada que no clasificó NADA; la que avanzó y se quedó corta es simplemente menos
        // óptima, y eso ya se lee en el número de intentos y sus repeticiones.
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

        // El ciclo del siguiente intento empieza aquí (no se cuenta ningún fallo: el escenario 3
        // no tiene failedAttempts). Antes de la espera, porque la corrutina muere si el Director
        // desactiva la estación durante retryDelay.
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

    /// <summary>
    /// Objetos de ese tipo que aún no están en su destino. Lo pregunta ProgramTrigger al pulsar
    /// Ejecutar, para el `remaining` del intento.
    /// </summary>
    public int Remaining(string argument)
    {
        if (!System.Enum.TryParse(argument, out ArmItemType type)) return -1;

        foreach (SortingGoal goal in goals)
            if (goal != null && goal.type == type && goal.destination != null)
                return Mathf.Max(0, goal.required - goal.destination.CountOf(type));

        return -1;
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

        // Tras cada acción del brazo, antes de mirar si terminó: la ejecución que completa el
        // escenario no llega a HandleRunFinished si el Director apaga la estación.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.UpdateLoopProgress(challengeId, Remaining);

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

        if (hints != null) Destroy(hints);

        OnScenarioFinished?.Invoke();
    }

    /// <summary>
    /// Bloquea el Step del Director hasta despejar la posición. Como en Scenario2Controller,
    /// no se reinicia el flag: si ya estaba resuelto, el paso se da por cumplido en vez de
    /// colgarse esperando algo que ya ocurrió.
    /// </summary>
    public IEnumerator Execute()
    {
        // El Director llega aquí justo después de los pasos de narración de la sala. Las piezas
        // se sueltan y el reto se abre en el mismo instante: desde aquí acepta acciones.
        SetPiecesPaused(false);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.StartChallenge(challengeId);

        yield return new WaitUntil(() => scenarioFinished || timedOut);
    }

    /// <summary>Las fichas de tipo y los bloques del brazo, sin los del tutorial.</summary>
    private void SetPiecesPaused(bool value)
    {
        foreach (ArmTypeChip chip in FindObjectsByType<ArmTypeChip>(FindObjectsInactive.Include))
            if (chip.GetComponentInParent<TutorialController>(true) == null)
                chip.SetPaused(value);

        foreach (ArmBlock block in FindObjectsByType<ArmBlock>(FindObjectsInactive.Include))
            if (block.GetComponentInParent<TutorialController>(true) == null)
                block.SetPaused(value);
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
