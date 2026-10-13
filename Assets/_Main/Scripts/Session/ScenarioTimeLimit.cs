using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Los dos límites de tiempo de la sesión:
///
///   · El de la SESIÓN, 15 minutos, que es el que se ve en los relojes (Timer y sus
///     TimerDisplay). Corre solo mientras hay un reto abierto: se pausa en narraciones,
///     puertas y traslados. Al llegar a cero se termina la experiencia
///   · El de cada SALA, 5 minutos, que no se ve. Existe para que nadie se quede atascado: si
///     se agotan sin resolver la sala, la historia pasa a la siguiente
///
/// Con cuatro salas, quien agote las tres primeras consume los 15 minutos y la sesión se
/// cierra ahí, sin cuarta sala.
///
/// Cómo funciona:
///   · Al empezar un reto de verdad (TelemetryManager.ChallengeStarted) arranca la cuenta de
///     su sala y el reloj visible arranca o sigue; al terminar, las dos se pausan
///   · Al agotarse cualquiera de las dos, si hay un intento ejecutándose (robot o brazo, salas
///     1 y 3) se le deja terminar: puede ser el que resuelve la sala
///   · Sala agotada, con tiempo de sesión: el reto se cierra en la telemetría con timedOut =
///     true y se suelta la espera del Director (ITimeLimitedChallenge.TimeOut). La frase de
///     felicitación que venía después se sustituye por timeUpLine, o se omite si está vacía
///   · Sesión agotada, o agotada la última sala: se cierra la sesión con TimeUpSequence, que
///     dice su propia frase (la 20) y vuelve a la pantalla del supervisor
/// </summary>
public class ScenarioTimeLimit : MonoBehaviour
{
    [Header("Tiempo")]
    [Tooltip("Tiempo de toda la sesión, en segundos: el que muestran los relojes. 900 = 15 minutos. " +
             "Solo corre durante los retos. Al agotarse se termina la experiencia.")]
    [SerializeField] private int sessionSeconds = 900;

    [Tooltip("Tiempo máximo de cada escenario, en segundos. 300 = 5 minutos. No se muestra: al " +
             "agotarse, la historia pasa a la sala siguiente.")]
    [SerializeField] private int secondsPerScenario = 300;

    [Tooltip("El reloj de la sesión. Sus pantallas (TimerDisplay) muestran lo que queda de ella.")]
    [SerializeField] private Timer timer;

    [Tooltip("Segundos máximos que se espera, con el tiempo ya agotado, a que termine el intento " +
             "en curso (robot o brazo). Pasado ese margen la sala se cierra igual.")]
    [SerializeField] private float maxBusyWaitSeconds = 90f;

    [Header("Al agotarse una sala, con tiempo de sesión")]
    [Tooltip("Frase que suena en lugar de la de felicitación de la sala. Clip y textId del CSV. " +
             "Vacía (sin clip ni textId): no suena nada y la historia sigue.")]
    [SerializeField] private NarrationEntry timeUpLine = new NarrationEntry();

    [Tooltip("El Narrator de la historia.")]
    [SerializeField] private Narrator narrator;

    [Header("Al agotarse la sesión o la última sala")]
    [Tooltip("Cierre de la sesión, con su frase de tiempo agotado (la 20) y la vuelta a Setup.")]
    [SerializeField] private TimeUpSequence sessionTimeUp;

    [Tooltip("Reto tras el que ya no hay sala siguiente.")]
    [SerializeField] private string lastChallengeId = TelemetryManager.Scenario4Id;

    [Header("Eventos")]
    [Tooltip("Al cerrarse una sala por tiempo: luces, sonido...")]
    public UnityEvent OnScenarioTimeUp;

    private readonly Dictionary<string, ITimeLimitedChallenge> challenges = new Dictionary<string, ITimeLimitedChallenge>();
    private ITimeLimitedChallenge current;
    private bool closing;
    private TelemetryManager telemetry;

    // Cuenta de la sala en curso. Va aparte del Timer, que lleva la de la sesión.
    private float roomElapsed;
    private bool roomRunning;
    private bool sessionClockStarted;

    public int SessionSeconds => sessionSeconds;
    public int SecondsPerScenario => secondsPerScenario;

    /// <summary>Segundos que le quedan a la sala en curso. Cero si no corre ninguna.</summary>
    public float RoomRemaining => roomRunning ? Mathf.Max(0f, secondsPerScenario - roomElapsed) : 0f;

    /// <summary>Reto cuyo tiempo corre ahora. Null antes del primero.</summary>
    public string CurrentChallengeId => current?.ChallengeId;

