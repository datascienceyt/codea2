using System.Collections;
using UnityEngine;

public class StartBlock : BlockNode
{
    public override IEnumerator Execute()
    {
        print("Iniciando.");
        yield break;
    }

    public void Clear()
    {
        foreach (Transform transform in transform.GetComponentInChildren<Transform>())
        {
            if(transform.CompareTag("Block"))
                Destroy(transform);
        }
    }
}
