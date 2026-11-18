using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// El índice se guarda en PlayerPrefs y en las piezas con DifficultyOnly de la escena: los
// valores nuevos van al final. Intermedia se llamaba Avanzada; su índice no cambió.
public enum Difficulty
{
    Basica,
    Intermedia
}

public enum SessionResult
{
    Completado,
    Abandonado
}

// Valores explícitos: el 1 era SecuenciaIncompleta, que nunca llegó a registrarse. Se quitó sin
// renumerar para no reasignar en silencio cualquier RegisterLogicError(int) cableado en escena.
// La secuencia incompleta es hoy un failReason de la ejecución, no un error de lógica.
public enum LogicErrorType
{
    ColisionBot = 0,
    ComandoInvalido = 2,
    UsoInvalido = 3
}

/// <summary>
/// Registra la telemetría de la sesión en un JSON por participante, en
/// Application.persistentDataPath/{pin}_{sessionId}_{deviceId}.json. Formato 1: ver
/// TELEMETRIA.md en la raíz del proyecto, que es el diccionario de cada campo.
///
/// El archivo se reescribe entero en cada evento crítico: son unos pocos KB y así un cierre
/// inesperado del visor no se lleva por delante los intentos ya hechos. Los eventos de alta
/// frecuencia se acumulan y se vuelcan cada saveInterval segundos.
///
/// Tiempo. Todas las horas y todas las duraciones salen del MISMO reloj, DateTime.UtcNow, y
/// las pausas se restan de las duraciones. Así se cumple por construcción que
/// totalSeconds = endedUtc − challengeStartedUtc − pausedSeconds, y ningún durationSeconds
/// puede superar al totalSeconds de su escenario.
///
/// Ventana de cada reto. Un escenario acepta eventos (intentos, selecciones, colocaciones,
/// agarres, errores) solo entre challengeStartedUtc y endedUtc. Lo que llegue fuera de ese
/// tramo —el tutorial, la narración, una pulsación tras el cierre— no se apunta en ningún
/// escenario: así ningún evento queda antes del inicio ni después del fin, y los contadores
/// de un escenario no se mezclan con los del siguiente.
/// </summary>
public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    /// <summary>Versión del formato del JSON. Súbela solo si cambia el contrato con el dashboard.</summary>
    public const int FormatVersion = 1;

    /// <summary>Ids de los retos. Son también las claves de cada escenario en el JSON.</summary>
    public const string Scenario1Id = "escenario1";
    public const string Scenario2Id = "escenario2";
    public const string Scenario3Id = "escenario3";
    public const string Scenario4Id = "escenario4";

    private static readonly string[] ScenarioIds = { Scenario1Id, Scenario2Id, Scenario3Id, Scenario4Id };

    // Identificación del participante: PIN numérico secuencial, NO username.
    //
    // Divergencia deliberada con RF-01 del SRS, que pide un username de 3-12 letras. Se decidió
    // mantener el PIN y corregir el documento, no al revés: cambiarlo obligaría a construir la
    // UI de entrada de texto y rompería los JSON ya recogidos, porque 'pin' es además parte del
    // nombre de archivo ({pin}_{sessionId}_{deviceId}.json). No lo "arregles" sin hablarlo con el equipo.
    private const string SessionCounterKey = "telemetry_session_counter";
    private const string PinCounterKey = "telemetry_pin_counter";
    private const string DifficultyPrefKey = "telemetry_difficulty";
    private const string DeviceIdPrefKey = "telemetry_device_id";

    /// <summary>Caracteres del código del visor. Corto para que quepa en una etiqueta.</summary>
    private const int DeviceIdLength = 5;

    [Header("Persistencia")]
    [Tooltip("Segundos mínimos entre escrituras diferidas. Los eventos críticos (intento, " +
             "escenario completado, fin de run) escriben al instante sin esperar.")]
    [SerializeField] private float saveInterval = 2f;

    [Header("Pausas")]
    [Tooltip("Desactivado (lo normal): en el editor no se registran pausas. La ventana del Meta XR " +
             "Simulator le quita el foco a Unity cada vez que se usa, y cada clic fuera del Game " +
             "contaría como pausa y pararía el reloj de la sala. En el visor siempre se detectan.")]
    [SerializeField] private bool detectPausesInEditor = false;

    private bool DetectPauses => !Application.isEditor || detectPausesInEditor;

    private RunRecord _run;
    private string _filePath;
    private Difficulty _currentDifficulty;
    private int _sessionId;

    // Escritura diferida: los eventos de alta frecuencia (errores de lógica, agarres)
    // solo marcan el archivo como sucio.
    private bool _dirty;
    private float _nextSaveTime;

    /// <summary>Último reto abierto, para los overloads sin argumentos que se cablean por UnityEvent.</summary>
    private string _currentChallengeId;

    /// <summary>Último seq repartido en esta run.</summary>
    private int _seq;

    /// <summary>Para no repetir el aviso de eventos fuera de la ventana del reto.</summary>
    private readonly HashSet<string> _warnedOutsideWindow = new HashSet<string>();

    // --- Estado de runtime de cada escenario. Fuera del JSON ---

    private class Pause
    {
        public DateTime start;
        public DateTime? end;
        public string reason;
    }

    private class ScenarioState
    {
        public DateTime? roomStart;
        public DateTime? challengeStart;
        public DateTime? end;

        /// <summary>Pausas que tocan el tramo del reto, ya recortadas a su inicio.</summary>
        public readonly List<Pause> pauses = new List<Pause>();

        // Inicio del ciclo de cada lista: desde aquí se cuenta el durationSeconds siguiente.
        public DateTime attemptCycle;
        public DateTime selectionCycle;
        public DateTime placementCycle;

        // Esc. 1: ejecución en curso, hasta saber por qué falló. Esc. 3: ejecución en curso,
        // hasta saber cuántos objetos trasladó.
        public AttemptRecord runningAttempt;
        public string runningFirstError;
        public LoopAttemptRecord runningLoop;

        public bool IsOpen => challengeStart.HasValue && !end.HasValue;
    }

    private readonly Dictionary<string, ScenarioState> _states = new Dictionary<string, ScenarioState>();

    // Esc. 4: fichas cuyo agarre se contó, para que chipsReleased nunca supere a chipsGrabbed.
    private readonly HashSet<BlockNode> _grabbedChips = new HashSet<BlockNode>();

    // --- Pausas de la run ---

    private DateTime _runStart;
    private readonly List<Pause> _runPauses = new List<Pause>();
    private Pause _openPause;

    // Fuentes de pausa. La pausa está abierta mientras alguna siga activa.
    private bool _appPaused;      // OnApplicationPause: en Quest, quitarse el visor
    private bool _hmdUnmounted;   // OVRManager.HMDUnmounted
    private bool _inputFocusLost; // OVRManager.InputFocusLost: menú del sistema encima de la app
    private bool _windowFocusLost; // OnApplicationFocus(false): cualquier otra pérdida de foco

    private void Awake()
    {
        // Uno por escena, y SIN DontDestroyOnLoad. Lo hubo, y rompía la segunda sesión: al
        // volver a cargar la escena de juego sobrevivía el de la sesión anterior y el nuevo se
        // autodestruía, pero los UnityEvent de la escena (StartChallenge, CompleteChallenge,
        // EndRun...) apuntan al de SU escena.
        //
        // Solo se descarta el duplicado de la MISMA escena. Uno de otra escena es el de la
        // que se está descargando: se le releva y su OnDestroy ya no tocará Instance.
        if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
        {
            Debug.LogWarning($"[Telemetry] Hay dos TelemetryManager en '{gameObject.scene.name}'. " +
                             "Se descarta este.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // La run se abre en Awake, no en Start: los controladores abren sus retos en su propio
        // Start() y el orden entre dos Start() no está garantizado.
        _currentDifficulty = GetPreparedDifficulty();

        BeginRun(PlayerPrefs.GetInt(PinCounterKey, 0).ToString("D4"));
    }

    private void OnEnable()
    {
        Director.ScenarioStarted += HandleDirectorScenario;
        OVRManager.HMDUnmounted += HandleHmdUnmounted;
        OVRManager.HMDMounted += HandleHmdMounted;
        OVRManager.InputFocusLost += HandleInputFocusLost;
        OVRManager.InputFocusAcquired += HandleInputFocusAcquired;
    }

    private void OnDisable()
    {
        Director.ScenarioStarted -= HandleDirectorScenario;
        OVRManager.HMDUnmounted -= HandleHmdUnmounted;
        OVRManager.HMDMounted -= HandleHmdMounted;
        OVRManager.InputFocusLost -= HandleInputFocusLost;
        OVRManager.InputFocusAcquired -= HandleInputFocusAcquired;
    }

    private void Start()
    {
        // La dificultad sale de PlayerPrefs, que la deja la pantalla del supervisor, y de ahí
        // mismo la lee DifficultyApplier para montar los retos: escena y JSON no pueden
        // discrepar. Sin aplicador, la escena se juega tal como se guardó mientras el JSON
        // dice la dificultad de Setup, y ese error es indetectable al analizar.
        if (FindAnyObjectByType<DifficultyApplier>(FindObjectsInactive.Include) != null) return;

        Debug.LogWarning($"[Telemetry] Ningún DifficultyApplier en '{gameObject.scene.name}'. " +
                         $"La run dice dificultad '{_currentDifficulty}', pero los retos no se " +
                         "han montado para ella. Añade un DifficultyApplier a esta escena.", this);
    }

    // --- Ciclo de la run ---

    /// <summary>
    /// Arranca una run nueva y su archivo. Se llama sola al iniciar y cada vez que el
    /// supervisor cambia el PIN, porque un PIN nuevo significa un participante nuevo.
    /// </summary>
    private void BeginRun(string pin)
    {
        string deviceId = GetDeviceId();

        _sessionId = PlayerPrefs.GetInt(SessionCounterKey, 0);

        // Nunca sobre un archivo que ya exista. El contador vive en PlayerPrefs y puede volver
        // atrás (datos de la app restaurados, PlayerPrefs borradas) con los JSON aún en disco.
        do
        {
            _sessionId++;
            _filePath = Path.Combine(Application.persistentDataPath, $"{pin}_{_sessionId}_{deviceId}.json");
        }
        while (File.Exists(_filePath));

        PlayerPrefs.SetInt(SessionCounterKey, _sessionId);
        PlayerPrefs.Save();

        DateTime now = DateTime.UtcNow;

        _run = new RunRecord
        {
            formatVersion = FormatVersion,
            appVersion = Application.version ?? string.Empty,
            pin = pin,
            deviceId = deviceId,
            sessionId = _sessionId,
            difficulty = ToDifficultyValue(_currentDifficulty),
            startedUtc = Utc(now)
        };

        _runStart = now;
        _runPauses.Clear();
        _openPause = null;
        _states.Clear();
        foreach (string id in ScenarioIds) _states[id] = new ScenarioState();
        _grabbedChips.Clear();
        _currentChallengeId = null;
        _seq = 0;
        _warnedOutsideWindow.Clear();

        // Una pausa que ya estaba abierta (el supervisor cambió de PIN con el visor quitado)
        // sigue contando para la run nueva.
        RefreshPause(now);

        Save();

        Debug.Log($"[Telemetry] Run iniciada (formato {FormatVersion}, app {_run.appVersion}): {_filePath}");
    }

    /// <summary>
    /// Código que identifica a este visor, el mismo en todas sus sesiones.
    ///
    /// Sale de SystemInfo.deviceUniqueIdentifier, que en Quest deriva del ANDROID_ID: sobrevive
    /// a reinicios, a actualizaciones de la app y a borrar sus datos. Cambia con un
    /// restablecimiento de fábrica, y también si la app se instala firmada con otra clave.
    /// Si el sistema no da identificador, se genera uno y se guarda en PlayerPrefs.
    /// </summary>
    public static string GetDeviceId()
    {
        string id = SystemInfo.deviceUniqueIdentifier;

        if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
        {
            id = PlayerPrefs.GetString(DeviceIdPrefKey, string.Empty);

            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceIdPrefKey, id);
                PlayerPrefs.Save();
            }
        }

        // Solo letras y cifras: va en un nombre de archivo.
        StringBuilder code = new StringBuilder(DeviceIdLength);

        foreach (char c in id)
        {
            if (!char.IsLetterOrDigit(c)) continue;

            code.Append(char.ToUpperInvariant(c));
            if (code.Length == DeviceIdLength) break;
        }

        return code.ToString();
    }

    /// <summary>
    /// La sesión terminó como estaba previsto: final de la historia o tiempo de sesión
    /// agotado. endReason = completed. Idempotente. Es el que cablea el Director.
    /// </summary>
    public void EndRun() => CloseRun(EndReasons.Completed);

    /// <summary>
    /// La run se cierra antes de su final: cambio de PIN, vuelta a Setup a mitad de partida o
    /// escena descargada. endReason = quit. Idempotente: si ya estaba cerrada no hace nada.
    /// </summary>
    public void QuitRun() => CloseRun(EndReasons.Quit);

    private void CloseRun(string reason)
    {
        if (_run == null || !string.IsNullOrEmpty(_run.endedUtc)) return;

        DateTime now = DateTime.UtcNow;

        // La pausa abierta termina con la run: un tramo sin fin no se puede restar.
        ClosePause(now);

        // Los escenarios abiertos se cierran con ella; los no empezados quedan en notStarted.
        string scenarioReason = reason == EndReasons.AppClosed ? EndReasons.AppClosed : EndReasons.Abandoned;
        foreach (string id in ScenarioIds)
        {
            ScenarioRecord scenario = GetScenario(id);
            if (scenario.started && scenario.endReason == EndReasons.Open)
                EndScenario(id, scenarioReason, now);
        }

        WriteRunClose(_run, reason, now);
        Save();

        ReportIntegrity();
    }

    /// <summary>Cierre de la raíz: hora, motivo y tiempo de sesión sin pausas.</summary>
    private void WriteRunClose(RunRecord run, string reason, DateTime now)
    {
        run.endReason = reason;
        run.endedUtc = Utc(now);
        run.totalSeconds = (float)ActiveSeconds(_runPauses, _runStart, now, now);
    }

    /// <summary>
    /// Resumen de lo capturado al cerrar la run, con aviso de lo que huele a cable olvidado o a
    /// datos incoherentes. Avisa, no corrige: un cero puede ser legítimo.
    /// </summary>
    [ContextMenu("Comprobar integridad de la run")]
    public void ReportIntegrity()
    {
        if (_run == null)
        {
            Debug.LogWarning("[Telemetry] No hay ninguna run abierta que comprobar.");
            return;
        }

        StringBuilder report = new StringBuilder();
        int warnings = 0;

        report.AppendLine($"[Telemetry] Resumen de la run · formato {_run.formatVersion} · app {_run.appVersion} · " +
                          $"pin {_run.pin} · visor {_run.deviceId} · sesión {_run.sessionId} · " +
                          $"dificultad {_run.difficulty} · {_run.totalSeconds:0.0}s · cierre '{_run.endReason}'");

        warnings += ReportScenario(report, Scenario1Id, _run.escenario1);
        warnings += ReportScenario(report, Scenario2Id, _run.escenario2);
        warnings += ReportScenario(report, Scenario3Id, _run.escenario3);
        warnings += ReportScenario(report, Scenario4Id, _run.escenario4);

        report.AppendLine($"  Archivo: {_filePath}");

        if (warnings == 0)
        {
            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine($"  {warnings} aviso(s): revisa el cableado antes de dar la sesión por buena.");

        Debug.LogWarning(report.ToString());
    }

    private static int ReportScenario(StringBuilder report, string id, ScenarioRecord scenario)
    {
        int warnings = 0;

        if (!scenario.started)
        {
            report.AppendLine($"  {id}: NUNCA INICIADO");
            return 1;
        }

        // Una sala cerrada por tiempo o abandonada puede quedarse legítimamente sin intentos.
        bool expectData = scenario.completed;
        string detalle = string.Empty;

        if (scenario is Scenario1Record s1)
        {
            detalle = $"{s1.attempts.Count} intento(s), {s1.failedAttempts} fallido(s), {s1.blocksGrabbed} agarre(s)";

            if (expectData && s1.attempts.Count == 0) warnings++;
            if (expectData && s1.blocksGrabbed == 0) warnings++;
        }
        else if (scenario is Scenario2Record s2)
        {
            detalle = $"{s2.selections.Count} pulsación(es), {s2.wrongSelections} incorrecta(s), " +
                      $"{s2.attempts.Count} Probar";

            if (expectData && s2.selections.Count == 0) warnings++;
            if (s2.modulesRequired.Count == 0) warnings++;
        }
        else if (scenario is Scenario3Record s3)
        {
            detalle = $"{s3.attempts.Count} intento(s), {s3.blocksGrabbed} agarre(s)";

            if (expectData && s3.attempts.Count == 0) warnings++;
            if (s3.itemsRequired.Count == 0) warnings++;

            // Sin itemType no se sabe con qué tipo se corrió cada pasada: suele significar que
            // falta el socket de tipo en las 'preconditions' del ProgramTrigger.
            foreach (LoopAttemptRecord attempt in s3.attempts)
                if (string.IsNullOrEmpty(attempt.itemType)) { warnings++; break; }
        }
        else if (scenario is Scenario4Record s4)
        {
            detalle = $"{s4.placements.Count} colocación(es), {s4.wrongPlacements} incorrecta(s), " +
                      $"{s4.chipsGrabbed} agarre(s)";

            if (expectData && s4.placements.Count == 0) warnings++;
            if (expectData && s4.chipsGrabbed == 0) warnings++;
            if (s4.socketsRequired.Count == 0) warnings++;
        }

        report.AppendLine($"  {id}: {(scenario.endReason == EndReasons.Open ? "abierto" : scenario.endReason)} · " +
                          $"{scenario.totalSeconds:0.0}s de reto · {scenario.pausedSeconds:0.0}s en pausa · {detalle}");

        return warnings;
    }

    // --- Ciclo de reto ---

    /// <summary>Un reto empieza a aceptar acciones. Lo usan ScenarioTimeLimit y Scenario1Controller.</summary>
    public event Action<string> ChallengeStarted;

    /// <summary>Un reto termina: resuelto, por tiempo agotado o abandonado.</summary>
    public event Action<string> ChallengeEnded;

    /// <summary>
    /// El Director empieza uno de sus capítulos. Si se llama como un escenario ("Escenario 1"
    /// → escenario1), es la entrada en esa sala: su startedUtc, con la narración incluida.
    /// Así el Escenario 1, cuyo reto se abre después de narrar, tiene también su inicio de sala.
    /// </summary>
    private void HandleDirectorScenario(string scenarioName)
    {
        if (string.IsNullOrEmpty(scenarioName)) return;

        string id = scenarioName.Replace(" ", string.Empty).ToLowerInvariant();
        if (Array.IndexOf(ScenarioIds, id) >= 0) EnterRoom(id);
    }

    /// <summary>
    /// Entrada en la sala: started = true y startedUtc. El reto todavía no acepta acciones
    /// (narración). Idempotente. Si había otro escenario abierto, se cierra como abandonado.
    /// </summary>
    public void EnterRoom(string challengeId)
    {
        if (!TryGetState(challengeId, out ScenarioRecord scenario, out ScenarioState state)) return;
        if (scenario.started) return;

        DateTime now = DateTime.UtcNow;

        AbandonOthers(challengeId, now);

        scenario.started = true;
        scenario.startedUtc = Utc(now);
        scenario.endReason = EndReasons.Open;
        state.roomStart = now;

        Save();
    }

    /// <summary>
    /// Abre el reto: desde ahora acepta acciones y corre su cronómetro (challengeStartedUtc).
    /// Si no se había entrado en la sala, la entrada es este mismo momento.
    ///
    /// Idempotente: la primera llamada fija el inicio y las siguientes no lo tocan. Un reto ya
    /// cerrado (resuelto, agotado o abandonado) no se vuelve a abrir.
    /// </summary>
    public void StartChallenge(string challengeId)
    {
        if (!TryGetState(challengeId, out ScenarioRecord scenario, out ScenarioState state)) return;

        EnterRoom(challengeId);

        if (state.challengeStart.HasValue || scenario.endReason != EndReasons.Open) return;

        DateTime now = DateTime.UtcNow;

        state.challengeStart = now;
        state.attemptCycle = state.selectionCycle = state.placementCycle = now;
        scenario.challengeStartedUtc = Utc(now);

        // Si el reto se abre con una pausa en marcha (menú abierto), su tramo empieza aquí.
        if (_openPause != null)
            state.pauses.Add(new Pause { start = now, reason = _openPause.reason });

        _currentChallengeId = challengeId;

        Save();

        ChallengeStarted?.Invoke(challengeId);
    }

    /// <summary>Marca el escenario como resuelto y congela su duración. Idempotente.</summary>
    public void CompleteChallenge(string challengeId)
    {
        if (!TryGetState(challengeId, out ScenarioRecord scenario, out ScenarioState state)) return;

        // Solo un reto abierto se resuelve. Uno cerrado por tiempo no se marca después como
        // resuelto: el Director llama a CompleteChallenge() en pasos posteriores.
        if (!state.IsOpen) return;

        // Escenarios de bloques: el intento en curso es, por definición, el que lo resolvió.
        // ProgramTrigger los registra con solved = false al pulsar, porque su corrutina no
        // sobrevive al momento en que el Director desactiva la estación de bloques.
        if (scenario is Scenario1Record blocks && blocks.attempts.Count > 0)
        {
            AttemptRecord last = blocks.attempts[blocks.attempts.Count - 1];
            last.solved = true;
            last.failReason = FailReasons.None;
            state.runningAttempt = null;
        }
        else if (scenario is Scenario3Record loops && loops.attempts.Count > 0)
        {
            loops.attempts[loops.attempts.Count - 1].solved = true;
        }

        EndScenario(challengeId, EndReasons.Completed, DateTime.UtcNow);
    }

    public void CompleteChallenge() => CompleteChallenge(_currentChallengeId);

    /// <summary>
    /// Se agotó el tiempo del reto sin resolverlo: timedOut = true, completed = false. Su
    /// totalSeconds es el tiempo de reto real, sin pausas, como cualquier otro cierre: con el
    /// reloj de sala de ScenarioTimeLimit leyendo de aquí mismo, son los 300 s del límite más
    /// lo que se esperó a que terminara la ejecución en curso. Idempotente.
    /// </summary>
    public void TimeOutChallenge(string challengeId)
    {
        if (!TryGetState(challengeId, out _, out ScenarioState state) || !state.IsOpen) return;

        EndScenario(challengeId, EndReasons.TimedOut, DateTime.UtcNow);
    }

    /// <summary>
    /// Cierra un escenario con su motivo: banderas, endedUtc, tiempos y pausas. Un escenario
    /// que no llegó a abrir su reto se cierra con totalSeconds 0 y challengeStartedUtc vacío.
    /// </summary>
    private void EndScenario(string challengeId, string reason, DateTime now)
    {
        ScenarioRecord scenario = GetScenario(challengeId);
        ScenarioState state = _states[challengeId];

        if (scenario.endReason != EndReasons.Open) return;

        state.end = now;
        foreach (Pause pause in state.pauses)
            if (!pause.end.HasValue) pause.end = now;

        scenario.endReason = reason;
        scenario.completed = reason == EndReasons.Completed;
        scenario.timedOut = reason == EndReasons.TimedOut;
        scenario.endedUtc = Utc(now);

        state.runningAttempt = null;
        state.runningLoop = null;

        Save();

        ChallengeEnded?.Invoke(challengeId);
    }

    /// <summary>Entrar en otra sala con esta abierta es haberla saltado.</summary>
    private void AbandonOthers(string challengeId, DateTime now)
    {
        foreach (string id in ScenarioIds)
            if (id != challengeId && GetScenario(id).endReason == EndReasons.Open)
            {
                Debug.LogWarning($"[Telemetry] Se entra en {challengeId} con {id} sin cerrar: queda como abandonado.");
                EndScenario(id, EndReasons.Abandoned, now);
            }
    }

    /// <summary>
    /// Si el reto acepta acciones ahora: abierto y sin cerrar. Lo consultan los botones (Ejecutar)
    /// para no ejecutar nada que la telemetría no vaya a poder apuntar. Un id que no es de
    /// ningún escenario (el tutorial) no se controla: true.
    /// </summary>
    public bool AcceptsActions(string challengeId)
    {
        if (string.IsNullOrEmpty(challengeId) || !_states.TryGetValue(challengeId, out ScenarioState state)) return true;
        return state.IsOpen;
    }

    /// <summary>
    /// Segundos de reto, sin pausas, del escenario: lo mismo que acabará en su totalSeconds.
    /// ScenarioTimeLimit lleva con esto la cuenta de los 5 minutos de cada sala, para que el
    /// límite y la telemetría midan exactamente lo mismo.
    /// </summary>
    public float ChallengeSeconds(string challengeId)
    {
        if (!_states.TryGetValue(challengeId ?? string.Empty, out ScenarioState state) || !state.challengeStart.HasValue)
            return 0f;

        DateTime now = DateTime.UtcNow;
        return (float)ActiveSeconds(state.pauses, state.challengeStart.Value, state.end ?? now, now);
    }

    // --- Lo que pide cada versión del juego ---

    /// <summary>Esc. 2: ids de los módulos a reparar. Esc. 4: ids de los huecos del panel.</summary>
    public void SetRequired(string challengeId, IEnumerable<string> ids)
    {
        List<string> target = GetScenario(challengeId) switch
        {
            Scenario2Record s2 => s2.modulesRequired,
            Scenario4Record s4 => s4.socketsRequired,
            _ => null
        };

        if (target == null || ids == null) return;

        target.Clear();
        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id)) target.Add(id);

        MarkDirty();
    }

    /// <summary>Esc. 3: cuántos objetos hay que trasladar de cada tipo.</summary>
    public void SetItemsRequired(string challengeId, IEnumerable<KeyValuePair<string, int>> items)
    {
        if (!(GetScenario(challengeId) is Scenario3Record scenario) || items == null) return;

        scenario.itemsRequired.Clear();
        foreach (KeyValuePair<string, int> item in items)
            scenario.itemsRequired.Add(new ItemCountRecord { itemType = item.Key ?? string.Empty, count = item.Value });

        MarkDirty();
    }

    // --- Intentos (una fila por pulsación del botón de ejecutar) ---

    /// <summary>
    /// Registra un intento de un escenario de bloques. El escenario 1 guarda la secuencia; el 3,
    /// además, el cuerpo del bucle, las repeticiones y el tipo.
    ///
    /// Lo llama ProgramTrigger, que es común a ambos escenarios y no debe saber en cuál está.
    /// </summary>
    /// <param name="solutionPercent">Cuánto se parece a la solución, de 0 a 100; -1 sin solución cargada.</param>
    /// <param name="remaining">Esc. 3: objetos de ese tipo por trasladar al pulsar; -1 si no aplica.</param>
    public void RegisterBlockAttempt(string challengeId, List<string> sequence, int repetitions = 1,
                                     string itemType = null, float solutionPercent = -1f, int remaining = -1)
    {
        if (!TryGetOpen(challengeId, "un intento", out ScenarioRecord scenario, out ScenarioState state)) return;

        DateTime now = DateTime.UtcNow;
        float duration = Duration(state, state.attemptCycle, now);

        if (scenario is Scenario1Record blocks)
        {
            AttemptRecord attempt = new AttemptRecord
            {
                seq = NextSeq(),
                sequence = Copy(sequence),
                solved = false,
                failReason = FailReasons.Other,
                solutionPercent = solutionPercent,
                durationSeconds = duration,
                timestamp = Utc(now)
            };

            blocks.attempts.Add(attempt);
            state.runningAttempt = attempt;
            state.runningFirstError = null;
        }
        else if (scenario is Scenario3Record loops)
        {
            bool typed = !string.IsNullOrEmpty(itemType) && remaining >= 0;

            LoopAttemptRecord attempt = new LoopAttemptRecord
            {
                seq = NextSeq(),
                sequence = Copy(sequence),
                repetitions = repetitions,
                itemType = itemType ?? string.Empty,
                remaining = typed ? remaining : -1,
                moved = typed ? 0 : -1,
                phaseCompleted = 0,
                solved = false,
                solutionPercent = solutionPercent,
                durationSeconds = duration,
                timestamp = Utc(now)
            };

            loops.attempts.Add(attempt);
            state.runningLoop = typed ? attempt : null;
        }
        else
        {
            return;
        }

        // El ciclo siguiente se abre al pulsar; el reinicio tras un fallo lo vuelve a abrir.
        state.attemptCycle = now;

        Save();
    }

    /// <summary>
    /// Esc. 1: la ejecución terminó sin resolver el nivel. Fija su failReason: el primer error
    /// de lógica que hubo durante ella (collision o invalidUse) o, si no hubo ninguno, que la
    /// secuencia se acabó sin llegar: incompleteSequence. Lo llama Scenario1Controller.
    /// </summary>
    public void FinishBlockRun(string challengeId)
    {
        if (!_states.TryGetValue(challengeId ?? string.Empty, out ScenarioState state) || state.runningAttempt == null) return;

        state.runningAttempt.failReason = state.runningFirstError ?? FailReasons.IncompleteSequence;
        state.runningAttempt = null;

        Save();
    }

    /// <summary>
    /// Esc. 3: tras cada acción del brazo, cuántos objetos lleva trasladados la ejecución en
    /// curso y si ha dejado su tipo a cero. Lo llama Scenario3Controller con su recuento.
    /// </summary>
    public void UpdateLoopProgress(string challengeId, Func<string, int> remainingOf)
    {
        if (!_states.TryGetValue(challengeId ?? string.Empty, out ScenarioState state) || remainingOf == null) return;

        LoopAttemptRecord attempt = state.runningLoop;
        if (attempt == null) return;

        int now = remainingOf(attempt.itemType);
        if (now < 0) return;

        attempt.moved = Mathf.Max(0, attempt.remaining - now);
        attempt.phaseCompleted = attempt.remaining > 0 && now == 0 ? 1 : 0;

        MarkDirty();
    }

    /// <summary>Esc. 3: la ejecución terminó; lo siguiente que mueva el brazo ya no es suyo.</summary>
    public void FinishLoopRun(string challengeId, Func<string, int> remainingOf)
    {
        UpdateLoopProgress(challengeId, remainingOf);

        if (_states.TryGetValue(challengeId ?? string.Empty, out ScenarioState state)) state.runningLoop = null;

        Flush();
    }

    /// <summary>
    /// Copia defensiva: las listas que llegan las reconstruye ProgramTrigger en cada intento,
    /// pero no quiero que el JSON dependa de que nadie las mute después.
    /// </summary>
    private static List<string> Copy(List<string> source) =>
        source != null ? new List<string>(source) : new List<string>();

    // --- Escenario 2: selecciones en los módulos ---

    /// <summary>
    /// Una pulsación de un botón de acción: marcar (selected = 1) o desmarcar (selected = 0).
    /// Una entrada por pulsación del niño. El desmarcado automático tras un Probar fallido no
    /// pasa por aquí: no es una acción suya.
    /// </summary>
    public void RegisterSelection(string challengeId, string moduleId, string option, bool correct, bool selected)
    {
        if (!TryGetOpen(challengeId, "una selección", out ScenarioRecord record, out ScenarioState state) ||
            !(record is Scenario2Record scenario)) return;

        DateTime now = DateTime.UtcNow;

        scenario.selections.Add(new SelectionRecord
        {
            seq = NextSeq(),
            module = moduleId ?? string.Empty,
            option = option ?? string.Empty,
            selected = selected ? 1 : 0,
            correct = correct,
            durationSeconds = Duration(state, state.selectionCycle, now),
            timestamp = Utc(now)
        });

        state.selectionCycle = now;

        Save();
    }

    /// <summary>
    /// Una pulsación de Probar en un módulo con algo marcado: lo que había marcado, si lo
    /// reparó y cuánto se acercaba a la solución.
    /// </summary>
    public void RegisterModuleAttempt(string challengeId, string moduleId, List<string> selection,
                                      int correctSelected, int wrongSelected, bool solved, float solutionPercent)
    {
        if (!TryGetOpen(challengeId, "un Probar", out ScenarioRecord record, out ScenarioState state) ||
            !(record is Scenario2Record scenario)) return;

        DateTime now = DateTime.UtcNow;

        scenario.attempts.Add(new ModuleAttemptRecord
        {
            seq = NextSeq(),
            module = moduleId ?? string.Empty,
            selection = Copy(selection),
            correctSelected = correctSelected,
            wrongSelected = wrongSelected,
            solved = solved,
            solutionPercent = solutionPercent,
            durationSeconds = Duration(state, state.attemptCycle, now),
            timestamp = Utc(now)
        });

        state.attemptCycle = now;

        Save();
    }

    // --- Escenario 4: emparejamiento de figuras ---

    /// <summary>Una ficha colocada en un hueco, bien o mal.</summary>
    public void RegisterPlacement(string challengeId, string socketId, string chipId, bool correct)
    {
        if (!TryGetOpen(challengeId, "una colocación", out ScenarioRecord record, out ScenarioState state) ||
            !(record is Scenario4Record scenario)) return;

        DateTime now = DateTime.UtcNow;

        scenario.placements.Add(new PlacementRecord
        {
            seq = NextSeq(),
            socket = socketId ?? string.Empty,
            chip = chipId ?? string.Empty,
            correct = correct,
            durationSeconds = Duration(state, state.placementCycle, now),
            timestamp = Utc(now)
        });

        state.placementCycle = now;

        Save();
    }

    // --- RF-03 / RF-04 ---

    /// <summary>
    /// El nivel se reinició tras una ejecución fallida (cableado en el OnAttemptFailed del
    /// Escenario 1; el 3 lo llama desde código). Ya no cuenta nada —failedAttempts se calcula
    /// de attempts[]—: solo abre el ciclo del siguiente intento, que se prepara desde aquí.
    /// </summary>
    public void RegisterFailedAttempt()
    {
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord scenario, out ScenarioState state)) return;
        if (!(scenario is Scenario1Record) && !(scenario is Scenario3Record)) return;

        state.attemptCycle = DateTime.UtcNow;
    }

    // --- Manipulación de piezas. Lo llama BlockNode desde código, no por UnityEvent ---

    /// <summary>
    /// Un agarre. Vale para los bloques de los escenarios 1 y 3 y para las fichas del 4:
    /// BlockNode es la clase base de los tres y no sabe en qué escenario está. Cuenta para el
    /// reto abierto; sin ninguno (tutorial, narración, traslados), para ninguno.
    /// </summary>
    public void RegisterBlockGrabbed(BlockNode block = null)
    {
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord scenario, out _)) return;

        if (scenario is Scenario1Record grid) grid.blocksGrabbed++;
        else if (scenario is Scenario3Record loops) loops.blocksGrabbed++;
        else if (scenario is Scenario4Record shapes)
        {
            shapes.chipsGrabbed++;
            if (block != null) _grabbedChips.Add(block);
        }
        else return;

        MarkDirty();
    }

    /// <summary>Esc. 4: se soltó una ficha. Solo cuenta si su agarre se contó.</summary>
    public void RegisterBlockReleased(BlockNode block)
    {
        if (block == null || !_grabbedChips.Remove(block)) return;
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord scenario, out _)) return;

        if (!(scenario is Scenario4Record shapes)) return;

        shapes.chipsReleased++;
        MarkDirty();
    }

    /// <summary>El jugador encajó un bloque en un hueco (escenarios 1 y 3).</summary>
    public void RegisterBlockConnected()
    {
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord scenario, out _)) return;

        if (scenario is Scenario1Record grid) grid.blocksConnected++;
        else if (scenario is Scenario3Record loops) loops.blocksConnected++;
        else return;

        MarkDirty();
    }

    /// <summary>Una pulsación del botón que devuelve los bloques a su sitio (Escenario 1).</summary>
    public void RegisterBlockReset()
    {
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord record, out _) ||
            !(record is Scenario1Record scenario)) return;

        scenario.blockResets++;

        MarkDirty();
    }

    /// <summary>
    /// Un error de lógica en el reto abierto. Esc. 1: "Usar" mal va a errorInvalidUse y todo lo
    /// demás (salirse, chocar) a errorCollisionBot; el primero de una ejecución es además su
    /// failReason. Esc. 3: errorInvalidCommand.
    /// </summary>
    public void RegisterLogicError(LogicErrorType errorType)
    {
        if (!TryGetOpen(_currentChallengeId, null, out ScenarioRecord scenario, out ScenarioState state)) return;

        if (scenario is Scenario1Record grid)
        {
            bool invalidUse = errorType == LogicErrorType.UsoInvalido;

            if (invalidUse) grid.errorInvalidUse++;
            else grid.errorCollisionBot++;

            if (state.runningAttempt != null && state.runningFirstError == null)
            {
                state.runningFirstError = invalidUse ? FailReasons.InvalidUse : FailReasons.Collision;
                state.runningAttempt.failReason = state.runningFirstError;
            }
        }
        else if (scenario is Scenario3Record loops) loops.errorInvalidCommand++;
        else return;

        MarkDirty();
    }

    public void RegisterLogicError(int errorTypeIndex)
    {
        RegisterLogicError((LogicErrorType)errorTypeIndex);
    }

    // --- Pausas ---

    private void OnApplicationPause(bool paused)
    {
        if (!DetectPauses) return;

        DateTime now = DateTime.UtcNow;

        if (paused)
        {
            // Lo primero, antes de abrir la pausa: si el sistema mata la app mientras está en
            // pausa (en Quest, con el visor quitado un rato), no llega OnApplicationQuit y lo
            // único que queda es el archivo. Se escribe como si la run se cerrara ahora, con
            // endReason appClosed, y se sigue en memoria como si nada. Al volver, el siguiente
            // guardado lo sobrescribe.
            SaveClosedSnapshot(EndReasons.AppClosed, now);
        }

        // No se cierra la run: quitarse el visor un momento pausa la aplicación.
        _appPaused = paused;
        RefreshPause(now);

        if (!paused) Save();
    }

    private void OnApplicationFocus(bool focused) => SetPauseSource(ref _windowFocusLost, !focused);

    private void HandleHmdUnmounted() => SetPauseSource(ref _hmdUnmounted, true);
    private void HandleHmdMounted() => SetPauseSource(ref _hmdUnmounted, false);
    private void HandleInputFocusLost() => SetPauseSource(ref _inputFocusLost, true);
    private void HandleInputFocusAcquired() => SetPauseSource(ref _inputFocusLost, false);

    private void SetPauseSource(ref bool source, bool active)
    {
        if (!DetectPauses) return;

        source = active;
        RefreshPause(DateTime.UtcNow);
    }

    /// <summary>Motivo de pausa por prioridad; null si no hay ninguna fuente activa.</summary>
    private string CurrentPauseReason()
    {
        if (_appPaused || _hmdUnmounted) return PauseReasons.HeadsetRemoved;
        if (_inputFocusLost) return PauseReasons.Menu;
        if (_windowFocusLost) return PauseReasons.FocusLost;
        return null;
    }

    /// <summary>
    /// Abre, mejora o cierra la pausa según las fuentes activas. Varias fuentes llegan a la vez
    /// (visor quitado = pausa + foco + HMDUnmounted): es UNA pausa, con el motivo más concreto.
    /// </summary>
    private void RefreshPause(DateTime now)
    {
        if (_run == null) return;

        string reason = CurrentPauseReason();

        if (reason == null)
        {
            ClosePause(now);
            return;
        }

        if (_openPause == null)
        {
            _openPause = new Pause { start = now, reason = reason };
            _runPauses.Add(_openPause);

            ScenarioState open = OpenState();
            if (open != null) open.pauses.Add(new Pause { start = now, reason = reason });

            MarkDirty();
            return;
        }

        if (Priority(reason) <= Priority(_openPause.reason)) return;

        _openPause.reason = reason;
        foreach (ScenarioState state in _states.Values)
            if (state.IsOpen && state.pauses.Count > 0 && !state.pauses[state.pauses.Count - 1].end.HasValue)
                state.pauses[state.pauses.Count - 1].reason = reason;

        MarkDirty();
    }

    private void ClosePause(DateTime now)
    {
        if (_openPause == null) return;

        _openPause.end = now;
        _openPause = null;

        foreach (ScenarioState state in _states.Values)
            if (state.IsOpen && state.pauses.Count > 0 && !state.pauses[state.pauses.Count - 1].end.HasValue)
                state.pauses[state.pauses.Count - 1].end = now;

        MarkDirty();
    }

    private static int Priority(string reason) =>
        reason == PauseReasons.HeadsetRemoved ? 3 : reason == PauseReasons.Menu ? 2 : 1;

    private ScenarioState OpenState()
    {
        foreach (ScenarioState state in _states.Values)
            if (state.IsOpen) return state;
        return null;
    }

    // --- Relojes ---

    /// <summary>
    /// Segundos entre from y to menos lo que se solapa con las pausas (una pausa sin cerrar
    /// cuenta hasta now). Es la única cuenta de tiempo de toda la telemetría.
    /// </summary>
    private static double ActiveSeconds(List<Pause> pauses, DateTime from, DateTime to, DateTime now)
    {
        double total = (to - from).TotalSeconds;

        foreach (Pause pause in pauses)
        {
            DateTime start = pause.start > from ? pause.start : from;
            DateTime end = pause.end ?? now;
            if (end > to) end = to;

            if (end > start) total -= (end - start).TotalSeconds;
        }

        return Math.Max(0d, total);
    }

    /// <summary>durationSeconds: tiempo de reto desde el inicio del ciclo, nunca antes de abrirse el reto.</summary>
    private static float Duration(ScenarioState state, DateTime cycleStart, DateTime now)
    {
        DateTime from = state.challengeStart.HasValue && cycleStart < state.challengeStart.Value
            ? state.challengeStart.Value
            : cycleStart;

        return (float)ActiveSeconds(state.pauses, from, now, now);
    }

    private int NextSeq() => ++_seq;

    // --- Preparación de la sesión desde la pantalla del supervisor ---

    /// <summary>
    /// Deja fijados el PIN y la dificultad para la run que abrirá el TelemetryManager de la
    /// escena de juego. Estático a propósito: la pantalla del supervisor NO necesita un
    /// TelemetryManager propio, que abriría una run huérfana.
    /// </summary>
    public static void PrepareSession(int pin, Difficulty difficulty)
    {
        PlayerPrefs.SetInt(PinCounterKey, pin);
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
        PlayerPrefs.Save();
    }

    /// <summary>Último PIN guardado, sin necesidad de que exista un TelemetryManager.</summary>
    public static int GetLastPin() => PlayerPrefs.GetInt(PinCounterKey, 0);

    /// <summary>Dificultad que dejó elegida la pantalla del supervisor.</summary>
    public static Difficulty GetPreparedDifficulty() =>
        (Difficulty)PlayerPrefs.GetInt(DifficultyPrefKey, (int)Difficulty.Basica);

    // --- Manejo de PIN ---

    /// <summary>Siguiente participante: nuevo PIN y, por tanto, nueva run y nuevo archivo.</summary>
    public void IncrementPin()
    {
        SetPin(PlayerPrefs.GetInt(PinCounterKey, 0) + 1);
    }

    public void SetPin(int pinValue)
    {
        // Cambiar de PIN abandona la run en curso y abre otra vacía, con archivo nuevo.
        if (HasRecordedData() && string.IsNullOrEmpty(_run.endedUtc))
            Debug.LogWarning($"[Telemetry] Se cambia de PIN con la run '{_filePath}' sin cerrar. " +
                             "Se cierra con endReason quit y se abre otra vacía. " +
                             "¿Está IncrementPin antes del upload en algún UnityEvent?");

        QuitRun();

        PlayerPrefs.SetInt(PinCounterKey, pinValue);
        PlayerPrefs.Save();

        BeginRun(pinValue.ToString("D4"));

        Debug.Log($"[Telemetry] Nuevo PIN activo: {_run.pin}");
    }

    /// <summary>Si la run en curso tiene algo que valga la pena conservar.</summary>
    private bool HasRecordedData()
    {
        if (_run == null) return false;

        return _run.escenario1.started || _run.escenario2.started ||
               _run.escenario3.started || _run.escenario4.started;
    }

    // --- Configuración de dificultad (se fija una vez, no por reto) ---

    public void SetDifficulty(Difficulty difficulty)
    {
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
        PlayerPrefs.Save();

        SetRunDifficulty(difficulty);
    }

    /// <summary>
    /// Corrige la dificultad de la run en curso sin tocar la que dejó elegida Setup. Lo usa
    /// DifficultyApplier cuando en el editor se fuerza otra para probar.
    /// </summary>
    public void SetRunDifficulty(Difficulty difficulty)
    {
        _currentDifficulty = difficulty;

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

    /// <summary>Recalcula lo derivado y escribe el archivo.</summary>
    private void Save()
    {
        if (_run == null) return;

        DateTime now = DateTime.UtcNow;
        RefreshDerived(_run, now, null);

        Write(JsonUtility.ToJson(_run, true));
    }

    /// <summary>
    /// Escribe una copia de la run como si se cerrara ahora con ese motivo, sin cerrarla en
    /// memoria. Para la pausa: si la app no vuelve, el archivo ya dice cómo y cuándo terminó.
    /// </summary>
    private void SaveClosedSnapshot(string reason, DateTime now)
    {
        if (_run == null || !string.IsNullOrEmpty(_run.endedUtc)) return;

        RunRecord copy = JsonUtility.FromJson<RunRecord>(JsonUtility.ToJson(_run));

        // Cierre del escenario abierto en la copia: mismo cálculo que EndScenario, con las
        // pausas abiertas terminadas ahora.
        foreach (string id in ScenarioIds)
        {
            ScenarioRecord scenario = GetScenario(copy, id);
            if (!scenario.started || scenario.endReason != EndReasons.Open) continue;

            scenario.endReason = reason;
            scenario.endedUtc = Utc(now);
        }

        RefreshDerived(copy, now, now);
        WriteRunClose(copy, reason, now);

        Write(JsonUtility.ToJson(copy, true));
    }

    /// <summary>
    /// Tiempos, pausas y contadores derivados de cada escenario. Los contadores se calculan de
    /// las listas en vez de llevarse aparte: así failedAttempts, wrongSelections y
    /// wrongPlacements no pueden descuadrar con sus eventos.
    /// </summary>
    /// <param name="endOverride">Fin forzado para los escenarios abiertos (copia de cierre).</param>
    private void RefreshDerived(RunRecord run, DateTime now, DateTime? endOverride)
    {
        if (string.IsNullOrEmpty(run.endedUtc))
            run.totalSeconds = (float)ActiveSeconds(_runPauses, _runStart, now, now);

        foreach (string id in ScenarioIds)
        {
            ScenarioRecord scenario = GetScenario(run, id);
            ScenarioState state = _states[id];

            RefreshTimes(scenario, state, now, state.end ?? endOverride);
        }

        int failed = 0;
        foreach (AttemptRecord attempt in run.escenario1.attempts) if (!attempt.solved) failed++;
        run.escenario1.failedAttempts = failed;

        failed = 0;
        foreach (ModuleAttemptRecord attempt in run.escenario2.attempts) if (!attempt.solved) failed++;
        run.escenario2.failedAttempts = failed;

        int wrong = 0;
        foreach (SelectionRecord selection in run.escenario2.selections) if (selection.selected == 1 && !selection.correct) wrong++;
        run.escenario2.wrongSelections = wrong;

        wrong = 0;
        foreach (PlacementRecord placement in run.escenario4.placements) if (!placement.correct) wrong++;
        run.escenario4.wrongPlacements = wrong;
    }

    /// <summary>totalSeconds, pausedSeconds y pauses[] del escenario, desde su estado de runtime.</summary>
    private static void RefreshTimes(ScenarioRecord scenario, ScenarioState state, DateTime now, DateTime? end)
    {
        scenario.pauses.Clear();

        if (!state.challengeStart.HasValue)
        {
            scenario.totalSeconds = 0f;
            scenario.pausedSeconds = 0f;
            return;
        }

        DateTime from = state.challengeStart.Value;
        DateTime to = end ?? now;
        double paused = 0d;

        foreach (Pause pause in state.pauses)
        {
            DateTime pauseEnd = pause.end ?? (end.HasValue ? to : now);
            if (pauseEnd > to) pauseEnd = to;

            scenario.pauses.Add(new PauseRecord
            {
                startUtc = Utc(pause.start),
                endUtc = pause.end.HasValue || end.HasValue ? Utc(pauseEnd) : string.Empty,
                reason = pause.reason
            });

            if (pauseEnd > pause.start) paused += (pauseEnd - pause.start).TotalSeconds;
        }

        scenario.pausedSeconds = (float)paused;
        scenario.totalSeconds = (float)Math.Max(0d, (to - from).TotalSeconds - paused);
    }

    private void Write(string json)
    {
        // El reintento se espacia igual que una escritura normal, salga bien o mal.
        _nextSaveTime = Time.unscaledTime + saveInterval;

        try
        {
            File.WriteAllText(_filePath, json, Encoding.UTF8);
            _dirty = false;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            // La telemetría no puede romper el juego: Save() se llama desde dentro de los
            // UnityEvent del Director y del botón de ejecutar. Los datos siguen en memoria y se
            // reintenta en el siguiente guardado.
            _dirty = true;
            Debug.LogError($"[Telemetry] No se pudo escribir '{_filePath}': {e.Message}. " +
                           "Se reintentará; la partida sigue.");
        }
    }

    // --- Búsqueda de escenarios ---

    private ScenarioRecord GetScenario(string challengeId) => _run != null ? GetScenario(_run, challengeId) : null;

    private static ScenarioRecord GetScenario(RunRecord run, string challengeId)
    {
        switch (challengeId)
        {
            case Scenario1Id: return run.escenario1;
            case Scenario2Id: return run.escenario2;
            case Scenario3Id: return run.escenario3;
            case Scenario4Id: return run.escenario4;
        }

        return null;
    }

    /// <summary>Registro y estado de un escenario. False, con aviso, si el id no es de ninguno.</summary>
    private bool TryGetState(string challengeId, out ScenarioRecord scenario, out ScenarioState state)
    {
        scenario = GetScenario(challengeId);
        state = null;

        if (scenario != null && _states.TryGetValue(challengeId, out state)) return true;

        if (_run != null)
            Debug.LogWarning($"[Telemetry] '{challengeId}' no es el id de ningún escenario; registro descartado.");

        return false;
    }

    /// <summary>
    /// Como TryGetState, pero solo con el reto abierto. Un evento fuera de su ventana no se
    /// apunta; con <paramref name="what"/> se avisa una vez por escenario y tipo de evento.
    /// </summary>
    private bool TryGetOpen(string challengeId, string what, out ScenarioRecord scenario, out ScenarioState state)
    {
        scenario = null;
        state = null;

        if (_run == null || string.IsNullOrEmpty(challengeId)) return false;
        if (!TryGetState(challengeId, out scenario, out state)) return false;
        if (state.IsOpen) return true;

        if (what != null && _warnedOutsideWindow.Add($"{challengeId}/{what}"))
            Debug.LogWarning($"[Telemetry] {challengeId}: llega {what} con el reto " +
                             $"{(state.end.HasValue ? "ya cerrado" : "todavía sin abrir")}; no se registra.");

        return false;
    }

    /// <summary>Básica = 1, Intermedia = 2. Se guarda 1-based para que el JSON se lea solo.</summary>
    private static int ToDifficultyValue(Difficulty difficulty) => (int)difficulty + 1;

    private static string Utc(DateTime utc) => utc.ToString("o");

    // --- Ciclo de vida ---

    private void OnApplicationQuit()
    {
        CloseRun(EndReasons.AppClosed);
    }

    /// <summary>
    /// La run pertenece a su escena: al descargarla se cierra con ella. SceneLoader ya lo hace
    /// antes de cambiar, pero cualquier otro camino (un LoadScene suelto) perdería lo diferido.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this) return;

        QuitRun();
        Flush();

        Instance = null;
    }

    public string GetCurrentFilePath() => _filePath;

