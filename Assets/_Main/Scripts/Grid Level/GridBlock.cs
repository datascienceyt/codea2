using System.Collections;

public enum GridBlockType { MoveForward, RotateLeft, RotateRight }

public class GridBlock : BlockNode
{
    public GridBlockType blockType;
    public Bot bot;

    public override IEnumerator Execute()
    {
        switch (blockType)
        {
            case GridBlockType.MoveForward: yield return bot.MoveForward(); break;
            case GridBlockType.RotateLeft: yield return bot.RotateLeft(); break;
            case GridBlockType.RotateRight: yield return bot.RotateRight(); break;
        }
    }
}