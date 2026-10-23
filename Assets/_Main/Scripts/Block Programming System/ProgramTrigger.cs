using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProgramTrigger : MonoBehaviour
{
    public ProgramRunner runner;
    public SocketRow socketRow;

    [Header("Ejecución")]
    [Tooltip("Desactivado: solo se puede ejecutar una vez, hasta que alguien llame a " +
             "ProgramRunner.ResetRunner(). Es lo que impide relanzar la secuencia con el bot " +
             "a medio camino del laberinto.\n\n" +
             "Activado: se puede ejecutar tantas veces como haga falta, sin reiniciar nada. " +
             "Lo necesita el Escenario 3, donde hacen falta al menos dos pasadas, una por tipo.")]
    [SerializeField] private bool allowRepeatedRuns = false;

    [Tooltip("Con allowRepeatedRuns desactivado: segundos tras el reinicio automático en los que " +
             "Ejecutar no hace nada.")]
    [SerializeField] private float pressAfterResetSeconds = 0.75f;

    [Tooltip("Activado: no se ejecuta si queda algún hueco de la fila vacío. Suena una frase de " +
             "error y se registra como comando inválido.\n\nLo necesita el Escenario 3: la fila " +
             "entera es el bucle, y sin esto un programa sin su último giro corría igual y " +
             "parecía correcto. En el Escenario 1 el bot avanza hasta el primer hueco vacío, " +
             "y eso sí es un intento.")]
    [SerializeField] private bool requireFullRow = false;

    [Tooltip("Mientras se ejecuta, los bloques de la fila y la ficha de tipo no se pueden sacar ni " +
             "cambiar. Si no, el niño reordenaba a mitad de ejecución y el intento registrado no " +
             "era el que se estaba ejecutando.")]
    [SerializeField] private bool lockRowWhileRunning = true;

    [Header("Condiciones previas")]
    [Tooltip("Opcional. Componentes que implementen IRunPrecondition y puedan vetar la " +
             "ejecución: por ejemplo el socket de tipo del Escenario 3, que sin ficha no deja " +
             "arrancar. Si alguno dice que no, no se ejecuta NI se registra el intento.")]
    [SerializeField] private MonoBehaviour[] preconditions;

    [Header("Telemetría")]
    [Tooltip("Id del reto al que pertenece este botón. Debe coincidir con los de TelemetryManager.")]
    [SerializeField] private string challengeId = "escenario1";

    [Tooltip("Soluciones correctas, para el porcentaje de solución de cada intento. Las pone la " +
             "variante de dificultad del escenario al cargar la escena: no se editan aquí.")]
    [SerializeField] private List<SolutionPath> solutions = new List<SolutionPath>();

    /// <summary>Lo llama la variante de dificultad, antes de que nadie pulse.</summary>
    public void SetSolutions(IEnumerable<SolutionPath> paths) =>
        solutions = paths != null ? new List<SolutionPath>(paths) : new List<SolutionPath>();

    public IReadOnlyList<SolutionPath> Solutions => solutions;

    [ContextMenu("Press")]
    public void OnPlayPressed()
    {
        if (runner.IsRunning)
        {
            Debug.Log($"[{challengeId}] Ejecutar ignorado: el programa ya está corriendo.", this);
            return;
        }

        // HasRun solo bloquea donde una ejecución agota el reto. Donde el reto se resuelve en
        // varias pasadas, exigir un reinicio entre ellas dejaría el botón muerto a mitad de
        // partida y sin nada que explique por qué.
        if (!allowRepeatedRuns && runner.HasRun)
        {
            Debug.Log($"[{challengeId}] Ejecutar ignorado: el programa ya corrió y falta reiniciar.", this);
            return;
        }

        // Una pulsación pegada al reinicio automático es la misma de antes, que llegó tarde: en
        // una sesión del 22/10/2026 entró a los 0,18 s y repitió el intento fallido sin cambios.
        if (!allowRepeatedRuns && Time.time - runner.LastResetTime < pressAfterResetSeconds)
        {
            Debug.Log($"[{challengeId}] Ejecutar ignorado: el nivel se acaba de reiniciar.", this);
            return;
        }

        if (socketRow.FirstSocket == null) return;

        // Se comprueba ANTES de registrar el intento: a un programa que no llega a ejecutarse
        // no se le puede medir nada, y apuntarlo en attempts[] inflaría el recuento con
        // pulsaciones que no probaron ninguna solución. Del aviso y del error de lógica se
        // encarga la propia condición, que es quien sabe qué falta.
        if (!CanRun())
        {
            PlayError();
            return;
        }

        if (requireFullRow && !socketRow.IsComplete)
        {
            Debug.LogWarning($"[{challengeId}] Faltan instrucciones en la fila: el programa no se ejecuta.", this);

            if (TelemetryManager.Instance != null)
                TelemetryManager.Instance.RegisterLogicError(LogicErrorType.ComandoInvalido);

            PlayError();
            return;
        }

        // El intento se registra ANTES de ejecutar, con solved = false.
        //
        // Registrarlo al terminar perdía siempre el intento ganador: el escenario se completa
        // DENTRO de la ejecución (el bloque Usar dispara Exit.Interact), el Director avanza y
        // desactiva esta estación, y al desactivarse el GameObject Unity mata la corrutina
        // antes de llegar al registro.
        //
        // De marcar el intento como resuelto se encarga TelemetryManager.CompleteChallenge().
        if (TelemetryManager.Instance != null)
        {
            List<string> sequence = ReadSequence(socketRow.FirstSocket);
            string argument = ReadArgument();

            TelemetryManager.Instance.RegisterBlockAttempt(
                challengeId,
                sequence,
                socketRow.Repetitions,
                argument,
                SolutionScore.Percent(sequence, solutions, argument),
                ReadRemaining(argument));
        }

        StartCoroutine(RunLocked());
    }

    // Lo que se bloqueó para esta ejecución, con su estado de antes, para dejarlo igual.
    private readonly List<(Socket socket, bool accepted)> frozenSockets = new List<(Socket, bool)>();
    private readonly List<(BlockNode block, bool interactable)> frozenBlocks = new List<(BlockNode, bool)>();

    private IEnumerator RunLocked()
    {
        if (lockRowWhileRunning) Freeze();

        yield return runner.Run(socketRow.FirstSocket, socketRow.Repetitions);

        Unfreeze();
    }

    /// <summary>
    /// La fila y los sockets de las condiciones previas (la ficha de tipo del Escenario 3): ni
    /// admiten bloques ni dejan sacar los que tienen.
    /// </summary>
    private void Freeze()
    {
        Unfreeze();

        List<Socket> sockets = new List<Socket>();
        for (Socket socket = socketRow.FirstSocket; socket != null; socket = socket.Next) sockets.Add(socket);

        foreach (IRunPrecondition precondition in activePreconditions)
            if (precondition is Component component && component.TryGetComponent(out Socket own))
                sockets.Add(own);

        foreach (Socket socket in sockets)
        {
            frozenSockets.Add((socket, socket.AcceptsBlocks));
            socket.SetAcceptsBlocks(false);

            if (socket.CurrentBlock == null) continue;

            frozenBlocks.Add((socket.CurrentBlock, socket.CurrentBlock.IsInteractable));
            socket.CurrentBlock.SetInteractable(false);
        }
    }

    private void Unfreeze()
    {
        foreach ((Socket socket, bool accepted) in frozenSockets)
            if (socket != null) socket.SetAcceptsBlocks(accepted);

        foreach ((BlockNode block, bool interactable) in frozenBlocks)
            if (block != null) block.SetInteractable(interactable);

        frozenSockets.Clear();
        frozenBlocks.Clear();
    }

    /// <summary>
    /// Si la estación se apaga a mitad de ejecución (el reto se completó dentro de ella y el
    /// Director avanza), la corrutina muere sin llegar a desbloquear.
    /// </summary>
    private void OnDisable() => Unfreeze();

    public string ChallengeId => challengeId;
    public bool RequireFullRow => requireFullRow;

    private Narrator narrator;

    /// <summary>
    /// Frase de error del narrador, con su texto y su audio. Se busca desde código: el botón
    /// vive en la estación y el narrador en la nave, y un cable más es uno más que se pierde.
    /// </summary>
    private void PlayError()
    {
        if (narrator == null)
            narrator = FindAnyObjectByType<Narrator>(FindObjectsInactive.Include);

        if (narrator != null) narrator.PlayErrorLine();
    }

    /// <summary>
    /// Las condiciones que de verdad se consultan: las del inspector que implementan la
    /// interfaz, más las que se registran solas con AddPrecondition.
    ///
    /// Existe porque la lista del inspector acepta cualquier MonoBehaviour. En Basico acabó
    /// apuntando a las dos FICHAS en vez de al socket, y como no implementan la interfaz se
    /// ignoraban sin avisar: el programa corría sin ficha, itemType salía vacío en todos los
    /// intentos y las repeticiones no volvían a 0.
    /// </summary>
    private readonly List<IRunPrecondition> activePreconditions = new List<IRunPrecondition>();

    private void Awake()
    {
        if (preconditions == null) return;

        foreach (MonoBehaviour candidate in preconditions)
        {
            if (candidate == null) continue;

            if (candidate is IRunPrecondition precondition)
                AddPrecondition(precondition);
            else
                Debug.LogWarning($"[ProgramTrigger] '{name}': '{candidate.name}' ({candidate.GetType().Name}) " +
                                 "está en preconditions pero no es una condición previa; se ignora. " +
                                 "Ahí va el socket (ArmTypeSocket), no las fichas.", this);
        }
    }

    /// <summary>Para que una condición se registre sola, sin depender del inspector.</summary>
    public void AddPrecondition(IRunPrecondition precondition)
    {
        if (precondition != null && !activePreconditions.Contains(precondition))
            activePreconditions.Add(precondition);
    }

    private IRemainingCounter remainingCounter;
    private bool remainingSearched;

    /// <summary>
    /// Cuántos faltan del tipo con que se ejecuta. Se busca el reto por su id desde código: el
    /// controlador y el botón están en la misma estación, pero así no hay cable que olvidar.
    /// </summary>
    private int ReadRemaining(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return -1;

        if (!remainingSearched)
        {
            remainingSearched = true;

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IRemainingCounter counter && counter.ChallengeId == challengeId)
                {
                    remainingCounter = counter;
                    break;
                }
        }

        return remainingCounter != null ? remainingCounter.Remaining(argument) : -1;
    }

    /// <summary>Primer argumento que aporte alguna condición. Null si ninguna aporta.</summary>
    private string ReadArgument()
    {
        foreach (IRunPrecondition precondition in activePreconditions)
            if (!string.IsNullOrEmpty(precondition.RunArgument))
                return precondition.RunArgument;

        return null;
    }

    /// <summary>Si esta condición veta la ejecución de este botón.</summary>
    public bool HasPrecondition(IRunPrecondition candidate) =>
        activePreconditions.Contains(candidate) ||
        (preconditions != null && System.Array.IndexOf(preconditions, candidate as MonoBehaviour) >= 0);

    private bool CanRun()
    {
        foreach (IRunPrecondition precondition in activePreconditions)
            if (!precondition.CanRun())
                return false;

        return true;
    }

    /// <summary>
    /// Recorre la cadena de sockets igual que ProgramRunner y devuelve las instrucciones
    /// en lenguaje natural. Se detiene en el primer socket vacío.
    /// </summary>
    private List<string> ReadSequence(Socket start)
    {
        List<string> sequence = new List<string>();

        for (Socket socket = start; socket != null && !socket.IsEmpty; socket = socket.Next)
            sequence.Add(socket.CurrentBlock.InstructionLabel);

        return sequence;
    }
}
