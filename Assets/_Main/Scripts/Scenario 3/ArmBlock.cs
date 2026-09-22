using System.Collections;
using UnityEngine;

// Los valores nuevos van SIEMPRE al final: el índice se serializa en los bloques de la
// escena, así que insertar en medio les cambiaría la acción.
public enum ArmActionType
{
    Pick,
    Drop,
    RotateLeft,
    RotateRight,

    /// <summary>
    /// Gira hacia donde va lo que el brazo tiene puesto en su socket de tipo. Es el giro de la
    /// dificultad básica: ahí el niño no ordena instrucciones ni elige sentidos, así que el
    /// bloque resuelve el destino por sí mismo a partir del argumento.
    /// </summary>
    RotateToDestination,

    /// <summary>Vuelve a la posición de recogida. El regreso del ciclo básico.</summary>
    RotateToOrigin
}

/// <summary>
/// Bloque de acción del Escenario 3. Mismo patrón que GridBlock: enum + switch en vez de
/// una clase por acción.
/// </summary>
public class ArmBlock : BlockNode
{
    public ArmActionType action;

    /// <summary>
    /// El brazo se busca una sola vez y se guarda.
    ///
    /// No es un campo del inspector a propósito: estos bloques son prefabs, y un prefab no
    /// puede guardar una referencia a un objeto de escena. Unity la anula al guardar, en
    /// silencio, y el bloque se quedaría sin brazo sin que nada avise.
    /// </summary>
    private RoboticArm arm;

    public override string InstructionLabel => action switch
    {
        ArmActionType.Pick => "Recoger",
        ArmActionType.Drop => "Soltar",
        ArmActionType.RotateLeft => "Girar Izquierda",
        ArmActionType.RotateRight => "Girar Derecha",
        ArmActionType.RotateToDestination => "Girar al destino",
        ArmActionType.RotateToOrigin => "Volver",
        _ => action.ToString()
    };

    public override IEnumerator Execute()
    {
        // Include: el brazo puede colgar de una estación que el Director todavía no ha
        // activado, y la búsqueda por defecto ignora los objetos inactivos. Antes devolvía
        // null y el bloque hacía yield break sin más: la secuencia entera se ejecutaba sin
        // efecto y sin un solo mensaje en consola.
        if (arm == null)
            arm = FindAnyObjectByType<RoboticArm>(FindObjectsInactive.Include);

        if (arm == null)
        {
            Debug.LogError($"[Escenario3] El bloque '{name}' no encuentra ningún RoboticArm " +
                           "en la escena: la secuencia se ejecuta sin efecto.", this);
            yield break;
        }

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
            case ArmActionType.RotateToDestination:
                yield return arm.RotateToDestination();
                break;
            case ArmActionType.RotateToOrigin:
                yield return arm.RotateToOrigin();
                break;
        }
    }
}
