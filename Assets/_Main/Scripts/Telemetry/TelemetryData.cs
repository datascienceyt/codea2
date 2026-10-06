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
    /// <summary>Instrucciones en lenguaje natural: "Avanzar", "Girar Derecha", "Avanzar 2"...</summary>
    public List<string> sequence = new List<string>();

    public bool solved;

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
/// Escenario 1 — Secuencialidad. Se resuelve montando bloques en el SocketRow.
///
/// No comparte clase con el Escenario 3 aunque los dos usen bloques: hubo una base común
/// (BlockScenarioRecord) y cada cambio pedido para uno arrastraba al otro. Los campos que
/// coinciden hoy se declaran en los dos, y así cada escenario puede cambiar por su cuenta.
/// </summary>
[Serializable]
public class Scenario1Record : ScenarioRecord
{
    /// <summary>
    /// Ejecuciones que terminaron sin resolver el escenario. Como el reinicio es automático
    /// al fallar, equivale a attempts.Count - 1 cuando el niño acaba resolviendo.
    /// </summary>
    public int failedAttempts;

    /// <summary>Veces que el jugador agarró un bloque.</summary>
    public int blocksGrabbed;

    /// <summary>
    /// Veces que el jugador encajó un bloque en un hueco. Sustituye a blocksReleased: soltar
    /// un bloque en cualquier parte no dice nada, conectarlo sí.
    /// </summary>
    public int blocksConnected;

    /// <summary>Pulsaciones del botón que devuelve los bloques a su sitio.</summary>
    public int blockResets;

    /// <summary>
    /// Movimientos imposibles del robot: salirse de la rejilla o chocar con un muro o un
    /// peligro.
    /// </summary>
    public int errorCollisionBot;

    /// <summary>"Usar" donde no hay nada que usar, o delante de algo que no lo admite.</summary>
    public int errorInvalidUse;

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

    /// <summary>Texto de la acción, p. ej. "Agregar gasolina".</summary>
    public string option;

    // No hay campo 'selected': solo se registran las veces que el niño MARCA una acción. Las
    // deselecciones no van al JSON, ni la automática de una incorrecta ni la manual de una
    // correcta, así que el campo valdría siempre true.

    /// <summary>Si esa acción formaba parte de la solución.</summary>
    public bool correct;

    /// <summary>
    /// Segundos desde la pulsación anterior del escenario, o desde su inicio si es la primera.
    /// </summary>
    public float durationSeconds;

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
    /// <summary>Instrucciones de la fila, en orden. Es el cuerpo del bucle.</summary>
    public List<string> sequence = new List<string>();

    /// <summary>Veces que se repite la fila.</summary>
    public int repetitions;

    /// <summary>
    /// Con qué tipo se corrió el programa: "Barril" o "Caja".
    ///
    /// Es el argumento que el niño puso en el socket aparte, y sin él dos ejecuciones con la
    /// misma secuencia y las mismas repeticiones serían indistinguibles pese a ser tareas
    /// distintas. Vacío en un escenario que no use argumento.
    /// </summary>
    public string itemType;

    public bool solved;

    /// <summary>Mismo criterio que AttemptRecord.durationSeconds: tiempo de preparación.</summary>
    public float durationSeconds;

    public string timestamp;
}

/// <summary>
/// Escenario 3 — Bucles. Comparte métricas de bloques con el 1, pero sus intentos guardan
/// además el cuerpo del bucle y el número de repeticiones.
/// </summary>
[Serializable]
public class Scenario3Record : ScenarioRecord
{
    /// <summary>Ejecuciones que no clasificaron ningún objeto.</summary>
    public int failedAttempts;

    /// <summary>Veces que el jugador agarró un bloque o una ficha de tipo.</summary>
    public int blocksGrabbed;

    /// <summary>Veces que el jugador encajó un bloque o una ficha de tipo en un hueco.</summary>
    public int blocksConnected;

    /// <summary>
    /// Recoger de una pila vacía, soltar en el lado equivocado, o intentar ejecutar sin ficha
    /// o con 0 repeticiones.
    /// </summary>
    public int errorInvalidCommand;

    public List<LoopAttemptRecord> attempts = new List<LoopAttemptRecord>();
}

/// <summary>
/// Una ficha colocada en un hueco del panel del Escenario 4.
///
/// Guarda qué figura fue a qué hueco, no solo el acierto: con figuras abstractas que se
/// parecen entre sí, saber cuál confundió con cuál dice mucho más que un simple acierto o fallo.
/// </summary>
[Serializable]
public class PlacementRecord
{
    /// <summary>Id del hueco. Por defecto, el nombre del recorte que espera.</summary>
    public string socket;

    /// <summary>Id de la figura que se intentó colocar.</summary>
    public string chip;

    public bool correct;

    /// <summary>
    /// Segundos desde la colocación anterior, o desde el inicio del escenario si es la primera.
    /// </summary>
    public float durationSeconds;

    public string timestamp;
}

/// <summary>
/// Escenario 4 — Patrones. Encontrar la figura idéntica, sin cadena ni ejecución.
/// </summary>
[Serializable]
public class Scenario4Record : ScenarioRecord
{
    /// <summary>
    /// Veces que cogió una ficha: la señal de duda y tanteo. No hay contador de conexiones
    /// porque cada ficha encajada ya es una entrada de placements[].
    ///
    /// Lleva nombre propio y no el blocksGrabbed de los escenarios 1 y 3: aquí se agarran
    /// fichas, no bloques.
    /// </summary>
    public int chipsGrabbed;

    public List<PlacementRecord> placements = new List<PlacementRecord>();
}

/// <summary>
/// Raíz del archivo: una run completa = un usuario = un JSON.
/// </summary>
[Serializable]
public class RunRecord
{
    public string pin;

    /// <summary>Código del visor. Es también la última parte del nombre del archivo.</summary>
    public string deviceId;

    public int sessionId;
    public int difficulty;
    public string startedUtc;
    public string endedUtc;

    /// <summary>
    /// Tiempo total de juego, en segundos, desde que se abre la run hasta que se cierra.
    /// Se actualiza en cada escritura, así que un cierre inesperado deja el último valor.
    /// </summary>
    public float totalSeconds;

    public Scenario1Record escenario1 = new Scenario1Record();
    public Scenario2Record escenario2 = new Scenario2Record();
    public Scenario3Record escenario3 = new Scenario3Record();
    public Scenario4Record escenario4 = new Scenario4Record();
}
