using System;
using UnityEngine;

public class Point : MonoBehaviour
{
    public Action OnCollected;

    private void OnTriggerEnter(Collider other)
    {
        OnCollected?.Invoke();
        gameObject.SetActive(false);
    }
}
