using UnityEngine;

/// <summary>
/// Contenido de un módulo de la sala de sistemas: qué está averiado y qué opciones
/// se ofrecen para repararlo.
///
/// Es un asset y no campos en la escena para poder preparar sets distintos por dificultad
/// (básica con menos distractores, intermedia con más) sin duplicar objetos, y para que el
/// contenido lo pueda revisar alguien que no toque Unity.
/// </summary>
[CreateAssetMenu(fileName = "Modulo", menuName = "Codea/Escenario 2/Módulo")]
public class ModuleData : ScriptableObject
{
    [Tooltip("Id corto y estable para telemetría: 'motores', 'generadores', 'enfriamiento'. " +
             "No lo cambies una vez empezada la recolección de datos.")]
    public string moduleId = "motores";

    [Tooltip("Nombre visible en la pantalla del módulo.")]
    public string moduleName = "Tanque de motores";

    [TextArea]
    [Tooltip("Problema que muestra la pantalla, p. ej. 'Falta combustible'.")]
    public string problem = "Falta combustible";

    [Tooltip("Texto de cada opción. El orden debe coincidir con el optionIndex de los botones.")]
    public string[] options = { "Agregar agua", "Agregar combustible" };

    [Tooltip("Posición de la opción correcta dentro de 'options'.")]
    public int correctOptionIndex = 1;

    public bool IsValid =>
        options != null && options.Length > 0 &&
        correctOptionIndex >= 0 && correctOptionIndex < options.Length;

    /// <summary>Texto de una opción, tolerante a índices fuera de rango para no romper la telemetría.</summary>
    public string OptionAt(int index) =>
        options != null && index >= 0 && index < options.Length
            ? options[index]
            : $"(opción {index} inexistente)";
}
