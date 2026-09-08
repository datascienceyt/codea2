using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Un módulo de la sala de sistemas: una pantalla con el problema y varios botones de acción
/// que se seleccionan y deseleccionan libremente.
///
/// El módulo se repara cuando el conjunto seleccionado coincide EXACTAMENTE con el conjunto de
/// acciones correctas: ni de menos ni de más. Eso obliga a descartar los distractores de forma
/// activa, no solo a acertar uno.
/// </summary>
public class SystemModule : MonoBehaviour
{
    [Header("Contenido")]
    [SerializeField] private ModuleData data;

    [Header("Pantalla")]
    [SerializeField] private Text titleText;

    [Tooltip("El problema se mantiene en texto; lo que pasa a ser visual es el estado.")]
    [SerializeField] private Text problemText;

    [Tooltip("Mensaje que sustituye al problema cuando el módulo queda reparado.")]
    [SerializeField] private string repairedMessage = "Sistema restablecido";

    [Header("Estado visual")]
    [Tooltip("Icono de estado. Sustituye a las etiquetas de texto AVERIADO / REPARADO.")]
    [SerializeField] private Image statusIcon;
    [SerializeField] private Sprite brokenIcon;
    [SerializeField] private Sprite repairedIcon;

    [Tooltip("Luz del módulo: se le cambia el color al material. Sirve un Renderer cualquiera.")]
    [SerializeField] private Renderer statusLight;
    [SerializeField] private Color brokenColor = Color.red;
    [SerializeField] private Color repairedColor = Color.green;

    [Tooltip("Si el material usa emisión, además del color base se tiñe _EmissionColor.")]
    [SerializeField] private bool tintEmission = true;

    [Header("Botones")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private ModuleOptionButton[] optionButtons;

    [Header("Eventos")]
    [Tooltip("Al seleccionar o deseleccionar cualquier acción: sonido, parpadeo...")]
    public UnityEvent OnSelectionChanged;

    [Tooltip("Al quedar reparado.")]
    public UnityEvent OnSolved;

    /// <summary>Módulo, índice, si quedó seleccionada, si esa acción era correcta.</summary>
    public event Action<SystemModule, int, bool, bool> OnOptionToggled;

    private readonly HashSet<int> selected = new HashSet<int>();

    public ModuleData Data => data;
    public bool IsSolved { get; private set; }

    /// <summary>Id estable para telemetría. Cae al nombre del objeto si falta el asset.</summary>
    public string ModuleId => data != null ? data.moduleId : name;

    private void Awake()
    {
        if (optionButtons == null || optionButtons.Length == 0)
            optionButtons = GetComponentsInChildren<ModuleOptionButton>(true);
    }

    private void Start() => Refresh();

    /// <summary>Vuelca el contenido del asset a la pantalla, los botones y el estado visual.</summary>
    public void Refresh()
    {
        if (data == null)
        {
            Debug.LogError($"[Escenario2] '{name}' no tiene ModuleData asignado.", this);
            return;
        }

        if (!data.IsValid)
            Debug.LogError($"[Escenario2] '{data.name}' no tiene opciones o no tiene ninguna correcta.", this);

        if (titleText != null) titleText.text = data.moduleName;
        if (problemText != null) problemText.text = IsSolved ? repairedMessage : data.problem;

        RefreshStatus();
        RefreshButtons();
    }

    private void RefreshStatus()
    {
        if (statusIcon != null)
        {
            Sprite sprite = IsSolved ? repairedIcon : brokenIcon;
            if (sprite != null) statusIcon.sprite = sprite;
        }

        if (statusLight == null) return;

        Color color = IsSolved ? repairedColor : brokenColor;

        // .material y no .sharedMaterial: sharedMaterial teñiría de golpe todos los módulos
        // que compartan ese material.
        statusLight.material.color = color;

        if (tintEmission && statusLight.material.HasProperty("_EmissionColor"))
        {
            statusLight.material.EnableKeyword("_EMISSION");
            statusLight.material.SetColor("_EmissionColor", color);
        }
    }

    private void RefreshButtons()
    {
        if (data == null) return;

        foreach (ModuleOptionButton button in optionButtons)
        {
            if (button == null) continue;

            button.SetLabel(data.LabelAt(button.OptionIndex));
            button.SetSelected(selected.Contains(button.OptionIndex));
            button.SetInteractable(!IsSolved);
        }
    }

    /// <summary>
    /// Lo llaman los botones. Alterna entre seleccionada y no seleccionada.
    /// Un módulo ya reparado ignora las pulsaciones.
    /// </summary>
    public void ToggleOption(int optionIndex)
    {
        if (IsSolved || data == null) return;

        bool nowSelected = !selected.Contains(optionIndex);

        if (nowSelected) selected.Add(optionIndex);
        else selected.Remove(optionIndex);

        // El estado se actualiza ANTES de avisar: el controlador comprueba el IsSolved de todos
        // los módulos para saber si el escenario terminó, y si avisáramos primero vería este
        // todavía sin resolver.
        if (MatchesSolution()) IsSolved = true;

        Refresh();

        OnOptionToggled?.Invoke(this, optionIndex, nowSelected, data.IsCorrectAt(optionIndex));
        OnSelectionChanged?.Invoke();

        if (IsSolved) OnSolved?.Invoke();
    }

    /// <summary>
    /// La selección debe coincidir exactamente con las acciones correctas. Sobrar una incorrecta
    /// cuenta como no resuelto, igual que faltar una correcta.
    /// </summary>
    private bool MatchesSolution()
    {
        int needed = data.CorrectCount;

        if (needed == 0 || selected.Count != needed) return false;

        foreach (int index in selected)
            if (!data.IsCorrectAt(index)) return false;

        return true;
    }

    [ContextMenu("Reset Module")]
    public void ResetModule()
    {
        IsSolved = false;
        selected.Clear();
        Refresh();
    }
}
