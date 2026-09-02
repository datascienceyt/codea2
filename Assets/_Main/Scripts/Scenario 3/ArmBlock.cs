using System.Collections;
using UnityEngine;

// Los valores nuevos van SIEMPRE al final: el índice se serializa en los bloques de la
// escena, así que insertar en medio les cambiaría la acción.
public enum ArmActionType { Pick, Drop, RotateLeft, RotateRight }

/// <summary>
/// Bloque de acción del Escenario 3. Mismo patrón que GridBlock: enum + switch en vez de
/// una clase por acción.
/// </summary>
public class ArmBlock : BlockNode
{
    public ArmActionType action;

    public override string InstructionLabel => action switch
    {
        ArmActionType.Pick => "Recoger",
        ArmActionType.Drop => "Soltar",
        ArmActionType.RotateLeft => "Girar Izquierda",
        ArmActionType.RotateRight => "Girar Derecha",
        _ => action.ToString()
    };

    public override IEnumerator Execute()
    {
        RoboticArm arm = FindAnyObjectByType<RoboticArm>();
        if (arm == null) yield break;

        switch (action)
        {
            case ArmActionType.Pick:
                yield return arm.Pick();
                break;
            case ArmActionType.Drop:
                yield return arm.Drop();
                break;
            case ArmActionType.RotateLeft:
                yield return arm.RotateLeft();
                break;
            case ArmActionType.RotateRight:
                yield return arm.RotateRight();
                break;
        }
    }
}