#if UNITY_EDITOR
    // --- Utilidades de prueba (fuera del build) ---

    /// <summary>Rellena escenario1 con una partida verosímil: tres intentos, el último resuelto.</summary>
    [ContextMenu("Test/1 - Simular datos del escenario 1")]
    private void SimulateScenario1Data()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Telemetry] Entra en Play Mode primero: la run se inicializa en Awake().");
            return;
        }

        StartChallenge(Scenario1Id);

        // Intento 1: se queda corto y choca contra un muro.
        SimulateBlockHandling(2);
        RegisterBlockAttempt(Scenario1Id, new List<string> { "Avanzar", "Girar Derecha" });
        RegisterLogicError(LogicErrorType.ColisionBot);
        FinishBlockRun(Scenario1Id);
        RegisterFailedAttempt();

        // Intento 2: llega al final pero usa sobre una casilla vacía.
        SimulateBlockHandling(3);
        RegisterBlockAttempt(Scenario1Id, new List<string> { "Avanzar 2", "Girar Izquierda", "Usar" });
        RegisterLogicError(LogicErrorType.UsoInvalido);
        FinishBlockRun(Scenario1Id);
        RegisterFailedAttempt();

        // Intento 3: resuelve. CompleteChallenge le pondrá solved = true.
        SimulateBlockHandling(4);
        RegisterBlockAttempt(Scenario1Id, new List<string> { "Avanzar", "Girar Derecha", "Avanzar 2", "Usar" });

        CompleteChallenge(Scenario1Id);
        EndRun();

        Debug.Log($"[Telemetry] Datos simulados escritos en:\n{_filePath}");
    }

    private void SimulateBlockHandling(int blocks)
    {
        for (int i = 0; i < blocks; i++)
        {
            RegisterBlockGrabbed();
            RegisterBlockConnected();
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

        JSONUploader uploader = FindAnyObjectByType<JSONUploader>();

        if (uploader == null)
        {
            Debug.LogError("[Telemetry] No hay ningún JSONUploader en la escena.");
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

        Save();
        Debug.Log($"[Telemetry] {_filePath}\n{JsonUtility.ToJson(_run, true)}");
    }
#endif
}
