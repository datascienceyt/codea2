using UnityEngine;

public class ScenarioController : MonoBehaviour
{
    [SerializeField] GameObject pos1;
    [SerializeField] GameObject pos2;

    public void SetPos1()
    {
        pos1.SetActive(true);
        pos2.SetActive(false);
    }

    public void SetPos2()
    {
        pos1.SetActive(false);
        pos2.SetActive(true);
    }
}
