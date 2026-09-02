using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [SerializeField] GameObject[] scenarios;

    [ContextMenu("Reset scene")]
    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ChangeScenario(int index)
    {
        if (scenarios == null || index < 0 || index >= scenarios.Length)
        {
            Debug.LogError($"[SceneController] Escenario {index} fuera de rango.", this);
            return;
        }

        foreach (GameObject go in scenarios)
        {
            if (go != null) go.SetActive(false);
        }

        if (scenarios[index] != null)
            scenarios[index].SetActive(true);
    }
}
