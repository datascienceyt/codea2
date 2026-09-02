using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AutomaticDoor : MonoBehaviour, IStepAction
{
    [SerializeField] GameObject door1;
    [SerializeField] GameObject door2;
    [SerializeField, Min(0.01f)] float openingSpeed = 2f;
    [SerializeField, Min(0f)] float openingDistance = 1f;
    [SerializeField] AudioClip openingSound;
    [SerializeField] AudioClip closingSound;

    Vector3 door1ClosedPosition;
    Vector3 door1OpenPosition;
    Vector3 door2ClosedPosition;
    Vector3 door2OpenPosition;
    AudioSource audioSource;
    bool isOpen;
    bool isMoving;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (door1 == null)
        {
            Debug.LogError($"{nameof(AutomaticDoor)} requires door1 to be assigned.", this);
            enabled = false;
            return;
        }

        door1ClosedPosition = door1.transform.localPosition;
        float door1Direction = door2 == null ? 1f : -1f;
        door1OpenPosition = door1ClosedPosition + Vector3.right * door1Direction * openingDistance;

        if (door2 != null)
        {
            door2ClosedPosition = door2.transform.localPosition;
            door2OpenPosition = door2ClosedPosition + Vector3.right * openingDistance;
        }
    }

    public IEnumerator Execute()
    {
        yield return isOpen ? Close() : Open();
    }

    public IEnumerator Open()
    {
        if (isOpen || isMoving)
            yield break;

        PlayDoorSound(openingSound);
        yield return MoveDoors(true);
        isOpen = true;
    }

    public IEnumerator Close()
    {
        if (!isOpen || isMoving)
            yield break;

        PlayDoorSound(closingSound);
        yield return MoveDoors(false);
        isOpen = false;
    }

    [ContextMenu("Open door")]
    void OpenFromContextMenu()
    {
        StartCoroutine(Open());
    }

    [ContextMenu("Close door")]
    void CloseFromContextMenu()
    {
        StartCoroutine(Close());
    }

    void PlayDoorSound(AudioClip sound)
    {
        if (sound != null)
            audioSource.PlayOneShot(sound);
    }

    IEnumerator MoveDoors(bool open)
    {
        isMoving = true;

        bool reachedTarget = false;
        while (!reachedTarget)
        {
            reachedTarget = true;

            Vector3 door1Target = open ? door1OpenPosition : door1ClosedPosition;
            door1.transform.localPosition = Vector3.MoveTowards(
                door1.transform.localPosition,
                door1Target,
                openingSpeed * Time.deltaTime);

            if (door1.transform.localPosition != door1Target)
                reachedTarget = false;

            if (door2 != null)
            {
                Vector3 door2Target = open ? door2OpenPosition : door2ClosedPosition;
                door2.transform.localPosition = Vector3.MoveTowards(
                    door2.transform.localPosition,
                    door2Target,
                    openingSpeed * Time.deltaTime);

                if (door2.transform.localPosition != door2Target)
                    reachedTarget = false;
            }

            yield return null;
        }

        isMoving = false;
    }
}
