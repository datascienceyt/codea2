using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conecta el Escenario 4 con el Director y con la telemetría. El escenario termina cuando
/// todos los huecos del panel tienen la ficha correcta.
///
/// La dificultad no se declara aquí. La mecánica es idéntica en las dos: lo único que cambia
/// es qué figuras se arrastran a los huecos de cada escena. Quién lo registra es
/// DifficultyScene, que además corrige la telemetría si la escena y el selector no coinciden;
/// un segundo indicador en este componente solo podría contradecirlo.
/// </summary>
public class Scenario4Controller : MonoBehaviour, IStepAction
{
    [Header("Panel")]
    [Tooltip("Si se deja vacío se buscan en los hijos, incluidos los desactivados.")]
    [SerializeField] private ShapeSocket[] sockets;

    [Header("Rechazo")]
    [Tooltip("Quien devuelve las fichas mal colocadas a su sitio.")]
    [SerializeField] private BlockResetter chipResetter;

    [Tooltip("Margen antes de devolver la ficha, para que el jugador vea que no encajó.")]
    [SerializeField] private float rejectDelay = 0.6f;

    [Header("Telemetría")]
    [SerializeField] private string challengeId = "escenario4";

    [Tooltip("Desactívalo si el Director ya llama a StartChallenge por evento.")]
    [SerializeField] private bool startChallengeOnStart = false;

    [Header("Eventos")]
    public UnityEvent OnCorrectPlacement;
    public UnityEvent OnWrongPlacement;
    public UnityEvent OnScenarioFinished;

    private bool scenarioFinished;

    private void Awake()
    {
        if (sockets == null || sockets.Length == 0)
            sockets = GetComponentsInChildren<ShapeSocket>(true);

        // Deliberadamente NO se adivina cuando el campo está vacío.
        //
        // Main.unity tiene un BlockResetter por escenario, y FindAnyObjectByType devuelve uno
        // cualquiera de entre los ACTIVOS. Si acertaba el que no es, ReturnBlock() fallaba y la
        // ficha rechazada se quedaba clavada en el hueco, que ya había quedado libre: se podía
        // soltar otra encima y quedaban superpuestas.
        if (chipResetter == null)
        {
            chipResetter = FindAnyObjectByType<BlockResetter>(FindObjectsInactive.Include);

            Debug.LogError(chipResetter != null
                ? $"[Escenario4] 'chipResetter' sin asignar en '{name}'; se ha tomado " +
                  $"'{chipResetter.name}'. Asígnalo a mano: hay un BlockResetter por escenario " +
                  "y esta búsqueda no distingue cuál es el de las fichas."
                : $"[Escenario4] '{name}' no encuentra ningún BlockResetter: las fichas mal " +
                  "colocadas no se podrán devolver a su sitio.", this);
        }

        foreach (ShapeSocket socket in sockets)
        {
            if (socket == null) continue;

            socket.OnChipEvaluated += HandleChipEvaluated;
        }
    }

    private void OnDestroy()
    {
        if (sockets == null) return;

        foreach (ShapeSocket socket in sockets)
            if (socket != null) socket.OnChipEvaluated -= HandleChipEvaluated;
    }

    private void Start()
    {
        if (startChallengeOnStart) StartScenario();
    }

    /// <summary>Cablear al Director si se prefiere lanzarlo desde un Step.</summary>
    public void StartScenario()
    {
        if (TelemetryManager.Instance == null) return;

        TelemetryManager.Instance.StartChallenge(challengeId);
    }

    private void HandleChipEvaluated(ShapeSocket socket, ShapeChip chip, bool correct)
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterPlacement(
                challengeId,
                socket.SocketId,
                chip.ShapeId,
                correct);

        if (correct)
        {
            OnCorrectPlacement?.Invoke();
            CheckCompletion();
            return;
        }

