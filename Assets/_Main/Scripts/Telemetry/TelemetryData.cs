using System;
using System.Collections.Generic;

// Modelo serializable del JSON de telemetría, formato 1 (17/11/2026). El diccionario completo,
// con cuándo se escribe cada campo y su unidad, está en TELEMETRIA.md, en la raíz del proyecto.
//
// Cada escenario tiene su PROPIA clase. No comparten estructura a propósito: no registran
// lo mismo, porque solo el 1 se resuelve con bloques. Van como campos con nombre dentro de
// RunRecord, no como lista, así el id del escenario es la propia clave del JSON.
//
// Restricciones de JsonUtility y del contrato del formato 1:
//   · Solo CAMPOS públicos de clases [Serializable]: nada de propiedades ni diccionarios
//     (las parejas tipo-cantidad van como lista de objetos).
//   · Nunca null: un string sin valor sale "" y un número desconocido, -1, nunca un 0 inventado.
//   · El orden de declaración es el orden del JSON.
//   · Los booleanos que ya existían (started, solved, correct...) siguen saliendo true/false;
//     las banderas nuevas del formato 1 (selected, phaseCompleted) son enteros 0/1, como pide
//     el contrato. El dashboard lee las dos formas.

/// <summary>Valores de endReason. Son texto en el JSON; aquí, para no escribirlos a mano.</summary>
public static class EndReasons
{
    // Raíz
    public const string Completed = "completed";
    public const string Quit = "quit";
    public const string AppClosed = "appClosed";

    // Escenario (además de Completed y AppClosed)
    public const string NotStarted = "notStarted";
    public const string TimedOut = "timedOut";
    public const string Abandoned = "abandoned";

    /// <summary>Escenario abierto o run sin cerrar: "sin cierre confirmado" para el dashboard.</summary>
    public const string Open = "";
}

/// <summary>Valores de failReason del Escenario 1.</summary>
public static class FailReasons
{
    public const string None = "";
    public const string Collision = "collision";
    public const string InvalidUse = "invalidUse";
    public const string IncompleteSequence = "incompleteSequence";
    public const string Other = "other";
}

/// <summary>Valores de reason de una pausa.</summary>
public static class PauseReasons
{
    public const string HeadsetRemoved = "headsetRemoved";
    public const string Menu = "menu";
    public const string FocusLost = "focusLost";
}

/// <summary>Un tramo en que el reto no corría: visor quitado, menú del sistema o foco perdido.</summary>
[Serializable]
public class PauseRecord
{
    public string startUtc = "";

    /// <summary>"" mientras la pausa sigue abierta (solo en un guardado a mitad de pausa).</summary>
    public string endUtc = "";

    public string reason = "";
}

/// <summary>
/// Una pulsación del botón de ejecutar del Escenario 1: qué secuencia había montada y si
/// resolvió el escenario.
/// </summary>
[Serializable]
public class AttemptRecord
{
    /// <summary>Orden del evento en toda la sesión, de 1 en adelante, único.</summary>
    public int seq;

    /// <summary>Instrucciones en lenguaje natural: "Avanzar", "Girar Derecha", "Avanzar 2"...</summary>
    public List<string> sequence = new List<string>();

    public bool solved;

    /// <summary>
    /// Por qué falló: collision | invalidUse | incompleteSequence | other. "" si solved.
    /// Mientras el robot está ejecutando vale "other"; se fija al terminar la ejecución.
    /// </summary>
    public string failReason = FailReasons.Other;

    /// <summary>
    /// Cuánto se parece la secuencia a la solución correcta, de 0 a 100: número de
    /// instrucciones y orden (ver SolutionScore). -1 si el nivel no tiene solución cargada.
    /// </summary>
    public float solutionPercent = -1f;

    /// <summary>
    /// Segundos de reto (sin pausas) que el jugador tardó en preparar este intento: desde que
    /// se abrió el reto, desde la pulsación anterior o desde el último reinicio, lo último que
    /// haya pasado, hasta que pulsó Ejecutar. No incluye la ejecución.
    /// </summary>
    public float durationSeconds;

    /// <summary>ISO-8601 UTC. Momento en que se pulsó Ejecutar.</summary>
    public string timestamp = "";
}

/// <summary>
/// Ciclo de vida, común a cualquier escenario. Lo que NO es común es el payload: cada
/// escenario lo declara en su propia subclase.
/// </summary>
[Serializable]
public class ScenarioRecord
{
    /// <summary>Se entró en la sala (empezó su narración).</summary>
    public bool started;

    public bool completed;

    /// <summary>Se agotaron los minutos de la sala sin resolverla. Nunca a la vez que completed.</summary>
    public bool timedOut;

    /// <summary>notStarted | completed | timedOut | abandoned | appClosed. "" mientras está abierto.</summary>
    public string endReason = EndReasons.NotStarted;

