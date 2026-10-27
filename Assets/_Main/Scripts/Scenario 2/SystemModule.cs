using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Un módulo de la sala de sistemas: una pantalla con el problema, varios botones de acción
/// que se marcan y desmarcan libremente, y un botón Probar.
///
/// Marcar no comprueba nada. Al pulsar Probar, el módulo se repara si lo marcado coincide
/// EXACTAMENTE con las acciones correctas: ni de menos ni de más. Si no, suena el error y se
/// desmarcan todos los botones para empezar de nuevo (reunión del 20/10/2026: "botón azul
/// probar, si error reiniciar 4 botones"). Cada Probar es un intento en la telemetría.
/// </summary>
public class SystemModule : MonoBehaviour, IStepAction
{
    [Header("Contenido")]
    [SerializeField] private ModuleData data;

    [Header("Pantalla")]
    [Tooltip("Título del sistema: 'Enfriamiento', 'Motores'... Sale del moduleName del asset, " +
             "así que no se escribe a mano por módulo ni cambia entre dificultades.")]
    [SerializeField] private Text titleText;

    [Tooltip("Igual, pero en TextMeshPro. Rellena solo el que uses.")]
    [SerializeField] private TMP_Text titleTextTmp;

    [Tooltip("El problema se mantiene en texto; lo que pasa a ser visual es el estado.")]
    [SerializeField] private Text problemText;

    [Tooltip("Mensaje que sustituye al problema cuando el módulo queda reparado.")]
    [SerializeField] private string repairedMessage = "Sistema restablecido";

    [Tooltip("Pregunta que se añade, en otra línea, a todos los problemas (feedback del 27/10/2026).")]
    [SerializeField] private string problemQuestion = "¿Qué hacemos?";

    [Tooltip("Tamaño máximo de letra del problema (antes, 30 fijo). Se ajusta solo para caber en " +
             "la pantalla sin bajar del tamaño que tenga el Text. 0 = el del Text, sin ajuste.")]
    [SerializeField] private int problemFontSize = 40;

    [Header("Estado visual")]
    [Tooltip("Icono de estado: es al que se le intercambia el sprite. Sustituye a las " +
             "etiquetas de texto AVERIADO / REPARADO.")]
    [SerializeField] private Image statusIcon;
    [SerializeField] private Sprite brokenIcon;
    [SerializeField] private Sprite repairedIcon;

    [Tooltip("Luz de averiado. Encendida desde el arranque y hasta que se repare el módulo.")]
    [SerializeField] private GameObject brokenLight;

    [Tooltip("Luz de reparado. Se enciende al resolverse, y apaga la de averiado.")]
    [SerializeField] private GameObject repairedLight;

    [Header("Botones")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private ModuleOptionButton[] optionButtons;

    [Tooltip("Segundos que los botones se quedan como estaban tras un Probar fallido, antes de " +
             "desmarcarse todos. Con 0 se desmarcan en el acto y el niño no llega a ver qué probó.")]
    [FormerlySerializedAs("wrongOptionSeconds")]
    [SerializeField] private float resetAfterErrorSeconds = 0.8f;

    [Tooltip("Opcional. El botón Probar del módulo: se apaga al repararse o al agotarse el tiempo.")]
    [SerializeField] private GameObject tryButton;

    [Tooltip("Sonido al repararse. El mismo que suena en el Escenario 4 al acertar una ficha.")]
    [SerializeField] private AudioClip solvedSound;

    [Header("Eventos")]
    [Tooltip("Al marcar o desmarcar cualquier acción: sonido, parpadeo...")]
    public UnityEvent OnSelectionChanged;

    [Tooltip("Al pulsar Probar sin acertar. Aquí va la frase de error del narrador, o un sonido.")]
    public UnityEvent OnWrongOption;

    [Tooltip("Al quedar reparado.")]
    public UnityEvent OnSolved;

    /// <summary>Módulo, índice, si quedó marcada, si esa acción era correcta.</summary>
    public event Action<SystemModule, int, bool, bool> OnOptionToggled;

    /// <summary>Módulo, acciones marcadas, cuántas correctas, cuántas incorrectas, si lo reparó y porcentaje.</summary>
    public event Action<SystemModule, List<string>, int, int, bool, float> OnTried;

    private readonly HashSet<int> selected = new HashSet<int>();

    // Tras un Probar fallido, mientras se espera para desmarcar: no se admiten pulsaciones.
    private bool resetting;

    public ModuleData Data => data;
    public bool IsSolved { get; private set; }

    /// <summary>Id estable para telemetría. Cae al nombre del objeto si falta el asset.</summary>
    public string ModuleId => data != null ? data.moduleId : name;

    // Si ya pasó Start. Antes, los botones aún no están resueltos y Start pintará solo.
    private bool started;

    // Se agotó el tiempo de la sala: ya no se espera a este módulo ni responden sus botones.
    private bool timedOut;

    /// <summary>
    /// Cambia el problema y las opciones del módulo. Lo usa Scenario2Difficulty al cargar la
    /// escena, antes de Awake; más tarde también vale, porque repinta.
    /// </summary>
    public void SetData(ModuleData value)
    {
        data = value;

        if (started) Refresh();
    }

    private void Awake()
    {
        if (optionButtons == null || optionButtons.Length == 0)
            optionButtons = GetComponentsInChildren<ModuleOptionButton>(true);
    }

    private void Start()
    {
        started = true;

        WarnAboutDuplicateIndices();
        Refresh();
    }

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

        // El título NO cambia al repararse: es el nombre del sistema, no su estado.
        UiText.Set(titleText, titleTextTmp, data.moduleName);

        if (problemText != null)
        {
            problemText.text = IsSolved ? repairedMessage : $"{data.problem}\n{problemQuestion}";
            if (problemFontSize > problemText.fontSize)
            {
                problemText.resizeTextMinSize = problemText.fontSize;
                problemText.resizeTextMaxSize = problemFontSize;
                problemText.resizeTextForBestFit = true;
            }
        }

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

        // Cada luz se comprueba por su lado: con un solo if, olvidar asignar una de las dos
        // dejaría la otra sin actualizar y el módulo acabaría con las dos encendidas a la vez.
        if (brokenLight != null) brokenLight.SetActive(!IsSolved);
        if (repairedLight != null) repairedLight.SetActive(IsSolved);
    }

