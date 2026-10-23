using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ProgramRunner : MonoBehaviour
{
    [Header("Eventos")]
    [Tooltip("Al terminar de recorrer la cadena, se haya resuelto el reto o no. Quien decide " +
             "si hubo éxito es el controlador del escenario, que es el que conoce la meta.")]
    public UnityEvent OnRunFinished;

    public bool IsRunning { get; private set; }
    public bool HasRun { get; private set; }

    /// <summary>
    /// Ejecuta la cadena. Con repetitions &gt; 1 la recorre varias veces seguidas: es el bucle
    /// del Escenario 3, donde la fila entera es el cuerpo del ciclo.
    /// </summary>
    public IEnumerator Run(Socket start, int repetitions = 1)
    {
        IsRunning = true;

        for (int i = 0; i < Mathf.Max(1, repetitions); i++)
            yield return ExecuteChain(start);

        // El estado se deja consistente ANTES de avisar: quien escuche puede querer
        // consultar IsRunning o HasRun, o llamar a ResetRunner().
        IsRunning = false;
        HasRun = true;

        OnRunFinished?.Invoke();
    }

    /// <summary>Ejecuta cada bloque de la cadena y se detiene en el primer hueco vacío.</summary>
    private static IEnumerator ExecuteChain(Socket start)
    {
        for (Socket current = start; current != null && !current.IsEmpty; current = current.Next)
            yield return current.CurrentBlock.Execute();
    }

    public void ResetRunner()
    {
        HasRun = false;
    }
}
