using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Grabbable))]
public class VRGrabEvents : MonoBehaviour
{
    public UnityEvent onHover;
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    private GrabInteractable interactable;

    private bool grabbed;

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

    private void OnStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState == InteractableState.Hover)
        {
            onHover?.Invoke();
        }
        else if (args.NewState == InteractableState.Normal && grabbed)
        {
            onReleased?.Invoke();
            grabbed = false;
        }
        else if (args.NewState == InteractableState.Select)
        {
            onGrabbed?.Invoke();
            grabbed = true;
        }
    }
}