    private void RefreshButtons()
    {
        if (data == null) return;

        foreach (ModuleOptionButton button in optionButtons)
        {
            if (button == null) continue;

            button.SetLabel(data.LabelAt(button.OptionIndex));
            button.SetSelected(selected.Contains(button.OptionIndex));
            button.SetInteractable(!IsSolved && !timedOut);
        }

        RefreshTryButton();
    }

    /// <summary>
    /// Probar se apaga al repararse el módulo o al acabarse el tiempo. Con un respiro: se repara
    /// DENTRO del evento de pulsación del propio botón, y apagarlo en el acto le cortaba el sonido.
    /// </summary>
    private void RefreshTryButton()
    {
        if (tryButton == null) return;

        bool show = !IsSolved && !timedOut;

        if (show || !Application.isPlaying || !isActiveAndEnabled) tryButton.SetActive(show);
        else if (tryButton.activeSelf) StartCoroutine(HideTryButton());
    }

    private IEnumerator HideTryButton()
    {
        yield return new WaitForSeconds(0.6f);

        if (IsSolved || timedOut) tryButton.SetActive(false);
    }

    /// <summary>
    /// Si este módulo tiene registrado ese botón.
    ///
    /// Existe para cazar el fallo de montaje más silencioso del escenario: un botón que
    /// apunta al módulo, pero que el módulo no conoce porque la lista se rellenó a mano y no
    /// se actualizó al añadirlo. Pulsarlo alterna la opción de verdad, pero el botón nunca
    /// recibe SetSelected y parece que no hace nada.
    /// </summary>
    public bool Knows(ModuleOptionButton button)
    {
        if (optionButtons == null) return false;

        foreach (ModuleOptionButton candidate in optionButtons)
            if (candidate == button) return true;

        return false;
    }

