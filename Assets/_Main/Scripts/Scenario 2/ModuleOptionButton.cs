using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Un botón de acción de un módulo. Alterna entre seleccionado y no seleccionado.
///
/// Deliberadamente NO conoce Oculus.Interaction: el prefab del botón solo tiene que llamar a
/// Press() desde el WhenSelect de su InteractableUnityEventWrapper. Así este script sigue
/// sirviendo si mañana se cambia la primitiva de interacción, y se puede probar sin visor
/// desde el menú contextual.
/// </summary>
public class ModuleOptionButton : MonoBehaviour
{
    [Tooltip("Si queda vacío se busca en los padres.")]
    [SerializeField] private SystemModule module;

    [Tooltip("Posición de esta acción dentro de ModuleData.options.")]
    [SerializeField] private int optionIndex;

    [Tooltip("Opcional. Se rellena solo con el texto de la acción.")]
    [SerializeField] private TMP_Text label;

    [Header("Estado seleccionado")]
    [Tooltip("La cara del botón, que se queda hundida mientras la acción está seleccionada. " +
             "En los botones de los samples de Meta es el hijo que lleva el PokeInteractableVisual.")]
    [SerializeField] private Transform pressTarget;

    [Tooltip("Cuánto se hunde y hacia dónde, en el espacio local del propio pressTarget. " +
             "En un botón de Meta suele ser el eje Z: prueba (0, 0, 0.006) y ajusta.")]
    [SerializeField] private Vector3 pressedOffset = new Vector3(0f, 0f, 0.006f);

    [Tooltip("El PokeInteractableVisual de Meta. Arrástralo aquí o el botón volverá a subir " +
             "en cuanto apartes el dedo, porque ese componente recoloca la cara al terminar " +
             "el contacto y desharía el hundido.")]
    [SerializeField] private Behaviour pressVisual;

    [Tooltip("Opcional. Renderer al que teñir el material. Ojo: en los botones de Meta el " +
             "color lo gobierna su propio visual y este tinte no se vería.")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.2f, 0.7f, 1f);

    [Tooltip("Opcional. Objeto que se apaga al resolver el módulo, para que el botón deje de " +
             "poder pulsarse. Apunta aquí al hijo que lleva el PokeInteractable.")]
    [SerializeField] private GameObject interactionRoot;

    [Header("Eventos")]
    public UnityEvent OnSelected;
    public UnityEvent OnDeselected;

    public int OptionIndex => optionIndex;
    public bool IsSelected { get; private set; }

    private bool interactable = true;
    private Vector3 pressTargetRest;

    private void Awake()
    {
        if (module == null)
            module = GetComponentInParent<SystemModule>();

        if (module == null)
            Debug.LogError($"[Escenario2] El botón '{name}' no encuentra su SystemModule.", this);

        // La posición de reposo se captura antes de que nadie la mueva. Guardarla y no
        // calcularla evita que el botón se hunda un poco más en cada selección.
        if (pressTarget != null)
            pressTargetRest = pressTarget.localPosition;
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

        ApplyPressed(value);

        if (targetRenderer != null)
            targetRenderer.material.color = value ? selectedColor : normalColor;

        if (!changed) return;

        if (value) OnSelected?.Invoke();
        else OnDeselected?.Invoke();
    }

    /// <summary>
    /// Deja la cara del botón hundida, o la devuelve a su sitio.
    ///
    /// Hay que apagar el visual de Meta mientras dure: PokeInteractableVisual recoloca esa
    /// misma transform cuando el dedo se aleja, y lo hace precisamente para devolver el botón
    /// arriba. Con él encendido, la selección se vería solo mientras tocas.
    /// </summary>
    private void ApplyPressed(bool pressed)
    {
        if (pressVisual != null)
            pressVisual.enabled = !pressed;

        if (pressTarget == null) return;

        pressTarget.localPosition = pressed
            ? pressTargetRest + pressedOffset
            : pressTargetRest;
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

    // --- Ajuste del hundido, para tunear pressedOffset sin ponerse el visor ---

    [ContextMenu("Previsualizar/Hundido")]
    private void PreviewPressed() => Preview(true);

    [ContextMenu("Previsualizar/Suelto")]
    private void PreviewReleased() => Preview(false);

    private void Preview(bool pressed)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Escenario2] Entra en Play Mode: la posición de reposo se captura " +
                           "al arrancar y sin ella el botón se descolocaría.", this);
            return;
        }

        ApplyPressed(pressed);
    }
}
