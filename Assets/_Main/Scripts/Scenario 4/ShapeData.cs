using UnityEngine;

/// <summary>
/// Una figura geométrica del Escenario 4.
///
/// Separa la identidad de la figura (shapeId) de su atributo medible (sides), porque el
/// escenario se juega de dos formas: en básica se empareja por figura completa y en
/// intermedia por número de lados. Ese salto de "la misma forma" a "la misma propiedad"
/// es la abstracción que el escenario enseña.
/// </summary>
[CreateAssetMenu(fileName = "Figura", menuName = "Codea/Escenario 4/Figura")]
public class ShapeData : ScriptableObject
{
    [Tooltip("Id corto y estable para telemetría: 'triangulo', 'circulo', 'estrella'. " +
             "No lo cambies una vez empezada la recolección de datos.")]
    public string shapeId = "triangulo";

    [Tooltip("Nombre visible.")]
    public string displayName = "Triángulo";

    [Tooltip("Número de lados, el criterio de la dificultad intermedia. Por convención del " +
             "GDD: triángulo 3, cuadrado 4, estrella 10, círculo 1.")]
    public int sides = 3;
}
