using System.Collections;
using System.Linq;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El cuarto del tutorial: cuatro gestos que el niño va a necesitar después, cada uno con la
/// misma pieza que se encontrará en su escenario.
///
///   1. Pulsar un botón como el de ejecutar del Escenario 1
///   2. Pulsar un botón como los de los módulos del Escenario 2
///   3. Encajar un bloque en un hueco, como en el Escenario 1
///   4. Encajar la ficha correcta en su hueco, como en el Escenario 4
///
/// El tutorial termina cuando se han hecho los cuatro, en cualquier orden. No registra
/// telemetría: aquí no hay nada que medir.
///
/// Al terminar arranca el Director de la escena: la experiencia empieza cuando el niño ha
/// demostrado que sabe hacer los cuatro gestos.
///
/// El cuarto trae una sola ficha, la que encaja. Si se añade una que no encaje, quien la
/// expulsa es un Scenario4Controller con el challengeId vacío puesto en este mismo objeto
/// (ya puesto en la escena): es el rechazo del escenario, sin copiarlo aquí.
/// </summary>
public class TutorialController : MonoBehaviour, IStepAction
{
    [Header("Las cuatro interacciones")]
    [Tooltip("Botón como el de ejecutar del Escenario 1. Cuenta al pulsarlo.")]
    [SerializeField] private InteractableUnityEventWrapper programButton;

    [Tooltip("Botón como los del Escenario 2, en modo 'standalone'. Cuenta al quedar marcado.")]
    [SerializeField] private ModuleOptionButton optionButton;

    [Tooltip("Hueco de bloque como los de la fila del Escenario 1. Cuenta al encajar un bloque.")]
    [SerializeField] private Socket blockSocket;

    [Tooltip("Hueco de figura como los del Escenario 4. Cuenta al encajar la ficha correcta.")]
    [SerializeField] private ShapeSocket shapeSocket;

    [Header("Al terminar")]
    [Tooltip("Arranca el Director de la escena al completar las cuatro. No hace falta cablear " +
             "nada: lo busca solo. NO cablees además Director.Play en OnTutorialFinished, o la " +
             "experiencia arrancaría dos veces.")]
    [SerializeField] private bool startDirectorOnFinish = true;

    [Tooltip("Color que toma el botón del Escenario 1 al pulsarlo. Es el único de los cuatro gestos " +
             "que no deja rastro por sí solo (el otro botón se queda marcado, el bloque y la ficha " +
             "se ven encajados), y sin esto el niño no sabe si contó.")]
    [SerializeField] private Color doneColor = new Color(0.3f, 1f, 0.4f);

    [Header("Eventos")]
    [Tooltip("Al completar cada interacción, una sola vez: una luz, un sonido, una frase.")]
    public UnityEvent OnProgramButtonPressed;
    public UnityEvent OnOptionButtonPressed;
    public UnityEvent OnBlockPlaced;
    public UnityEvent OnChipPlaced;

    [Tooltip("Cuando ya se han hecho las cuatro.")]
    public UnityEvent OnTutorialFinished;

    [Header("Indicaciones")]
    [Tooltip("Los huecos parpadean en ámbar suave (HintStyle), como en las salas: el de bloque " +
             "hasta que se encaja uno (Sala 1) y el de figura mientras la ficha está en la mano (Sala 4).")]
    [SerializeField] private bool showHints = true;

    private bool programButtonDone;
    private bool optionButtonDone;
    private bool blockDone;
    private bool chipDone;

    private Socket chipSocket;
    private BlockNode[] blocks;
    private ShapeChip[] chips;
    private bool blockLit;
    private bool chipLit;

    public bool IsFinished { get; private set; }

    private void Awake()
    {
        // Una interacción sin su pieza no se podría completar nunca y el Director se quedaría
        // esperando para siempre. Se avisa y se da por hecha, que es el fallo menos caro.
        programButtonDone = Missing(programButton, nameof(programButton));
        optionButtonDone = Missing(optionButton, nameof(optionButton));
        blockDone = Missing(blockSocket, nameof(blockSocket));
        chipDone = Missing(shapeSocket, nameof(shapeSocket));

        // Desde código y no por UnityEvent: si alguno de estos cables se olvidara, el tutorial
        // no terminaría y nada avisaría de por qué.
        if (programButton != null) programButton.WhenSelect.AddListener(HandleProgramButton);
        if (optionButton != null) optionButton.OnSelected.AddListener(HandleOptionButton);
        if (blockSocket != null) blockSocket.OnOccupied += HandleBlock;
        if (shapeSocket != null) shapeSocket.OnChipEvaluated += HandleChip;
    }

