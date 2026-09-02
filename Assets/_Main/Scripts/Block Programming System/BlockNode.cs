using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(VRGrabEvents))]
public abstract class BlockNode : MonoBehaviour
{
    public Transform plugPoint;
    public float snapDistance = 0.05f;

    [Tooltip("Opcional. Objeto que se apaga cuando el bloque deja de poder agarrarse: apunta " +
             "aquí al hijo que lleva el Grabbable. Lo usa SocketRow al bloquear la fila.")]
    [SerializeField] GameObject interactionRoot;

    Socket currentSocket;
    VRGrabEvents grabEvents;
    Socket candidate;
    bool isGrabbed;

    public abstract IEnumerator Execute();

    /// <summary>
    /// Nombre en lenguaje natural de la instrucción, para el registro de telemetría.
    /// Deliberadamente NO se lee del Text del bloque: así el JSON no se rompe si mañana
    /// se cambia el texto visible o se traduce (RF-07 pide narración multilenguaje).
    /// </summary>
    public virtual string InstructionLabel => gameObject.name;

    protected virtual void Awake()
    {
        grabEvents = GetComponent<VRGrabEvents>();
        grabEvents.onGrabbed.AddListener(OnGrabbed);
        grabEvents.onReleased.AddListener(OnReleased);
    }

    void OnGrabbed()
    {
        isGrabbed = true;

        // La telemetría se registra AQUÍ y no cableando UnityEvents en el prefab.
        //
        // Un prefab no puede guardar una referencia a un objeto de escena: Unity la anula al
        // guardar, y el evento queda apuntando a nada sin avisar. Desde código funciona para
        // todo bloque de todo escenario, sin nada que recordar cablear.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterBlockGrabbed();

        if (currentSocket != null)
        {
            currentSocket.Clear();
            currentSocket = null;
        }
        transform.SetParent(null);
    }

    void Update()
    {
        if (isGrabbed)
            candidate = FindNearestFreeSocket();
    }

    void OnReleased()
    {
        isGrabbed = false;

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterBlockReleased();

        if (IsValidTarget(candidate)) AttachTo(candidate);
        candidate = null;
    }

    [ContextMenu("TryAttatch")]
    public void TryAtattch()
    {
        isGrabbed = true;
        if (currentSocket != null)
        {
            currentSocket.Clear();
            currentSocket = null;
        }
        transform.SetParent(null);
        candidate = FindNearestFreeSocket();
        isGrabbed = false;
        if (IsValidTarget(candidate))
        {
            AttachTo(candidate);
        }
        candidate = null;
    }

    /// <summary>
    /// Olvida el socket que ocupaba, sin tocarlo. Lo usa BlockResetter tras devolver el
    /// bloque a su sitio: si no, al volver a agarrarlo vaciaría su socket anterior,
    /// expulsando al bloque que lo hubiera ocupado mientras tanto.
    /// </summary>
    public void ForgetSocket() => currentSocket = null;

    /// <summary>
    /// Se desacopla del socket que ocupaba, vaciándolo de verdad.
    ///
    /// No es lo mismo que ForgetSocket(): aquel solo limpia el lado del bloque, y dejaba al
    /// socket creyendo que seguía ocupado. Un socket así ya no admite nada más.
    /// </summary>
    public void DetachFromSocket()
    {
        // Release() se encarga de avisar al bloque, así que currentSocket queda a null.
        if (currentSocket != null) currentSocket.Release();

        currentSocket = null;
    }

    /// <summary>Permite o impide que el jugador agarre este bloque.</summary>
    public void SetInteractable(bool value)
    {
        if (interactionRoot != null)
            interactionRoot.SetActive(value);
    }

    /// <summary>
    /// Acopla el bloque a un socket por código, sin pasar por el agarre. Lo usa SocketRow
    /// para dejar la fila premontada en dificultad básica.
    /// </summary>
    public void AttachToSocket(Socket socket)
    {
        if (socket == null || !socket.IsEmpty) return;

        AttachTo(socket);
    }

    bool IsValidTarget(Socket socket)
    {
        if (socket == null || !socket.IsEmpty) return false;

        // Una fila bloqueada (dificultad básica) no admite cambios.
        if (!socket.AcceptsBlocks) return false;

        // Un bloque no puede acoplarse dentro de sí mismo.
        //
        // Con RepeatBlock esto es un riesgo real y no teórico: sus sockets internos están en
        // el registro global igual que los demás, así que el jugador podría soltar el bloque
        // Repetir dentro de su propio hueco. Al ejecutar sería recursión infinita y el visor
        // se congelaría.
        if (socket.transform.IsChildOf(transform)) return false;

        return true;
    }

    Socket FindNearestFreeSocket()
    {
        Socket best = null;
        float bestDist = snapDistance;

        // Se recorre el registro estático de Socket en vez de FindObjectsByType: esto corre
        // cada frame por cada bloque agarrado, y la búsqueda global es cara en Quest (RNF-01).
        IReadOnlyList<Socket> sockets = Socket.Active;

        for (int i = 0; i < sockets.Count; i++)
        {
            Socket socket = sockets[i];
            if (socket == null || !socket.IsEmpty) continue;

            float dist = Vector3.Distance(plugPoint.position, socket.transform.position);
            if (dist < bestDist) { best = socket; bestDist = dist; }
        }

        return best;
    }

    void AttachTo(Socket socket)
    {
        transform.SetPositionAndRotation(socket.transform.position, socket.transform.rotation);
        transform.SetParent(socket.transform);
        socket.Occupy(this);
        currentSocket = socket;
    }
}