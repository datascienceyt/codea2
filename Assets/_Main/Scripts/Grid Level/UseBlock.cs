using System.Collections;
using UnityEngine;

public class UseBlock : BlockNode
{
    public override IEnumerator Execute()
    {
        Bot bot = FindAnyObjectByType<Bot>();
        if (bot == null) yield break;

        yield return bot.Use();
    }
}