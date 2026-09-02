using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cómo se decide si una ficha encaja.</summary>
public enum ShapeMatchMode
{
    /// <summary>Básica: tiene que ser la misma figura.</summary>
    Shape,

    /// <summary>Intermedia: basta con que coincida el número de lados.</summary>
    SideCount
}

/// <summary>
/// Un hueco del panel del Escenario 4. Se apoya en el Socket de siempre para el acople y
/// solo añade la validación de la figura.
/// </summary>
[RequireComponent(typeof(Socket))]
public class ShapeSocket : MonoBehaviour
{
    [Header("Figura esperada")]
    [SerializeField] private ShapeData expectedShape;

    [Tooltip("Opcional. Muestra la figura esperada o su número de lados, según el modo.")]
    [SerializeField] private Text label;

    [Tooltip("Id para telemetría. Si queda vacío se usa el de la figura esperada.")]
    [SerializeField] private string socketId;

    private Socket socket;
    private ShapeMatchMode mode = ShapeMatchMode.Shape;

    /// <summary>Hueco, ficha colocada, si encajaba.</summary>
    public event Action<ShapeSocket, ShapeChip, bool> OnChipEvaluated;

    public bool IsSolved { get; private set; }
    public ShapeData Expected => expectedShape;

    public string SocketId =>
        !string.IsNullOrEmpty(socketId) ? socketId :
        expectedShape != null ? expectedShape.shapeId : name;

    public int ExpectedSides => expectedShape != null ? expectedShape.sides : 0;

    private void Awake()
    {
        socket = GetComponent<Socket>();
        socket.OnOccupied += HandleOccupied;
    }

    private void OnDestroy()
    {
        if (socket != null)
            socket.OnOccupied -= HandleOccupied;
    }

    /// <summary>Lo fija Scenario4Controller según la dificultad de la escena.</summary>
    public void SetMode(ShapeMatchMode value)
    {
        mode = value;
        Refresh();
    }

    private void Start() => Refresh();

    private void Refresh()
    {
        if (label == null || expectedShape == null) return;

        // En intermedia el hueco NO revela qué figura espera, solo cuántos lados: si mostrara
        // la figura, el emparejamiento por atributo dejaría de tener sentido.
        label.text = mode == ShapeMatchMode.Shape
            ? expectedShape.displayName
            : expectedShape.sides.ToString();
    }

    private void HandleOccupied(BlockNode block)
    {
        // Un hueco ya resuelto tiene su ficha bloqueada, así que no debería recibir otra.
        if (IsSolved) return;

        ShapeChip chip = block as ShapeChip;
        if (chip == null) return;

        bool correct = Matches(chip);

        if (correct)
        {
            IsSolved = true;
            chip.SetLocked(true);
        }

        OnChipEvaluated?.Invoke(this, chip, correct);
    }

    private bool Matches(ShapeChip chip)
    {
        if (expectedShape == null || chip.Shape == null) return false;

        return mode == ShapeMatchMode.Shape
            ? chip.Shape.shapeId == expectedShape.shapeId
            : chip.Sides == expectedShape.sides;
    }

    /// <summary>Vacía el hueco y lo devuelve a su estado inicial.</summary>
    public void ResetSocket()
    {
        IsSolved = false;

        if (socket != null) socket.Release();

        Refresh();
    }
}
