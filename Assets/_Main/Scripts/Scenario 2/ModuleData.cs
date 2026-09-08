using System.Collections.Generic;
using UnityEngine;

/// <summary>Una acción que se ofrece para reparar el módulo.</summary>
[System.Serializable]
public class ModuleOption
{
    [Tooltip("Texto del botón, p. ej. 'Agregar gasolina'.")]
    public string label = "Opción";

    [Tooltip("Si forma parte de la solución. Puede haber varias correctas en el mismo módulo.")]
    public bool isCorrect;
}

/// <summary>
/// Contenido de un módulo de la sala de sistemas: qué está averiado y qué acciones se ofrecen.
///
/// La dificultad se define aquí, en los datos, no en código:
///   Básica     → 2 opciones, 1 correcta
///   Intermedia → 4 opciones, 2 correctas (o 3 y 1, etc.)
///
/// Es un asset y no campos en la escena para poder preparar un set por dificultad sin duplicar
/// objetos, y para que el contenido lo pueda revisar alguien que no toque Unity.
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

    [Tooltip("Acciones ofrecidas, en el mismo orden que los botones de la escena.")]
    public List<ModuleOption> options = new List<ModuleOption>();

    /// <summary>Cuántas acciones hay que seleccionar para resolver el módulo.</summary>
    public int CorrectCount
    {
        get
        {
            if (options == null) return 0;

            int count = 0;
            foreach (ModuleOption option in options)
                if (option != null && option.isCorrect) count++;

            return count;
        }
    }

    public bool IsValid => options != null && options.Count > 0 && CorrectCount > 0;

    public bool IsCorrectAt(int index) =>
        options != null && index >= 0 && index < options.Count &&
        options[index] != null && options[index].isCorrect;

    /// <summary>Texto de una opción, tolerante a índices fuera de rango para no romper la telemetría.</summary>
    public string LabelAt(int index) =>
        options != null && index >= 0 && index < options.Count && options[index] != null
            ? options[index].label
            : $"(opción {index} inexistente)";
}
