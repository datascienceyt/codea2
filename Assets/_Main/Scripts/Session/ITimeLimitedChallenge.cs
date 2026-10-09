/// <summary>
/// Un reto con tiempo límite. Lo implementan los cuatro controladores de escenario y lo usa
/// ScenarioTimeLimit para cerrarlos cuando se agotan sus minutos.
/// </summary>
public interface ITimeLimitedChallenge
{
    /// <summary>Id del reto en la telemetría ("escenario1"...).</summary>
    string ChallengeId { get; }

    /// <summary>Si el niño ya lo resolvió.</summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Si hay un intento ejecutándose (el robot o el brazo moviéndose). Al agotarse el tiempo
    /// se le deja terminar: puede ser justo el que resuelve el reto.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Da el reto por terminado sin resolver: suelta la espera del Director para que la
    /// historia siga. La telemetría la cierra ScenarioTimeLimit, no el reto.
    /// </summary>
    void TimeOut();
}
