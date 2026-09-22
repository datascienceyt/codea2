using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una posición alrededor del brazo robótico. El brazo gira entre posiciones y recoge o suelta
/// en la que tenga delante.
///
/// Cada posición puede tener **varias pilas, una por tipo**. Eso es lo que permite que en el
/// frente convivan los barriles y las cajas: el brazo no gira para elegir entre ellas, elige
/// por el tipo que lleve puesto en su socket de argumento. Los destinos, en cambio, tienen una
/// sola pila y por eso solo admiten lo suyo.
/// </summary>
public class ArmSlot : MonoBehaviour
{
    [Serializable]
    public class TypedStack
    {
        [Tooltip("Qué tipo se apila aquí.")]
        public ArmItemType type = ArmItemType.Barril;

        [Tooltip("Dónde se apilan. Si queda vacío se usa el transform del slot.")]
        public Transform stackRoot;

        [Tooltip("Objetos que ya están aquí al empezar.")]
        public List<Transform> initialItems = new List<Transform>();

        [Tooltip("Activado: los objetos iniciales se recolocan apilados sobre stackRoot. " +
                 "Desactivado: se quedan donde los hayas puesto en la escena y solo se " +
                 "registran, útil cuando son barriles o cajas ya colocados a mano en el " +
                 "decorado.\n\nSolo afecta al arranque: lo que el brazo suelte siempre se apila.")]
        public bool stackInitialItems = true;

        [NonSerialized] public List<Transform> items = new List<Transform>();
        [NonSerialized] public List<ItemHome> homes = new List<ItemHome>();
    }

    public struct ItemHome
    {
        public Transform item;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    [Tooltip("Ángulo en Y al que gira el brazo para quedar frente a esta posición.")]
    [SerializeField] private float yaw;

    [Tooltip("Separación vertical entre objetos apilados.")]
    [SerializeField] private float stackSpacing = 0.35f;

    [Header("Pilas")]
    [Tooltip("Una por tipo. El frente lleva dos (barriles y cajas); cada destino, una sola.")]
    [SerializeField] private List<TypedStack> stacks = new List<TypedStack>();

    public float Yaw => yaw;

    /// <summary>Objetos de todas sus pilas.</summary>
    public int Count
    {
        get
        {
            int total = 0;

            foreach (TypedStack stack in stacks)
                if (stack != null) total += stack.items.Count;

            return total;
        }
    }

    public bool IsEmpty => Count == 0;

    private void Awake()
    {
        foreach (TypedStack stack in stacks)
        {
            if (stack == null) continue;

            if (stack.stackRoot == null) stack.stackRoot = transform;

            stack.homes.Clear();
        }

        CaptureHomes();
        ResetSlot();
    }

    /// <summary>Si esta posición tiene pila para ese tipo. Un destino solo admite lo suyo.</summary>
    public bool Accepts(ArmItemType type) => Find(type) != null;

    /// <summary>Cuántos objetos de ese tipo hay aquí.</summary>
    public int CountOf(ArmItemType type)
    {
        TypedStack stack = Find(type);

        return stack != null ? stack.items.Count : 0;
    }

    /// <summary>Saca el objeto de arriba de la pila de ese tipo. Null si no hay o está vacía.</summary>
    public Transform Take(ArmItemType type)
    {
        TypedStack stack = Find(type);

        if (stack == null || stack.items.Count == 0) return null;

        int top = stack.items.Count - 1;
        Transform item = stack.items[top];
        stack.items.RemoveAt(top);

        return item;
    }

    /// <summary>
    /// Apila un objeto en la pila que le corresponde por su ArmItem. Devuelve false si esta
    /// posición no admite ese tipo, y entonces no toca nada: quien llama decide qué hacer.
    /// </summary>
    public bool Put(Transform item)
    {
        if (item == null) return false;

        TypedStack stack = Find(TypeOf(item));
        if (stack == null) return false;

        item.SetParent(stack.stackRoot);
        item.localPosition = Vector3.up * (stackSpacing * stack.items.Count);
        item.localRotation = Quaternion.identity;

        stack.items.Add(item);

        return true;
    }

    /// <summary>
    /// Tipo de un objeto. Sin ArmItem se asume el de la primera pila: así una escena a medio
    /// montar sigue funcionando en vez de tragarse los objetos en silencio.
    /// </summary>
    public static ArmItemType TypeOf(Transform item)
    {
        if (item != null && item.TryGetComponent(out ArmItem armItem))
            return armItem.type;

        Debug.LogWarning($"[Escenario3] '{(item != null ? item.name : "null")}' no tiene " +
                         "ArmItem: no se sabe de qué tipo es.", item);

        return default;
    }

