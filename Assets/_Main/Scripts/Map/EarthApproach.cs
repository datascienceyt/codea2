using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// El regreso a casa, al final del Escenario 4. Es una acción del Director (paso "Completar reto
/// y cambiar escenario", en paralelo con la frase 19), así que la vuelta a Setup llega justo al
/// terminar el acercamiento (09/11/2026):
///
///   1. Deja arrancar los motores: `engineLeadSeconds` desde que se completó el panel
///   2. Acerca la Tierra durante `durationSeconds`, con arranque y llegada suaves, mientras los
///      motores rugen. El clip (engineStart_corto.wav, 39 s) se apaga solo, sin fundido, justo
///      cuando llega la Tierra: 5 + 32 = 37 s
///   3. Termina, el Director sube los datos y vuelve a Setup; la Tierra se queda donde llegó
///
/// Desde el 16/11/2026 la nave vuelve a ECUADOR. La Tierra ya no gira sola (EarthRotation está
/// apagado en la escena): al encenderse esta vista queda quieta, con el norte arriba y Ecuador
/// un poco al oeste del punto que mira a la nave. Durante el regreso:
///
///   · la Tierra se coloca en el frente de la nave, en el centro de la ventana de la Sala 4
///     (`shipForward`, `arrivalElevation`), como si la nave virase hacia ella
///   · gira sobre su eje lo que le faltaba (`startTurnDegrees`) hasta dejar Ecuador justo
///     delante, y la nave baja hacia ese punto
///   · una baliza marca Ecuador, con su nombre, desde que arranca el regreso
///
/// Se mueve la Tierra hacia el jugador en vez de agrandarla. A la vista es lo mismo, pero no
/// se toca la escala del modelo ni la de su atmósfera, y la Luna y el Sol conservan su sitio.
/// Si se agota el tiempo y la misión no se completa, el Director no llega a este paso.
/// </summary>
public class EarthApproach : MonoBehaviour, IStepAction
{
    [Tooltip("Lo que se acerca: la Tierra de esta vista exterior.")]
    [SerializeField] private Transform body;

    [Tooltip("Segundos que dura el acercamiento. Al acabar, el Director vuelve a Setup.")]
    [SerializeField] private float durationSeconds = 32f;

    [Tooltip("Reto cuyo final (el arranque de los motores) marca el inicio del regreso.")]
    [SerializeField] private string challengeId = TelemetryManager.Scenario4Id;

    [Tooltip("Segundos desde que se completa el panel hasta que la Tierra empieza a acercarse: " +
             "lo que tardan los motores en arrancar. Con 5 + durationSeconds la Tierra llega " +
             "cuando el sonido de los motores se está apagando.")]
    [SerializeField] private float engineLeadSeconds = 5f;

    [Header("Destino")]
    [Tooltip("Longitud del punto al que vuelve la nave, en grados (oeste, negativa). Ecuador: -78,2.")]
    [SerializeField] private float landingLongitude = -78.2f;

    [Tooltip("Latitud del punto al que vuelve la nave, en grados (sur, negativa). Ecuador: -1,5.")]
    [SerializeField] private float landingLatitude = -1.5f;

    [Tooltip("Distancia al centro de la Tierra al llegar, en radios terrestres. Con 1,28 la Tierra " +
             "llena la ventana y se reconoce el noroeste de Sudamérica; más cerca, la textura se " +
             "ve borrosa y a partir de ~1,07 la cámara entra en la atmósfera.")]
    [SerializeField, Range(1.12f, 2f)] private float arrivalDistance = 1.28f;

    [Tooltip("Hacia dónde mira el frente de la nave, en ejes del mundo: la ventana de la Sala 4.")]
    [SerializeField] private Vector3 shipForward = Vector3.forward;

    [Tooltip("Grados sobre el horizonte a los que queda el destino al llegar. La ventana de la " +
             "Sala 4 va de 0 a 36; cuanto más alto, menos luz del sol le da a esa cara.")]
    [SerializeField, Range(0f, 40f)] private float arrivalElevation = 13f;

    [Tooltip("Grados que le faltan por girar a la Tierra, sobre su eje, para que el destino quede " +
             "delante de la nave. Así se queda al encenderse la vista, y los recorre durante el " +
             "regreso. 0 = el destino ya está delante y la Tierra no gira.")]
    [SerializeField, Range(0f, 90f)] private float startTurnDegrees = 35f;

    [Header("Baliza del destino")]
    [SerializeField] private bool showBeacon = true;

    [Tooltip("Nombre que acompaña a la baliza. Vacío = solo la marca.")]
    [SerializeField] private string beaconLabel = "ECUADOR";

    [Tooltip("Material sin luz para la marca (Materials/Confeti.mat). Sin él solo sale el nombre: " +
             "Shader.Find falla en el APK con un shader que nadie referencia.")]
    [SerializeField] private Material beaconMaterial;

