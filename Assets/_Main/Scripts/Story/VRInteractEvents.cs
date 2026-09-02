using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Dispara a mano los eventos de un InteractableUnityEventWrapper desde el menú contextual.
///
/// Es el equivalente para botones pulsables de lo que VRGrabEvents ofrece para los agarrables:
/// permite probar el cableado de un botón sin ponerse el visor, con click derecho sobre el
/// componente en el inspector.
///
/// A diferencia de VRGrabEvents, este NO declara eventos propios ni se suscribe a nada. El
/// wrapper de Meta ya hace ese trabajo; aquí solo se le da al gatillo.
/// </summary>
public class VRInteractEvents : MonoBehaviour
{
    [Tooltip("Si queda vacío se busca en este objeto y luego en sus hijos.")]
    [SerializeField] private InteractableUnityEventWrapper wrapper;

    private void Awake()
    {
        if (wrapper == null)
            wrapper = GetComponent<InteractableUnityEventWrapper>();

        if (wrapper == null)
            wrapper = GetComponentInChildren<InteractableUnityEventWrapper>();
    }

    [ContextMenu("Invoke When Hover")]
    public void InvokeWhenHover() => Invoke(w => w.WhenHover, "WhenHover");

    [ContextMenu("Invoke When Unhover")]
    public void InvokeWhenUnhover() => Invoke(w => w.WhenUnhover, "WhenUnhover");

    [ContextMenu("Invoke When Select")]
    public void InvokeWhenSelect() => Invoke(w => w.WhenSelect, "WhenSelect");

    [ContextMenu("Invoke When Unselect")]
    public void InvokeWhenUnselect() => Invoke(w => w.WhenUnselect, "WhenUnselect");

    /// <summary>
    /// Pulsación completa: Select y a continuación Unselect. Es lo que ocurre de verdad al
    /// pulsar y soltar el botón, y hace falta para oír los dos sonidos del prefab de Meta.
    /// </summary>
    [ContextMenu("Invoke Full Press")]
    public void InvokeFullPress()
    {
        InvokeWhenSelect();
        InvokeWhenUnselect();
    }

    private void Invoke(System.Func<InteractableUnityEventWrapper, UnityEngine.Events.UnityEvent> selector,
                        string eventName)
    {
        // Awake no corre si se pulsa el menú contextual fuera de Play Mode.
        if (wrapper == null)
            wrapper = GetComponent<InteractableUnityEventWrapper>() ??
                      GetComponentInChildren<InteractableUnityEventWrapper>();

        if (wrapper == null)
        {
            Debug.LogError($"[VRInteractEvents] '{name}' no encuentra ningún " +
                           "InteractableUnityEventWrapper.", this);
            return;
        }

        selector(wrapper)?.Invoke();

        Debug.Log($"[VRInteractEvents] {name} → {eventName}", this);
    }
}
