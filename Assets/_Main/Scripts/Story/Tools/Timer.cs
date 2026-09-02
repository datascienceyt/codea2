using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Cuenta atrás de sesión (RF-06). Se maneja por funciones (StartTimer / Pause / Continue /
/// Stop), pensadas para engancharse a los instantEvents de un Step o a cualquier UnityEvent.
///
/// Si se le asigna un Text lo va mostrando; si se deja vacío cuenta en privado y el tiempo
/// se consulta desde código con Elapsed y Remaining.
/// </summary>
public class Timer : MonoBehaviour
{
    [Serializable]
    public class TimerAlert
    {
        [Tooltip("Segundos restantes a los que salta el aviso. Para 5 minutos, 300.")]
        public int remainingSeconds = 300;

        [Tooltip("Narración, parpadeo del HUD, sonido...")]
        public UnityEvent onReached;

        [NonSerialized] public bool fired;
    }

    [Header("Visualización")]
    [Tooltip("Opcional. Si queda vacío el temporizador cuenta en privado, sin mostrar nada. " +
             "Para encender/apagar el canvas entero usa Tools.SetActive / SetInactive.")]
    [SerializeField] private Text display;

    [Header("Tiempo límite")]
    [Tooltip("Tiempo límite en segundos. 900 = 15 minutos.")]
    [SerializeField] private int timeLimit = 900;

    [Header("Formato")]
    [Tooltip("Muestra hh:mm:ss en vez de mm:ss.")]
    [SerializeField] private bool showHours = false;

    [Header("Eventos")]
    [Tooltip("Avisos por tiempo restante (RF-06: 15, 10 y 5 minutos).")]
    [SerializeField] private List<TimerAlert> alerts = new List<TimerAlert>();

    [Tooltip("Se dispara UNA vez al agotarse el tiempo. Aquí va el cierre de sesión: " +
             "guardar la telemetría y volver a la pantalla inicial.")]
    public UnityEvent OnTimeUp;

    /// <summary>Segundos acumulados. No avanza mientras está pausado.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Segundos que quedan. Nunca baja de cero.</summary>
    public float Remaining => Mathf.Max(0f, timeLimit - Elapsed);

    public bool IsRunning { get; private set; }

    /// <summary>True desde que se agota el tiempo hasta el siguiente StartTimer/Stop.</summary>
    public bool TimeIsUp { get; private set; }

    private void Start() => Refresh();

    private void Update()
    {
        if (!IsRunning) return;

        Elapsed += Time.deltaTime;
        Refresh();

        CheckAlerts();

        if (Elapsed >= timeLimit)
            TriggerTimeUp();
    }

    /// <summary>Pone el contador a cero y arranca.</summary>
    [ContextMenu("Start")]
    public void StartTimer()
    {
        Elapsed = 0f;
        IsRunning = true;
        TimeIsUp = false;

        ResetAlerts();
        Refresh();
    }

    /// <summary>Detiene la cuenta conservando el tiempo acumulado.</summary>
    [ContextMenu("Pause")]
    public void Pause() => IsRunning = false;

    /// <summary>Reanuda desde donde se pausó. No hace nada si el tiempo ya se agotó.</summary>
    [ContextMenu("Continue")]
    public void Continue()
    {
        if (TimeIsUp) return;

        IsRunning = true;
    }

    /// <summary>Detiene la cuenta y la devuelve a cero.</summary>
    [ContextMenu("Stop")]
    public void Stop()
    {
        IsRunning = false;
        TimeIsUp = false;
        Elapsed = 0f;

        ResetAlerts();
        Refresh();
    }

    /// <summary>Permite fijar el límite desde el inspector o por evento.</summary>
    public void SetTimeLimit(int seconds)
    {
        timeLimit = Mathf.Max(0, seconds);
        Refresh();
    }

    private void TriggerTimeUp()
    {
        // Una sola vez: sin este guardia se dispararía en cada frame posterior al límite.
        if (TimeIsUp) return;

        TimeIsUp = true;
        IsRunning = false;
        Elapsed = timeLimit;

        Refresh();
        OnTimeUp?.Invoke();
    }

    private void CheckAlerts()
    {
        float remaining = Remaining;

        foreach (TimerAlert alert in alerts)
        {
            if (alert == null || alert.fired) continue;
            if (remaining > alert.remainingSeconds) continue;

            alert.fired = true;
            alert.onReached?.Invoke();
        }
    }

    private void ResetAlerts()
    {
        foreach (TimerAlert alert in alerts)
            if (alert != null) alert.fired = false;
    }

    private void Refresh()
    {
        if (display == null) return;

        display.text = Format(Remaining);
    }

    /// <summary>
    /// Formatea segundos restantes. Antes se calculaba 'timeLimit - Elapsed' sin acotar, y al
    /// pasarse del límite la división entera producía cosas como "00:-1" en pantalla.
    /// </summary>
    private string Format(float remainingSeconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

        if (showHours)
            return $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";

        return $"{total / 60:00}:{total % 60:00}";
    }
}
