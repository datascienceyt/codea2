using UnityEngine;

/// <summary>
/// Teletransporta al jugador (OVRCameraRig) a la posición/rotación de un Transform destino.
/// Mueve el rig completo compensando el offset de la cabeza, no la cámara directamente,
/// porque el tracking del headset sobreescribe la posición de la cámara cada frame.
/// </summary>
public class Teleporter : MonoBehaviour
{
    [SerializeField] private OVRCameraRig cameraRig;

    [Tooltip("Opcional. Solo si el rig usa CharacterController (ej. OVRPlayerController).")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("Si está activo, rota al jugador para que mire hacia adelante del target.")]
    [SerializeField] private bool alignRotation = true;

    private void Reset()
    {
        cameraRig = GetComponentInChildren<OVRCameraRig>();
        characterController = GetComponent<CharacterController>();
    }

    public void Teleport(Transform target)
    {
        if (cameraRig == null || target == null)
        {
            Debug.LogWarning("[PlayerTeleporter] Falta cameraRig o target.");
            return;
        }

        Transform rig = cameraRig.transform;
        Transform head = cameraRig.centerEyeAnchor;

        bool hadController = characterController != null && characterController.enabled;
        if (hadController) characterController.enabled = false;

        // 1. Rotar el rig alrededor de la cabeza para no desplazar al jugador al rotar.
        if (alignRotation)
        {
            float deltaYaw = target.eulerAngles.y - head.eulerAngles.y;
            rig.RotateAround(head.position, Vector3.up, deltaYaw);
        }

        // 2. Trasladar el rig para que la cabeza (proyectada en XZ) caiga en target.position.
        //    La altura (Y) la sigue definiendo el tracking real del headset.
        Vector3 headToRig = rig.position - head.position;
        headToRig.y = 0f;
        rig.position = target.position + headToRig;

        if (hadController) characterController.enabled = true;
    }
}