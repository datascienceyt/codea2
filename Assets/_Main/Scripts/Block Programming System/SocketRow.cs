using System;
using TMPro;
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

    [Tooltip("Igual, pero en TextMeshPro. Rellena solo el que uses.")]
    [SerializeField] private TMP_Text repetitionsLabelTmp;

    [Tooltip("Al cambiar el contador: sonido, parpadeo...")]
    public UnityEvent OnRepetitionsChanged;

    [Header("Edición")]
    [Tooltip("Desactivado: la fila entera queda bloqueada. Ni se pueden sacar los bloques que " +
             "trae ni meter otros.")]
    [SerializeField] private bool editable = true;

    [Tooltip("Con qué arranca la fila, un elemento por socket y en orden.\n\n" +
             "Un elemento sin bloque deja ese socket VACÍO para que el niño lo rellene, y " +
             "marcarlo como fijo lo deja intocable aunque la fila sea editable. Con eso se " +
             "monta la dificultad básica: Recoger y Soltar puestos y fijos, y los dos huecos " +
             "de giro libres para que el niño decida hacia dónde.")]
    [SerializeField] private List<InitialBlock> initialBlocks = new List<InitialBlock>();

    /// <summary>
    /// Con qué arranca un socket de la fila. Un elemento por socket, en orden.
    ///
    /// Va emparejado y no como dos listas sueltas porque los sockets se generan en Awake y no
    /// existen en el editor: no hay dónde marcar "este no se toca" salvo aquí, junto al bloque
    /// que le corresponde.
    /// </summary>
    [Serializable]
    public class InitialBlock
    {
        [Tooltip("Vacío deja el socket libre para que lo rellene el niño.")]
        public BlockNode block;

        [Tooltip("Bloqueado aunque la fila sea editable: ni se saca ni se sustituye.")]
        public bool fixedInPlace;
    }

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

        UiText.Set(repetitionsLabel, repetitionsLabelTmp, repetitions.ToString());
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
        for (int i = 0; i < sockets.Count; i++)
        {
            Socket socket = sockets[i];
            if (socket == null) continue;

            // Un socket fijo se queda bloqueado aunque la fila sea editable. Es lo que separa
            // las dos dificultades: en básica solo se abren los huecos de giro, en intermedia
            // no hay ninguno fijo y se reordena todo.
            bool open = editable && !IsFixed(i);

            socket.SetAcceptsBlocks(open);

            if (socket.CurrentBlock != null)
                socket.CurrentBlock.SetInteractable(open);
        }
    }

    private bool IsFixed(int index) =>
        index >= 0 && index < initialBlocks.Count &&
        initialBlocks[index] != null && initialBlocks[index].fixedInPlace;

    /// <summary>
    /// Coloca los bloques iniciales, un elemento de la lista por socket y en orden.
    /// Vuelve a llamarse desde el menú contextual para recolocarlos en pruebas.
    ///
    /// Un elemento sin bloque NO se salta: consume su socket y lo deja vacío. Así se pueden
    /// dejar huecos en medio de una secuencia ya montada, que es lo que pide la básica —
    /// Recoger y Soltar puestos, los giros por decidir.
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

        if (initialBlocks.Count > sockets.Count)
            Debug.LogWarning($"[SocketRow] '{name}' tiene más elementos iniciales " +
                             $"({initialBlocks.Count}) que sockets ({sockets.Count}).", this);

        for (int i = 0; i < initialBlocks.Count && i < sockets.Count; i++)
        {
            BlockNode block = initialBlocks[i] != null ? initialBlocks[i].block : null;
            if (block == null) continue;

            // Si venía de otro socket hay que soltarlo antes, o aquel se quedaría ocupado.
            block.DetachFromSocket();
            block.AttachToSocket(sockets[i]);
        }

        // Después de colocar, no antes: un bloque recién acoplado tiene que recibir su estado
        // de bloqueo, y al revés se quedaba agarrable en un socket marcado como fijo.
        ApplyEditable();
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
