using System;
using System.Collections.Generic;

// Modelo serializable del JSON de telemetría.
//
// Cada escenario tiene su PROPIA clase. No comparten estructura a propósito: no registran
// lo mismo, porque solo el 1 se resuelve con bloques. Van como campos con nombre dentro de
// RunRecord, no como lista, así el id del escenario es la propia clave del JSON.
//
// Restricción de JsonUtility: solo serializa CAMPOS públicos de clases [Serializable].
// Nada de propiedades, diccionarios ni arrays en la raíz. Una clase SIN campos se emite
// como {}, que es justo lo que queremos para los escenarios todavía por definir.

/// <summary>
/// Una pulsación del botón de ejecutar: qué secuencia había montada y si resolvió el escenario.
/// </summary>
[Serializable]
public class AttemptRecord
{
    /// <summary>1 = básica, 2 = intermedia. Hoy solo existe la 1.</summary>
    public int difficulty;

    /// <summary>Instrucciones en lenguaje natural: "Avanzar", "Girar Derecha", "Avanzar 2"...</summary>
    public List<string> sequence = new List<string>();

    /// <summary>0/1 en vez de bool, tal como se pidió el formato.</summary>
    public int solved;

    /// <summary>
    /// Segundos que el jugador tardó en preparar este intento: desde que empezó el escenario
    /// (o desde el último reinicio) hasta que pulsó Ejecutar.
    ///
    /// NO incluye el tiempo de ejecución de la secuencia, que está determinado por su longitud
    /// y no dice nada del razonamiento.
    /// </summary>
    public float durationSeconds;

    /// <summary>ISO-8601 UTC. Momento en que se pulsó Ejecutar.</summary>
    public string timestamp;
}

/// <summary>
/// Metadatos de ciclo de vida, comunes a cualquier escenario. Lo que NO es común es el
/// payload: cada escenario mide cosas distintas y las declara en su propia subclase.
/// JsonUtility serializa los campos heredados siempre que el campo se declare con el
/// tipo concreto, que es como están en RunRecord.
/// </summary>
[Serializable]
public class ScenarioRecord
{
    public bool started;
    public bool completed;

    /// <summary>Segundos desde StartChallenge hasta CompleteChallenge.</summary>
    public float totalSeconds;

    public string startedUtc;
    public string endedUtc;
}

/// <summary>
/// Métricas comunes a los escenarios que se resuelven con bloques (1 y 3). El escenario 2
/// no las hereda: se resuelve pulsando botones, no montando secuencias.
/// </summary>
[Serializable]
public class BlockScenarioRecord : ScenarioRecord
{
    /// <summary>
    /// Ejecuciones que terminaron sin resolver el escenario. Como el reinicio es automático
    /// al fallar, equivale a attempts.Count - 1 cuando el niño acaba resolviendo.
    /// </summary>
    public int failedAttempts;

    // Manipulación de bloques. Se cablean desde VRGrabEvents.onGrabbed / onReleased.
    public int blocksGrabbed;
    public int blocksReleased;

    // RF-04, taxonomía cerrada actual.
    public int errorCollisionBot;
    public int errorIncompleteSequence;
    public int errorInvalidCommand;
}

/// <summary>
/// Escenario 1 — Secuencialidad. Se resuelve montando bloques en el SocketRow.
/// </summary>
[Serializable]
public class Scenario1Record : BlockScenarioRecord
{
    public List<AttemptRecord> attempts = new List<AttemptRecord>();
}

/// <summary>
/// Una pulsación de botón en un módulo de la sala de sistemas.
/// </summary>
[Serializable]
public class SelectionRecord
{
    /// <summary>Id del módulo: "motores", "generadores", "enfriamiento".</summary>
    public string module;

    /// <summary>Texto de la opción elegida, p. ej. "Agregar combustible".</summary>
    public string option;

    /// <summary>0/1, mismo criterio que AttemptRecord.solved.</summary>
    public int correct;

    /// <summary>ISO-8601 UTC.</summary>
    public string timestamp;
}

/// <summary>
/// Escenario 2 — Condicionales. Paneles con problema y opciones; no hay cadena de bloques,
/// así que no comparte métricas con el escenario 1.
/// </summary>
[Serializable]
public class Scenario2Record : ScenarioRecord
{
    /// <summary>Elecciones incorrectas acumuladas. Es el indicador de error del escenario.</summary>
    public int wrongSelections;

    public List<SelectionRecord> selections = new List<SelectionRecord>();
}

/// <summary>
/// Un intento del Escenario 3, donde la fila entera es el cuerpo del bucle.
///
/// Guarda las dos cosas que mide el escenario: qué instrucciones puso y en qué orden
/// (abstracción: identificar la unidad que se repite) y cuántas repeticiones eligió
/// (reconocimiento de patrones: contar cuántas veces se repite).
/// </summary>
[Serializable]
public class LoopAttemptRecord
{
    public int difficulty;

    /// <summary>Instrucciones de la fila, en orden. Es el cuerpo del bucle.</summary>
    public List<string> sequence = new List<string>();

    /// <summary>Veces que se repite la fila.</summary>
    public int repetitions;

    public int solved;

    /// <summary>Mismo criterio que AttemptRecord.durationSeconds: tiempo de preparación.</summary>
    public float durationSeconds;

    public string timestamp;
}

/// <summary>
/// Escenario 3 — Bucles. Comparte métricas de bloques con el 1, pero sus intentos guardan
/// además el cuerpo del bucle y el número de repeticiones.
/// </summary>
[Serializable]
public class Scenario3Record : BlockScenarioRecord
{
    public List<LoopAttemptRecord> attempts = new List<LoopAttemptRecord>();
}

/// <summary>
/// Una ficha colocada en un hueco del panel del Escenario 4.
///
/// Guarda los lados de ambos lados de la comparación, no solo el acierto: en dificultad
/// intermedia el emparejamiento es por número de lados, y saber qué creyó equivalente el
/// jugador dice mucho más que un simple 0/1.
/// </summary>
[Serializable]
public class PlacementRecord
{
    /// <summary>Id del hueco: "estrella", "circulo", "triangulo".</summary>
    public string socket;

    /// <summary>Id de la figura que se intentó colocar.</summary>
    public string chip;

    public int chipSides;
    public int expectedSides;

    /// <summary>0/1, mismo criterio que el resto de escenarios.</summary>
    public int correct;

    public string timestamp;
}

/// <summary>
/// Escenario 4 — Patrones. Emparejamiento por atributo, sin cadena ni ejecución.
/// </summary>
[Serializable]
public class Scenario4Record : ScenarioRecord
{
    /// <summary>"forma" o "lados". Se guarda porque cambia por completo qué mide el escenario.</summary>
    public string matchMode;

    /// <summary>Colocaciones incorrectas acumuladas.</summary>
    public int wrongPlacements;

    public List<PlacementRecord> placements = new List<PlacementRecord>();
}

/// <summary>
/// Raíz del archivo: una run completa = un usuario = un JSON.
/// </summary>
[Serializable]
public class RunRecord
{
    public string pin;
    public int sessionId;
    public int difficulty;
    public string startedUtc;
    public string endedUtc;

    public Scenario1Record escenario1 = new Scenario1Record();
    public Scenario2Record escenario2 = new Scenario2Record();
    public Scenario3Record escenario3 = new Scenario3Record();
    public Scenario4Record escenario4 = new Scenario4Record();
}
