using UnityEngine;

public class BlockGenerator : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    Vector3 scale;
    GameObject currentBlock;

    void Awake()
    {
        if (!prefab)
            prefab = transform.GetChild(0).gameObject;

        scale = prefab.transform.localScale;
        currentBlock = prefab;
        Hook(currentBlock);
    }

    void Hook(GameObject block)
    {
        var grabEvents = block.GetComponent<VRGrabEvents>();
        if (grabEvents != null)
            grabEvents.onGrabbed.AddListener(OnBlockTaken);
    }

    void OnBlockTaken()
    {
        var grabEvents = currentBlock.GetComponent<VRGrabEvents>();
        if (grabEvents != null)
            grabEvents.onGrabbed.RemoveListener(OnBlockTaken); // solo dispara una vez

        SpawnNext();
    }

    void SpawnNext()
    {
        GameObject aux = Instantiate(prefab, transform.position, transform.rotation);
        aux.transform.localScale = scale;
        currentBlock = aux;
        Hook(currentBlock);
    }
}