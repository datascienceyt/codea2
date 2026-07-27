using UnityEngine;

public class Tools : MonoBehaviour
{
    public void DestroyObject(GameObject obj)
    {
        Destroy(obj);
    }

    public void SetActive(GameObject obj)
    {
        obj.SetActive(true);
    }

    public void SetInactive(GameObject obj)
    {
        obj.SetActive(false);
    }
}
