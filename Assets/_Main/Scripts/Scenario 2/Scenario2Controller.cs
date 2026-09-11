using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 2 con el Director y con la telemetría, igual que Scenario1Controller
/// hace con el Escenario 1. El escenario termina cuando todos los módulos están reparados.
/// </summary>
public class Scenario2Controller : MonoBehaviour, IStepAction
{
    [Header("Módulos")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private SystemModule[] modules;

    [Header("Telemetría")]
    [Tooltip("Debe coincidir con el id que use el Director al llamar StartChallenge.")]
    [SerializeField] private string challengeId = "escenario2";

    [Tooltip("Si el Director ya llama a StartChallenge por evento, déjalo desactivado " +
             "para no reiniciar el cronómetro del escenario dos veces.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Acciones al finalizar")]
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

    private void Awake()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<SystemModule>(true);

        WarnAboutMissingModules();

        foreach (SystemModule module in modules)
            if (module != null) module.OnOptionToggled += HandleOptionToggled;
    }

    /// <summary>
    /// Un hueco vacío en la lista de módulos es el fallo más caro del escenario, y el más
    /// silencioso: ese módulo no registra ninguna selección, y CheckCompletion se salta los
    /// nulos, así que el escenario se da por completado sin haberlo resuelto entero.
    /// </summary>
    private void WarnAboutMissingModules()
    {
        if (modules == null) return;

        int empty = 0;

        foreach (SystemModule module in modules)
            if (module == null) empty++;

        if (empty > 0)
            Debug.LogError($"[Escenario2] '{name}' tiene {empty} hueco(s) vacío(s) en su lista " +
                           "Modules: esos módulos no registrarán nada en telemetria y el " +
                           "escenario se dará por completado sin ellos. Asígnalos, o vacía la " +
                           "lista del todo para que se busquen entre los hijos.", this);

        int inChildren = GetComponentsInChildren<SystemModule>(true).Length;

        if (modules.Length - empty < inChildren)
            Debug.LogWarning($"[Escenario2] '{name}' tiene {modules.Length - empty} módulo(s) " +
                             $"en su lista, pero cuelgan {inChildren} de él. ¿Falta alguno?", this);
    }

    private void OnDestroy()
    {
        if (modules == null) return;

        foreach (SystemModule module in modules)
            if (module != null) module.OnOptionToggled -= HandleOptionToggled;
    }

    private void Start()
    {
        if (startChallengeOnStart) StartScenario();
    }

    /// <summary>Cablear al Director si se prefiere lanzarlo desde un Step.</summary>
    public void StartScenario()
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.StartChallenge(challengeId);
    }

    private void HandleOptionToggled(SystemModule module, int optionIndex, bool selected, bool correct)
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterSelection(
                challengeId,
                module.ModuleId,
                module.Data != null ? module.Data.LabelAt(optionIndex) : optionIndex.ToString(),
                selected,
                correct);

        // Se comprueba en cada pulsación, no solo en los aciertos: con selección múltiple, el
        // módulo puede resolverse al DESELECCIONAR una acción que sobraba.
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (scenarioFinished) return;

        foreach (SystemModule module in modules)
            if (module != null && !module.IsSolved) return;

        scenarioFinished = true;

        Debug.Log($"[Escenario 2] COMPLETADO · {modules.Length} módulos reparados · " +
                  $"challengeId '{challengeId}'", this);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.CompleteChallenge(challengeId);

        OnScenarioFinished?.Invoke();
    }

    /// <summary>
    /// Bloquea el Step del Director hasta que todos los módulos estén reparados.
    /// A diferencia de Scenario1Controller no se reinicia el flag: si el escenario ya estaba
    /// resuelto cuando el Director llega aquí, el paso se da por cumplido en vez de colgarse.
    /// </summary>
    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => scenarioFinished);
    }

    [ContextMenu("Reset Scenario")]
    public void ResetScenario()
    {
        scenarioFinished = false;

        foreach (SystemModule module in modules)
            if (module != null) module.ResetModule();
    }
}
