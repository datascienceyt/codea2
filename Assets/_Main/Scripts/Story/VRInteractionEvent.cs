using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class VRInteractionEvent : MonoBehaviour, IStepAction
{
    public enum WaitType
    {
        Grab,
        Release,
        Hover,
    }

    [Header("Config")]
    public VRGrabEvents target;
    public WaitType waitType;

    [Header("Events")]
    public UnityEvent onStart;
    public UnityEvent onComplete;

    private bool done = false;

    public IEnumerator Execute()
    {
        done = false;

        onStart?.Invoke();

        if (target != null)
        {
            if (waitType == WaitType.Grab)
                target.onGrabbed.AddListener(OnDone);
            else if (waitType == WaitType.Release)
                target.onReleased.AddListener(OnDone);
            else if (waitType == WaitType.Hover)
                target.onHover.AddListener(OnDone);
        }

        yield return new WaitUntil(() => done);

        if (target != null)
        {
            if (waitType == WaitType.Grab)
                target.onGrabbed.RemoveListener(OnDone);
            else if (waitType == WaitType.Release)
                target.onReleased.RemoveListener(OnDone);
            else if (waitType == WaitType.Hover)
                target.onHover.RemoveListener(OnDone);
        }

        onComplete?.Invoke();
    }

    private void OnDone()
    {
        done = true;
    }
}