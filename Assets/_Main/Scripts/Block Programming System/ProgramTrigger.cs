using System.Collections.Generic;
using UnityEngine;

public class ProgramTrigger : MonoBehaviour
{
    public ProgramRunner runner;
    public SocketRow socketRow;

    [Header("Telemetría")]
    [Tooltip("Id del reto al que pertenece este botón. Debe coincidir con los de TelemetryManager.")]
    [SerializeField] private string challengeId = "escenario1";

    [ContextMenu("Press")]
    public void OnPlayPressed()
    {
        if (runner.IsRunning || runner.HasRun)
        {
            print("Programm is running or has run");
            return;
        }

        if (socketRow.FirstSocket == null) return;

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
                socketRow.Repetitions);

        StartCoroutine(runner.Run(socketRow.FirstSocket, socketRow.Repetitions));
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
