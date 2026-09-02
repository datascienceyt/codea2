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

    void Awake() => audioSource = GetComponent<AudioSource>();

    void OnEnable() => active.Add(this);

    void OnDisable() => active.Remove(this);

    /// <summary>
    /// Al acoplarse un bloque. Lo usa ShapeSocket (Escenario 4) para validar la figura.
    ///
    /// Quien escuche esto NO debe mover ni desacoplar el bloque en el acto: AttachTo todavía
    /// tiene que terminar de enlazarlo. El rechazo se difiere al menos un frame.
    /// </summary>
    public event Action<BlockNode> OnOccupied;

    public void Occupy(BlockNode block)
    {
        CurrentBlock = block;
        PlayClip(clipIn);

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
        block.ForgetSocket();
    }

    public void ClearAndDestroyBlock()
    {
        BlockNode block = CurrentBlock;
        Release();

        if (block != null)
            Destroy(block.gameObject);
    }

    public void ClearAndDetachBlock()
    {
        BlockNode block = CurrentBlock;
        Release();

        if (block != null)
            block.transform.SetParent(null);
    }

    void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
