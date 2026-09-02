using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una posición alrededor del brazo robótico, con su pila de objetos.
///
/// El brazo gira entre posiciones; recoge de la pila de la posición actual y suelta en ella.
/// Con eso, el ciclo "Recoger, Girar, Soltar, Girar" traslada objetos de un montón a otro,
/// que es justo el patrón que el Escenario 3 pide repetir.
/// </summary>
public class ArmSlot : MonoBehaviour
{
    private struct ItemHome
    {
        public Transform item;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    [Tooltip("Ángulo en Y al que gira el brazo para quedar frente a esta posición.")]
    [SerializeField] private float yaw;

    [Tooltip("Dónde se apilan los objetos. Si queda vacío se usa este mismo transform.")]
    [SerializeField] private Transform stackRoot;

    [Tooltip("Separación vertical entre objetos apilados.")]
    [SerializeField] private float stackSpacing = 0.35f;

    [Header("Contenido inicial")]
    [Tooltip("Objetos que ya están aquí al empezar.")]
    [SerializeField] private List<Transform> initialItems = new List<Transform>();

    [Tooltip("Activado: los objetos iniciales se recolocan apilados sobre stackRoot. " +
             "Desactivado: se quedan donde los hayas puesto en la escena y solo se registran, " +
             "útil cuando son barriles o cajas ya colocadas a mano en el decorado.\n\n" +
             "Solo afecta al arranque: lo que el brazo suelte durante la partida siempre se apila.")]
    [SerializeField] private bool stackInitialItems = true;

    private readonly List<Transform> items = new List<Transform>();
    private readonly List<ItemHome> homes = new List<ItemHome>();

    public float Yaw => yaw;
    public int Count => items.Count;
    public bool IsEmpty => items.Count == 0;

    private void Awake()
    {
        if (stackRoot == null) stackRoot = transform;

        CaptureHomes();
        ResetSlot();
    }

    /// <summary>
    /// Guarda dónde estaba cada objeto inicial antes de tocarlo, para poder devolverlo ahí
    /// en un reinicio aunque no se apile.
    /// </summary>
    private void CaptureHomes()
    {
        homes.Clear();

        foreach (Transform item in initialItems)
        {
            if (item == null) continue;

            homes.Add(new ItemHome
            {
                item = item,
                parent = item.parent,
                localPosition = item.localPosition,
                localRotation = item.localRotation
            });
        }
    }

    /// <summary>Saca el objeto de arriba de la pila. Devuelve null si está vacía.</summary>
    public Transform Take()
    {
        if (items.Count == 0) return null;

        int top = items.Count - 1;
        Transform item = items[top];
        items.RemoveAt(top);

        return item;
    }

    /// <summary>Apila un objeto encima del montón.</summary>
    public void Put(Transform item)
    {
        if (item == null) return;

        item.SetParent(stackRoot);
        item.localPosition = Vector3.up * (stackSpacing * items.Count);
        item.localRotation = Quaternion.identity;

        items.Add(item);
    }

    /// <summary>Registra un objeto sin moverlo, tal y como está colocado en la escena.</summary>
    private void PutInPlace(ItemHome home)
    {
        home.item.SetParent(home.parent);
        home.item.localPosition = home.localPosition;
        home.item.localRotation = home.localRotation;

        items.Add(home.item);
    }

    /// <summary>Devuelve la pila a su contenido y su disposición iniciales.</summary>
    [ContextMenu("Reset Slot")]
    public void ResetSlot()
    {
        items.Clear();

        foreach (ItemHome home in homes)
        {
            if (home.item == null) continue;

            if (stackInitialItems) Put(home.item);
            else PutInPlace(home);
        }
    }
}
