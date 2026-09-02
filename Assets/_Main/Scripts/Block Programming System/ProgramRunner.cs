using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ProgramRunner : MonoBehaviour
{
    [Header("Eventos")]
    [Tooltip("Al terminar de recorrer la cadena, se haya resuelto el reto o no. Quien decide " +
             "si hubo éxito es el controlador del escenario, que es el que conoce la meta.")]
    public UnityEvent OnRunFinished;

    /// <summary>
    /// Tope de anidamiento de sub-cadenas. El diseño solo contempla un nivel (el bloque
    /// Repetir del escenario 3), así que esto es una red de seguridad: si por un error de
    /// montaje se encadenan varios, corta en vez de colgar el visor.
    /// </summary>
    public const int MaxNestingDepth = 4;

    static int depth;

    public bool IsRunning { get; private set; }
    public bool HasRun { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDepth() => depth = 0;

    /// <summary>
    /// Ejecuta la cadena. Con repetitions &gt; 1 la recorre varias veces seguidas: es el bucle
    /// del Escenario 3, donde la fila entera es el cuerpo del ciclo.
    /// </summary>
    public IEnumerator Run(Socket start, int repetitions = 1)
    {
        IsRunning = true;

        // Se reinicia por si una ejecución anterior se cortó a media corrutina y dejó
        // el contador sin cerrar.
        depth = 0;

        for (int i = 0; i < Mathf.Max(1, repetitions); i++)
            yield return ExecuteChain(start);

        // El estado se deja consistente ANTES de avisar: quien escuche puede querer
        // consultar IsRunning o HasRun, o llamar a ResetRunner().
        IsRunning = false;
        HasRun = true;

        OnRunFinished?.Invoke();
    }

    /// <summary>
    /// Recorre una cadena de sockets ejecutando cada bloque, y se detiene en el primer hueco
    /// vacío.
    ///
    /// Es estático y sin estado porque lo usan dos sitios: Run(), para la cadena principal, y
    /// RepeatBlock, para su sub-cadena. Así el runner no necesita saber que existen los bucles.
    /// </summary>
    public static IEnumerator ExecuteChain(Socket start)
    {
        if (depth >= MaxNestingDepth)
        {
            Debug.LogError($"[ProgramRunner] Anidamiento por encima de {MaxNestingDepth}; se corta la ejecución.");
            yield break;
        }

        depth++;

        Socket current = start;
        while (current != null && !current.IsEmpty)
        {
            yield return current.CurrentBlock.Execute();
            current = current.Next;
        }

        depth--;
    }

    public void ResetRunner()
    {
        HasRun = false;
    }
}
