using UnityEngine;

public class AutomaticDoor : MonoBehaviour
{
    [SerializeField] GameObject[] doors;
    [SerializeField, Range(1, 10)] float openingSpeed;
    [SerializeField, Range(1, 10)] float openingDistance;

    private Vector3 doorsPos1;
    private Vector3 doorsPos2;

    private void Awake()
    {
        switch (doors.Length)
        {
            case 1:

            break;
            case 2:

            break;
            default:

            break;
        }
    }

    private void Open()
    {
        
    }

    private void Close()
    {

    }

    private void MoveDoors()
    {

    }
}