    [SerializeField] private Color beaconColor = new Color(1f, 0.85f, 0.2f);

    // En este modelo (Earth.fbx) el norte es +Z local, Greenwich +X y el oeste gira hacia +Y.
    private static readonly Vector3 North = Vector3.forward;

    // Parte del acercamiento en la que la Tierra termina de girar: el último tramo es solo bajar.
    private const float TurnEnds = 0.8f;

    // Tamaños de la baliza, como fracción de su distancia al jugador: siempre se ve igual de grande.
    private const float DotSize = 0.012f;
    private const float RingSize = 0.03f;
    private const float LabelSize = 0.022f;

    private Scenario4Controller controller;
    private Vector3 startLocalPosition;
    private bool started;

    private Transform beacon, ring, dot, label;
    private Material beaconInstance;

    private void Awake()
    {
        if (body != null) startLocalPosition = body.localPosition;
    }

    private void Start()
    {
        if (body == null)
            Debug.LogError($"[Tierra] '{name}' no tiene asignado 'body': no habrá acercamiento.", this);
    }

    /// <summary>Al encenderse la vista: la Tierra quieta, a `startTurnDegrees` de su pose de llegada.</summary>
    private void OnEnable()
    {
        if (body != null && !started) body.rotation = Facing(body.position, Viewpoint(), startTurnDegrees);
    }

    /// <summary>Como acción del Director: termina cuando la Tierra ha llegado.</summary>
    public IEnumerator Execute()
    {
        if (started || body == null) yield break;

        started = true;

        if (controller == null)
            controller = FindObjectsByType<Scenario4Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .FirstOrDefault(c => c.ChallengeId == challengeId);

        if (controller != null && controller.CompletedTime >= 0f)
        {
            float until = controller.CompletedTime + engineLeadSeconds;
            while (Time.time < until) yield return null;
        }

        yield return Approach();
    }

    [ContextMenu("Probar acercamiento")]
    private void Begin()
    {
        if (!started && isActiveAndEnabled) StartCoroutine(Execute());
    }

    private IEnumerator Approach()
    {
        Vector3 viewpoint = Viewpoint();
        Vector3 from = body.position;

        // Delante de la nave y algo por encima del horizonte, donde está la ventana.
        Vector3 forward = Vector3.ProjectOnPlane(shipForward, Vector3.up);
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        float elevation = arrivalElevation * Mathf.Deg2Rad;
        Vector3 to = viewpoint + (forward * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation)) * (arrivalDistance * Radius());

        float duration = Mathf.Max(0.1f, durationSeconds);
        Debug.Log($"[Tierra] Regreso: {Vector3.Distance(from, to):0} m en {duration:0.#} s, hasta " +
                  $"{Vector3.Distance(viewpoint, to) - Radius():0} m del suelo.", this);