    /// <summary>Devuelve todas las pilas a su contenido y su disposición iniciales.</summary>
    [ContextMenu("Reset Slot")]
    public void ResetSlot()
    {
        foreach (TypedStack stack in stacks)
        {
            if (stack == null) continue;

            stack.items.Clear();

            foreach (ItemHome home in stack.homes)
            {
                if (home.item == null) continue;

                if (stack.stackInitialItems) PutStacked(stack, home.item);
                else PutInPlace(stack, home);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Revisa el montaje de las pilas sin entrar en Play. Busca lo que en Play solo se ve como
    /// "el brazo coge lo que no es": objetos listados en la pila del tipo equivocado, objetos
    /// sin ArmItem, y dos pilas compartiendo el mismo stackRoot.
    /// </summary>
    [ContextMenu("Comprobar pilas")]
    private void CheckStacks()
    {
        int problemas = 0;

        for (int i = 0; i < stacks.Count; i++)
        {
            TypedStack stack = stacks[i];
            if (stack == null) continue;

            foreach (Transform item in stack.initialItems)
            {
                if (item == null) continue;

                if (!item.TryGetComponent(out ArmItem armItem))
                {
                    Debug.LogError($"[Escenario3] '{item.name}' no tiene ArmItem: no se sabe " +
                                   "de qué tipo es.", item);
                    problemas++;
                    continue;
                }

                if (armItem.type == stack.type) continue;

                Debug.LogError($"[Escenario3] '{item.name}' está en la pila '{stack.type}' de " +
                               $"'{name}' pero es '{armItem.type}'.", item);
                problemas++;
            }

            // Dos pilas en el mismo sitio se superponen en cuanto el brazo suelte algo ahí:
            // al arrancar no se nota si stackInitialItems está desactivado.
            for (int j = i + 1; j < stacks.Count; j++)
                if (stacks[j] != null && stacks[j].stackRoot == stack.stackRoot)
                {
                    Debug.LogWarning($"[Escenario3] Las pilas '{stack.type}' y '{stacks[j].type}' " +
                                     $"de '{name}' comparten stackRoot: se apilarán encima.", this);
                    problemas++;
                }
        }

        Debug.Log($"[Escenario3] '{name}' · {stacks.Count} pila(s) · {problemas} problema(s)", this);
    }
#endif

    private TypedStack Find(ArmItemType type)
    {
        foreach (TypedStack stack in stacks)
            if (stack != null && stack.type == type) return stack;

        return null;
    }

    /// <summary>
    /// Guarda dónde estaba cada objeto inicial antes de tocarlo, para poder devolverlo ahí en
    /// un reinicio aunque no se apile.
    ///
    /// Reparte cada objeto por el tipo de su PROPIO ArmItem, no por la lista en la que esté
    /// escrito. El tipo de un objeto es una sola cosa y la lleva él; que además hubiera que
    /// escribirlo en la lista correcta era una segunda fuente de verdad, y en cuanto las dos
    /// discrepaban el brazo cogía de la pila equivocada sin que nada avisara.
    /// </summary>
    private void CaptureHomes()
    {
        foreach (TypedStack listed in stacks)
        {
            if (listed == null) continue;

            foreach (Transform item in listed.initialItems)
            {
                if (item == null) continue;

                ArmItemType type = TypeOf(item);
                TypedStack owner = Find(type);

                if (owner == null)
                {
                    Debug.LogError($"[Escenario3] '{item.name}' es de tipo '{type}' y en " +
                                   $"'{name}' no hay ninguna pila de ese tipo: se queda fuera " +
                                   "del recuento y no se podrá recoger.", item);
                    continue;
                }

                if (owner != listed)
                    Debug.LogWarning($"[Escenario3] '{item.name}' estaba listado en la pila " +
                                     $"'{listed.type}' de '{name}', pero su ArmItem dice " +
                                     $"'{type}'. Se coloca en la suya: manda el objeto.", item);

                owner.homes.Add(new ItemHome
                {
                    item = item,
                    parent = item.parent,
                    localPosition = item.localPosition,
                    localRotation = item.localRotation
                });
            }
        }
    }

    private void PutStacked(TypedStack stack, Transform item)
    {
        item.SetParent(stack.stackRoot);
        item.localPosition = Vector3.up * (stackSpacing * stack.items.Count);
        item.localRotation = Quaternion.identity;

        stack.items.Add(item);
    }

    private static void PutInPlace(TypedStack stack, ItemHome home)
    {
        home.item.SetParent(home.parent);
        home.item.localPosition = home.localPosition;
        home.item.localRotation = home.localRotation;

        stack.items.Add(home.item);
    }
}
