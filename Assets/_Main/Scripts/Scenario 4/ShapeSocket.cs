using System;
using UnityEngine;

/// <summary>
/// Un hueco del panel del Escenario 4. Se apoya en el Socket de siempre para el acople y solo
/// añade la validación de la figura.
///
/// Encaja la ficha cuyo sprite sea EL MISMO asset que el esperado. Se compara la referencia y
/// no el nombre a propósito: con figuras abstractas que se parecen mucho entre sí, el nombre
/// es un texto suelto que se puede renombrar desde el Sprite Editor, y el emparejamiento se
/// rompería sin que nada avisara. La referencia o está o no está.
/// </summary>
[RequireComponent(typeof(Socket))]
public class ShapeSocket : MonoBehaviour
{
    [Header("Figura esperada")]
    [Tooltip("El mismo sprite que lleva la ficha que debe encajar aquí.")]
    [SerializeField] private Sprite expectedShape;

    [Tooltip("Dónde se dibuja la figura esperada. El hueco tiene que ENSEÑAR la figura: el " +
             "reto es encontrar la idéntica entre otras muy parecidas, y un rótulo con su " +
             "nombre lo resolvería leyendo en vez de mirando.")]
    [SerializeField] private SpriteRenderer shapeImage;

    [Tooltip("Id para telemetría. Si queda vacío se usa el nombre del sprite esperado.")]
    [SerializeField] private string socketId;

    [Header("Resuelto")]
    [Tooltip("Color al que pasa la figura del hueco al encajar la ficha correcta. Es el aviso de " +
             "que ese hueco ya está: con fichas tan parecidas, sin él el niño no sabe si acertó.")]
    [SerializeField] private Color solvedColor = new Color(0.3f, 1f, 0.4f);

    [Tooltip("Opcional. Suena en el hueco al resolverse.")]
    [SerializeField] private AudioClip solvedSound;

    [Tooltip("Al encajar la ficha correcta: partículas, luz...")]
    public UnityEngine.Events.UnityEvent OnSolved;

    private Color originalColor = Color.white;

    private Socket socket;

    /// <summary>Hueco, ficha colocada, si encajaba.</summary>
    public event Action<ShapeSocket, ShapeChip, bool> OnChipEvaluated;

    public bool IsSolved { get; private set; }
    public Sprite Expected => expectedShape;

    public string SocketId =>
        !string.IsNullOrEmpty(socketId) ? socketId :
        expectedShape != null ? expectedShape.name : name;

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

    private void Start()
    {
        if (shapeImage != null) originalColor = shapeImage.color;

        Refresh();
    }

    /// <summary>Pinta en el hueco la figura que espera.</summary>
    public void Refresh()
    {
        if (shapeImage == null) return;

        shapeImage.sprite = expectedShape;
    }

    /// <summary>
    /// Nombre del hijo que lleva el SpriteRenderer de la figura. El mismo que en ShapeChip:
    /// hueco y ficha se montan igual, así no hay dos convenciones que recordar.
    /// </summary>
    private const string SpriteChildName = "Sprite";

    /// <summary>
    /// Engancha el SpriteRenderer del hijo "Sprite" y pinta la figura esperada de una vez.
    ///
    /// Gemela de ShapeChip.ApplyShapeToChild(). Deja shapeImage guardado, no solo pintado: si
    /// únicamente se asignara el sprite, al entrar en Play el Refresh() de Start lo
    /// encontraría vacío y el hueco saldría en blanco.
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
        UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { this, target }, "Aplicar figura");
#endif

        shapeImage = target;
        Refresh();

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
            chip.SetInteractable(false);

            if (shapeImage != null) shapeImage.color = solvedColor;
            if (solvedSound != null) AudioSource.PlayClipAtPoint(solvedSound, transform.position);

            OnSolved?.Invoke();
        }

        OnChipEvaluated?.Invoke(this, chip, correct);
    }

    private bool Matches(ShapeChip chip) => expectedShape != null && chip.Shape == expectedShape;

    /// <summary>Vacía el hueco y lo devuelve a su estado inicial.</summary>
    public void ResetSocket()
    {
        IsSolved = false;

        if (shapeImage != null && Application.isPlaying) shapeImage.color = originalColor;

        if (socket != null)
        {
            // Se desbloquea ANTES de soltar: Release() pone CurrentBlock a null y después ya
            // no hay forma de saber qué ficha había aquí. Sin esto, toda ficha acertada quedaba
            // inagarrable para siempre y el escenario no se podía repetir tras un reinicio.
            if (socket.CurrentBlock is ShapeChip chip)
                chip.SetInteractable(true);

            socket.Release();
        }

        Refresh();
    }

#if UNITY_EDITOR
    /// <summary>Igual que en la ficha: el panel se monta viendo la figura, no adivinándola.</summary>
    private void OnValidate() => Refresh();
#endif
}
