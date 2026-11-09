using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Socket : MonoBehaviour
{
    public Socket Next;
    public AudioClip clipIn, clipOut;

    /// <summary>
    /// Registro de todos los sockets vivos. Existe para que BlockNode no tenga que llamar a
    /// FindObjectsByType cada frame por cada bloque agarrado, que con dos manos y una fila
    /// llena era un coste real contra el objetivo de 60 FPS (RNF-01).
    /// </summary>
    static readonly List<Socket> active = new List<Socket>();
    public static IReadOnlyList<Socket> Active => active;

    /// <summary>
    /// Los estáticos sobreviven al cambio de escena y, si el proyecto desactiva el domain
    /// reload al entrar en Play, también entre sesiones del editor. Vaciar aquí evita
    /// arrastrar sockets de una partida anterior.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => active.Clear();

    AudioSource audioSource;

    public BlockNode CurrentBlock { get; private set; }

    public bool IsEmpty => CurrentBlock == null;

    /// <summary>
    /// Si admite bloques nuevos. Lo apaga SocketRow cuando la fila no es editable: sin esto,
    /// desactivar colliders impediría agarrar los bloques ya puestos pero no impediría meter
    /// otros, porque el acople de BlockNode busca por distancia, no por colisión.
    /// </summary>
    public bool AcceptsBlocks { get; private set; } = true;

    public void SetAcceptsBlocks(bool value) => AcceptsBlocks = value;

    [Header("Color al recibir un bloque")]
    [Tooltip("El hueco se tiñe de este color mientras tiene un bloque (feedback del 27/10/2026). " +
             "Los huecos de figuras del Escenario 4 no: allí el verde es solo para la ficha correcta, " +
             "y lo pone ShapeSocket.")]
    [SerializeField] private bool tintWhenOccupied = true;
    [SerializeField] private Color occupiedColor = new Color(0.3f, 0.85f, 0.35f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    // Los renderers propios del hueco, tomados en Awake: después el bloque encajado cuelga de
    // este objeto y GetComponentsInChildren lo tiñería también.
    //
    // Solo el marco (09/11/2026): todos los huecos son una placa en el centro y cuatro barras
    // alrededor. Teñida la placa, en la Sala 4 no se distinguía bien la figura; ahora el ámbar
    // y el verde van solo a las barras.
    Renderer[] tintRenderers;

    // Distancia al centro del hueco (en sus unidades locales) por debajo de la cual una pieza
    // es la placa central. Las barras están a 0,03 como poco.
    const float CenterTolerance = 0.01f;
    MaterialPropertyBlock tintBlock;

    // Verde al ocupar: no en los huecos de figura, que también se tiñen pero solo con la
    // indicación ámbar (ShapeSocketHints, 09/11/2026).
    bool tintOccupied;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        tintOccupied = tintWhenOccupied && GetComponent<ShapeSocket>() == null;
        Renderer[] own = System.Array.FindAll(GetComponentsInChildren<Renderer>(true),
            r => !(r is SpriteRenderer) && r.GetComponentInParent<BlockNode>() == null);

        // Un hueco sin marco (solo una pieza) se sigue tiñendo entero.
        Renderer[] frame = System.Array.FindAll(own, r => !IsCenterPlate(r));
        tintRenderers = frame.Length > 0 ? frame : own;
        tintBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// La placa del centro: la pieza centrada en el hueco. Se mira la posición del transform y no
    /// los bounds, que en un renderer recién activado pueden no estar calculados.
    /// </summary>
    bool IsCenterPlate(Renderer r)
    {
        Vector3 local = transform.InverseTransformPoint(r.transform.position);
        return Mathf.Abs(local.x) < CenterTolerance && Mathf.Abs(local.y) < CenterTolerance;
    }

    /// <summary>
    /// Verde mientras tiene bloque (si tintOccupied); si no, el color propio. Quitar el tinte
    /// también borra la indicación ámbar, que no debe quedarse puesta con una ficha dentro.
    /// </summary>
    void SetTint(bool occupied)
    {
        if (tintRenderers == null) return;

        bool green = occupied && tintOccupied;

        foreach (Renderer r in tintRenderers)
        {
            if (r == null) continue;

            if (!green)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            r.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(BaseColorId, occupiedColor);
            tintBlock.SetColor(ColorId, occupiedColor);
            r.SetPropertyBlock(tintBlock);
            tintBlock.Clear();
        }
    }

    /// <summary>
    /// Ilumina el hueco vacío para indicar dónde va el bloque. 0 lo deja como estaba. Con un
    /// bloque dentro no hace nada: manda el verde de ocupado.
    /// </summary>
    public void SetHint(float amount, Color color)
    {
        if (tintRenderers == null || !IsEmpty) return;

        foreach (Renderer r in tintRenderers)
        {
            if (r == null) continue;

            if (amount <= 0f)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            Color tint = Color.Lerp(Color.white, color, amount);
            r.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(BaseColorId, tint);
            tintBlock.SetColor(ColorId, tint);
            r.SetPropertyBlock(tintBlock);
            tintBlock.Clear();
        }
    }

    void OnEnable() => active.Add(this);

    void OnDisable() => active.Remove(this);

    /// <summary>
    /// Al acoplarse un bloque. Lo usa ShapeSocket (Escenario 4) para validar la figura.
    ///
    /// Quien escuche esto NO debe mover ni desacoplar el bloque en el acto: AttachTo todavía
    /// tiene que terminar de enlazarlo. El rechazo se difiere al menos un frame.
    /// </summary>
    public event Action<BlockNode> OnOccupied;

    /// <param name="playSound">
    /// False cuando el bloque lo coloca el juego y no el jugador: la fila que viene montada al
    /// cargar la escena encajaba sus bloques a la vez, y sonaban todos los clipIn de golpe.
    /// </param>
    public void Occupy(BlockNode block, bool playSound = true)
    {
        CurrentBlock = block;
        SetTint(true);
        if (playSound) PlayClip(clipIn);

        OnOccupied?.Invoke(block);
    }

    /// <summary>Vacía el socket con sonido. Es el camino del agarre normal.</summary>
    public void Clear()
    {
        Release();
        PlayClip(clipOut);
    }

    /// <summary>
    /// Vacía el socket sin destruir ni mover el bloque, y sin sonido: en un reinicio se vacían
    /// todos a la vez y N clipOut simultáneos serían un petardazo.
    ///
    /// Avisa al bloque de que ya no ocupa este socket. Sin ese aviso, el bloque conservaba la
    /// referencia y al volver a agarrarlo vaciaba su socket anterior, expulsando al bloque que
    /// lo hubiera ocupado mientras tanto.
    /// </summary>
    public void Release()
    {
        if (CurrentBlock == null) return;

        BlockNode block = CurrentBlock;
        CurrentBlock = null;
        SetTint(false);
        block.ForgetSocket();
    }

    void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
