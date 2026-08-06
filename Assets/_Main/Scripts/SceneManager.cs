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
        foreach(GameObject go in scenarios)
        {
            go.SetActive(false);
        }

        scenarios[index].SetActive(true);
    }
}
