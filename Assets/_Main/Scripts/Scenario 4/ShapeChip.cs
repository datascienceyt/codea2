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

    [Header("Física")]
    [Tooltip("Soltada fuera de un hueco, la ficha cae con gravedad en vez de quedarse flotando.")]
    [SerializeField] private bool usePhysics = true;

    [Tooltip("Metros por debajo de su sitio de origen a partir de los que se considera caída " +
             "al suelo y se devuelve sola.")]
    [SerializeField] private float fallenBelow = 0.3f;

    [Tooltip("Segundos en el suelo antes de volver a su sitio. Da tiempo a recogerla a mano.")]
    [SerializeField] private float returnDelay = 4f;

    private Rigidbody body;
    private BlockResetter resetter;
    private float homeY;
    private Coroutine looseRoutine;

    protected override void Awake()
    {
        base.Awake();

        // Se toma antes de que nada la mueva: al arrancar, la ficha está en su sitio de origen.
        body = GetComponent<Rigidbody>();
        resetter = GetComponentInParent<BlockResetter>();
        homeY = transform.position.y;

        VRGrabEvents grab = GetComponent<VRGrabEvents>();
        grab.onGrabbed.AddListener(StopLoose);
        grab.onReleased.AddListener(HandleReleased);

        ApplyShape();
    }

    // --- Física: caer al soltarla fuera de un hueco, y volver sola si acaba en el suelo ---

    private void HandleReleased()
    {
        if (!usePhysics || body == null) return;

        StopLoose();
        looseRoutine = StartCoroutine(Loose());
    }

    /// <summary>
    /// Suelta la ficha de su hueco donde está y la deja caer con su física. Para el rechazo:
    /// el hueco ya la liberó, y aquí se desengancha del padre y vuelve a ser dinámica. Si acaba
    /// en el suelo, vuelve sola a su sitio como cualquier ficha caída.
    /// </summary>
    public void DropInPlace(Vector3 launchVelocity = default)
    {
        // Mantiene la posición en el mundo; cuelga otra vez del BlockResetter para que la
        // jerarquía siga ordenada y el reinicio la encuentre donde espera.
        transform.SetParent(resetter != null ? resetter.transform : null, true);

        if (!usePhysics || body == null) return;

        body.isKinematic = false;
        body.useGravity = true;

        // VelocityChange y no Impulse: el empujón no depende de la masa que tenga la ficha.
        if (launchVelocity != Vector3.zero)
            body.AddForce(launchVelocity, ForceMode.VelocityChange);

        StopLoose();
        looseRoutine = StartCoroutine(Loose());
    }

    // Devuelta a su sitio (reinicio o caída): ya no hay caída que vigilar.
    public override void OnReturnedHome() => StopLoose();

    private void StopLoose()
    {
        if (looseRoutine != null) StopCoroutine(looseRoutine);
        looseRoutine = null;
    }

    private IEnumerator Loose()
    {
        // Se espera un paso de física: el Grabbable devuelve el Rigidbody a su estado original
        // (cinemático) al soltar, y si esto corriera antes, lo pisaría. El acople al hueco, en
        // cambio, ya ocurrió: BlockNode escucha el mismo evento y se suscribió primero.
        //
        // Encajada, se congela antes y después de esa espera: una ficha que ya había caído
        // vuelve a ser dinámica al soltarla, y en el hueco se caería de él.
        if (IsAttached) body.isKinematic = true;

        yield return new WaitForFixedUpdate();
        yield return null;

        if (IsAttached)
        {
            body.isKinematic = true;
            looseRoutine = null;
            yield break;
        }

        if (IsGrabbed) yield break;

        body.isKinematic = false;
        body.useGravity = true;

        float onFloorSince = -1f;

        // Termina también si alguien la congela: BlockResetter la devuelve cinemática.
        while (!IsAttached && !IsGrabbed && !body.isKinematic)
        {
            bool fallen = transform.position.y < homeY - fallenBelow;

            if (!fallen) onFloorSince = -1f;
            else if (onFloorSince < 0f) onFloorSince = Time.time;
            else if (Time.time - onFloorSince >= returnDelay) break;

            yield return null;
        }

        looseRoutine = null;

        // Recogida a mano, encajada o devuelta mientras tanto: nada que hacer.
        if (IsAttached || IsGrabbed || body.isKinematic) yield break;

        if (resetter == null || !resetter.ReturnBlock(this))
        {
            Debug.LogWarning($"[Escenario4] '{name}' se cayó y no hay BlockResetter del que " +
                             "cuelgue para devolverla.", this);
            body.isKinematic = true;
        }
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
