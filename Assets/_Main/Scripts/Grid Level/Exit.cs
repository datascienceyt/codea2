using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Exit : MonoBehaviour, IInteractable
{
    // LevelLoader la inyecta al instanciar el tile. El fallback de Awake cubre los Exit
    // colocados a mano en la escena, pero con varios escenarios en Main.unity puede resolver
    // al LevelManager equivocado: la inyección del loader siempre manda.
    [HideInInspector] public LevelManager levelManager;

    void Awake()
    {
        if (levelManager == null)
            levelManager = FindAnyObjectByType<LevelManager>();
    }

    public UnityEvent OnLevelFinished;

    public bool CanInteract() => true;

    public IEnumerator Interact()
    {
        // El evento se dispara aunque no haya LevelManager: es lo que escucha la narrativa,
        // y perderlo dejaría al Director esperando para siempre.
        if (levelManager != null)
            levelManager.CompleteLevel();
        else
            Debug.LogError("[Exit] Sin LevelManager: el nivel no se marcará como completado.", this);

        OnLevelFinished?.Invoke();
        yield break;
    }
}