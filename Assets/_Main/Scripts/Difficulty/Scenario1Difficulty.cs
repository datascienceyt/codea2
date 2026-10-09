using System;
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
    }

    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private SocketRow row;

    protected override void Apply(Settings settings)
    {
        if (levelLoader != null && settings.level != null) levelLoader.levelJson = settings.level;
        else Debug.LogError($"[Dificultad] Escenario 1: falta el LevelLoader o el nivel en '{name}'.", this);

        if (row != null) row.SetSocketsQuantity(settings.sockets);
        else Debug.LogError($"[Dificultad] Escenario 1: falta la fila en '{name}'.", this);
    }

    public void SetTargets(LevelLoader loader, SocketRow socketRow)
    {
        levelLoader = loader;
        row = socketRow;
    }
}
