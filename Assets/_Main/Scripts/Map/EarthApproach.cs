using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// El regreso a casa: al completar el último escenario, la Tierra se acerca a la nave poco a
/// poco, de modo que por las ventanas se la ve crecer mientras suena la despedida.
///
/// Se mueve la Tierra hacia el jugador en vez de agrandarla. A la vista es lo mismo, pero no
/// se toca la escala del modelo ni la de su atmósfera, y la Luna y el Sol conservan su sitio.
///
/// Arranca solo: escucha desde código el final del escenario. Si se agota el tiempo y la misión
/// no se completa, la Tierra no se acerca.
/// </summary>
public class EarthApproach : MonoBehaviour
{
    [Tooltip("Lo que se acerca: la Tierra de esta vista exterior.")]
    [SerializeField] private Transform body;

    [Tooltip("Segundos que dura el acercamiento. Tiene que cubrir la frase final y la espera " +
             "antes de volver a la pantalla del supervisor.")]
    [SerializeField] private float duration = 25f;

    [Tooltip("Qué parte de la distancia hasta el jugador recorre. La Tierra ya está cerca y es " +
             "enorme: con 0,3 pasa de ocupar media ventana a llenarla. Con 0,4 la cámara ya " +
             "queda dentro de su atmósfera y deja de verse; por eso el tope.")]
    [SerializeField, Range(0.05f, 0.38f)] private float approachFraction = 0.3f;

    [Tooltip("Cómo avanza en el tiempo: lento al principio y al final.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Reto cuyo final dispara el acercamiento.")]
    [SerializeField] private string challengeId = TelemetryManager.Scenario4Id;

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
        {
            Debug.LogError($"[Tierra] '{name}' no tiene asignado 'body': no habrá acercamiento.", this);
            return;
        }

        // Desde código y no cableado en el Director: si se olvidara el cable, el final se
        // quedaría sin su regreso y nada avisaría.
        controller = FindObjectsByType<Scenario4Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .FirstOrDefault(c => c.ChallengeId == challengeId);

        if (controller != null) controller.OnScenarioFinished.AddListener(Begin);
        else Debug.LogWarning($"[Tierra] No hay controlador del reto '{challengeId}': el acercamiento no arrancará solo.", this);
    }

    private void OnDestroy()
    {
        if (controller != null) controller.OnScenarioFinished.RemoveListener(Begin);
    }

    [ContextMenu("Probar acercamiento")]
    public void Begin()
    {
        if (started || body == null || !isActiveAndEnabled) return;

        started = true;
        StartCoroutine(Approach());
    }

    private IEnumerator Approach()
    {
        Vector3 from = body.position;

        // Hacia donde está el jugador en ese momento. Sin cámara, hacia este objeto.
        Camera viewer = Camera.main;
        Vector3 to = Vector3.Lerp(from, viewer != null ? viewer.transform.position : transform.position, approachFraction);

        Debug.Log($"[Tierra] Regreso: se acerca de {Vector3.Distance(from, to) / approachFraction:0} a " +
                  $"{Vector3.Distance(from, to) / approachFraction * (1f - approachFraction):0} m en {duration:0} s.", this);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            body.position = Vector3.LerpUnclamped(from, to, ease.Evaluate(elapsed / duration));
            yield return null;
        }

        body.position = to;
    }

    [ContextMenu("Devolver la Tierra a su sitio")]
    private void ResetBody()
    {
        StopAllCoroutines();
        started = false;

        if (body != null) body.localPosition = startLocalPosition;
    }
}
