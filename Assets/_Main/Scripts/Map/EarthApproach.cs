using System.Collections;
using System.Linq;
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

    [Tooltip("Tope: qué parte de la distancia hasta el jugador recorre. La Tierra ya está cerca y " +
             "es enorme: con 0,3 pasa de ocupar media ventana a llenarla. Con 0,4 la cámara ya " +
             "queda dentro de su atmósfera y deja de verse; por eso el tope.")]
    [SerializeField, Range(0.05f, 0.38f)] private float approachFraction = 0.36f;

    [Tooltip("Reto cuyo final (el arranque de los motores) marca el inicio del regreso.")]
    [SerializeField] private string challengeId = TelemetryManager.Scenario4Id;

    [Tooltip("Segundos desde que se completa el panel hasta que la Tierra empieza a acercarse: " +
             "lo que tardan los motores en arrancar. Con 5 + durationSeconds la Tierra llega " +
             "cuando el sonido de los motores se está apagando.")]
    [SerializeField] private float engineLeadSeconds = 5f;

    private Scenario4Controller controller;
    private Vector3 startLocalPosition;
    private bool started;

    private void Awake()
    {
        if (body != null) startLocalPosition = body.localPosition;
    }

    private void Start()
    {
        if (body == null)
            Debug.LogError($"[Tierra] '{name}' no tiene asignado 'body': no habrá acercamiento.", this);
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
        Vector3 from = body.position;

        // Hacia donde está el jugador en ese momento. Sin cámara, hacia este objeto.
        Camera viewer = Camera.main;
        Vector3 to = Vector3.Lerp(from, viewer != null ? viewer.transform.position : transform.position, approachFraction);

        float duration = Mathf.Max(0.1f, durationSeconds);
        Debug.Log($"[Tierra] Regreso: {Vector3.Distance(from, to):0} m en {duration:0.#} s.", this);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;

            // Smootherstep: arranca y llega sin tirones.
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
            body.position = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        body.position = to;
        Debug.Log("[Tierra] Regreso terminado: vuelta a Setup.", this);
    }

    [ContextMenu("Devolver la Tierra a su sitio")]
    private void ResetBody()
    {
        StopAllCoroutines();
        started = false;

        if (body != null) body.localPosition = startLocalPosition;
    }
}
