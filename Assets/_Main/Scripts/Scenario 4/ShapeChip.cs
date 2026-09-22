using System.Collections;
using UnityEngine;

/// <summary>
/// Ficha con una figura, del Escenario 4.
///
/// La figura ES el sprite. Antes había un ScriptableObject por figura, y valía la pena
/// mientras el escenario emparejaba por número de lados: ese asset separaba la identidad de
/// la figura de su atributo medible. Al pasar a "encuentra la idéntica" no queda atributo que
/// separar, así que el asset se reducía a un envoltorio de un solo campo. Con un pliego de 34
/// recortes eso son 34 ficheros para arrastrar 34 sprites.
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
    [Tooltip("Recorte del pliego de figuras. Arrastra EXACTAMENTE el mismo sprite al hueco " +
             "que deba aceptar esta ficha: el emparejamiento compara la referencia al asset, " +
             "no el dibujo ni el nombre.")]
    [SerializeField] private Sprite shape;

    [Tooltip("Dónde se pinta la figura. Vive en el propio prefab, así que la referencia " +
             "sobrevive al guardado; solo hay que asignar el sprite por ficha.")]
    [SerializeField] private SpriteRenderer shapeImage;

    public Sprite Shape => shape;

    /// <summary>
    /// Id para telemetría. Sale del nombre del recorte, que es lo único estable que queda al
    /// no haber asset propio: renombrarlo en el Sprite Editor CAMBIA los datos. Ponles el
    /// nombre definitivo antes de empezar a recoger sesiones.
    /// </summary>
    public string ShapeId => shape != null ? shape.name : name;

    public override string InstructionLabel => ShapeId;

    protected override void Awake()
    {
        base.Awake();
        ApplyShape();
    }

    /// <summary>
    /// Pinta la figura en la ficha.
    ///
    /// Sigue existiendo aunque el sprite ya no venga de un asset intermedio: el prefab trae su
    /// SpriteRenderer y esto es lo que lo rellena al instanciar y al tocar el campo.
    /// </summary>
    public void ApplyShape()
    {
        if (shapeImage == null) return;

        shapeImage.sprite = shape;
    }

    /// <summary>Cambia la figura en caliente. Para pruebas y para armar variantes.</summary>
    public void SetShape(Sprite value)
    {
        shape = value;
        ApplyShape();
    }

    /// <summary>
    /// Nombre del hijo que lleva el SpriteRenderer de la figura. Fijo a propósito: todas las
    /// fichas salen del mismo prefab y todas traen ese hijo.
    /// </summary>
    private const string SpriteChildName = "Sprite";

    /// <summary>
    /// Engancha el SpriteRenderer del hijo "Sprite" y pinta la figura de una vez.
    ///
    /// Con un pliego de 34 recortes, montar las fichas obliga a arrastrar dos cosas por ficha:
    /// el sprite y el renderer donde va. Lo segundo es siempre el mismo hijo, así que esto lo
    /// resuelve solo. Deja shapeImage guardado, no solo pintado: si únicamente se asignara el
    /// sprite, en Play volvería a estar vacío.
    /// </summary>
    [ContextMenu("Aplicar figura al hijo 'Sprite'")]
    public void ApplyShapeToChild()
    {
        SpriteRenderer target = FindSpriteChild();

        if (target == null)
        {
            Debug.LogWarning($"[Escenario4] '{name}' no tiene ningún hijo '{SpriteChildName}' " +
                             "con SpriteRenderer: no hay dónde pintar la figura.", this);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObjects(new Object[] { this, target }, "Aplicar figura");
#endif

        shapeImage = target;
        ApplyShape();

#if UNITY_EDITOR
        // Sin marcar sucio, el campo aparece relleno en el inspector pero no se guarda: al
        // recargar la escena o entrar en Play vuelve a estar vacío.
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }

    private SpriteRenderer FindSpriteChild()
    {
        Transform child = transform.Find(SpriteChildName);

        if (child != null && child.TryGetComponent(out SpriteRenderer direct))
            return direct;

        // Rebusca más abajo e incluye los desactivados, por si la figura cuelga de un pivote
        // o de un grupo de visuales en vez de ser hija directa.
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name == SpriteChildName) return renderer;

        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Para que la ficha enseñe su figura en el editor nada más asignarle el sprite, sin
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