    private void Awake()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is ITimeLimitedChallenge challenge && !string.IsNullOrEmpty(challenge.ChallengeId) &&
                !challenges.ContainsKey(challenge.ChallengeId))
                challenges[challenge.ChallengeId] = challenge;

        if (timer == null)
            Debug.LogError($"[Tiempo] '{name}' no tiene Timer: los relojes no mostrarán el tiempo de la " +
                           "sesión y esta no se cerrará al agotarse. El límite por sala sí funciona.", this);
        else
            timer.SetTimeLimit(sessionSeconds); // los relojes muestran 15:00 desde el principio
    }

    // Start y no Awake: TelemetryManager se crea en su propio Awake.
    private void Start()
    {
        telemetry = TelemetryManager.Instance;
        if (telemetry != null)
        {
            telemetry.ChallengeStarted += HandleChallengeStarted;
            telemetry.ChallengeEnded += HandleChallengeEnded;
        }
        else
        {
            Debug.LogWarning("[Tiempo] Sin TelemetryManager: no se sabe cuándo empieza cada sala.", this);
        }

        // Desde código y no cableado: si se olvidara, la sesión no tendría límite.
        if (timer != null) timer.OnTimeUp.AddListener(HandleTimeUp);
    }

    private void OnDestroy()
    {
        if (telemetry != null)
        {
            telemetry.ChallengeStarted -= HandleChallengeStarted;
            telemetry.ChallengeEnded -= HandleChallengeEnded;
        }

        if (timer != null) timer.OnTimeUp.RemoveListener(HandleTimeUp);
    }

    private void Update()
    {
        if (!roomRunning || closing) return;

        roomElapsed += Time.deltaTime;

        if (roomElapsed >= secondsPerScenario) HandleTimeUp();
    }

    private void HandleChallengeStarted(string challengeId)
    {
        if (!challenges.TryGetValue(challengeId, out ITimeLimitedChallenge challenge) || challenge.IsCompleted) return;

        current = challenge;
        closing = false;
        roomElapsed = 0f;
        roomRunning = true;

        if (timer == null) return;

        // El primer reto pone el reloj de la sesión en marcha desde 15:00; los demás lo
        // reanudan donde lo dejó la sala anterior.
        if (!sessionClockStarted)
        {
            sessionClockStarted = true;
            timer.SetTimeLimit(sessionSeconds);
            timer.StartTimer();
        }
        else
        {
            timer.Continue();
        }
    }

    private void HandleChallengeEnded(string challengeId)
    {
        if (current == null || current.ChallengeId != challengeId) return;

        roomRunning = false;

        // Pausado: entre salas los relojes muestran lo que queda de sesión, sin gastarlo.
        if (timer != null) timer.Pause();
    }

    /// <summary>Se agotó la sala o la sesión. Cuál de las dos se decide al cerrar.</summary>
    private void HandleTimeUp()
    {
        if (current == null || closing) return;

        StartCoroutine(Close(current));
    }

    private IEnumerator Close(ITimeLimitedChallenge challenge)
    {
        closing = true;

        // Mientras se espera al intento en curso no se sigue gastando sesión.
        if (timer != null) timer.Pause();

        // El intento en curso termina: el robot llega o no a la meta, el brazo suelta o no.
        if (challenge.IsBusy)
            Debug.Log($"[Tiempo] {challenge.ChallengeId}: tiempo agotado; se espera a que termine el intento en curso.");

        // Con tope: si el intento se quedara colgado (una excepción dentro de un bloque deja
        // al ProgramRunner "corriendo" para siempre), la sala no terminaría nunca.
        float giveUpAt = Time.time + maxBusyWaitSeconds;

        while (challenge.IsBusy && Time.time < giveUpAt) yield return null;

        if (challenge.IsBusy)
            Debug.LogWarning($"[Tiempo] {challenge.ChallengeId}: el intento en curso no terminó en " +
                             $"{maxBusyWaitSeconds:0} s; se cierra la sala sin esperarlo más.", this);

        // Un frame más: el reto se da por resuelto dentro de la propia ejecución y su aviso
        // puede llegar justo al terminar.
        yield return null;

        bool sessionOver = timer != null && timer.TimeIsUp;
        bool isLast = challenge.ChallengeId == lastChallengeId;

        if (challenge.IsCompleted)
        {
            Debug.Log($"[Tiempo] {challenge.ChallengeId}: el último intento lo resolvió a tiempo.");

            // Resuelta la última sala, la misión está cumplida aunque el reloj marque cero. En
            // cualquier otra, sin tiempo de sesión no hay sala siguiente.
            if (sessionOver && !isLast) EndSession(challenge);

            yield break;
        }

        Debug.Log(sessionOver
            ? $"[Tiempo] {challenge.ChallengeId}: {sessionSeconds / 60f:0.#} min de sesión agotados; se termina la experiencia."
            : $"[Tiempo] {challenge.ChallengeId}: {secondsPerScenario / 60f:0.#} min agotados sin resolver; " +
              "se pasa a la siguiente sala.");

        roomRunning = false;

        if (telemetry != null) telemetry.TimeOutChallenge(challenge.ChallengeId);

        OnScenarioTimeUp?.Invoke();

        if (sessionOver || isLast)
        {
            EndSession(challenge);
            yield break;
        }

        // Antes de soltar la espera: la siguiente frase del Director es la de felicitación.
        if (narrator != null) narrator.ReplaceNextLine(timeUpLine);

        challenge.TimeOut();
    }

    private void EndSession(ITimeLimitedChallenge challenge)
    {
        roomRunning = false;

        if (sessionTimeUp != null)
        {
            sessionTimeUp.Begin();
            return;
        }

        // Sin cierre de sesión montado, al menos la historia no se queda esperando al reto.
        Debug.LogError("[Tiempo] Falta TimeUpSequence en 'Session Time Up': la sesión no se puede cerrar.", this);
        challenge.TimeOut();
    }

    /// <summary>Para probar sin esperar los cinco minutos.</summary>
    [ContextMenu("Agotar el tiempo de la sala actual")]
    public void ForceTimeUp() => HandleTimeUp();
}
