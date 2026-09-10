using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Un botón de acción de un módulo. Alterna entre seleccionado y no seleccionado, y mientras
/// lo está se queda hundido y con otro material.
///
/// Deliberadamente NO conoce Oculus.Interaction: el botón solo tiene que llamar a Press()
/// desde el evento que prefieras de su InteractableUnityEventWrapper, sea hover o select. Así
/// este script sigue sirviendo si cambia la primitiva de interacción, y se puede probar sin
/// visor desde el menú contextual.
/// </summary>
public class ModuleOptionButton : MonoBehaviour
{
    [Tooltip("Si queda vacío se busca en los padres.")]
    [SerializeField] private SystemModule module;

    [Tooltip("Posición de esta acción dentro de ModuleData.options.")]
    [SerializeField] private int optionIndex;

    [Tooltip("Opcional. Se rellena solo con el texto de la acción.")]
    [SerializeField] private TMP_Text label;

    [Header("Aspecto al quedar seleccionado")]
    [Tooltip("Qué se mueve al seleccionar. Si queda vacío se mueve este mismo objeto.")]
    [SerializeField] private Transform pressTarget;

    [Tooltip("Cuánto se hunde, en metros, sobre el eje Z local de pressTarget. " +
             "Ponlo en negativo si tu botón se hunde hacia el otro lado.")]
    [SerializeField] private float pressDistance = 0.006f;

    [Tooltip("Renderer al que se le cambia el material. Si queda vacío se busca en los hijos.")]
    [SerializeField] private Renderer targetRenderer;

    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material selectedMaterial;

    [Tooltip("El PokeInteractableVisual de Meta, si tu botón lo lleva. Recoloca la cara del " +
             "botón al terminar el contacto, así que desharía el hundido: se apaga mientras " +
             "la acción está seleccionada y se vuelve a encender al soltarla.")]
    [SerializeField] private Behaviour pressVisual;

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

        if (pressTarget == null) pressTarget = transform;
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

        // La posición de reposo se guarda una vez, antes de que nadie la mueva. Calcularla
        // cada vez haría que el botón se hundiera un poco más en cada selección.
        pressTargetRest = pressTarget.localPosition;
    }

    private void Start()
    {
        // Se comprueba en Start y no en Awake: SystemModule rellena su lista en su propio
        // Awake, y el orden entre dos Awake no está garantizado.
        if (module != null && !module.Knows(this))
            Debug.LogError($"[Escenario2] '{name}' apunta al módulo '{module.name}', pero no " +
                           "está en su lista Option Buttons: al pulsarlo se alterna la opción, " +
                           "pero este botón no se hunde ni cambia de material. Añádelo a esa " +
                           "lista, o vacíala del todo para que el módulo busque entre sus hijos.", this);
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

        ApplySelectedLook(value);

        if (!changed) return;

        if (value) OnSelected?.Invoke();
        else OnDeselected?.Invoke();
    }

    /// <summary>
    /// Hunde el botón y le cambia el material, o lo devuelve a su estado normal.
    ///
    /// Apagar el visual de Meta mientras dura no es opcional si tu botón lo lleva:
    /// PokeInteractableVisual recoloca esa misma transform cuando el dedo se aleja, y lo hace
    /// justamente para devolver el botón arriba. Con él encendido, la selección solo se vería
    /// mientras el niño mantiene el dedo puesto.
    /// </summary>
    private void ApplySelectedLook(bool selected)
    {
        if (pressVisual != null)
            pressVisual.enabled = !selected;

        if (pressTarget != null)
            pressTarget.localPosition = selected
                ? pressTargetRest + Vector3.forward * pressDistance
                : pressTargetRest;

        if (targetRenderer == null) return;

        Material material = selected ? selectedMaterial : normalMaterial;

        if (material != null)
            targetRenderer.material = material;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;

        if (interactionRoot != null)
            interactionRoot.SetActive(value);
    }

    /// <summary>
    /// Punto de entrada del botón: alterna la acción. Cablea aquí el evento de hover o de
    /// select de tu InteractableUnityEventWrapper.
    /// </summary>
    [ContextMenu("Press")]
    public void Press()
    {
        if (module == null)
        {
            Debug.LogError($"[Escenario2] '{name}' no tiene SystemModule: ni cuelga de uno ni " +
                           "lo tiene asignado a mano.", this);
            return;
        }

        if (!interactable)
        {
            Debug.LogWarning($"[Escenario2] '{name}' está bloqueado porque su módulo ya está " +
                             "reparado.", this);
            return;
        }

        module.ToggleOption(optionIndex);
    }

    // --- Para ajustar pressDistance sin ponerse el visor. Necesitan Play Mode ---

    [ContextMenu("Previsualizar/Seleccionado")]
    private void PreviewSelected() => Preview(true);

    [ContextMenu("Previsualizar/Normal")]
    private void PreviewNormal() => Preview(false);

    private void Preview(bool selected)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Escenario2] Entra en Play Mode: la posición de reposo se captura " +
                           "al arrancar y sin ella el botón se descolocaría.", this);
            return;
        }

        ApplySelectedLook(selected);
    }
}
