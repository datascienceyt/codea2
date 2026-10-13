using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Envuelve el agarre de Meta (GrabInteractable) en tres UnityEvents: acercar la mano, agarrar
/// y soltar. Es lo que escuchan los bloques y las fichas.
///
/// Cada agarre dispara onGrabbed UNA vez y cada suelta onReleased UNA vez, también si se agarra
/// con las dos manos: mientras quede una mano agarrando, el objeto sigue agarrado.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class VRGrabEvents : MonoBehaviour
{
    public UnityEvent onHover;
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    private GrabInteractable interactable;

    private bool grabbed;

    // Suelta detectada en este frame, a la espera de LateUpdate.
    private bool releasePending;

    private void Awake()
    {
        interactable = GetComponentInChildren<GrabInteractable>();

        if (interactable != null)
            interactable.WhenStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (interactable != null)
            interactable.WhenStateChanged -= OnStateChanged;
    }

    [ContextMenu("Invoke On Hover")]
    public void InvokeOnHover()
    {
        onHover?.Invoke();
    }

    [ContextMenu("Invoke On Grabbed")]
    public void InvokeOnGrabbed()
    {
        onGrabbed?.Invoke();
    }

    [ContextMenu("Invoke On Released")]
    public void InvokeOnReleased()
    {
        onReleased?.Invoke();
    }

    private void OnStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState == InteractableState.Select)
        {
            // Vuelto a agarrar antes de avisar de la suelta: primero se cierra la anterior.
            FlushRelease();

            if (grabbed) return;

            grabbed = true;
            onGrabbed?.Invoke();
            return;
        }

        // Soltar es SALIR de Select, vaya a donde vaya. Al abrir la mano el estado pasa a
        // Hover, porque la mano sigue al lado, y no a Normal: antes solo se avisaba al llegar
        // a Normal, y el bloque no encajaba hasta que el niño apartaba la mano.
        if (grabbed)
        {
            grabbed = false;
            releasePending = true;
            return;
        }

        if (args.NewState == InteractableState.Hover)
            onHover?.Invoke();
    }

    /// <summary>
    /// El aviso de la suelta sale aquí y no dentro del cambio de estado. En ese momento el
    /// Grabbable de Meta todavía no ha terminado de soltar el objeto (le devuelve su física
    /// justo después), y lo que el bloque hace al soltarse —colocarse en su hueco— quedaría
    /// pisado. LateUpdate corre en el mismo frame, cuando la interacción ya terminó: no se
    /// nota ningún retraso. Si el objeto se apaga antes, el aviso sale al volver a encenderse.
    /// </summary>
    private void LateUpdate() => FlushRelease();

    private void FlushRelease()
    {
        if (!releasePending) return;

        releasePending = false;
        onReleased?.Invoke();
    }
}
