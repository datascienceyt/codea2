using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El socket del argumento: qué tipo de objeto va a mover el brazo en esta ejecución.
///
/// Va **fuera** del SocketRow a propósito, y no solo por estética. SocketRow.SetEditable()
/// bloquea la fila entera de golpe, así que un socket dentro de ella quedaría bloqueado junto
/// a las instrucciones; separándolo, el tipo se puede seguir cambiando entre ejecuciones
/// aunque la secuencia esté fija, que es justo lo que pide la dificultad básica.
///
/// Implementa IRunPrecondition: sin ficha puesta el programa no llega a ejecutarse.
/// </summary>
[RequireComponent(typeof(Socket))]
public class ArmTypeSocket : MonoBehaviour, IRunPrecondition
{
    [Tooltip("Se dispara al intentar ejecutar sin ficha de tipo. Para el aviso visual o sonoro.")]
    public UnityEvent OnMissingType;

    private Socket socket;

    public bool HasType => Chip != null;

    /// <summary>Tipo seleccionado. Solo tiene sentido si HasType.</summary>
    public ArmItemType SelectedType => Chip != null ? Chip.Type : default;

    /// <summary>
    /// Con qué tipo se ejecutó el programa. Va a la telemetría porque sin él dos intentos con
    /// la misma secuencia y las mismas repeticiones son indistinguibles, y son retos distintos.
    /// </summary>
    public string RunArgument => HasType ? SelectedType.ToString() : null;

    private ArmTypeChip Chip => socket != null ? socket.CurrentBlock as ArmTypeChip : null;

    private void Awake() => socket = GetComponent<Socket>();

    /// <summary>
    /// Un programa sin su argumento está incompleto: no se ejecuta y tampoco cuenta como
    /// intento. Sí se registra como error de lógica, que es lo que realmente ocurrió.
    /// </summary>
    public bool CanRun()
    {
        if (HasType) return true;

        Debug.LogWarning($"[Escenario3] '{name}' no tiene ficha de tipo: el programa no se " +
                         "ejecuta porque no sabe qué debe mover.", this);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);

        OnMissingType?.Invoke();

        return false;
    }
}
