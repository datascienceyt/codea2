using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El socket del argumento: qué tipo de objeto va a mover el brazo en esta ejecución.
///
/// Va **fuera** del SocketRow a propósito, y no solo por estética. SocketRow.SetEditable()
/// bloquea la fila entera de golpe, así que un socket dentro de ella quedaría bloqueado junto
/// a las instrucciones; separándolo, el tipo se puede seguir cambiando entre ejecuciones
/// aunque la secuencia esté fija, que es justo lo que pide la dificultad básica.
///
/// Al poner una ficha, las repeticiones de la fila vuelven a 0: cambiar de tipo es preparar
/// una pasada nueva, y arrastrar el número de la anterior dejaba ejecutar sin decidirlo.
///
/// Implementa IRunPrecondition: sin ficha o con 0 repeticiones el programa no se ejecuta.
/// </summary>
[RequireComponent(typeof(Socket))]
public class ArmTypeSocket : MonoBehaviour, IRunPrecondition
{
    [Tooltip("La fila de instrucciones cuyas repeticiones se ponen a 0 al colocar una ficha. " +
             "Si queda vacío se toma la del ProgramTrigger que tenga este socket en sus " +
             "preconditions. Su Min Repetitions debe ser 0, o el valor se quedará en el mínimo.")]
    [SerializeField] private SocketRow repetitionsRow;

    [Tooltip("Se dispara al intentar ejecutar sin ficha de tipo. Para el aviso visual o sonoro.")]
    public UnityEvent OnMissingType;

    [Tooltip("Se dispara al intentar ejecutar con 0 repeticiones.")]
    public UnityEvent OnMissingRepetitions;

    [Tooltip("Al colocar una ficha de tipo: sonido, luz...")]
    public UnityEvent OnTypePlaced;

    private Socket socket;

    public bool HasType => Chip != null;

    /// <summary>Tipo seleccionado. Solo tiene sentido si HasType.</summary>
    public ArmItemType SelectedType => Chip != null ? Chip.Type : default;

    /// <summary>
    /// Con qué tipo se ejecutó el programa. Va a la telemetría porque sin él dos intentos con
    /// la misma secuencia y las mismas repeticiones son indistinguibles, y son retos distintos.
    /// </summary>
    public string RunArgument => HasType ? SelectedType.ToString() : null;

    private ArmTypeChip Chip => socket != null ? socket.CurrentBlock as ArmTypeChip : null;

    private void Awake()
    {
        socket = GetComponent<Socket>();
        socket.OnOccupied += HandleOccupied;
    }

    private void Start()
    {
        // El socket vive en un prefab y el botón en la escena: un prefab no puede guardar esa
        // referencia (trampa 1). Así que el socket busca su botón y se registra en él, en vez
        // de depender de que alguien lo arrastre a la lista del inspector. Esa lista ya acabó
        // apuntando a las fichas, y el escenario perdió el veto, itemType y el reinicio a 0.
        ProgramTrigger trigger = FindTrigger();

        if (trigger == null)
        {
            Debug.LogError($"[Escenario3] '{name}' no encuentra el ProgramTrigger del escenario 3: " +
                           "el programa correrá sin ficha e itemType no se registrará.", this);
            return;
        }

        trigger.AddPrecondition(this);

        if (repetitionsRow == null) repetitionsRow = trigger.socketRow;
    }

    /// <summary>El botón que ya lo lista, o si no, el del reto del escenario 3.</summary>
    private ProgramTrigger FindTrigger()
    {
        ProgramTrigger[] triggers = FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ProgramTrigger trigger in triggers)
            if (trigger.HasPrecondition(this)) return trigger;

        foreach (ProgramTrigger trigger in triggers)
            if (trigger.ChallengeId == TelemetryManager.Scenario3Id) return trigger;

        return null;
    }

    private void OnDestroy()
    {
        if (socket != null) socket.OnOccupied -= HandleOccupied;
    }

    private void HandleOccupied(BlockNode block)
    {
        if (!(block is ArmTypeChip)) return;

        if (repetitionsRow != null) repetitionsRow.SetRepetitions(0);

        OnTypePlaced?.Invoke();
    }

    /// <summary>
    /// Un programa sin su argumento, o que no se repite ninguna vez, está incompleto: no se
    /// ejecuta y tampoco cuenta como intento. Sí se registra como error de lógica, que es lo
    /// que realmente ocurrió.
    /// </summary>
    public bool CanRun()
    {
        if (!HasType)
        {
            Debug.LogWarning($"[Escenario3] '{name}' no tiene ficha de tipo: el programa no se " +
                             "ejecuta porque no sabe qué debe mover.", this);

            RegisterInvalid();
            OnMissingType?.Invoke();
            return false;
        }

        if (repetitionsRow != null && repetitionsRow.Repetitions <= 0)
        {
            Debug.LogWarning("[Escenario3] 0 repeticiones: el programa no se ejecuta.", this);

            RegisterInvalid();
            OnMissingRepetitions?.Invoke();
            return false;
        }

        return true;
    }

    private static void RegisterInvalid()
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);
    }
}
