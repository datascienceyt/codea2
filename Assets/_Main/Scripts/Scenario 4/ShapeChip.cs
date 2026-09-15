using System.Collections;
using UnityEngine;

/// <summary>
/// Ficha con una figura, del Escenario 4.
///
/// Hereda de BlockNode aunque no ejecute ningún programa. No es un atajo: así reutiliza tal
/// cual el agarre, la búsqueda del socket más cercano, el acople, el reinicio por
/// BlockResetter y el conteo de manipulación en telemetría. Lo único que no aplica es
/// Execute(), que queda vacío a propósito.
///
/// El bloqueo también se hereda: SetInteractable() de BlockNode apaga el interactionRoot.
/// Aquí había antes un campo propio con ese mismo nombre, que tapaba al de la clase base.
/// Como el de la base es privado, C# no emitía ningún aviso y Unity serializaba los dos: en
/// el inspector aparecían dos casillas "Interaction Root" y rellenar solo una dejaba media
/// funcionalidad muerta en silencio.
/// </summary>
public class ShapeChip : BlockNode
{
    [Header("Figura")]
    [SerializeField] private ShapeData shape;

    [Tooltip("Dónde se pinta el sprite de la figura. Vive en el propio prefab, así que la " +
             "referencia sobrevive al guardado; solo hay que asignar el ShapeData por ficha.")]
    [SerializeField] private SpriteRenderer shapeImage;

    public ShapeData Shape => shape;
    public string ShapeId => shape != null ? shape.shapeId : name;
    public int Sides => shape != null ? shape.sides : 0;

    public override string InstructionLabel => shape != null ? shape.displayName : name;

    protected override void Awake()
    {
        base.Awake();
        ApplyShape();
    }

    /// <summary>
    /// Copia el sprite del ShapeData a la ficha.
    ///
    /// La imagen NO se asigna a mano en cada instancia a propósito: con cuatro fichas por
    /// escena y dos escenas de dificultad, tener la figura y su imagen en dos sitios
    /// distintos acaba en una ficha que dice ser un triángulo y enseña un círculo.
    /// </summary>
    public void ApplyShape()
    {
        if (shapeImage == null) return;

        shapeImage.sprite = shape != null ? shape.sprite : null;
    }

    /// <summary>Cambia la figura en caliente. Para pruebas y para armar variantes.</summary>
    public void SetShape(ShapeData value)
    {
        shape = value;
        ApplyShape();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Para que la ficha enseñe su figura en el editor nada más asignarle el ShapeData, sin
    /// entrar en Play: el panel se monta mirando, no adivinando.
    /// </summary>
    private void OnValidate() => ApplyShape();
#endif

    /// <summary>El Escenario 4 no ejecuta secuencias: las fichas solo se colocan.</summary>
    public override IEnumerator Execute()
    {
        yield break;
    }
}
