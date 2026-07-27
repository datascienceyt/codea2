using System.Collections;
using UnityEngine;

public class ProgramRunner : MonoBehaviour
{
    public bool IsRunning { get; private set; }

    public IEnumerator Run(BlockNode start)
    {
        IsRunning = true;
        var current = start;
        while (current != null)
        {
            yield return current.Execute();
            current = current.Next;
        }
        IsRunning = false;
    }
}