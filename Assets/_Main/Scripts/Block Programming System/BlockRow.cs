using System.Collections.Generic;
using UnityEngine;

public class BlockRow : MonoBehaviour
{
    [SerializeField] Transform start;
    [SerializeField] Transform end;
    [SerializeField] GameObject socket_prefab;
    [SerializeField] int socketsQuantity;

    private List<GameObject> sockets = new List<GameObject>();

    private void Awake()
    {
        Vector3 socketPosition;

        socketsQuantity++;

        for (float i = 1; i < socketsQuantity; i++)
        {
            float thresh = i / (socketsQuantity);
            socketPosition = Vector3.Lerp(start.position, end.position, thresh);
            GameObject aux = Instantiate(socket_prefab, socketPosition, Quaternion.identity);
            aux.transform.parent = transform;
            sockets.Add(aux);
        }
    }
}
