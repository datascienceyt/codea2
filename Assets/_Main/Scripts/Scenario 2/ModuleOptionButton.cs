using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un botón de opción de un módulo.
///
/// Deliberadamente NO conoce Oculus.Interaction: el prefab de PokeInteractable solo tiene
/// que llamar a Press() desde su InteractableUnityEventWrapper (WhenSelect). Así este script
/// sigue sirviendo si mañana se cambia la primitiva de interacción, y se puede probar sin
/// visor desde el menú contextual.
/// </summary>
public class ModuleOptionButton : MonoBehaviour
{
    [Tooltip("Si queda vacío se busca en los padres.")]
    [SerializeField] private SystemModule module;

    [Tooltip("Posición de esta opción dentro de ModuleData.options.")]
    [SerializeField] private int optionIndex;

    [Tooltip("Opcional. Se rellena solo con el texto de la opción correspondiente.")]
    [SerializeField] private Text label;

    [Tooltip("Opcional. Objeto que se apaga al resolver el módulo para que el botón deje de " +
             "poder pulsarse. Apunta aquí al hijo que lleva el PokeInteractable.")]
    [SerializeField] private GameObject interactionRoot;

    public int OptionIndex => optionIndex;

    private bool interactable = true;

    private void Awake()
    {
        if (module == null)
            module = GetComponentInParent<SystemModule>();

        if (module == null)
            Debug.LogError($"[Escenario2] El botón '{name}' no encuentra su SystemModule.");
    }

    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
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

        module.ChooseOption(optionIndex);
    }
}
