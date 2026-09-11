using UnityEngine;

/// <summary>
/// Mantiene un panel delante del jugador, pero sin pegarse a su cabeza.
///
/// Un panel emparentado a la cámara se mueve con cada micromovimiento del cuello: el texto
/// nunca se está quieto, cuesta leerlo y marea. Uno fijo en el mundo tiene el problema
/// contrario: en cuanto el niño se gira, lo pierde de vista.
///
/// La solución intermedia es esperar. El panel se queda donde está mientras siga estando
/// razonablemente delante, y solo cuando el jugador gira o se aleja lo bastante se desliza a
/// su nueva posición. Así el texto está quieto mientras se lee, y aparece donde toca cuando
/// el jugador ha cambiado de sitio.
/// </summary>
public class ScreenFollower : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("Cabeza del jugador. Si queda vacío se busca el centerEyeAnchor del OVRCameraRig.")]
    [SerializeField] private Transform head;

    [Header("Colocación")]
    [Tooltip("A qué distancia del jugador se coloca, en metros.")]
    [SerializeField] private float distance = 1.6f;

    [Tooltip("Cuánto por debajo de la línea de los ojos, en metros. Subirlo mucho obliga a " +
             "agachar la cabeza para leer, que cansa en sesiones largas.")]
    [SerializeField] private float drop = 0.3f;

    [Header("Cuándo se recoloca")]
    [Tooltip("Grados que el jugador puede girar la cabeza antes de que el panel lo siga. " +
             "Bajarlo mucho lo convierte en un panel pegado a la cara.")]
    [SerializeField] private float yawThreshold = 35f;

    [Tooltip("Metros que el jugador puede acercarse o alejarse antes de que el panel lo siga.")]
    [SerializeField] private float distanceThreshold = 0.6f;

    [Tooltip("Diferencia de altura que dispara la recolocación. Cubre el caso de teletransportarse.")]
    [SerializeField] private float heightThreshold = 0.5f;

    [Header("Movimiento")]
    [Tooltip("Cómo de rápido se desliza a su nuevo sitio. Más alto, más lento.")]
    [SerializeField] private float moveTime = 0.35f;

    [Tooltip("Distancia a la que se da por llegado y se queda quieto otra vez.")]
    [SerializeField] private float arriveDistance = 0.03f;

    private Vector3 velocity;
    private bool moving;

    private void Awake()
    {
        if (head != null) return;

        OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>(FindObjectsInactive.Include);

        if (rig != null) head = rig.centerEyeAnchor;
        else if (Camera.main != null) head = Camera.main.transform;

        if (head == null)
            Debug.LogError($"[Pantalla] '{name}' no encuentra la cabeza del jugador.", this);
    }

    private void Start() => PlaceNow();

    // LateUpdate y no Update: la cabeza ya ha terminado de moverse este frame, así que el
    // panel no va un fotograma por detrás.
    private void LateUpdate()
    {
        if (head == null) return;

        if (!moving && NeedsReposition()) moving = true;
        if (!moving) return;

        Vector3 target = TargetPosition();

        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, moveTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, TargetRotation(),
                                              1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, moveTime)));

        if (Vector3.Distance(transform.position, target) <= arriveDistance)
            moving = false;
    }

    /// <summary>
    /// Manda al panel a su sitio deslizándose, sin esperar a que se cumpla ningún umbral.
    /// Cablear al OnLineStarted del Narrator: cuando empieza una frase nueva interesa que el
    /// niño la tenga delante, aunque estuviera mirando a otro lado.
    /// </summary>
    [ContextMenu("Recolocar")]
    public void Reposition() => moving = true;

    /// <summary>
    /// Coloca el panel de golpe, sin deslizamiento.
    ///
    /// Es lo que hay que llamar tras un teletransporte: deslizarse hasta la sala nueva
    /// significaría ver el panel cruzando la nave entera por el aire.
    /// </summary>
    [ContextMenu("Colocar ya")]
    public void PlaceNow()
    {
        if (head == null) return;

        transform.SetPositionAndRotation(TargetPosition(), TargetRotation());

        velocity = Vector3.zero;
        moving = false;
    }

    private Vector3 TargetPosition()
    {
        Vector3 forward = FlatForward();

        return head.position + forward * distance + Vector3.down * drop;
    }

    /// <summary>
    /// El panel solo gira sobre su eje vertical. Si copiara la inclinación de la cabeza, el
    /// texto aparecería torcido cada vez que el niño mira hacia abajo.
    /// </summary>
    private Quaternion TargetRotation() => Quaternion.LookRotation(FlatForward(), Vector3.up);

    /// <summary>Hacia dónde mira el jugador, ignorando si mira arriba o abajo.</summary>
    private Vector3 FlatForward()
    {
        Vector3 forward = head.forward;
        forward.y = 0f;

        // Mirando en vertical, forward se queda casi a cero y la dirección deja de ser fiable.
        return forward.sqrMagnitude < 0.0001f ? head.up : forward.normalized;
    }

    private bool NeedsReposition()
    {
        Vector3 toPanel = transform.position - head.position;

        if (Mathf.Abs(toPanel.y + drop) > heightThreshold) return true;

        toPanel.y = 0f;

        if (toPanel.sqrMagnitude < 0.0001f) return true;

        if (Vector3.Angle(FlatForward(), toPanel) > yawThreshold) return true;

        return Mathf.Abs(toPanel.magnitude - distance) > distanceThreshold;
    }
}
