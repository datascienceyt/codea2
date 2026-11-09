using UnityEngine;

/// <summary>
/// Cómo se ven las indicaciones de "aquí va" en todas las salas: los huecos de bloque de la Sala 1
/// (Scenario1Hints), los de figura de la Sala 4 (ShapeSocketHints) y los dos del tutorial
/// (TutorialController).
///
/// 09/11/2026: el ámbar original (1, 0.8, 0.15) a pleno parpadeo se veía demasiado fuerte, y la
/// primera rebaja (1, 0.82, 0.4) al 50 % quedó demasiado apagada. Ahora está a medio camino
/// entre las dos; se ajusta solo aquí.
/// </summary>
public static class HintStyle
{
    /// <summary>Ámbar. También el de la flecha y el rótulo de la Sala 1.</summary>
    public static readonly Color Color = new Color(1f, 0.81f, 0.275f);

    /// <summary>Cuánto se tiñe el hueco en lo más alto del parpadeo (0 nada, 1 el color entero).</summary>
    public const float MaxAmount = 0.75f;

    /// <summary>Velocidad del parpadeo (radianes por segundo).</summary>
    public const float PulseSpeed = 4f;

    /// <summary>Intensidad del parpadeo en este instante, entre 0 y MaxAmount.</summary>
    public static float Pulse => MaxAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed));
}