    /// <summary>Entrada en la sala, con su narración.</summary>
    public string startedUtc = "";

    /// <summary>Desde cuándo el reto acepta acciones. "" si no llegó a abrirse.</summary>
    public string challengeStartedUtc = "";

    public string endedUtc = "";

    /// <summary>
    /// Segundos de reto sin pausas: endedUtc − challengeStartedUtc − pausedSeconds. Con el
    /// escenario abierto, lo jugado hasta el último guardado. 0 si el reto no llegó a abrirse.
    /// </summary>
    public float totalSeconds;

    /// <summary>Suma de pauses, recortadas al tramo del reto.</summary>
    public float pausedSeconds;

    public List<PauseRecord> pauses = new List<PauseRecord>();
}

/// <summary>
/// Escenario 1 — Secuencialidad. Se resuelve montando bloques en el SocketRow.
///
/// No comparte clase con el Escenario 3 aunque los dos usen bloques: cada cambio pedido para
/// uno arrastraba al otro.
/// </summary>
[Serializable]
public class Scenario1Record : ScenarioRecord
{
    /// <summary>Ejecuciones con solved = false. Se recalcula en cada guardado: no puede descuadrar.</summary>
    public int failedAttempts;

    /// <summary>Veces que el jugador agarró un bloque con el reto abierto.</summary>
    public int blocksGrabbed;

    /// <summary>Veces que el jugador encajó un bloque en un hueco con el reto abierto.</summary>
    public int blocksConnected;

    /// <summary>Pulsaciones del botón que devuelve los bloques a su sitio.</summary>
    public int blockResets;

    /// <summary>Movimientos imposibles del robot: salirse de la rejilla o chocar con un muro.</summary>
    public int errorCollisionBot;

    /// <summary>"Usar" donde no hay nada que usar, o delante de algo que no lo admite.</summary>
    public int errorInvalidUse;

    public List<AttemptRecord> attempts = new List<AttemptRecord>();
}

/// <summary>Una pulsación de un botón de acción en un módulo de la sala de sistemas.</summary>
[Serializable]
public class SelectionRecord
{
    public int seq;

    /// <summary>Id del módulo: "motores", "generadores", "enfriamiento".</summary>
    public string module = "";

    /// <summary>Texto de la acción, p. ej. "Agregar gasolina".</summary>
    public string option = "";

    /// <summary>1 = la marcó; 0 = la desmarcó (deselección).</summary>
    public int selected = 1;

    /// <summary>Si esa acción forma parte de la solución (también en una deselección).</summary>
    public bool correct;

    /// <summary>Segundos de reto desde la pulsación anterior de esta lista, o desde que se abrió el reto.</summary>
    public float durationSeconds;

    public string timestamp = "";
}

/// <summary>Una pulsación del botón Probar de un módulo: lo que había marcado y si lo reparó.</summary>
[Serializable]
public class ModuleAttemptRecord
{
    public int seq;

    /// <summary>Id del módulo: "motores", "generadores", "enfriamiento".</summary>
    public string module = "";

    /// <summary>Acciones marcadas al pulsar Probar, en el orden de los botones.</summary>
    public List<string> selection = new List<string>();

    public int correctSelected;
    public int wrongSelected;

    public bool solved;

    /// <summary>Acertadas entre todas las que intervienen (marcadas o correctas), de 0 a 100.</summary>
    public float solutionPercent;

    /// <summary>Segundos de reto desde el Probar anterior del escenario, o desde que se abrió el reto.</summary>
    public float durationSeconds;

    public string timestamp = "";
}

/// <summary>Escenario 2 — Condicionales. Paneles con problema y opciones.</summary>
[Serializable]
public class Scenario2Record : ScenarioRecord
{
    /// <summary>Pulsaciones de Probar que no repararon el módulo (= attempts con solved = false).</summary>
    public int failedAttempts;

    /// <summary>Selecciones con selected = 1 y correct = false.</summary>
    public int wrongSelections;

    /// <summary>Ids de los módulos que hay que reparar en esta versión.</summary>
    public List<string> modulesRequired = new List<string>();

    /// <summary>Cada vez que el niño marca o desmarca una acción.</summary>
    public List<SelectionRecord> selections = new List<SelectionRecord>();

    /// <summary>Cada pulsación de Probar con algo marcado.</summary>
    public List<ModuleAttemptRecord> attempts = new List<ModuleAttemptRecord>();
}

/// <summary>
/// Un intento del Escenario 3, donde la fila entera es el cuerpo del bucle.
/// </summary>
[Serializable]
public class LoopAttemptRecord
{
    public int seq;

    /// <summary>Instrucciones de la fila, en orden. Es el cuerpo del bucle.</summary>
    public List<string> sequence = new List<string>();

    /// <summary>Veces que se repite la fila.</summary>
    public int repetitions;

