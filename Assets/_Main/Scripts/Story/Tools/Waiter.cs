using System.Collections;
using UnityEngine;

public class Waiter : MonoBehaviour, IStepAction
{
    [SerializeField] public float seconds = 1f;

    public IEnumerator Execute()
    {
        yield return StartCoroutine(Wait());
    }

    public void SetSeconds(float newSeconds)
    {
        seconds = Mathf.Max(0, newSeconds);
    }

    public IEnumerator Wait()
    {
        yield return new WaitForSeconds(seconds);
    }
}
