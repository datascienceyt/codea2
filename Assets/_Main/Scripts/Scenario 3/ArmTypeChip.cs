using System.Collections;
using UnityEngine;

/// <summary>
/// Ficha de tipo del Escenario 3: el argumento del programa.
///
/// No es una instrucción. No se ejecuta y no vive en la fila: va en un socket aparte, y lo
/// único que hace es decidir de qué pila coge el brazo. Es el hueco de argumento de un bloque
/// de programación por bloques, y por eso está separado de la secuencia — la forma del
/// programa se queda igual y lo que cambia es el dato.
///
/// Hereda de BlockNode como ShapeChip: reutiliza el agarre, el acople al socket más cercano,
/// el reinicio por BlockResetter y el conteo de manipulación en telemetría, con Execute()
/// vacío a propósito.
/// </summary>
public class ArmTypeChip : BlockNode
{
    [Header("Tipo")]
    [SerializeField] private ArmItemType type = ArmItemType.Barril;

    public ArmItemType Type => type;

    public override string InstructionLabel => type.ToString();

    /// <summary>El socket de tipo no ejecuta nada: solo se consulta qué ficha tiene puesta.</summary>
    public override IEnumerator Execute()
    {
        yield break;
    }
}
