// TelemetryManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public enum Difficulty
{
    Basica,
    Avanzada
}

public enum SessionResult
{
    Completado,
    Abandonado
}

public enum LogicErrorType
{
    ColisionBot,
    SecuenciaIncompleta,
    ComandoInvalido
}

/// <summary>
/// Registra la telemetría de la sesión en un JSON por participante, en
/// Application.persistentDataPath/{pin}_{sessionId}.json.
///
/// El archivo se reescribe entero en cada evento crítico: son unos pocos KB y así un cierre
/// inesperado del visor no se lleva por delante los intentos ya hechos. Los eventos de alta
/// frecuencia se acumulan y se vuelcan cada saveInterval segundos.
/// </summary>
public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    /// <summary>
    /// Ids de escenario con métricas definidas. El 4 existe en el JSON como objeto vacío:
    /// cuando se decidan sus métricas tendrá su propio registro aquí.
    /// </summary>
    public const string Scenario1Id = "escenario1";
    public const string Scenario2Id = "escenario2";
    public const string Scenario3Id = "escenario3";
    public const string Scenario4Id = "escenario4";

    private const string SessionCounterKey = "telemetry_session_counter";
    private const string PinCounterKey = "telemetry_pin_counter";
    private const string DifficultyPrefKey = "telemetry_difficulty";

    [Header("Persistencia")]
    [Tooltip("Segundos mínimos entre escrituras diferidas. Los eventos críticos (intento, " +
             "escenario completado, fin de run) escriben al instante sin esperar.")]
    [SerializeField] private float saveInterval = 2f;

    private RunRecord _run;
    private string _filePath;
    private Difficulty _currentDifficulty;
    private int _sessionId;

    // Escritura diferida: los eventos de alta frecuencia (errores de lógica, agarres)
    // solo marcan el archivo como sucio. Sin esto, un jugador chocando repetidamente
    // provocaba un File.WriteAllText síncrono por choque.
    private bool _dirty;
    private float _nextSaveTime;

    /// <summary>Reto activo, para los overloads sin argumentos que se cablean por UnityEvent.</summary>
    private string _currentChallengeId;

    /// <summary>Time.time del inicio de cada reto. Fuera del JSON: es estado de runtime.</summary>
    private readonly Dictionary<string, float> _challengeStartTimes = new Dictionary<string, float>();

    /// <summary>
    /// Time.time en que empezó el ciclo del intento en curso: el momento desde el que se
    /// cuenta cuánto tarda el jugador en preparar su siguiente ejecución.
    /// </summary>
    private float _attemptCycleStart;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // La run se abre en Awake, no en Start: Scenario1Controller.StartChallenge() corre
        // en su propio Start() y el orden entre dos Start() no está garantizado. Si la run
        // no existiera todavía, el reto se registraría contra null y se perdería en silencio.

        // ID secuencial persistido: sigue subiendo aunque la app se cierre o el headset se reinicie
        _sessionId = PlayerPrefs.GetInt(SessionCounterKey, 0) + 1;
        PlayerPrefs.SetInt(SessionCounterKey, _sessionId);

        // Dificultad: configuración fija de la sesión, no varía por reto
        _currentDifficulty = (Difficulty)PlayerPrefs.GetInt(DifficultyPrefKey, (int)Difficulty.Basica);
        PlayerPrefs.Save();

        BeginRun(PlayerPrefs.GetInt(PinCounterKey, 0).ToString("D4"));
    }

    // --- Ciclo de la run ---

    /// <summary>
    /// Arranca una run nueva y su archivo. Se llama sola al iniciar y cada vez que el
    /// supervisor cambia el PIN, porque un PIN nuevo significa un participante nuevo.
    /// </summary>
    private void BeginRun(string pin)
    {
        _run = new RunRecord
        {
            pin = pin,
            sessionId = _sessionId,
            difficulty = ToDifficultyValue(_currentDifficulty),
            startedUtc = NowUtc(),
            endedUtc = string.Empty
        };

        _challengeStartTimes.Clear();
        _currentChallengeId = null;
        _attemptCycleStart = Time.time;

        // El sessionId evita que dos participantes con el mismo PIN se pisen el archivo.
        _filePath = Path.Combine(Application.persistentDataPath, $"{pin}_{_sessionId}.json");
        Save();

        Debug.Log($"[Telemetry] Run iniciada: {_filePath}");
    }

    /// <summary>Cierra la run. Idempotente.</summary>
    public void EndRun()
    {
        if (_run == null || !string.IsNullOrEmpty(_run.endedUtc)) return;

        _run.endedUtc = NowUtc();
        Save();
    }

    // --- Ciclo de reto ---

    public void StartChallenge(string challengeId)
    {
        // Un id nulo llegando desde un UnityEvent mal cableado reventaría el diccionario.
        if (string.IsNullOrEmpty(challengeId))
        {
            Debug.LogWarning("[Telemetry] StartChallenge con id vacío; se ignora.");
            return;
        }

        // El escenario activo se recuerda siempre, aunque todavía no tenga métricas:
        // así los overloads sin argumentos saben a quién dirigirse cuando las tenga.
        _currentChallengeId = challengeId;
        _challengeStartTimes[challengeId] = Time.time;

        // El primer intento se cronometra desde el arranque del escenario.
        _attemptCycleStart = Time.time;

        ScenarioRecord scenario = GetScenario(challengeId);
        if (scenario == null) return;

        scenario.started = true;
        scenario.startedUtc = NowUtc();

        Save();
    }

    /// <summary>Marca el escenario como resuelto y congela su duración. Idempotente.</summary>
    public void CompleteChallenge(string challengeId)
    {
        ScenarioRecord scenario = GetScenario(challengeId);
        if (scenario == null || scenario.completed) return;

        scenario.completed = true;
        scenario.endedUtc = NowUtc();
        scenario.totalSeconds = ElapsedIn(challengeId);

        // Escenarios de bloques: el intento en curso es, por definición, el que lo resolvió.
        // ProgramTrigger los registra con solved = 0 al pulsar, porque su corrutina no sobrevive
        // al momento en que el Director desactiva la estación de bloques.
        if (scenario is Scenario1Record blocks && blocks.attempts.Count > 0)
            blocks.attempts[blocks.attempts.Count - 1].solved = 1;
        else if (scenario is Scenario3Record loops && loops.attempts.Count > 0)
            loops.attempts[loops.attempts.Count - 1].solved = 1;

        Save();
    }

    public void CompleteChallenge() => CompleteChallenge(_currentChallengeId);

    // --- Intentos (una fila por pulsación del botón de ejecutar) ---

    /// <summary>
    /// Registra un intento de un escenario de bloques. Enruta al formato que corresponda:
    /// el escenario 1 solo guarda la secuencia; el 3 guarda además el cuerpo del bucle y las
    /// repeticiones, que son su dato pedagógico.
    ///
    /// Lo llama ProgramTrigger, que es común a ambos escenarios y no debe saber en cuál está.
    /// </summary>
    public void RegisterBlockAttempt(string challengeId, List<string> sequence, int repetitions = 1)
    {
        ScenarioRecord scenario = GetScenario(challengeId);
        if (scenario == null) return;

        float duration = Time.time - _attemptCycleStart;

        if (scenario is Scenario1Record blocks)
        {
            blocks.attempts.Add(new AttemptRecord
            {
                difficulty = _run.difficulty,
                sequence = Copy(sequence),
                solved = 0,
                durationSeconds = duration,
                timestamp = NowUtc()
            });
        }
        else if (scenario is Scenario3Record loops)
        {
            loops.attempts.Add(new LoopAttemptRecord
            {
                difficulty = _run.difficulty,
                sequence = Copy(sequence),
                repetitions = repetitions,
                solved = 0,
                durationSeconds = duration,
                timestamp = NowUtc()
            });
        }
        else
        {
            return;
        }

        // Se abre el ciclo siguiente aquí también, no solo en RegisterFailedAttempt: si ese
        // evento no llegara a cablearse, la duración sigue acotada entre ejecuciones
        // consecutivas en vez de acumularse desde el inicio del escenario.
        _attemptCycleStart = Time.time;

        Save();
    }

    public void RegisterAttempt(string challengeId, List<string> sequence, bool solved)
    {
        RegisterBlockAttempt(challengeId, sequence);
    }

    /// <summary>
    /// Copia defensiva: las listas que llegan las reconstruye ProgramTrigger en cada intento,
    /// pero no quiero que el JSON dependa de que nadie las mute después.
    /// </summary>
    private static List<string> Copy(List<string> source) =>
        source != null ? new List<string>(source) : new List<string>();

    // --- Escenario 2: selecciones en los módulos ---

    /// <summary>
    /// Una pulsación de botón en un módulo. Se registran también los fallos: son el dato
    /// pedagógico del escenario, no ruido.
    /// </summary>
    public void RegisterSelection(string challengeId, string moduleId, string option, bool correct)
    {
        Scenario2Record scenario = GetScenario2(challengeId);
        if (scenario == null) return;

        scenario.selections.Add(new SelectionRecord
        {
            module = moduleId,
            option = option,
            correct = correct ? 1 : 0,
            timestamp = NowUtc()
        });

        if (!correct) scenario.wrongSelections++;

        Save();
    }

    // --- Escenario 4: emparejamiento de figuras ---

    /// <summary>
    /// Una ficha colocada en un hueco. Se registran también los fallos: en dificultad
    /// intermedia, qué figuras creyó equivalentes el jugador es el dato pedagógico.
    /// </summary>
    public void RegisterPlacement(string challengeId, string socketId, string chipId,
                                  int chipSides, int expectedSides, bool correct)
    {
        Scenario4Record scenario = GetScenario4(challengeId);
        if (scenario == null) return;

        scenario.placements.Add(new PlacementRecord
        {
            socket = socketId,
            chip = chipId,
            chipSides = chipSides,
            expectedSides = expectedSides,
            correct = correct ? 1 : 0,
            timestamp = NowUtc()
        });

        if (!correct) scenario.wrongPlacements++;

        Save();
    }

    /// <summary>
    /// Modo de emparejamiento del escenario 4 ("forma" o "lados"). Lo fija su controlador:
    /// sin este dato, dos sesiones con la misma tasa de acierto no son comparables.
    /// </summary>
    public void SetMatchMode(string challengeId, string mode)
    {
        Scenario4Record scenario = GetScenario4(challengeId);
        if (scenario == null) return;

        scenario.matchMode = mode;
        MarkDirty();
    }

    // --- RF-03 / RF-04 ---

    /// <summary>
    /// Un intento que terminó sin resolver el escenario. Se cablea al evento de fallo del
    /// controlador, que es quien dispara el reinicio automático.
    /// </summary>
    public void RegisterFailedAttempt()
    {
        BlockScenarioRecord scenario = GetBlockScenario(_currentChallengeId);
        if (scenario == null) return;

        scenario.failedAttempts++;

        // El fallo abre un ciclo nuevo: a partir de aquí se cuenta lo que tarda en montar
        // el siguiente intento.
        _attemptCycleStart = Time.time;

        Save();
    }

    // --- Manipulación de bloques (cablear a VRGrabEvents.onGrabbed / onReleased) ---

    public void RegisterBlockGrabbed()
    {
        BlockScenarioRecord scenario = GetBlockScenario(_currentChallengeId);
        if (scenario == null) return;

        scenario.blocksGrabbed++;
        MarkDirty();

        print("RegisterBlockGrabbed");
    }

    public void RegisterBlockReleased()
    {
        BlockScenarioRecord scenario = GetBlockScenario(_currentChallengeId);
        if (scenario == null) return;

        scenario.blocksReleased++;
        MarkDirty();

        print("RegisterBlockReleased");
    }

    public void RegisterLogicError(LogicErrorType errorType)
    {
        BlockScenarioRecord scenario = GetBlockScenario(_currentChallengeId);
        if (scenario == null) return;

        switch (errorType)
        {
            case LogicErrorType.ColisionBot:
                scenario.errorCollisionBot++;
                break;
            case LogicErrorType.SecuenciaIncompleta:
                scenario.errorIncompleteSequence++;
                break;
            case LogicErrorType.ComandoInvalido:
                scenario.errorInvalidCommand++;
                break;
        }

        MarkDirty();
    }

    public void RegisterLogicError(int errorTypeIndex)
    {
        RegisterLogicError((LogicErrorType)errorTypeIndex);
    }

    // --- Manejo de PIN ---

    /// <summary>Siguiente participante: nuevo PIN y, por tanto, nueva run y nuevo archivo.</summary>
    public void IncrementPin()
    {
        SetPin(PlayerPrefs.GetInt(PinCounterKey, 0) + 1);
    }

    public void SetPin(int pinValue)
    {
        // Cambiar de PIN abandona la run en curso y abre otra vacía, con archivo nuevo.
        // Si se dispara por error (o en mal orden dentro de un UnityEvent) lo que se sube
        // después es el archivo nuevo y vacío, no la sesión real. Avisar es barato y este
        // fallo es invisible en el JSON resultante.
        if (HasRecordedData())
            Debug.LogWarning($"[Telemetry] Se cambia de PIN con datos sin subir en '{_filePath}'. " +
                             "La run en curso se cierra y se abre otra vacía. " +
                             "¿Está IncrementPin antes del upload en algún UnityEvent?");

        EndRun();

        PlayerPrefs.SetInt(PinCounterKey, pinValue);
        PlayerPrefs.Save();

        BeginRun(pinValue.ToString("D4"));

        Debug.Log($"[Telemetry] Nuevo PIN activo: {_run.pin}");
    }

    /// <summary>Si la run en curso tiene algo que valga la pena conservar.</summary>
    private bool HasRecordedData()
    {
        if (_run == null) return false;

        return _run.escenario1.started || _run.escenario2.started || _run.escenario3.started;
    }

    public string GetCurrentPin() => _run?.pin;

    // --- Configuración de dificultad (se fija una vez, no por reto) ---

    public void SetDifficulty(Difficulty difficulty)
    {
        _currentDifficulty = difficulty;
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
        PlayerPrefs.Save();

        if (_run != null)
        {
            _run.difficulty = ToDifficultyValue(difficulty);
            Save();
        }

        Debug.Log($"[Telemetry] Dificultad configurada: {_currentDifficulty}");
    }

    public void SetDifficulty(int difficultyIndex)
    {
        SetDifficulty((Difficulty)difficultyIndex);
    }

    public Difficulty GetCurrentDifficulty() => _currentDifficulty;

    // --- Persistencia ---

    private void Update()
    {
        if (!_dirty || Time.unscaledTime < _nextSaveTime) return;

        Save();
    }

    /// <summary>Marca cambios pendientes sin escribir. Para eventos de alta frecuencia.</summary>
    private void MarkDirty() => _dirty = true;

    /// <summary>
    /// Fuerza la escritura si hay cambios pendientes. Llámalo antes de subir el archivo:
    /// el uploader lee de disco y con escritura diferida podría subir una versión vieja.
    /// </summary>
    public void Flush()
    {
        if (_dirty) Save();
    }

    private void Save()
    {
        if (_run == null) return;

        File.WriteAllText(_filePath, JsonUtility.ToJson(_run, true), Encoding.UTF8);

        _dirty = false;
        _nextSaveTime = Time.unscaledTime + saveInterval;
    }

    /// <summary>
    /// Metadatos de ciclo de vida de cualquier escenario ya definido. El escenario 4 todavía
    /// no tiene estructura, así que se descarta con aviso en vez de forzarlo a una que no le toca.
    /// </summary>
    private ScenarioRecord GetScenario(string challengeId)
    {
        if (_run == null || string.IsNullOrEmpty(challengeId)) return null;

        switch (challengeId)
        {
            case Scenario1Id: return _run.escenario1;
            case Scenario2Id: return _run.escenario2;
            case Scenario3Id: return _run.escenario3;
            case Scenario4Id: return _run.escenario4;
        }

        Debug.LogWarning($"[Telemetry] '{challengeId}' todavía no tiene métricas definidas; registro descartado.");
        return null;
    }

    /// <summary>
    /// Registro de un escenario de bloques (1 o 3), para las métricas que comparten:
    /// reinicios, manipulación de bloques y errores de lógica.
    /// </summary>
    private BlockScenarioRecord GetBlockScenario(string challengeId) =>
        GetScenario(challengeId) as BlockScenarioRecord;

    /// <summary>Registro del escenario 2, para las métricas propias de selección.</summary>
    private Scenario2Record GetScenario2(string challengeId) => GetScenario(challengeId) as Scenario2Record;

    /// <summary>Registro del escenario 4, para las métricas propias de emparejamiento.</summary>
    private Scenario4Record GetScenario4(string challengeId) => GetScenario(challengeId) as Scenario4Record;

    private float ElapsedIn(string challengeId)
    {
        return _challengeStartTimes.TryGetValue(challengeId, out float startTime)
            ? Time.time - startTime
            : 0f;
    }

    /// <summary>Básica = 1, Avanzada = 2. Se guarda 1-based para que el JSON se lea solo.</summary>
    private static int ToDifficultyValue(Difficulty difficulty) => (int)difficulty + 1;

    private static string NowUtc() => DateTime.UtcNow.ToString("o");

    // --- Ciclo de vida ---

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;

        // Solo se vuelca a disco, NO se cierra la run.
        //
        // En Quest, quitarse el visor un momento pausa la aplicación. Cerrar la run ahí ponía
        // endedUtc en mitad de la sesión y, como EndRun es idempotente, ya no se corregía:
        // el resto de la partida quedaba registrada DESPUÉS de su propia hora de fin.
        Flush();
    }

    private void OnApplicationQuit()
    {
        EndRun();
        Flush();
    }

    public string GetCurrentFilePath() => _filePath;

