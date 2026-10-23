using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escenario 1 según la dificultad: el nivel y los huecos de la fila. Los bloques de la paleta
/// que solo están en una dificultad llevan DifficultyOnly.
///
/// Los dos niveles comparten la geometría de la sala (salida frente al botón, robot en el
/// mismo sitio): un nivel con otra forma deja la meta lógica lejos del botón físico.
/// </summary>
public class Scenario1Difficulty : DifficultyVariant<Scenario1Difficulty.Settings>
{
    [Serializable]
    public class Settings
    {
        [Tooltip("JSON del nivel, de Assets/_Main/Levels.")]
        public TextAsset level;

        [Tooltip("Huecos de la fila. Con los justos de la solución, un camino más largo no cabe.")]
        public int sockets = 8;

        [Tooltip("Caminos correctos del nivel, para el porcentaje de solución de cada intento. " +
                 "Si hay varios, cada intento se compara con el más cercano.")]
        public List<SolutionPath> solutions = new List<SolutionPath>();
    }

    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private SocketRow row;

    protected override void Apply(Settings settings)
    {
        if (levelLoader != null && settings.level != null) levelLoader.levelJson = settings.level;
        else Debug.LogError($"[Dificultad] Escenario 1: falta el LevelLoader o el nivel en '{name}'.", this);

        if (row != null) row.SetSocketsQuantity(settings.sockets);
        else Debug.LogError($"[Dificultad] Escenario 1: falta la fila en '{name}'.", this);

        ApplySolutions(row, settings.solutions, this);
    }

    /// <summary>
    /// Pasa las soluciones al botón de ejecutar de esa fila. Se busca por la fila, que el botón
    /// ya conoce: así no hace falta un cable más en el inspector.
    /// </summary>
    public static void ApplySolutions(SocketRow row, List<SolutionPath> solutions, MonoBehaviour context)
    {
        if (row == null) return;

        if (solutions == null || solutions.Count == 0)
            Debug.LogWarning($"[Dificultad] '{context.name}' no tiene soluciones: el porcentaje de " +
                             "solución de los intentos saldrá -1.", context);

        foreach (ProgramTrigger trigger in FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (trigger.socketRow != row) continue;

            trigger.SetSolutions(solutions);
            return;
        }

        Debug.LogWarning($"[Dificultad] '{context.name}': ningún ProgramTrigger usa la fila '{row.name}'; " +
                         "los intentos saldrán sin porcentaje de solución.", context);
    }

    public void SetTargets(LevelLoader loader, SocketRow socketRow)
    {
        levelLoader = loader;
        row = socketRow;
    }
}
