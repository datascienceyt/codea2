using System.Collections;
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
/// (lo añade Tools → Codea → 4): es el rechazo del escenario, sin copiarlo aquí.
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

    [Header("Eventos")]
    [Tooltip("Al completar cada interacción, una sola vez: una luz, un sonido, una frase.")]
    public UnityEvent OnProgramButtonPressed;
    public UnityEvent OnOptionButtonPressed;
    public UnityEvent OnBlockPlaced;
    public UnityEvent OnChipPlaced;

    [Tooltip("Cuando ya se han hecho las cuatro.")]
    public UnityEvent OnTutorialFinished;

    private bool programButtonDone;
    private bool optionButtonDone;
    private bool blockDone;
    private bool chipDone;

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
                       "da por hecha. Pásale Tools → Codea → 4.", this);
        return true;
    }

    [ContextMenu("Probar/1 - Botón del Escenario 1")]
    private void HandleProgramButton() => Complete(ref programButtonDone, OnProgramButtonPressed);

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
