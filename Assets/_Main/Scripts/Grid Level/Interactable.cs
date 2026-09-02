using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour, IInteractable
{
    public float interactDuration = 0.5f;
    public UnityEvent OnUsed;

    bool used;
    public LevelManager levelManager;

    public bool CanInteract()
    {
        return !used;
    }

    public IEnumerator Interact()
    {
        used = true;
        yield return new WaitForSeconds(interactDuration);

        if (levelManager != null)
            levelManager.IncreasePoint();

        OnUsed?.Invoke();
    }
}