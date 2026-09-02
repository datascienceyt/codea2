using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Un módulo de la sala de sistemas: una pantalla con el problema y N botones de opción.
/// El jugador pulsa una; si acierta el módulo queda reparado y deja de aceptar pulsaciones,
/// si falla puede reintentar sin límite.
/// </summary>
public class SystemModule : MonoBehaviour
{
    [Header("Contenido")]
    [SerializeField] private ModuleData data;

    [Header("Pantalla")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text problemText;
    [SerializeField] private Text statusText;

    [Header("Botones")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private ModuleOptionButton[] optionButtons;

    [Header("Estados (RI-02)")]
    [SerializeField] private string brokenLabel = "AVERIADO";
    [SerializeField] private string repairedLabel = "REPARADO";

    [Tooltip("Mensaje que sustituye al problema cuando el módulo queda reparado.")]
    [SerializeField] private string repairedMessage = "Sistema restablecido";

    [Header("Eventos")]
    [Tooltip("Acierto: para sonido, luz verde, animación...")]
    public UnityEvent OnCorrect;

    [Tooltip("Fallo: para sonido de rechazo, parpadeo en rojo...")]
    public UnityEvent OnWrong;

    /// <summary>Módulo, índice elegido, si fue acierto. Lo consume Scenario2Controller.</summary>
    public event Action<SystemModule, int, bool> OnOptionChosen;

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

    /// <summary>Vuelca el contenido del asset a la pantalla y a los botones.</summary>
    public void Refresh()
    {
        if (data == null)
        {
            Debug.LogError($"[Escenario2] '{name}' no tiene ModuleData asignado.");
            return;
        }

        if (!data.IsValid)
            Debug.LogError($"[Escenario2] '{data.name}': correctOptionIndex fuera del rango de options.");

        if (titleText != null) titleText.text = data.moduleName;
        if (problemText != null) problemText.text = IsSolved ? repairedMessage : data.problem;
        if (statusText != null) statusText.text = IsSolved ? repairedLabel : brokenLabel;

        foreach (ModuleOptionButton button in optionButtons)
        {
            if (button == null) continue;

            button.SetLabel(data.OptionAt(button.OptionIndex));
            button.SetInteractable(!IsSolved);
        }
    }

    /// <summary>
    /// Lo llaman los botones. Un módulo ya resuelto ignora cualquier pulsación, así que
    /// no se puede "desreparar" ni inflar la telemetría pulsando de más.
    /// </summary>
    public void ChooseOption(int optionIndex)
    {
        if (IsSolved || data == null) return;

        bool correct = optionIndex == data.correctOptionIndex;

        // El estado se actualiza ANTES de avisar.
        //
        // Al revés no funcionaba: el controlador comprueba el IsSolved de todos los módulos
        // para saber si el escenario terminó, y al acertar el último lo veía todavía sin
        // resolver, así que el escenario no se cerraba nunca.
        if (correct)
        {
            IsSolved = true;
            Refresh();
        }

        OnOptionChosen?.Invoke(this, optionIndex, correct);

        if (correct) OnCorrect?.Invoke();
        else OnWrong?.Invoke();
    }

    [ContextMenu("Reset Module")]
    public void ResetModule()
    {
        IsSolved = false;
        Refresh();
    }
}
