using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Un botón de acción de un módulo. Alterna entre seleccionado y no seleccionado.
///
/// Deliberadamente NO conoce Oculus.Interaction: el prefab de PokeInteractable solo tiene que
/// llamar a Press() desde su InteractableUnityEventWrapper (WhenSelect). Así este script sigue
/// sirviendo si mañana se cambia la primitiva de interacción, y se puede probar sin visor desde
/// el menú contextual.
/// </summary>
public class ModuleOptionButton : MonoBehaviour
{
    [Tooltip("Si queda vacío se busca en los padres.")]
    [SerializeField] private SystemModule module;

    [Tooltip("Posición de esta acción dentro de ModuleData.options.")]
    [SerializeField] private int optionIndex;

    [Tooltip("Opcional. Se rellena solo con el texto de la acción.")]
    [SerializeField] private Text label;

    [Header("Estado seleccionado")]
    [Tooltip("Opcional. Renderer del botón: se le tiñe el material según esté seleccionado.")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.2f, 0.7f, 1f);

    [Tooltip("Opcional. Objeto que se enciende solo cuando la acción está seleccionada: " +
             "un tick, un borde iluminado...")]
    [SerializeField] private GameObject selectedMarker;

    [Tooltip("Opcional. Objeto que se apaga al resolver el módulo, para que el botón deje de " +
             "poder pulsarse. Apunta aquí al hijo que lleva el PokeInteractable.")]
    [SerializeField] private GameObject interactionRoot;

    [Header("Eventos")]
    public UnityEvent OnSelected;
    public UnityEvent OnDeselected;

    public int OptionIndex => optionIndex;
    public bool IsSelected { get; private set; }

    private bool interactable = true;

    private void Awake()
    {
        if (module == null)
            module = GetComponentInParent<SystemModule>();

        if (module == null)
            Debug.LogError($"[Escenario2] El botón '{name}' no encuentra su SystemModule.", this);
    }

    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    /// <summary>Refleja el estado de selección. Lo llama SystemModule, no el jugador.</summary>
    public void SetSelected(bool value)
    {
        bool changed = IsSelected != value;
        IsSelected = value;

        if (targetRenderer != null)
            targetRenderer.material.color = value ? selectedColor : normalColor;

        if (selectedMarker != null)
            selectedMarker.SetActive(value);

        if (!changed) return;

        if (value) OnSelected?.Invoke();
        else OnDeselected?.Invoke();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;

        if (interactionRoot != null)
            interactionRoot.SetActive(value);
    }

    /// <summary>
    /// Punto de entrada del botón. Cablear aquí el WhenSelect del PokeInteractable.
    /// </summary>
    [ContextMenu("Press")]
    public void Press()
    {
        if (!interactable || module == null) return;

        module.ToggleOption(optionIndex);
    }
}
