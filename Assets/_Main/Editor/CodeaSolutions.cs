using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Soluciones correctas de los retos de bloques, para el porcentaje de solución de cada intento
/// (reunión de métricas: "número de instrucciones y orden, en base a la solución correcta").
/// Lo llama la herramienta 3 al poner al día la escena de juego.
///
/// Escenario 1: los caminos que llevan a la salida en el número exacto de huecos de la fila,
/// calculados sobre el JSON de cada nivel. La básica tiene dos; la intermedia, uno.
///
/// Escenario 3: se deduce de la escena, con la misma regla del brazo (el camino corto entre su
/// posición de recogida y el destino de cada tipo). Así, si alguien cambia los destinos, la
/// solución sigue a la escena en vez de quedarse con la de antes.
///
/// En el Escenario 3, además, la fila tiene que estar completa para ejecutarse.
/// </summary>
public static class CodeaSolutions
{
    // Comprobados con el resolvedor sobre reto.json y escenario1_intermedio.json (13/10/2026).
    private static readonly string[] Scenario1Basic =
    {
        "Avanzar, Girar Derecha, Avanzar, Avanzar 2, Girar Derecha, Avanzar 2, Girar Izquierda, Usar",
        "Avanzar, Girar Derecha, Avanzar 2, Girar Derecha, Avanzar 2, Girar Izquierda, Avanzar, Usar",
    };

    private static readonly string[] Scenario1Intermediate =
    {
        "Girar Izquierda, Avanzar, Girar Izquierda, Avanzar 2, Girar Izquierda, Avanzar 2, Girar Izquierda, Avanzar, Girar Derecha, Avanzar 2, Usar",
    };

    // La básica no deja tocar la fila: viene montada y fija, igual para los dos tipos.
    private const string Scenario3Basic = "Recoger, Girar al destino, Soltar, Volver";

    public static void Apply(StringBuilder log)
    {
        Scenario1(log);
        Scenario3(log);
    }

    private static void Scenario1(StringBuilder log)
    {
        Scenario1Difficulty variant = Object.FindAnyObjectByType<Scenario1Difficulty>(FindObjectsInactive.Include);
        if (variant == null)
        {
            log.AppendLine("  ⚠ No hay Scenario1Difficulty: el Escenario 1 queda sin porcentaje de solución");
            return;
        }

        Set(variant, "basica", Scenario1Basic.Select(s => new SolutionPath(null, s)));
        Set(variant, "intermedia", Scenario1Intermediate.Select(s => new SolutionPath(null, s)));

        log.AppendLine($"  Esc. 1: soluciones para el porcentaje · básica {Scenario1Basic.Length}, " +
                       $"intermedia {Scenario1Intermediate.Length}");
    }

    private static void Scenario3(StringBuilder log)
    {
        Scenario3Difficulty variant = Object.FindAnyObjectByType<Scenario3Difficulty>(FindObjectsInactive.Include);
        Scenario3Controller controller = Object.FindAnyObjectByType<Scenario3Controller>(FindObjectsInactive.Include);

        if (variant == null || controller == null)
        {
            log.AppendLine("  ⚠ Falta Scenario3Difficulty o Scenario3Controller: el Escenario 3 queda sin porcentaje de solución");
            return;
        }

        List<SolutionPath> intermediate = Scenario3Intermediate(controller, log);
        List<SolutionPath> basic = intermediate.Select(p => new SolutionPath(p.argument, Scenario3Basic)).ToList();

        if (basic.Count == 0) basic.Add(new SolutionPath(null, Scenario3Basic));

        Set(variant, "basica", basic);
        Set(variant, "intermedia", intermediate);

        foreach (SolutionPath path in intermediate)
            log.AppendLine($"  Esc. 3 · intermedia · {path.argument}: {path.steps}");

        // La fila entera es el bucle: sin un giro, el programa corría y parecía correcto.
        SerializedProperty trigger = new SerializedObject(variant).FindProperty("row");
        SocketRow row = trigger != null ? trigger.objectReferenceValue as SocketRow : null;

        foreach (ProgramTrigger button in Object.FindObjectsByType<ProgramTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                                .Where(t => t.ChallengeId == TelemetryManager.Scenario3Id || (row != null && t.socketRow == row)))
        {
            SerializedObject so = new SerializedObject(button);
            so.FindProperty("requireFullRow").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            log.AppendLine($"  Esc. 3: '{button.name}' no ejecuta con huecos vacíos (frase de error)");
        }
    }

    /// <summary>
    /// Por tipo: Recoger, girar hacia su destino, Soltar y girar de vuelta. El sentido sale de
    /// la misma cuenta que hace RoboticArm.RotateToSlot.
    /// </summary>
    private static List<SolutionPath> Scenario3Intermediate(Scenario3Controller controller, StringBuilder log)
    {
        List<SolutionPath> paths = new List<SolutionPath>();

        SerializedObject so = new SerializedObject(controller);
        RoboticArm arm = so.FindProperty("arm").objectReferenceValue as RoboticArm
                         ?? Object.FindAnyObjectByType<RoboticArm>(FindObjectsInactive.Include);

        if (arm == null)
        {
            log.AppendLine("  ⚠ Esc. 3: no hay RoboticArm; sin solución de la intermedia");
            return paths;
        }

        SerializedObject armSo = new SerializedObject(arm);
        SerializedProperty slots = armSo.FindProperty("slots");
        int count = slots.arraySize;
        int start = Mathf.Clamp(armSo.FindProperty("startSlotIndex").intValue, 0, Mathf.Max(0, count - 1));

        SerializedProperty goals = so.FindProperty("goals");

        for (int g = 0; g < goals.arraySize; g++)
        {
            SerializedProperty goal = goals.GetArrayElementAtIndex(g);
            ArmItemType type = (ArmItemType)goal.FindPropertyRelative("type").enumValueIndex;
            Object destination = goal.FindPropertyRelative("destination").objectReferenceValue;

            int index = -1;
            for (int i = 0; i < count; i++)
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue == destination) index = i;

            if (index < 0 || count == 0)
            {
                log.AppendLine($"  ⚠ Esc. 3: el destino de {type} no es ninguna posición del brazo");
                continue;
            }

            int forward = ((index - start) % count + count) % count;
            int backward = count - forward;

            if (forward == 0 || Mathf.Min(forward, backward) != 1)
            {
                log.AppendLine($"  ⚠ Esc. 3: {type} queda a {Mathf.Min(forward, backward)} giros; " +
                               "una fila de 4 no lo resuelve y no hay solución que comparar");
                continue;
            }

            bool right = forward <= backward;
            string go = right ? "Girar Derecha" : "Girar Izquierda";
            string back = right ? "Girar Izquierda" : "Girar Derecha";

            paths.Add(new SolutionPath(type.ToString(), $"Recoger, {go}, Soltar, {back}"));
        }

        return paths;
    }

    private static void Set(Object variant, string difficulty, IEnumerable<SolutionPath> paths)
    {
        SerializedObject so = new SerializedObject(variant);
        SerializedProperty list = so.FindProperty($"{difficulty}.solutions");

        List<SolutionPath> items = paths.ToList();
        list.arraySize = items.Count;

        for (int i = 0; i < items.Count; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("argument").stringValue = items[i].argument ?? "";
            item.FindPropertyRelative("steps").stringValue = items[i].steps;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(variant);
    }
}