#if UNITY_EDITOR
    // --- Utilidades de prueba (fuera del build) ---
    //
    // Sirven para validar el pipeline completo — modelo, escritura y subida — sin tener
    // que jugarse el escenario entero con el visor puesto.

    /// <summary>
    /// Rellena escenario1 con una partida verosímil: tres intentos, el último resuelto.
    /// </summary>
    [ContextMenu("Test/1 - Simular datos del escenario 1")]
    private void SimulateScenario1Data()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Telemetry] Entra en Play Mode primero: la run se inicializa en Awake().");
            return;
        }

        StartChallenge(Scenario1Id);

        // Finge que el escenario empezó hace un par de minutos, o totalSeconds saldría 0:
        // aquí StartChallenge y CompleteChallenge ocurren en el mismo frame.
        _challengeStartTimes[Scenario1Id] = Time.time - 137.4f;

        // Intento 1: se queda corto y choca contra un muro.
        SimulateBlockHandling(2);
        RegisterAttempt(Scenario1Id, new List<string> { "Avanzar", "Girar Derecha" }, false);
        RegisterLogicError(LogicErrorType.ColisionBot);
        RegisterFailedAttempt();

        // Intento 2: llega al final pero usa sobre una casilla vacía.
        SimulateBlockHandling(3);
        RegisterAttempt(Scenario1Id, new List<string> { "Avanzar 2", "Girar Izquierda", "Usar" }, false);
        RegisterLogicError(LogicErrorType.ComandoInvalido);
        RegisterFailedAttempt();

        // Intento 3: resuelve. CompleteChallenge le pondrá solved = 1.
        SimulateBlockHandling(4);
        RegisterAttempt(Scenario1Id, new List<string> { "Avanzar", "Girar Derecha", "Avanzar 2", "Usar" }, false);

        CompleteChallenge(Scenario1Id);
        EndRun();

        Debug.Log($"[Telemetry] Datos simulados escritos en:\n{_filePath}");
    }

    private void SimulateBlockHandling(int blocks)
    {
        for (int i = 0; i < blocks; i++)
        {
            RegisterBlockGrabbed();
            RegisterBlockReleased();
        }
    }

    /// <summary>Fuerza la escritura pendiente y lanza la subida al servidor.</summary>
    [ContextMenu("Test/2 - Subir el JSON al servidor")]
    private void PushTelemetryNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Telemetry] Entra en Play Mode primero: UnityWebRequest necesita corrutinas.");
            return;
        }

        CsvUploader uploader = FindAnyObjectByType<CsvUploader>();

        if (uploader == null)
        {
            Debug.LogError("[Telemetry] No hay ningún CsvUploader en la escena.");
            return;
        }

        Flush();
        uploader.UploadTelemetry();
    }

    /// <summary>Las dos anteriores de un tirón.</summary>
    [ContextMenu("Test/3 - Simular y subir")]
    private void SimulateAndPush()
    {
        SimulateScenario1Data();
        PushTelemetryNow();
    }

    [ContextMenu("Test/Mostrar JSON actual en consola")]
    private void DumpCurrentJson()
    {
        if (_run == null)
        {
            Debug.LogWarning("[Telemetry] No hay ninguna run abierta.");
            return;
        }

        Debug.Log($"[Telemetry] {_filePath}\n{JsonUtility.ToJson(_run, true)}");
    }
#endif
}
