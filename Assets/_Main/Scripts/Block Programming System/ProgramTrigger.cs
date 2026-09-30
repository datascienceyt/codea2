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

    [Header("Condiciones previas")]
    [Tooltip("Opcional. Componentes que implementen IRunPrecondition y puedan vetar la " +
             "ejecución: por ejemplo el socket de tipo del Escenario 3, que sin ficha no deja " +
             "arrancar. Si alguno dice que no, no se ejecuta NI se registra el intento.")]
    [SerializeField] private MonoBehaviour[] preconditions;

    [Header("Telemetría")]
    [Tooltip("Id del reto al que pertenece este botón. Debe coincidir con los de TelemetryManager.")]
    [SerializeField] private string challengeId = "escenario1";

    [ContextMenu("Press")]
    public void OnPlayPressed()
    {
        if (runner.IsRunning)
        {
            print("Programm is running");
            return;
        }

        // HasRun solo bloquea donde una ejecución agota el reto. Donde el reto se resuelve en
        // varias pasadas, exigir un reinicio entre ellas dejaría el botón muerto a mitad de
        // partida y sin nada que explique por qué.
        if (!allowRepeatedRuns && runner.HasRun)
        {
            print("Programm has run");
            return;
        }

        if (socketRow.FirstSocket == null) return;

        // Se comprueba ANTES de registrar el intento: a un programa que no llega a ejecutarse
        // no se le puede medir nada, y apuntarlo en attempts[] inflaría el recuento con
        // pulsaciones que no probaron ninguna solución. Del aviso y del error de lógica se
        // encarga la propia condición, que es quien sabe qué falta.
        if (!CanRun()) return;

        // El intento se registra ANTES de ejecutar, con solved = false.
        //
        // Registrarlo al terminar perdía siempre el intento ganador: el escenario se completa
        // DENTRO de la ejecución (el bloque Usar dispara Exit.Interact), el Director avanza y
        // desactiva esta estación, y al desactivarse el GameObject Unity mata la corrutina
        // antes de llegar al registro.
        //
        // De marcar el intento como resuelto se encarga TelemetryManager.CompleteChallenge().
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterBlockAttempt(
                challengeId,
                ReadSequence(socketRow.FirstSocket),
                socketRow.Repetitions,
                ReadArgument());

        StartCoroutine(runner.Run(socketRow.FirstSocket, socketRow.Repetitions));
    }

    public string ChallengeId => challengeId;

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
