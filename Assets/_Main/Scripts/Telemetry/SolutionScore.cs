using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una solución correcta de un reto de bloques, para medir cuánto se acerca cada intento.
///
/// Las instrucciones van en un solo texto separado por comas, con las mismas etiquetas que el
/// JSON ("Avanzar, Girar Derecha, Usar"): así se lee y se corrige en el inspector sin abrir
/// listas anidadas, que JsonUtility y el inspector manejan mal.
/// </summary>
[Serializable]
public class SolutionPath
{
    [Tooltip("Opcional. Con qué argumento vale esta solución: \"Barril\" o \"Caja\" en el " +
             "Escenario 3. Vacío vale para cualquiera.")]
    public string argument;

    [Tooltip("Instrucciones en orden, separadas por comas. Mismas etiquetas que el JSON.")]
    public string steps;

    public SolutionPath() { }

    public SolutionPath(string argument, string steps)
    {
        this.argument = argument;
        this.steps = steps;
    }

    public List<string> Steps
    {
        get
        {
            List<string> list = new List<string>();
            if (string.IsNullOrEmpty(steps)) return list;

            foreach (string part in steps.Split(','))
            {
                string step = part.Trim();
                if (step.Length > 0) list.Add(step);
            }

            return list;
        }
    }

    public bool AppliesTo(string runArgument) =>
        string.IsNullOrEmpty(argument) || string.Equals(argument, runArgument, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Porcentaje de solución de un intento: número de instrucciones y orden, frente a la solución
/// correcta (acuerdo de la reunión de métricas).
///
/// Se usa la subsecuencia común más larga (LCS): cuenta las instrucciones del intento que
/// aparecen en el mismo orden que en la solución. Se divide entre la más larga de las dos
/// secuencias, así que faltar instrucciones resta y sobrar también:
///
///   solución A R A U, intento A R A U   → 100 %
///   solución A R A U, intento A R U     → 75 %  (falta una)
///   solución A R A U, intento A R A A U → 80 %  (sobra una)
///   solución A R A U, intento U A R A   → 75 %  (la U fuera de orden no cuenta)
///
/// Con varias soluciones válidas (la básica del Escenario 1 tiene dos caminos de 8) vale la
/// más cercana. Por eso un intento puede puntuar más de lo que daría contarlo contra una sola:
/// "Avanzar · Avanzar · Avanzar 2 · Avanzar 2 · Usar · Girar Der · Girar Izq · Girar Der" es
/// 4 de 8 contra un camino y 5 de 8 contra el otro, y vale 62,5. Lo comprueba CodeaSceneCheck.
///
/// Sin redondear (pedido del 23/10/2026): 5 de 8 es 62,5 y 2 de 3, 66,67.
/// </summary>
public static class SolutionScore
{
    /// <summary>De 0 a 100. -1 si no hay ninguna solución que aplique: el dato falta, no es un 0.</summary>
    public static float Percent(IList<string> attempt, IEnumerable<SolutionPath> solutions, string runArgument = null)
    {
        if (solutions == null) return -1f;

        float best = -1f;

        foreach (SolutionPath solution in solutions)
        {
            if (solution == null || !solution.AppliesTo(runArgument)) continue;

            List<string> steps = solution.Steps;
            if (steps.Count == 0) continue;

            best = Mathf.Max(best, Percent(attempt, steps));
        }

        return best;
    }

    public static float Percent(IList<string> attempt, IList<string> solution)
    {
        int attemptCount = attempt != null ? attempt.Count : 0;
        int longest = Mathf.Max(attemptCount, solution.Count);

        if (longest == 0) return 100f;
        if (attemptCount == 0) return 0f;

        return 100f * CommonInOrder(attempt, solution) / longest;
    }

    /// <summary>Longitud de la subsecuencia común más larga. Las secuencias son de 4 a 11 pasos.</summary>
    private static int CommonInOrder(IList<string> a, IList<string> b)
    {
        int[,] table = new int[a.Count + 1, b.Count + 1];

        for (int i = 1; i <= a.Count; i++)
            for (int j = 1; j <= b.Count; j++)
                table[i, j] = string.Equals(a[i - 1], b[j - 1], StringComparison.OrdinalIgnoreCase)
                    ? table[i - 1, j - 1] + 1
                    : Mathf.Max(table[i - 1, j], table[i, j - 1]);

        return table[a.Count, b.Count];
    }

    /// <summary>
    /// Escenario 2: lo que el niño marcó frente a las acciones correctas del módulo. Las
    /// acertadas, entre todas las que intervienen (marcadas o correctas): marcar de más resta
    /// igual que dejarse una.
    ///
    ///   correctas {agua, ventilador}, marcó {agua}                  → 50 %
    ///   correctas {agua, ventilador}, marcó {agua, ventilador, gas} → 66,67 %
    /// </summary>
    public static float SelectionPercent(ICollection<int> selected, ICollection<int> correct)
    {
        int hits = 0;
        foreach (int index in selected)
            if (correct.Contains(index)) hits++;

        int union = selected.Count + correct.Count - hits;

        return union == 0 ? 100f : 100f * hits / union;
    }
}
