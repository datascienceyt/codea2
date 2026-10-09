using System;
using UnityEngine;

/// <summary>
/// Lo que cambia de un reto según la dificultad. Cada reto tiene el suyo, junto a su
/// controlador, y en el inspector muestra lado a lado los datos de la básica y los de la
/// intermedia: para saber en qué se diferencian las dos versiones de un reto basta con
/// mirar ese componente.
///
/// Los datos se cambian aquí; las piezas que solo existen en una dificultad (bloques de
/// sobra, otras fichas) se marcan con DifficultyOnly. DifficultyApplier llama a Apply una
/// vez, al cargar la escena y antes que el Awake de cualquier otro script.
/// </summary>
public abstract class DifficultyVariant : MonoBehaviour
{
    public abstract void Apply(Difficulty difficulty);
}

/// <summary>Variante con un juego de datos por dificultad, del tipo que necesite el reto.</summary>
public abstract class DifficultyVariant<TSettings> : DifficultyVariant where TSettings : class, new()
{
    [Tooltip("Datos del reto en básica.")]
    [SerializeField] protected TSettings basica = new TSettings();

    [Tooltip("Datos del reto en intermedia.")]
    [SerializeField] protected TSettings intermedia = new TSettings();

    public TSettings Basica => basica;
    public TSettings Intermedia => intermedia;

    public override void Apply(Difficulty difficulty) =>
        Apply(difficulty == Difficulty.Basica ? basica : intermedia);

    protected abstract void Apply(TSettings settings);

    /// <summary>Para las herramientas de montaje: rellena los dos juegos de datos de una vez.</summary>
    public void SetSettings(TSettings basic, TSettings intermediate)
    {
        basica = basic ?? throw new ArgumentNullException(nameof(basic));
        intermedia = intermediate ?? throw new ArgumentNullException(nameof(intermediate));
    }
}
