using System.Collections;
using UnityEngine;
using Oculus.Interaction.Locomotion;

public class Teleporter : MonoBehaviour, IStepAction
{
    [HideInInspector] private Transform teleportTarget;
    [SerializeField] private LocomotionEventsConnection locomotionEvents; // del BodyTeleportInteractor

    public void SetTeleportTarget(Transform target) => teleportTarget = target;

    public void Teleport(Transform target)
    {
        StartCoroutine(IEnumeratorTeleport(target));
    }

    public IEnumerator Execute()
    {
        yield return StartCoroutine(IEnumeratorTeleport(teleportTarget));
    }

    IEnumerator IEnumeratorTeleport(Transform target)
    {
        OVRScreenFade.instance.FadeOut();
        yield return new WaitForSeconds(OVRScreenFade.instance.fadeTime);

        var teleportEvent = new LocomotionEvent(
            locomotionEvents.GetEntityId(),
            target.position, // Corregido: se pasa Vector3 en vez de Pose
            LocomotionEvent.TranslationType.Absolute
        );
        locomotionEvents.HandleLocomotionEvent(teleportEvent);

        OVRScreenFade.instance.FadeIn();
        yield return new WaitForSeconds(OVRScreenFade.instance.fadeTime);
    }
}