    /// <summary>Tipo con que se corrió: "Barril" o "Caja". "" si no había ficha de tipo.</summary>
    public string itemType = "";

    /// <summary>Objetos de ese tipo que quedaban por trasladar ANTES de ejecutar. -1 sin tipo.</summary>
    public int remaining = -1;

    /// <summary>Objetos de ese tipo que esta ejecución dejó en su destino. -1 sin tipo.</summary>
    public int moved = -1;

    /// <summary>1 si esta ejecución dejó ese tipo en 0 (quedaba alguno antes y ya no queda ninguno).</summary>
    public int phaseCompleted;

    /// <summary>true solo en la ejecución que completa el escenario.</summary>
    public bool solved;

    /// <summary>
    /// Cuánto se parece la secuencia a la solución de ese tipo, de 0 a 100 (ver SolutionScore).
    /// No mide las repeticiones. -1 si no hay solución cargada.
    /// </summary>
    public float solutionPercent = -1f;

    /// <summary>Mismo criterio que AttemptRecord.durationSeconds: tiempo de preparación.</summary>
    public float durationSeconds;

    public string timestamp = "";
}

/// <summary>Cuántos objetos de un tipo hay que trasladar.</summary>
[Serializable]
public class ItemCountRecord
{
    public string itemType = "";
    public int count;
}

/// <summary>Escenario 3 — Bucles.</summary>
[Serializable]
public class Scenario3Record : ScenarioRecord
{
    /// <summary>Veces que el jugador agarró un bloque o una ficha de tipo con el reto abierto.</summary>
    public int blocksGrabbed;

    /// <summary>Veces que el jugador encajó un bloque o una ficha de tipo en un hueco.</summary>
    public int blocksConnected;

    /// <summary>
    /// Recoger de una pila vacía, soltar en el lado equivocado, o intentar ejecutar sin ficha,
    /// con 0 repeticiones o con algún hueco de la fila vacío.
    /// </summary>
    public int errorInvalidCommand;

    /// <summary>
    /// Está por el contrato del formato 1. El brazo gira en su sitio y no puede chocar con nada:
    /// en esta versión del juego nada lo incrementa y vale siempre 0.
    /// </summary>
    public int errorCollisionBot;

    /// <summary>Cuántos hay que trasladar de cada tipo en esta versión.</summary>
    public List<ItemCountRecord> itemsRequired = new List<ItemCountRecord>();

    public List<LoopAttemptRecord> attempts = new List<LoopAttemptRecord>();
}

/// <summary>Una ficha colocada en un hueco del panel del Escenario 4.</summary>
[Serializable]
public class PlacementRecord
{
    public int seq;

    /// <summary>Id del hueco: el nombre del recorte que espera.</summary>
    public string socket = "";

    /// <summary>Id de la figura que se intentó colocar.</summary>
    public string chip = "";

    public bool correct;

    /// <summary>Segundos de reto desde la colocación anterior, o desde que se abrió el reto.</summary>
    public float durationSeconds;

    public string timestamp = "";
}

/// <summary>Escenario 4 — Patrones. Encontrar la figura idéntica.</summary>
[Serializable]
public class Scenario4Record : ScenarioRecord
{
    /// <summary>Veces que cogió una ficha con el reto abierto.</summary>
    public int chipsGrabbed;

    /// <summary>Veces que soltó una ficha cuyo agarre se contó. Nunca mayor que chipsGrabbed.</summary>
    public int chipsReleased;

    /// <summary>Colocaciones con correct = false.</summary>
    public int wrongPlacements;

    /// <summary>Ids de los huecos del panel de esta versión.</summary>
    public List<string> socketsRequired = new List<string>();

    public List<PlacementRecord> placements = new List<PlacementRecord>();
}

/// <summary>Raíz del archivo: una run completa = un participante = un JSON.</summary>
[Serializable]
public class RunRecord
{
    /// <summary>Versión del formato del JSON. Constante: TelemetryManager.FormatVersion.</summary>
    public int formatVersion;

    /// <summary>Application.version del APK que grabó la sesión.</summary>
    public string appVersion = "";

    public string pin = "";

    /// <summary>Código del visor. Es también la última parte del nombre del archivo.</summary>
    public string deviceId = "";

    public int sessionId;
    public int difficulty;
    public string startedUtc = "";
    public string endedUtc = "";

    /// <summary>completed | quit | appClosed. "" mientras la run sigue abierta.</summary>
    public string endReason = EndReasons.Open;

    /// <summary>
    /// Segundos de sesión sin pausas, desde startedUtc hasta endedUtc (o hasta el último
    /// guardado con la run abierta).
    /// </summary>
    public float totalSeconds;

    public Scenario1Record escenario1 = new Scenario1Record();
    public Scenario2Record escenario2 = new Scenario2Record();
    public Scenario3Record escenario3 = new Scenario3Record();
    public Scenario4Record escenario4 = new Scenario4Record();
}
