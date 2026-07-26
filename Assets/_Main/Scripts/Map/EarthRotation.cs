using UnityEngine;

public class EarthRotation : MonoBehaviour
{
    public float omega = 2.39e-4f; // rad/s

    void Update()
    {
        float degPerSec = omega * Mathf.Rad2Deg;
        transform.Rotate(Vector3.forward, degPerSec * Time.deltaTime, Space.Self);
    }
}
