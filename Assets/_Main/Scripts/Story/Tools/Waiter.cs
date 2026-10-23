using System.Collections;
using UnityEngine;

/// <summary>
/// Espera unos segundos. Como acción de un paso del Director, lo retiene ese tiempo; los
/// segundos se fijan desde los instantEvents del mismo paso con SetSeconds.
/// </summary>
public class Waiter : MonoBehaviour, IStepAction
{
    public float seconds = 1f;

    public IEnumerator Execute() => Wait();

    public void SetSeconds(float newSeconds)
    {
        seconds = Mathf.Max(0, newSeconds);
    }

    public IEnumerator Wait()
    {
        yield return new WaitForSeconds(seconds);
    }
}
