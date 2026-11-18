using System.Collections;

public interface IInteractable
{
    bool CanInteract();
    IEnumerator Interact();
}