        OnWrongPlacement?.Invoke();
        StartCoroutine(RejectChip(socket, chip));
    }

    /// <summary>
    /// Devuelve la ficha a su sitio, pero NO en el acto.
    ///
    /// El aviso llega desde Socket.Occupy(), y BlockNode.AttachTo todavía tiene que terminar
    /// de enlazar el bloque a ese socket: desacoplarlo ahí dejaría al chip apuntando a un
    /// hueco que ya no ocupa. Esperar también da un momento para que se vea el rechazo.
    /// </summary>
    private IEnumerator RejectChip(ShapeSocket socket, ShapeChip chip)
    {
        yield return new WaitForSeconds(rejectDelay);

        socket.ResetSocket();

        if (chipResetter == null || !chipResetter.ReturnBlock(chip))
            Debug.LogWarning($"[Escenario4] No se pudo devolver '{chip.name}' a su sitio: " +
                             "¿cuelga del objeto con BlockResetter?", chip);
    }

    private void CheckCompletion()
    {
        if (scenarioFinished) return;

        foreach (ShapeSocket socket in sockets)
            if (socket != null && !socket.IsSolved) return;

        scenarioFinished = true;

        Debug.Log($"[Escenario 4] COMPLETADO · {sockets.Length} huecos encajados · " +
                  $"challengeId '{challengeId}'", this);

        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.CompleteChallenge(challengeId);

        OnScenarioFinished?.Invoke();
    }

    public IEnumerator Execute()
    {
        yield return new WaitUntil(() => scenarioFinished);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Lanza de una pasada el "Aplicar figura al hijo 'Sprite'" de todos los huecos del panel
    /// y de todas las fichas de la bandeja.
    ///
    /// Con 34 recortes que repartir, hacerlo pieza por pieza es justo donde se cuela el
    /// despiste: una ficha con el renderer sin enganchar se ve perfecta en el editor y sale en
    /// blanco al entrar en Play.
    /// </summary>
    [ContextMenu("Aplicar figuras a todo el panel")]
    private void ApplyShapesToPanel()
    {
        ShapeSocket[] panel = ResolveSockets();
        ShapeChip[] fichas = ResolveChips();

        foreach (ShapeSocket socket in panel)
            if (socket != null) socket.ApplyShapeToChild();

        foreach (ShapeChip chip in fichas)
            if (chip != null) chip.ApplyShapeToChild();

        Debug.Log($"[Escenario4] Figuras aplicadas · {panel.Length} huecos · " +
                  $"{fichas.Length} fichas", this);
    }

    /// <summary>
    /// Comprueba el montaje del panel antes de calzarse el visor.
    ///
    /// Busca el fallo que no se ve mirando la escena: un hueco cuya figura no la lleva ninguna
    /// ficha deja el escenario IRRESOLUBLE, y como el escenario termina cuando todos los huecos
    /// están resueltos, el Director se queda esperando para siempre. Eso solo se descubre con
    /// alguien plantado delante del panel sin poder terminar.
    /// </summary>
    [ContextMenu("Comprobar figuras del panel")]
    private void CheckPanelSetup()
    {
        ShapeSocket[] panel = ResolveSockets();
        ShapeChip[] fichas = ResolveChips();

        int problemas = 0;

        Dictionary<Sprite, int> pedidas = new Dictionary<Sprite, int>();
        Dictionary<Sprite, int> disponibles = new Dictionary<Sprite, int>();

        foreach (ShapeSocket socket in panel)
        {
            if (socket == null) continue;

            if (socket.Expected == null)
            {
                Debug.LogError($"[Escenario4] El hueco '{socket.name}' no espera ninguna " +
                               "figura: nada podrá encajar en él.", socket);
                problemas++;
                continue;
            }

            pedidas.TryGetValue(socket.Expected, out int veces);
            pedidas[socket.Expected] = veces + 1;
        }

        foreach (ShapeChip chip in fichas)
        {
            if (chip == null) continue;

            if (chip.Shape == null)
            {
                Debug.LogError($"[Escenario4] La ficha '{chip.name}' no lleva figura.", chip);
                problemas++;
                continue;
            }

            disponibles.TryGetValue(chip.Shape, out int veces);
            disponibles[chip.Shape] = veces + 1;
        }

        // Se cuenta, no se comprueba solo la presencia: dos huecos pueden esperar la misma
        // figura, y entonces hacen falta dos fichas que la lleven. La primera que acierte se
        // bloquea en su hueco y ya no sirve para el otro.
        foreach (KeyValuePair<Sprite, int> pedida in pedidas)
        {
            disponibles.TryGetValue(pedida.Key, out int hay);
            if (hay >= pedida.Value) continue;

            Debug.LogError($"[Escenario4] '{pedida.Key.name}': {pedida.Value} hueco(s) la piden " +
                           $"y solo {hay} ficha(s) la llevan. El panel no se puede completar.", this);
            problemas++;
        }

        int distractoras = 0;

        foreach (KeyValuePair<Sprite, int> libre in disponibles)
        {
            pedidas.TryGetValue(libre.Key, out int veces);
            distractoras += Mathf.Max(0, libre.Value - veces);
        }

        // Sin fichas de sobra el último hueco se resuelve por eliminación, sin mirar la figura.
        if (problemas == 0 && distractoras == 0)
            Debug.LogWarning($"[Escenario4] No hay ninguna ficha distractora: con " +
                             $"{fichas.Length} fichas para {panel.Length} huecos, el último se " +
                             "resuelve descartando en vez de comparando.", this);

        Debug.Log($"[Escenario4] Comprobación · {panel.Length} huecos · {fichas.Length} fichas · " +
                  $"{distractoras} distractoras · {problemas} problema(s)", this);
    }

    private ShapeSocket[] ResolveSockets() => sockets != null && sockets.Length > 0
        ? sockets
        : GetComponentsInChildren<ShapeSocket>(true);

    /// <summary>
    /// Aquí sí se puede buscar a ciegas, al revés que con el BlockResetter del Awake: hay un
    /// resetter por escenario y por eso aquel se niega a adivinar, pero los ShapeChip solo
    /// existen en este escenario y no hay con qué confundirlos. El resetter, si está asignado,
    /// se usa por acotar, no por seguridad.
    /// </summary>
    private ShapeChip[] ResolveChips() => chipResetter != null
        ? chipResetter.GetComponentsInChildren<ShapeChip>(true)
        : FindObjectsByType<ShapeChip>(FindObjectsInactive.Include);
#endif

    [ContextMenu("Reset Scenario")]
    public void ResetScenario()
    {
        scenarioFinished = false;

        foreach (ShapeSocket socket in sockets)
            if (socket != null) socket.ResetSocket();

        if (chipResetter != null) chipResetter.ResetBlocks();
    }
}
