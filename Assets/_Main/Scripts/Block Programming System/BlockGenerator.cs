using Unity.VisualScripting;
using UnityEngine;

public class BlockGenerator : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    [SerializeField] Vector3 scale, position;
    [SerializeField] Quaternion rotation;

    private void Awake()
    {
        if(!prefab)
            prefab = transform.GetChild(0).gameObject;

        position = prefab.transform.position;
        rotation = prefab.transform.rotation;
        scale = prefab.transform.localScale;
    }

    private void Update()
    {
        if(transform.childCount == 0)
        {
            GameObject aux = Instantiate(prefab, transform.position, transform.rotation);
            aux.transform.localScale = scale;
            aux.transform.parent = gameObject.transform;
        }
    }
}
