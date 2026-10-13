using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambia lo que se ve por las ventanas de la nave: de su lista de vistas exteriores deja
/// encendida una sola. En la escena de juego son Pos_earth (0) y Pos_sun (1), y el Director
/// pasa a la Tierra al terminar los escenarios 3 y 4.
///
/// Los nombres vienen de cuando cada entrada era un escenario entero. Se conservan porque el
/// campo y ChangeScenario están cableados en la escena.
/// </summary>
public class SceneController : MonoBehaviour
{
    [Tooltip("Vistas exteriores. ChangeScenario enciende la del índice y apaga las demás.")]
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
            Debug.LogError($"[SceneController] Vista {index} fuera de rango.", this);
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