    /// <summary>Dos botones con el mismo optionIndex alternan la misma acción.</summary>
    private void WarnAboutDuplicateIndices()
    {
        if (optionButtons == null) return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;

            for (int j = i + 1; j < optionButtons.Length; j++)
            {
                if (optionButtons[j] == null) continue;
                if (optionButtons[i].OptionIndex != optionButtons[j].OptionIndex) continue;

                Debug.LogError($"[Escenario2] En '{name}', los botones " +
                               $"'{optionButtons[i].name}' y '{optionButtons[j].name}' tienen " +
                               $"el mismo optionIndex ({optionButtons[i].OptionIndex}): " +
                               "alternan la misma acción.", this);
            }
        }
    }

    /// <summary>
    /// Lo llaman los botones de acción: marca o desmarca, sin comprobar nada. Lo que vale es lo
    /// que haya marcado al pulsar Probar.
    /// </summary>
    public void ToggleOption(int optionIndex)
    {
        if (data == null)
        {
            Debug.LogError($"[Escenario2] '{name}' no tiene ModuleData asignado: pulsar sus " +
                           "botones no hará nada.", this);
            return;
        }

        if (IsSolved || timedOut || resetting) return;

        bool nowSelected = !selected.Contains(optionIndex);

        if (nowSelected) selected.Add(optionIndex);
        else selected.Remove(optionIndex);

        RefreshButtons();

        OnOptionToggled?.Invoke(this, optionIndex, nowSelected, data.IsCorrectAt(optionIndex));
        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// El botón Probar. Repara el módulo si lo marcado es exactamente la solución; si no,
    /// suena el error y, pasado un momento, se desmarcan todos los botones.
    ///
    /// Probar sin nada marcado no hace nada ni cuenta como intento (sesiones del 22/10/2026).
    /// Casi siempre era una segunda pulsación justo después de que el módulo se desmarcara
    /// solo, y sumaba un fallo que el niño no había decidido.
    /// </summary>
    [ContextMenu("Probar")]
    public void Try()
    {
        if (data == null)
        {
            Debug.LogError($"[Escenario2] '{name}' no tiene ModuleData asignado: Probar no hará nada.", this);
            return;
        }

        if (IsSolved || timedOut || resetting) return;

        if (selected.Count == 0)
        {
            Debug.Log($"[Escenario2] '{name}': Probar sin nada marcado, ignorado.", this);
            return;
        }

        bool solved = MatchesSolution();
        float percent = SolutionScore.SelectionPercent(selected, CorrectIndices());
        List<string> marked = SelectedLabels();

        int correctMarked = 0;
        foreach (int index in selected)
            if (data.IsCorrectAt(index)) correctMarked++;

        // El estado se actualiza ANTES de avisar: el controlador comprueba el IsSolved de todos
        // los módulos para saber si el escenario terminó, y si avisáramos primero vería este
        // todavía sin resolver.
        if (solved)
        {
            IsSolved = true;
            Refresh();
        }

        OnTried?.Invoke(this, marked, correctMarked, selected.Count - correctMarked, solved, percent);

        if (solved)
        {
            if (solvedSound != null) AudioSource.PlayClipAtPoint(solvedSound, transform.position);

            OnSolved?.Invoke();
            return;
        }

        OnWrongOption?.Invoke();

        // Con el módulo apagado (una prueba desde el inspector) no hay corrutina que espere.
        if (isActiveAndEnabled && resetAfterErrorSeconds > 0f) StartCoroutine(ResetAfterError());
        else ClearSelection();
    }

    private IEnumerator ResetAfterError()
    {
        resetting = true;

        yield return new WaitForSeconds(resetAfterErrorSeconds);

        ClearSelection();
    }

    /// <summary>Desmarca todos los botones. No se avisa a la telemetría: no es una decisión del niño.</summary>
    private void ClearSelection()
    {
        resetting = false;
        selected.Clear();
        RefreshButtons();
    }

    /// <summary>
    /// Si el módulo se apaga a mitad de la espera, su corrutina muere: sin esto se quedaría
    /// sin admitir pulsaciones para siempre.
    /// </summary>
    private void OnDisable()
    {
        if (resetting) ClearSelection();
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

    private HashSet<int> CorrectIndices()
    {
        HashSet<int> correct = new HashSet<int>();

        for (int i = 0; data.options != null && i < data.options.Count; i++)
            if (data.IsCorrectAt(i)) correct.Add(i);

        return correct;
    }

    /// <summary>Lo marcado, en el orden de los botones, para que dos intentos iguales se lean igual.</summary>
    private List<string> SelectedLabels()
    {
        List<int> indices = new List<int>(selected);
        indices.Sort();

        List<string> labels = new List<string>();
        foreach (int index in indices) labels.Add(data.LabelAt(index));

        return labels;
    }

    /// <summary>
    /// Bloquea el paso del Director hasta que ESTE módulo quede reparado.
    ///
    /// Es por módulo y no por escenario a propósito: así la narración puede reaccionar a cada
    /// avería por separado en vez de esperar a las tres. Scenario2Controller sigue encargándose
    /// del escenario completo y de la telemetría.
    ///
    /// No se reinicia el estado al entrar: si el módulo ya estaba reparado cuando el Director
    /// llega aquí, el paso se da por cumplido en vez de colgarse esperando algo que ya ocurrió.
    /// </summary>
    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => IsSolved || timedOut);
    }

    /// <summary>
    /// Se agotó el tiempo de la sala: el paso del Director deja de esperar a este módulo y sus
    /// botones dejan de responder. Lo llama Scenario2Controller.
    /// </summary>
    public void TimeOut()
    {
        timedOut = true;

        if (resetting) StopAllCoroutines();
        resetting = false;

        RefreshButtons();
    }

    [ContextMenu("Reset Module")]
    public void ResetModule()
    {
        StopAllCoroutines();

        IsSolved = false;
        resetting = false;
        selected.Clear();
        Refresh();
    }
}
