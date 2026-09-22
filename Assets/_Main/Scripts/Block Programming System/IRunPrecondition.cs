/// <summary>
/// Condición que puede vetar la ejecución de un programa, y que además aporta el dato con el
/// que se ejecutó.
///
/// La comprueba ProgramTrigger antes de registrar el intento y antes de arrancar: un programa
/// al que le falta un argumento no se ejecuta, y tampoco cuenta como intento, porque no llegó
/// a probarse ninguna solución.
///
/// Las dos cosas van juntas a propósito. Quien exige el dato es quien lo tiene, así que puede
/// avisar, registrar el error y decir con qué valor se corrió, sin repartir esa información
/// entre varios scripts.
/// </summary>
public interface IRunPrecondition
{
    /// <summary>False para impedir la ejecución. Quien implementa avisa y registra el error.</summary>
    bool CanRun();

    /// <summary>
    /// Con qué se ejecutó, para la telemetría. Null o vacío si esta condición no aporta ningún
    /// dato y solo sirve de veto.
    /// </summary>
    string RunArgument { get; }
}
