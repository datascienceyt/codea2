using System.Collections;
using UnityEngine;

public class StartBlock : BlockNode
{
    public override IEnumerator Execute()
    {
        print("Iniciando.");
        yield break;
    }
}
