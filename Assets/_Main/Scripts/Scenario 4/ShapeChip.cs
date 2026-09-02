using System.Collections;
using UnityEngine;

/// <summary>
/// Ficha con una figura, del Escenario 4.
///
/// Hereda de BlockNode aunque no ejecute ningún programa. No es un atajo: así reutiliza tal
/// cual el agarre, la búsqueda del socket más cercano, el acople, el reinicio por
/// BlockResetter y el conteo de manipulación en telemetría. Lo único que no aplica es
/// Execute(), que queda vacío a propósito.
/// </summary>
public class ShapeChip : BlockNode
{
    [Header("Figura")]
    [SerializeField] private ShapeData shape;

    [Tooltip("Opcional. Objeto que se apaga al quedar bien colocado, para que la ficha ya no " +
             "pueda volver a agarrarse. Apunta aquí al hijo con el Grabbable.")]
    [SerializeField] private GameObject interactionRoot;

    public ShapeData Shape => shape;
    public string ShapeId => shape != null ? shape.shapeId : name;
    public int Sides => shape != null ? shape.sides : 0;

    public override string InstructionLabel => shape != null ? shape.displayName : name;

    /// <summary>El Escenario 4 no ejecuta secuencias: las fichas solo se colocan.</summary>
    public override IEnumerator Execute()
    {
        yield break;
    }

    public void SetLocked(bool locked)
    {
        if (interactionRoot != null)
            interactionRoot.SetActive(!locked);
    }
}
