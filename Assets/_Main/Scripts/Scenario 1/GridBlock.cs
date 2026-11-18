using System.Collections;

// Los valores nuevos van SIEMPRE al final: el índice se serializa en los bloques
// de la escena, así que insertar en medio les cambiaría la acción.
public enum GridActionType { MoveForward, RotateLeft, RotateRight, Use, MoveForwardTwice }

public class GridBlock : BlockNode
{
    public GridActionType action;

    public override string InstructionLabel => action switch
    {
        GridActionType.MoveForward => "Avanzar",
        GridActionType.MoveForwardTwice => "Avanzar 2",
        GridActionType.RotateLeft => "Girar Izquierda",
        GridActionType.RotateRight => "Girar Derecha",
        GridActionType.Use => "Usar",
        _ => action.ToString()
    };

    public override IEnumerator Execute()
    {
        Bot bot = FindAnyObjectByType<Bot>();
        if (bot == null) yield break;

        switch (action)
        {
            case GridActionType.MoveForward:
                yield return bot.MoveForward();
                break;
            case GridActionType.MoveForwardTwice:
                // Dos avances de una celda, no un salto de dos: cada paso revalida el grid,
                // así que si hay un muro en medio el bot se detiene donde corresponde.
                yield return bot.MoveForward();
                yield return bot.MoveForward();
                break;
            case GridActionType.RotateLeft:
                yield return bot.RotateLeft();
                break;
            case GridActionType.RotateRight:
                yield return bot.RotateRight();
                break;
            case GridActionType.Use:
                yield return bot.Use();
                break;
        }
    }
}