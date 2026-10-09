using System;
using UnityEngine;

/// <summary>
/// Narración según la dificultad: qué lista del Narrator suena. Las listas se diferencian en
/// las variantes del CSV (9.1/9.2 en el Escenario 2, 14.1/14.2 en el 3) y tienen que tener
/// las mismas entradas en el mismo orden: el Director reproduce una por cada espera.
/// </summary>
public class NarrationDifficulty : DifficultyVariant<NarrationDifficulty.Settings>
{
    [Serializable]
    public class Settings
    {
        [Tooltip("Índice de la lista en el Narrator. 1 = Victor (Basica), 2 = Victor (Intermedia).")]
        public int listIndex = 1;
    }

    [SerializeField] private Narrator narrator;

    protected override void Apply(Settings settings)
    {
        if (narrator == null) narrator = GetComponent<Narrator>();

        if (narrator != null) narrator.SetAudioListIndex(settings.listIndex);
        else Debug.LogError($"[Dificultad] Narración: falta el Narrator en '{name}'.", this);
    }

    public void SetNarrator(Narrator target) => narrator = target;
}
