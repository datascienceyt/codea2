// TelemetryManager.cs
using System;
using System.Globalization;
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

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    private const string CsvHeader =
        "session_id,pin,challenge_id,difficulty,start_timestamp_utc,end_timestamp_utc,duration_seconds," +
        "resets_total,session_result," +
        "blocks_connected_total,final_sequence_length,executions_requested," +
        "error_colision_bot,error_secuencia_incompleta,error_comando_invalido";

    private const string FileName = "telemetry.csv";
    private const string SessionCounterKey = "telemetry_session_counter";
    private const string PinCounterKey = "telemetry_pin_counter";
    private const string DifficultyPrefKey = "telemetry_difficulty";

    private int _sessionId;
    private string _currentPin;
    private Difficulty _currentDifficulty;
    private string _filePath;

    // --- Estado del intento actual ---
    private string _currentChallengeId;
    private float _attemptStartTime;
    private DateTime _attemptStartTimeUtc;

    private int _resetsTotal;
    private int _blocksConnectedTotal;
    private int _finalSequenceLength;
    private int _executionsRequested;
    private int _errorCollisionBot;
    private int _errorIncompleteSequence;
    private int _errorInvalidCommand;

    private bool _attemptInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // ID secuencial persistido: sigue subiendo aunque la app se cierre o el headset se reinicie
        _sessionId = PlayerPrefs.GetInt(SessionCounterKey, 0) + 1;
        PlayerPrefs.SetInt(SessionCounterKey, _sessionId);
        PlayerPrefs.Save();

        // PIN persistido, formato de 4 dígitos
        int pinValue = PlayerPrefs.GetInt(PinCounterKey, 0);
        _currentPin = pinValue.ToString("D4");

        // Dificultad: configuración fija de la sesión, no varía por reto
        int difficultyValue = PlayerPrefs.GetInt(DifficultyPrefKey, (int)Difficulty.Basica);
        _currentDifficulty = (Difficulty)difficultyValue;

        _filePath = Path.Combine(Application.persistentDataPath, FileName);

        // Solo crea el archivo y el header si no existe (nunca se borra ni se sobreescribe)
        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, CsvHeader + "\n", Encoding.UTF8);
    }

    // --- Manejo de PIN ---

    public void IncrementPin()
    {
        int pinValue = PlayerPrefs.GetInt(PinCounterKey, 0) + 1;
        PlayerPrefs.SetInt(PinCounterKey, pinValue);
        PlayerPrefs.Save();

        _currentPin = pinValue.ToString("D4");

        Debug.Log($"[Telemetry] Nuevo PIN activo: {_currentPin}");
    }

    public void SetPin(int pinValue)
    {
        PlayerPrefs.SetInt(PinCounterKey, pinValue);
        PlayerPrefs.Save();

        _currentPin = pinValue.ToString("D4");
    }

    public string GetCurrentPin() => _currentPin;

    // --- Configuración de dificultad (se fija una vez, no por reto) ---

    public void SetDifficulty(Difficulty difficulty)
    {
        _currentDifficulty = difficulty;
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
        PlayerPrefs.Save();

        Debug.Log($"[Telemetry] Dificultad configurada: {_currentDifficulty}");
    }

    public void SetDifficulty(int difficultyIndex)
    {
        SetDifficulty((Difficulty)difficultyIndex);
    }

    public Difficulty GetCurrentDifficulty() => _currentDifficulty;

    // --- Ciclo de intento ---

    public void StartChallenge(string challengeId)
    {
        _currentChallengeId = challengeId;
        _attemptStartTime = Time.time;
        _attemptStartTimeUtc = DateTime.UtcNow;

        _resetsTotal = 0;
        _blocksConnectedTotal = 0;
        _finalSequenceLength = 0;
        _executionsRequested = 0;
        _errorCollisionBot = 0;
        _errorIncompleteSequence = 0;
        _errorInvalidCommand = 0;

        _attemptInProgress = true;
    }

    public void EndChallenge(SessionResult result)
    {
        if (!_attemptInProgress) return;

        float duration = Time.time - _attemptStartTime;
        WriteRow(result, duration);
        _attemptInProgress = false;
    }

    public void EndChallenge(int resultIndex)
    {
        EndChallenge((SessionResult)resultIndex);
    }

    // --- RF-03: reinicios ---

    public void RegisterReset()
    {
        if (!_attemptInProgress) return;
        _resetsTotal++;
    }

    // --- RF-04: interacción lógica ---

    public void RegisterBlockConnected()
    {
        if (!_attemptInProgress) return;
        _blocksConnectedTotal++;
    }

    public void RegisterExecutionRequested()
    {
        if (!_attemptInProgress) return;
        _executionsRequested++;
    }

    public void RegisterLogicError(LogicErrorType errorType)
    {
        if (!_attemptInProgress) return;

        switch (errorType)
        {
            case LogicErrorType.ColisionBot:
                _errorCollisionBot++;
                break;
            case LogicErrorType.SecuenciaIncompleta:
                _errorIncompleteSequence++;
                break;
            case LogicErrorType.ComandoInvalido:
                _errorInvalidCommand++;
                break;
        }
    }

    public void RegisterLogicError(int errorTypeIndex)
    {
        RegisterLogicError((LogicErrorType)errorTypeIndex);
    }

    public void ResetFinalSequenceLenght()
    {
        _finalSequenceLength = 0;
    }

    public void IncrementFinalSequenceLenght()
    {
        _finalSequenceLength++;
    }

    // --- Escritura (append, nunca sobreescribe ni borra) ---

    private void WriteRow(SessionResult result, float duration)
    {
        string row = string.Join(",",
            _sessionId,
            _currentPin,
            _currentChallengeId,
            _currentDifficulty.ToString().ToLowerInvariant(),
            _attemptStartTimeUtc.ToString("o"),
            DateTime.UtcNow.ToString("o"),
            duration.ToString("F1", CultureInfo.InvariantCulture),
            _resetsTotal,
            result.ToString().ToLowerInvariant(),
            _blocksConnectedTotal,
            _finalSequenceLength,
            _executionsRequested,
            _errorCollisionBot,
            _errorIncompleteSequence,
            _errorInvalidCommand
        );

        File.AppendAllText(_filePath, row + "\n", Encoding.UTF8);
    }

    // --- Ciclo de vida ---

    private void OnApplicationPause(bool paused)
    {
        if (paused && _attemptInProgress)
            EndChallenge(SessionResult.Abandonado);
    }

    private void OnApplicationQuit()
    {
        if (_attemptInProgress)
            EndChallenge(SessionResult.Abandonado);
    }

    public string GetCurrentFilePath() => _filePath;
}