using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cierre de la sesión cuando se agota el tiempo (RF-06). Cablear Timer.OnTimeUp → Begin().
///
/// Es un componente y no una lista de llamadas en el propio OnTimeUp porque el orden importa
/// y hay que ESPERAR entre medias: un UnityEvent lo dispara todo en el mismo frame, y cargar la
/// escena ahí cortaría el audio de "tiempo agotado" y la subida del JSON.
///
/// Orden:
///   1. Para el Director y corta la narración en curso: la historia no sigue sin tiempo.
///   2. OnTimeUpStarted: bloquear estaciones, fundido, luces...
///   3. Cierra la run y la guarda, ANTES del audio: si el supervisor le quita el visor al niño
///      a mitad de la frase, los datos ya están en disco.
///   4. Lanza la subida, que corre mientras suena la frase.
///   5. Reproduce la frase de tiempo agotado y espera a que termine.
///   6. Vuelve a la pantalla del supervisor, si hay SceneLoader. SceneLoader espera a su vez a
///      que acabe la subida.
/// </summary>
public class TimeUpSequence : MonoBehaviour
{
    [Header("Qué detener")]
    [Tooltip("El Director de esta escena. Sin él la historia seguiría avanzando sin tiempo.")]
    [SerializeField] private Director director;

    [Tooltip("El Narrator de la escena. Se corta lo que esté diciendo y dice la frase de abajo.")]
    [SerializeField] private Narrator narrator;

    [Header("Frase de tiempo agotado")]
    [Tooltip("Clip y textId del CSV. Sin clip solo se escribe; sin textId solo suena.")]
    [SerializeField] private NarrationEntry timeUpLine = new NarrationEntry();

    [Tooltip("Pausa tras la frase antes de volver, para que no corte en seco.")]
    [SerializeField] private float delayAfterLine = 2f;

    [Header("Cierre")]
    [Tooltip("Opcional. Si está, se sube el JSON mientras suena la frase.")]
    [SerializeField] private JSONUploader uploader;

    [Tooltip("Opcional. Si está, al terminar se vuelve a la escena del supervisor. Si no, la " +
             "escena se queda quieta después de la frase.")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Eventos")]
    [Tooltip("Nada más agotarse el tiempo: bloquear estaciones, fundido, luces...")]
    public UnityEvent OnTimeUpStarted;

    [Tooltip("Al terminar la frase, justo antes de volver.")]
    public UnityEvent OnTimeUpFinished;

    private bool running;

    [ContextMenu("Probar tiempo agotado")]
    public void Begin()
    {
        // El Timer ya dispara OnTimeUp una sola vez, pero esto también puede cablearse a mano.
        if (running) return;
        running = true;

        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        Debug.Log("[Tiempo] Tiempo agotado: se cierra la sesión.", this);

        if (director != null) director.Stop();
        else Debug.LogWarning("[Tiempo] Sin Director asignado: la historia seguirá avanzando.", this);

        if (narrator != null) narrator.Interrupt();

        OnTimeUpStarted?.Invoke();

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.Flush();
            TelemetryManager.Instance.EndRun();
        }

        if (uploader != null) uploader.UploadTelemetry();

        if (narrator != null) yield return narrator.PlayEntry(timeUpLine);

        if (delayAfterLine > 0f) yield return new WaitForSeconds(delayAfterLine);

        OnTimeUpFinished?.Invoke();

        if (sceneLoader != null) sceneLoader.Load();
    }
}
