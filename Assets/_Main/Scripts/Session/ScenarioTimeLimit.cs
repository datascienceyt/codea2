using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tiempo límite por escenario: cada sala tiene sus minutos (5 por defecto) y, si se agotan
/// sin resolverla, la historia pasa a la siguiente. Así todo niño llega a jugar las cuatro.
/// Sustituye al límite único de 15 minutos para la sesión entera, con el que quien se atascaba
/// en una sala no veía las demás.
///
/// Cómo funciona:
///   · El reloj (Timer, con sus cinco pantallas) se reinicia a 5:00 cuando un reto empieza de
///     verdad (TelemetryManager.ChallengeStarted) y se pausa cuando termina
///   · Al llegar a cero, si hay un intento ejecutándose (robot o brazo, salas 1 y 3) se le deja
///     terminar: puede ser el que resuelve la sala. Si la resuelve, no pasa nada más
///   · Si no, el reto se cierra en la telemetría con timedOut = true y se suelta la espera del
///     Director (ITimeLimitedChallenge.TimeOut). La frase de felicitación que venía después se
///     sustituye por timeUpLine, o se omite si está vacía; puertas y traslados siguen igual
///   · En el último escenario no hay sala siguiente: se cierra la sesión con TimeUpSequence,
///     que dice su propia frase (la 20) y vuelve a la pantalla del supervisor
/// </summary>
public class ScenarioTimeLimit : MonoBehaviour
{
    [Header("Tiempo")]
    [Tooltip("Tiempo de cada escenario, en segundos. 300 = 5 minutos.")]
    [SerializeField] private int secondsPerScenario = 300;

    [Tooltip("El reloj de la sesión. Sus pantallas (TimerDisplay) muestran el tiempo de la sala.")]
    [SerializeField] private Timer timer;

    [Header("Al agotarse en una sala que no es la última")]
    [Tooltip("Frase que suena en lugar de la de felicitación de la sala. Clip y textId del CSV. " +
             "Vacía (sin clip ni textId): no suena nada y la historia sigue.")]
    [SerializeField] private NarrationEntry timeUpLine = new NarrationEntry();

    [Tooltip("El Narrator de la historia.")]
    [SerializeField] private Narrator narrator;

    [Header("Al agotarse en la última sala")]
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

    public int SecondsPerScenario => secondsPerScenario;

    /// <summary>Reto cuyo tiempo corre ahora. Null antes del primero.</summary>
    public string CurrentChallengeId => current?.ChallengeId;

    private void Awake()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is ITimeLimitedChallenge challenge && !string.IsNullOrEmpty(challenge.ChallengeId) &&
                !challenges.ContainsKey(challenge.ChallengeId))
                challenges[challenge.ChallengeId] = challenge;

        if (timer == null)
            Debug.LogError($"[Tiempo] '{name}' no tiene Timer: no habrá límite por escenario.", this);
        else
            timer.SetTimeLimit(secondsPerScenario); // los relojes muestran 05:00 desde el principio
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

        // Desde código y no cableado: si se olvidara, las salas no tendrían límite.
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

    private void HandleChallengeStarted(string challengeId)
    {
        if (!challenges.TryGetValue(challengeId, out ITimeLimitedChallenge challenge) || challenge.IsCompleted) return;

        current = challenge;
        closing = false;

        if (timer == null) return;
        timer.SetTimeLimit(secondsPerScenario);
        timer.StartTimer();
    }

    private void HandleChallengeEnded(string challengeId)
    {
        if (current == null || current.ChallengeId != challengeId) return;

        // Pausado y no a cero: entre salas los relojes muestran lo que le sobró al niño.
        if (timer != null) timer.Pause();
    }

    private void HandleTimeUp()
    {
        if (current == null || closing) return;

        StartCoroutine(Close(current));
    }

    private IEnumerator Close(ITimeLimitedChallenge challenge)
    {
        closing = true;

        // El intento en curso termina: el robot llega o no a la meta, el brazo suelta o no.
        if (challenge.IsBusy)
            Debug.Log($"[Tiempo] {challenge.ChallengeId}: tiempo agotado; se espera a que termine el intento en curso.");

        while (challenge.IsBusy) yield return null;

        // Un frame más: el reto se da por resuelto dentro de la propia ejecución y su aviso
        // puede llegar justo al terminar.
        yield return null;

        if (challenge.IsCompleted)
        {
            Debug.Log($"[Tiempo] {challenge.ChallengeId}: el último intento lo resolvió a tiempo.");
            yield break;
        }

        Debug.Log($"[Tiempo] {challenge.ChallengeId}: {secondsPerScenario / 60f:0.#} min agotados sin resolver; " +
                  "se pasa a la siguiente sala.");

        if (telemetry != null) telemetry.TimeOutChallenge(challenge.ChallengeId);

        OnScenarioTimeUp?.Invoke();

        if (challenge.ChallengeId == lastChallengeId && sessionTimeUp != null)
        {
            sessionTimeUp.Begin();
            yield break;
        }

        // Antes de soltar la espera: la siguiente frase del Director es la de felicitación.
        if (narrator != null) narrator.ReplaceNextLine(timeUpLine);

        challenge.TimeOut();
    }

    /// <summary>Para probar sin esperar los cinco minutos.</summary>
    [ContextMenu("Agotar el tiempo de la sala actual")]
    public void ForceTimeUp() => HandleTimeUp();
}
