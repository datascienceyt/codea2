using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla del supervisor (RI-03): teclea el PIN del participante, elige la dificultad y
/// lanza la sesión.
///
/// Sustituye a DifficultySelector, que cargaba la escena en cuanto se pulsaba una dificultad
/// y por eso no dejaba sitio para introducir el PIN antes.
///
/// La dificultad se fija AQUÍ y queda cerrada para toda la sesión: el jugador no puede
/// cambiarla desde dentro del juego.
/// </summary>
public class SessionSetup : MonoBehaviour
{
    [Header("Entrada")]
    [Tooltip("Si queda vacío se busca en los hijos.")]
    [SerializeField] private PinEntry pinEntry;

    [Header("Escenas de destino")]
    [Tooltip("Deben estar añadidas en File > Build Settings, o LoadScene falla.")]
    [SerializeField] private string basicSceneName = "Basica";

    [Tooltip("Si un día unificas las dos dificultades en una sola escena, pon aquí el mismo " +
             "nombre que arriba: la dificultad se seguirá registrando bien.")]
    [SerializeField] private string intermediateSceneName = "Intermedia";

    [Header("Transición")]
    [Tooltip("Margen antes de cargar, para que dé tiempo al fundido o al sonido del botón.")]
    [SerializeField] private float loadDelay = 0.5f;

    [Header("Eventos")]
    [Tooltip("Al cambiar de dificultad: para encender la luz del botón elegido.")]
    public UnityEvent OnDifficultyChanged;

    [Tooltip("Al pasar a estar listo para empezar, o a dejar de estarlo. Para encender o " +
             "apagar el botón de empezar.")]
    public UnityEvent OnReadyChanged;

    [Tooltip("Se intentó empezar sin PIN completo o sin dificultad: para un aviso en pantalla.")]
    public UnityEvent OnRejected;

    [Tooltip("Justo antes de cargar la escena: fundido, sonido...")]
    public UnityEvent OnSessionStarting;

    public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Basica;

    /// <summary>
    /// Si el supervisor ha pulsado una dificultad. Es distinto de que SelectedDifficulty
    /// valga Basica: sin este flag, no elegir nada se confundiría con elegir la básica y
    /// media sesión se registraría con la dificultad equivocada.
    /// </summary>
    public bool DifficultyChosen { get; private set; }

    public bool IsReady => DifficultyChosen && pinEntry != null && pinEntry.IsComplete;

    private bool starting;
    private bool lastReady;

    private void Awake()
    {
        if (pinEntry == null) pinEntry = GetComponentInChildren<PinEntry>(true);

        if (pinEntry == null)
            Debug.LogError($"[Sesión] {name} no encuentra ningún PinEntry.", this);
    }

    private void Start()
    {
        // La suscripción va en código y no cableada en el inspector: si se olvidara, el botón
        // de empezar no se encendería nunca al completar el PIN y el fallo parecería del
        // teclado. Es la misma razón por la que los contadores de bloques van en código.
        if (pinEntry != null) pinEntry.OnChanged.AddListener(RefreshReady);

        RefreshReady();
    }

    private void OnDestroy()
    {
        if (pinEntry != null) pinEntry.OnChanged.RemoveListener(RefreshReady);
    }

    // --- Dificultad. Elegir NO carga nada: solo lo apunta ---

    [ContextMenu("Elegir básica")]
    public void SelectBasic() => Select(Difficulty.Basica);

    [ContextMenu("Elegir intermedia")]
    public void SelectIntermediate() => Select(Difficulty.Avanzada);

    /// <summary>0 = básica, 1 = intermedia. Para cablear por UnityEvent con argumento int.</summary>
    public void SelectByIndex(int difficultyIndex) =>
        Select(difficultyIndex == (int)Difficulty.Basica ? Difficulty.Basica : Difficulty.Avanzada);

    private void Select(Difficulty value)
    {
        if (starting) return;

        SelectedDifficulty = value;
        DifficultyChosen = true;

        OnDifficultyChanged?.Invoke();
        RefreshReady();
    }

    // --- Arranque ---

    /// <summary>Cablear al botón de empezar.</summary>
    [ContextMenu("Empezar sesión")]
    public void StartSession()
    {
        // Una segunda pulsación mientras se funde a negro cargaría la escena dos veces.
        if (starting) return;

        if (!IsReady)
        {
            string falta = !DifficultyChosen ? "elegir la dificultad" : "completar el PIN";
            Debug.LogWarning($"[Sesión] No se puede empezar: falta {falta}.", this);

            OnRejected?.Invoke();
            return;
        }

        string sceneName = SceneFor(SelectedDifficulty);

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"[Sesión] No hay escena configurada para {SelectedDifficulty}.", this);

            OnRejected?.Invoke();
            return;
        }

        // Se comprueba ANTES de fundir a negro: si la escena no está en Build Settings,
        // LoadScene lanza una excepción y el supervisor se queda mirando una pantalla en
        // negro sin forma de volver.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[Sesión] La escena {sceneName} no está en File > Build Settings.", this);

            OnRejected?.Invoke();
            return;
        }

        ApplySession();

        starting = true;
        OnSessionStarting?.Invoke();

        StartCoroutine(LoadAfterDelay(sceneName));
    }

    /// <summary>
    /// Deja el PIN y la dificultad fijados para la run que abrirá la escena de juego.
    ///
    /// El camino normal es PrepareSession: escribe los PlayerPrefs que TelemetryManager lee
    /// en su Awake, así que la run nace ya con el PIN correcto en lugar de abrirse con el
    /// anterior y cerrarse enseguida, que dejaría un JSON vacío en disco por cada sesión.
    /// Por eso esta pantalla NO necesita un TelemetryManager propio.
    ///
    /// Pero si alguien pone uno igualmente, es DontDestroyOnLoad: sobrevive al cambio de
    /// escena, el de la escena de juego se autodestruye y manda el de aquí, que ya abrió su
    /// run en Awake con el PIN viejo. Por eso, si existe, se le aplica también en caliente.
    /// </summary>
    private void ApplySession()
    {
        int pin = pinEntry.Pin;

        TelemetryManager.PrepareSession(pin, SelectedDifficulty);

        if (TelemetryManager.Instance != null)
        {
            // La dificultad primero: SetPin abre una run nueva y la copia dentro. Al revés
            // también acaba bien, pero deja la run naciendo con la dificultad anterior.
            TelemetryManager.Instance.SetDifficulty(SelectedDifficulty);
            TelemetryManager.Instance.SetPin(pin);
        }

        Debug.Log($"[Sesión] PIN {pin:D4} - dificultad {SelectedDifficulty} - " +
                  $"escena {SceneFor(SelectedDifficulty)}", this);
    }

    private string SceneFor(Difficulty difficulty) =>
        difficulty == Difficulty.Basica ? basicSceneName : intermediateSceneName;

    private IEnumerator LoadAfterDelay(string sceneName)
    {
        yield return new WaitForSeconds(loadDelay);

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Avisa solo cuando el estado cambia, para no repetir sonidos ni parpadeos.</summary>
    private void RefreshReady()
    {
        if (lastReady == IsReady) return;

        lastReady = IsReady;
        OnReadyChanged?.Invoke();
    }
}