        if (showBeacon) BuildBeacon();

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            Pose(from, to, viewpoint, elapsed / duration);
            yield return null;
        }

        Pose(from, to, viewpoint, 1f);
        Debug.Log("[Tierra] Regreso terminado: vuelta a Setup.", this);
    }

    private void Pose(Vector3 from, Vector3 to, Vector3 viewpoint, float t)
    {
        body.position = Vector3.LerpUnclamped(from, to, Smooth(t));
        body.rotation = Facing(body.position, viewpoint, startTurnDegrees * (1f - Smooth(t / TurnEnds)));
    }

    /// <summary>Smootherstep: arranca y llega sin tirones.</summary>
    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>Desde dónde se mira: el jugador. Sin cámara, este objeto.</summary>
    private Vector3 Viewpoint()
    {
        Camera viewer = Camera.main;
        return viewer != null ? viewer.transform.position : transform.position;
    }

    /// <summary>El destino, como dirección desde el centro de la Tierra, en los ejes del modelo.</summary>
    private Vector3 LandingDirection()
    {
        float latitude = landingLatitude * Mathf.Deg2Rad;
        float angle = -landingLongitude * Mathf.Deg2Rad;

        return new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle), Mathf.Cos(latitude) * Mathf.Sin(angle), Mathf.Sin(latitude));
    }

    /// <summary>
    /// Giro de la Tierra, puesta en `center`, con el destino mirando a `viewpoint` y el norte
    /// arriba, y después retrasada `pendingDegrees` sobre su eje: lo que le falta por girar.
    /// </summary>
    private Quaternion Facing(Vector3 center, Vector3 viewpoint, float pendingDegrees)
    {
        Vector3 toViewer = viewpoint - center;
        if (toViewer.sqrMagnitude < 0.0001f) return body.rotation;

        Quaternion facing = Quaternion.LookRotation(toViewer.normalized, Vector3.up) *
                            Quaternion.Inverse(Quaternion.LookRotation(LandingDirection(), North));

        return facing * Quaternion.AngleAxis(pendingDegrees, North);
    }

    /// <summary>Radio de la Tierra en metros de escena: la esfera más pequeña del modelo, sin atmósfera.</summary>
    private float Radius()
    {
        float radius = float.MaxValue;

        foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || (beacon != null && filter.transform.IsChildOf(beacon))) continue;

            radius = Mathf.Min(radius, filter.sharedMesh.bounds.extents.x * filter.transform.lossyScale.x);
        }

        return radius < float.MaxValue ? radius : body.lossyScale.x;
    }

    // --- Baliza ---

    /// <summary>
    /// Un punto, un anillo que late y el nombre, pegados a la Tierra sobre el destino y por
    /// fuera de la atmósfera. Cuelgan de la Tierra: giran y se acercan con ella.
    /// </summary>
    private void BuildBeacon()
    {
        if (beacon != null) return;

        Vector3 direction = LandingDirection();
        float radius = Radius();

        beacon = new GameObject("Baliza del destino").transform;
        beacon.SetParent(body, false);
        beacon.localPosition = direction * (radius * 1.025f / body.lossyScale.x);

        // +Z hacia fuera de la Tierra y +Y hacia el norte.
        beacon.localRotation = Quaternion.LookRotation(direction, North);

        if (beaconMaterial != null)
        {
            beaconInstance = new Material(beaconMaterial) { color = beaconColor };
            if (beaconInstance.HasProperty("_BaseColor")) beaconInstance.SetColor("_BaseColor", beaconColor);

            dot = BuildDisc("Punto", 0f);
            ring = BuildDisc("Anillo", 0.8f);
        }
        else
        {
            Debug.LogWarning($"[Tierra] '{name}' no tiene 'beaconMaterial': la baliza sale solo con el nombre.", this);
        }

        if (!string.IsNullOrWhiteSpace(beaconLabel))
        {
            // TextMeshPro cambia el Transform del objeto por un RectTransform: se guarda después.
            TextMeshPro text = new GameObject("Nombre").AddComponent<TextMeshPro>();
            label = text.transform;
            label.SetParent(beacon, false);

            // El texto de TextMeshPro se lee mirando hacia su +Z: de espaldas a la Tierra.
            label.localRotation = Quaternion.Euler(0f, 180f, 0f);

            text.text = beaconLabel;
            text.fontSize = 10f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = beaconColor;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
        }

        StartCoroutine(AnimateBeacon());
    }

    /// <summary>Disco (inner = 0) o anillo de radio 1, en el plano XY, visible por las dos caras.</summary>
    private Transform BuildDisc(string discName, float inner)
    {
        const int segments = 48;

        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 12];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 rim = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            vertices[i * 2] = rim * inner;
            vertices[i * 2 + 1] = rim;

            int a = i * 2, b = a + 1, c = (a + 2) % (segments * 2), d = (a + 3) % (segments * 2);
            int at = i * 12;

            // Las dos caras: no depende de por qué lado se mire.
            triangles[at] = a; triangles[at + 1] = b; triangles[at + 2] = c;
            triangles[at + 3] = b; triangles[at + 4] = d; triangles[at + 5] = c;
            triangles[at + 6] = a; triangles[at + 7] = c; triangles[at + 8] = b;
            triangles[at + 9] = b; triangles[at + 10] = c; triangles[at + 11] = d;
        }

        Mesh mesh = new Mesh { name = discName, vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();

        GameObject disc = new GameObject(discName);
        disc.transform.SetParent(beacon, false);
        disc.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = disc.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = beaconInstance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return disc.transform;
    }

    private IEnumerator AnimateBeacon()
    {
        while (beacon != null)
        {
            // Todo se mide en metros de escena y se pasa a la escala de la Tierra, de la que cuelga.
            float distance = Vector3.Distance(Viewpoint(), beacon.position);
            float unit = distance / Mathf.Max(0.0001f, beacon.lossyScale.x);

            // Un latido por segundo y medio: el anillo se abre desde el punto y vuelve a empezar.
            float beat = Mathf.Repeat(Time.time / 1.5f, 1f);

            if (dot != null) dot.localScale = Vector3.one * (DotSize * unit);
            if (ring != null) ring.localScale = Vector3.one * (Mathf.Lerp(DotSize, RingSize, beat) * unit);

            if (label != null)
            {
                label.localScale = Vector3.one * (LabelSize * unit);
                label.localPosition = Vector3.up * (RingSize * 1.7f * unit);
            }

            yield return null;
        }
    }

    [ContextMenu("Devolver la Tierra a su sitio")]
    private void ResetBody()
    {
        StopAllCoroutines();
        started = false;

        if (beacon != null) Destroy(beacon.gameObject);
        beacon = null;

        if (body == null) return;

        body.localPosition = startLocalPosition;
        body.rotation = Facing(body.position, Viewpoint(), startTurnDegrees);
    }

    private void OnDestroy()
    {
        if (beaconInstance != null) Destroy(beaconInstance);
    }
}
