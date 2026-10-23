using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escenario 4 según la dificultad: qué figura lleva cada ficha de la mesa. Las fichas son las
/// mismas en las dos dificultades, en el mismo sitio; lo que cambia es el dibujo.
///
/// Los huecos del panel sí son piezas distintas (cuántos hay y qué esperan cambia de una a
/// otra) y llevan DifficultyOnly. En intermedia son las seis letras de YACHAY en el alfabeto
/// de glifos cuadrados, y entre las fichas tienen que estar las seis que hacen falta: una
/// ficha acertada se queda bloqueada en su hueco, así que las letras repetidas (Y, A)
/// necesitan dos fichas.
/// </summary>
public class Scenario4Difficulty : DifficultyVariant<Scenario4Difficulty.Settings>
{
    [Serializable]
    public class Settings
    {
        [Tooltip("Una figura por ficha, en el mismo orden que la lista de fichas. Vacía: las " +
                 "fichas se quedan con la figura que tienen en la escena.")]
        public List<Sprite> chipShapes = new List<Sprite>();
    }

    [Tooltip("Las fichas de la mesa, en el orden de las figuras de cada dificultad.")]
    [SerializeField] private List<ShapeChip> chips = new List<ShapeChip>();

    public IReadOnlyList<ShapeChip> Chips => chips;

    protected override void Apply(Settings settings)
    {
        if (settings.chipShapes.Count == 0) return;

        if (settings.chipShapes.Count != chips.Count)
            Debug.LogError($"[Dificultad] Escenario 4: hay {chips.Count} fichas y " +
                           $"{settings.chipShapes.Count} figuras en '{name}'.", this);

        for (int i = 0; i < chips.Count && i < settings.chipShapes.Count; i++)
            if (chips[i] != null && settings.chipShapes[i] != null)
                chips[i].SetShape(settings.chipShapes[i]);
    }

    public void SetChips(List<ShapeChip> shapeChips) => chips = shapeChips;
}