    private void Start()
    {
        if (shapeSocket != null) chipSocket = shapeSocket.GetComponent<Socket>();

        // Las piezas del cuarto, no las de las salas.
        BlockNode[] pieces = GetComponentsInChildren<BlockNode>(true);
        chips = pieces.OfType<ShapeChip>().ToArray();
        blocks = pieces.Where(p => !(p is ShapeChip)).ToArray();
    }

    /// <summary>
    /// Las indicaciones de los huecos. Hasta el 09/11/2026 el tutorial no tenía ninguna: las de
    /// la Sala 1 y la Sala 4 dejan fuera a propósito las piezas del tutorial.
    /// </summary>
    private void Update()
    {
        if (!showHints) return;

        float pulse = HintStyle.Pulse;

        // Bloque: como el primer hueco libre de la Sala 1, se ilumina aunque no haya nada en la mano.
        blockLit = Hint(blockSocket, !blockDone, pulse, blockLit);

        // Ficha: como en la Sala 4, solo con la ficha en la mano.
        bool holdingChip = chips.Any(c => c != null && c.IsGrabbed);
        chipLit = Hint(chipSocket, !chipDone && holdingChip, pulse, chipLit);
    }

    private static bool Hint(Socket socket, bool show, float pulse, bool wasLit)
    {
        if (socket == null) return false;

        // SetHint no toca un hueco ocupado: se queda el verde de encajado.
        if (show) socket.SetHint(pulse, HintStyle.Color);
        else if (wasLit) socket.SetHint(0f, HintStyle.Color);

        return show;
    }

    private void OnDisable()
    {
        if (blockLit && blockSocket != null) blockSocket.SetHint(0f, HintStyle.Color);
        if (chipLit && chipSocket != null) chipSocket.SetHint(0f, HintStyle.Color);
        blockLit = chipLit = false;
    }

    private void OnDestroy()
    {
        if (programButton != null) programButton.WhenSelect.RemoveListener(HandleProgramButton);
        if (optionButton != null) optionButton.OnSelected.RemoveListener(HandleOptionButton);
        if (blockSocket != null) blockSocket.OnOccupied -= HandleBlock;
        if (shapeSocket != null) shapeSocket.OnChipEvaluated -= HandleChip;
    }

    private bool Missing(Object piece, string field)
    {
        if (piece != null) return false;

        Debug.LogError($"[Tutorial] '{name}' no tiene asignado '{field}': esa interacción se " +
                       "da por hecha. Asígnalo en el inspector.", this);
        return true;
    }

    [ContextMenu("Probar/1 - Botón del Escenario 1")]
    private void HandleProgramButton()
    {
        if (!programButtonDone) MarkDone(programButton);

        Complete(ref programButtonDone, OnProgramButtonPressed);
    }

    /// <summary>Tiñe la pieza para que se vea que ya está hecha.</summary>
    private void MarkDone(Component piece)
    {
        Renderer target = piece != null ? piece.GetComponentInChildren<Renderer>() : null;
        if (target == null) return;

        // Según el shader, el color base se llama de una forma u otra.
        Material material = target.material;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", doneColor);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", doneColor);
    }

    [ContextMenu("Probar/2 - Botón del Escenario 2")]
    private void HandleOptionButton() => Complete(ref optionButtonDone, OnOptionButtonPressed);

    private void HandleBlock(BlockNode block) => Complete(ref blockDone, OnBlockPlaced);

    private void HandleChip(ShapeSocket socket, ShapeChip chip, bool correct)
    {
        // La incorrecta no cuenta: la expulsa el Scenario4Controller y el niño prueba la otra.
        if (correct) Complete(ref chipDone, OnChipPlaced);
    }

    private void Complete(ref bool done, UnityEvent onDone)
    {
        if (done) return;

        done = true;
        onDone?.Invoke();

        if (IsFinished || !programButtonDone || !optionButtonDone || !blockDone || !chipDone) return;

        IsFinished = true;

        Debug.Log("[Tutorial] COMPLETADO · las cuatro interacciones hechas", this);

        OnTutorialFinished?.Invoke();

        if (!startDirectorOnFinish) return;

        // Se busca en la escena en vez de cablearlo: el cuarto es un prefab compartido por
        // las escenas de juego, y un prefab no puede guardar una referencia a su Director.
        Director director = FindAnyObjectByType<Director>();

        // Si el Director ya corre es que el tutorial es uno de sus pasos (lo arrancó el botón
        // de inicio y está esperando a este componente): no hay nada que arrancar.
        if (director != null)
        {
            if (!director.IsPlaying) director.Play();
        }
        else Debug.LogError("[Tutorial] No hay Director en la escena: la experiencia no arranca.", this);
    }

    /// <summary>Bloquea el paso del Director hasta que se completen las cuatro.</summary>
    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => IsFinished);
    }
}
