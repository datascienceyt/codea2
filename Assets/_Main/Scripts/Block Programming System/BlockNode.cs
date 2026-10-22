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

    // De quién colgaba al arrancar. Suelto, el bloque vuelve aquí y no a la raíz de la escena.
    Transform homeParent;
    VRGrabEvents grabEvents;
    Socket candidate;
    bool isGrabbed;

    // Frame de la última conexión contada. Los prefabs de bloque cablean además TryAtattch en
    // onReleased, así que una misma suelta puede pasar dos veces por AttachTo.
    int lastConnectionFrame = -1;

    public abstract IEnumerator Execute();

    /// <summary>
    /// Nombre en lenguaje natural de la instrucción, para el registro de telemetría.
    /// Deliberadamente NO se lee del Text del bloque: así el JSON no se rompe si mañana
    /// se cambia el texto visible o se traduce (RF-07 pide narración multilenguaje).
    /// </summary>
    public virtual string InstructionLabel => gameObject.name;

    protected virtual void Awake()
    {
        homeParent = transform.parent;

        // Se pudo bloquear antes de despertar (su sala estaba apagada): se aplica ahora.
        if (!IsInteractable) SetInteractable(false);

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

        LeaveSocket();
    }

    void Update()
    {
        if (isGrabbed)
            candidate = FindNearestFreeSocket();
    }

    void OnReleased()
    {
        isGrabbed = false;

        if (IsValidTarget(candidate)) AttachTo(candidate, true);
        candidate = null;
    }

    /// <summary>
    /// Encaja el bloque en el hueco libre más cercano, como si el jugador lo acabara de soltar
    /// ahí. Los prefabs de bloque lo llaman desde el onReleased de su VRGrabEvents, y sirve
    /// para probar sin visor desde el menú contextual.
    ///
    /// El nombre lleva la errata a propósito: está cableado así en los prefabs y en la escena.
    /// </summary>
    [ContextMenu("TryAttatch")]
    public void TryAtattch()
    {
        LeaveSocket();

        candidate = FindNearestFreeSocket();
        OnReleased();
    }

    /// <summary>
    /// Sale del socket que ocupaba, con su sonido, y vuelve a colgar de su padre de origen.
    ///
    /// No de la raíz: el Grabbable de Meta fija la escala LOCAL del bloque a la que tenía al
    /// arrancar. Los bloques del Escenario 3 cuelgan de una columna a escala 1,5; soltados en
    /// la raíz, el agarre les volvía a poner su escala local y se quedaban a 2/3 de tamaño,
    /// también al volver a encajarlos. Bajo su padre de origen, esa escala local es la buena.
    /// </summary>
    void LeaveSocket()
    {
        if (currentSocket != null)
        {
            currentSocket.Clear();
            currentSocket = null;
        }

        transform.SetParent(homeParent);
    }

    /// <summary>
    /// El hijo que lleva los interactables de Meta, para los bloques que no lo tienen asignado
    /// (los del Escenario 1). Sin él, SetInteractable no tenía efecto en ellos.
    ///
    /// IInteractable va cualificado a propósito: el grid del Escenario 1 tiene su propio
    /// IInteractable global, y sin el espacio de nombres se comparaba con ese y no encontraba nada.
    /// </summary>
    GameObject FindInteractionRoot()
    {
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is Oculus.Interaction.IInteractable && behaviour.transform != transform)
                return behaviour.gameObject;

        return null;
    }

    public bool IsAttached => currentSocket != null;
    public bool IsGrabbed => isGrabbed;

    /// <summary>
    /// Olvida el socket que ocupaba, sin tocarlo. Lo llama Socket.Release() al vaciarse: si
    /// no, al volver a agarrar el bloque vaciaría su socket anterior, expulsando al que lo
    /// hubiera ocupado mientras tanto.
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

    /// <summary>False si el juego lo bloqueó (ficha acertada, bloque fijo de la fila).</summary>
    public bool IsInteractable { get; private set; } = true;

    /// <summary>Permite o impide que el jugador agarre este bloque.</summary>
    public void SetInteractable(bool value)
    {
        IsInteractable = value;

        // Se busca aquí y no solo en Awake: a un bloque de una sala apagada se le puede bloquear
        // antes de su Awake, y entonces aún no sabía qué apagar.
        if (interactionRoot == null) interactionRoot = FindInteractionRoot();

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

        AttachTo(socket, false);
    }

    bool IsValidTarget(Socket socket)
    {
        if (socket == null || !socket.IsEmpty) return false;

        // Una fila bloqueada (dificultad básica) no admite cambios.
        if (!socket.AcceptsBlocks) return false;

        // Un bloque no puede acoplarse dentro de sí mismo.
        //
        // Hoy ningún bloque lleva sockets dentro, pero todos los sockets están en el mismo
        // registro global: uno que los llevara podría soltarse en su propio hueco.
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

    /// <summary>
    /// Lo llama BlockResetter al devolver el bloque a su sitio. Para que las piezas con
    /// comportamiento propio al estar sueltas (la física de ShapeChip) dejen de vigilarlo.
    /// </summary>
    public virtual void OnReturnedHome() { }

    /// <param name="byPlayer">
    /// Si lo encajó el jugador al soltarlo. Solo esas conexiones van a la telemetría: las que
    /// monta SocketRow por código al preparar la fila no son una acción del niño.
    /// </param>
    void AttachTo(Socket socket, bool byPlayer)
    {
        if (byPlayer && lastConnectionFrame != Time.frameCount && TelemetryManager.Instance != null)
        {
            lastConnectionFrame = Time.frameCount;
            TelemetryManager.Instance.RegisterBlockConnected();
        }

        // Encajado, el bloque no puede ser dinámico: un Rigidbody con física ignora al padre
        // del que cuelga, y la gravedad lo sacaba del hueco en el siguiente paso de física.
        // Pasó en el Escenario 4 al dar física a todas las fichas: parecían no encajar nunca.
        if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        transform.SetPositionAndRotation(socket.transform.position, socket.transform.rotation);
        transform.SetParent(socket.transform);
        socket.Occupy(this, byPlayer);
        currentSocket = socket;
    }
}