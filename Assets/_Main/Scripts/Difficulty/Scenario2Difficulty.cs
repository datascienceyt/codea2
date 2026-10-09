using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escenario 2 según la dificultad: qué ModuleData lleva cada módulo. La dificultad vive en
/// los datos (cuántas opciones son correctas, el enunciado); la mecánica es la misma.
///
/// Los moduleId tienen que coincidir entre los dos assets de un módulo para que la
/// telemetría agregue por módulo sin mirar la dificultad.
/// </summary>
public class Scenario2Difficulty : DifficultyVariant<Scenario2Difficulty.Settings>
{
    [Serializable]
    public class Settings
    {
        [Tooltip("Un asset por módulo, en el mismo orden que la lista de módulos.")]
        public List<ModuleData> modules = new List<ModuleData>();
    }

    [Tooltip("Los módulos del escenario, en el orden de los datos de cada dificultad.")]
    [SerializeField] private List<SystemModule> modules = new List<SystemModule>();

    protected override void Apply(Settings settings)
    {
        if (settings.modules.Count != modules.Count)
            Debug.LogError($"[Dificultad] Escenario 2: hay {modules.Count} módulos y " +
                           $"{settings.modules.Count} datos en '{name}'.", this);

        for (int i = 0; i < modules.Count && i < settings.modules.Count; i++)
        {
            if (modules[i] == null || settings.modules[i] == null) continue;

            if (modules[i].Data != null && settings.modules[i].moduleId != modules[i].Data.moduleId)
                Debug.LogWarning($"[Dificultad] Escenario 2: '{settings.modules[i].name}' tiene otro " +
                                 $"moduleId que '{modules[i].Data.name}'; la telemetría no los agregará.", this);

            modules[i].SetData(settings.modules[i]);
        }
    }

    public void SetModules(List<SystemModule> systemModules) => modules = systemModules;
}
