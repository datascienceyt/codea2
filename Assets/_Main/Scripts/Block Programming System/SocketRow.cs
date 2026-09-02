using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Fila de sockets donde el jugador monta su secuencia.
///
/// En el Escenario 3 la fila entera es el bucle: se ejecuta Repetitions veces. Con el valor
/// por defecto en 1, el resto de escenarios se comportan como siempre.
/// </summary>
public class SocketRow : MonoBehaviour
{
    [Header("Points")]
    [SerializeField] private Transform start;
    [SerializeField] private Transform end;

    [Header("Sockets")]
    [SerializeField] private GameObject socketPrefab;
    [SerializeField] private int socketsQuantity;

    [Header("Orientación")]
    [Tooltip("Referencia de rotación para los sockets. La base ya se coloca a mano en la escena: " +
             "este Transform solo se lee, nunca se mueve ni se escala. Si queda vacío se usa la " +
             "rotación del propio SocketRow.")]
    [FormerlySerializedAs("baseObject")]
    [SerializeField] private Transform socketOrientation;

    // El socket.prefab es un marco en el plano XY (todas sus piezas son finas en Z),
    // así que su cara mira hacia su +Z local.
    private Quaternion SocketRotation =>
        socketOrientation != null ? socketOrientation.rotation : transform.rotation;

    [Header("Repetición")]
    [Tooltip("Cuántas veces se ejecuta la fila entera. 1 = comportamiento normal.")]
    [SerializeField] private int repetitions = 1;

    [SerializeField] private int minRepetitions = 1;
    [SerializeField] private int maxRepetitions = 9;

    [Tooltip("Opcional. Muestra el número actual de repeticiones.")]
    [SerializeField] private Text repetitionsLabel;

    [Tooltip("Al cambiar el contador: sonido, parpadeo...")]
    public UnityEvent OnRepetitionsChanged;

    [Header("Edición")]
    [Tooltip("Desactivado: la fila queda bloqueada. Ni se pueden sacar los bloques que trae " +
             "ni meter otros. Es la dificultad básica, donde el niño solo ajusta las repeticiones.")]
    [SerializeField] private bool editable = true;

    [Tooltip("Bloques con los que arranca la fila, en orden. Necesario si no es editable: " +
             "una fila bloqueada y vacía no se podría resolver.")]
    [SerializeField] private List<BlockNode> initialBlocks = new List<BlockNode>();

    public Socket FirstSocket { get; private set; }

    public int Repetitions => repetitions;
    public bool IsEditable => editable;

    private List<Socket> sockets = new List<Socket>();

    private void Awake()
    {
        CreateSockets();
    }

    private void Start()
    {
        PlaceInitialBlocks();
        ApplyEditable();
        RefreshLabel();
    }

    // --- Repeticiones. Cablear a los PokeInteractable de + y - ---

    [ContextMenu("Repeticiones +1")]
    public void IncreaseRepetitions() => SetRepetitions(repetitions + 1);

    [ContextMenu("Repeticiones -1")]
    public void DecreaseRepetitions() => SetRepetitions(repetitions - 1);

    public void SetRepetitions(int value)
    {
        int clamped = Mathf.Clamp(value, minRepetitions, maxRepetitions);
        if (clamped == repetitions) return;

        repetitions = clamped;

        RefreshLabel();
        OnRepetitionsChanged?.Invoke();
    }

    private void RefreshLabel()
    {
        repetitions = Mathf.Clamp(repetitions, minRepetitions, maxRepetitions);

        if (repetitionsLabel != null)
            repetitionsLabel.text = repetitions.ToString();
    }

    // --- Bloqueo de edición ---

    public void SetEditable(bool value)
    {
        editable = value;
        ApplyEditable();
    }

    [ContextMenu("Bloquear fila")]
    public void Lock() => SetEditable(false);

    [ContextMenu("Desbloquear fila")]
    public void Unlock() => SetEditable(true);

    /// <summary>
    /// Bloquear necesita las dos mitades: que los sockets dejen de admitir bloques nuevos y
    /// que los que ya están dentro dejen de poder agarrarse. Solo una de las dos deja la
    /// fila a medio bloquear.
    /// </summary>
    private void ApplyEditable()
    {
        foreach (Socket socket in sockets)
        {
            if (socket == null) continue;

            socket.SetAcceptsBlocks(editable);

            if (socket.CurrentBlock != null)
                socket.CurrentBlock.SetInteractable(editable);
        }
    }

    /// <summary>
    /// Coloca los bloques iniciales en los primeros sockets, en el orden de la lista.
    /// Vuelve a llamarse desde el menú contextual para recolocarlos en pruebas.
    /// </summary>
    [ContextMenu("Rellenar fila")]
    public void PlaceInitialBlocks()
    {
        if (sockets.Count == 0)
        {
            Debug.LogWarning($"[SocketRow] '{name}' aún no ha creado sus sockets. " +
                             "Los crea en Awake, así que esto solo funciona en Play Mode.", this);
            return;
        }

        int slot = 0;

        foreach (BlockNode block in initialBlocks)
        {
            if (block == null) continue;

            if (slot >= sockets.Count)
            {
                Debug.LogWarning($"[SocketRow] '{name}' tiene más bloques iniciales " +
                                 $"({initialBlocks.Count}) que sockets ({sockets.Count}).", this);
                break;
            }

            // Si venía de otro socket hay que soltarlo antes, o aquel se quedaría ocupado.
            block.DetachFromSocket();
            block.AttachToSocket(sockets[slot]);

            slot++;
        }
    }

    private void CreateSockets()
    {
        if (start == null || end == null || socketPrefab == null)
        {
            Debug.LogError($"[SocketRow] '{name}' necesita start, end y socketPrefab asignados.", this);
            return;
        }

        for (int i = 1; i <= socketsQuantity; i++)
        {
            // Distribuye los sockets entre Start y End
            float threshold = (float)i / (socketsQuantity + 1);

            Vector3 socketPosition = Vector3.Lerp(
                start.position,
                end.position,
                threshold
            );

            GameObject aux = Instantiate(
                socketPrefab,
                socketPosition,
                SocketRotation,
                transform
            );

            Socket socket = aux.GetComponent<Socket>();

            // Sin esto, un prefab sin el componente metía nulls en la lista y el encadenado
            // de abajo reventaba con un NRE difícil de rastrear hasta el prefab.
            if (socket == null)
            {
                Debug.LogError($"[SocketRow] El prefab '{socketPrefab.name}' no tiene componente Socket.", this);
                Destroy(aux);
                continue;
            }

            sockets.Add(socket);
        }

        for (int i = 0; i < sockets.Count - 1; i++)
        {
            sockets[i].Next = sockets[i + 1];
        }

        FirstSocket = sockets.Count > 0 ? sockets[0] : null;

        // socketOrientation es opcional (SocketRotation cae a transform.rotation si falta),
        // así que aquí no puede darse por hecho que exista.
        if (socketOrientation != null)
            socketOrientation.gameObject.SetActive(false);
    }

    /// <summary>
    /// Vacía todos los sockets. Ya no destruye ni desparenta nada: de devolver los
    /// bloques a su sitio se encarga BlockResetter.
    /// </summary>
    /// <param name="destroyBlocks">
    /// Obsoleto, se ignora. Se conserva solo para no romper el enlace del UnityEvent
    /// del botón de reinicio, que sigue pasando un bool.
    /// </param>
    public void ClearRow(bool destroyBlocks = true)
    {
        foreach (var socket in sockets)
            if (socket != null) socket.Release();
    }
}
