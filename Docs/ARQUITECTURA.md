# Arquitectura técnica — Codea VR 2

> Estado verificado contra el código el **25/08/2026**. Este documento describe el sistema
> **tal como está implementado**, no como se planeó. Para el contexto pedagógico y narrativo
> ver `CONTEXTO_CODEA_VR_2.md`; para requisitos formales, el SRS (IEEE 830).
>
> Cuando ambos documentos se contradigan, manda este.

---

## 1. Estado del proyecto

| Subsistema | Estado |
|---|---|
| Sistema de bloques (Escenario 1) | Funcional y probado en visor |
| Bot y grid | Funcional |
| Sistema narrativo (Director) | Funcional |
| Telemetría JSON + subida | Funcional de punta a punta |
| Escenario 2 (módulos de sistemas) | Código completo y **montado en escena** |
| Escenario 3 (bucles) | Código completo y montado. Mecánica del brazo **sin validar** |
| Escenario 4 (patrones) | **Código completo, sin montar en escena** |
| Narración en pantalla (`ScreenNarrator`) | Código completo, alimentado por CSV |
| Selección de dificultad | **Código completo, sin montar en escena** |
| HUD diegético (RI-02) | No implementado |
| Username (RI-01 / RF-01) | No implementado — se usa PIN |
| Temporizador de sesión (RF-06) | Código listo: cuenta atrás, avisos por umbral y `OnTimeUp`. Falta cablearlo en escena |
| Reinicio supervisado (RF-08) | No implementado |

## 2. Stack y restricciones

- Unity `6000.4.1f1`, URP + Shader Graph
- Meta XR All-in-One SDK `203.0.2`, Horizon OS
- Meta Quest 3S exclusivamente, solo mandos físicos, locomoción por teletransporte
- Serialización JSON con **`JsonUtility`** (`com.unity.modules.jsonserialize`)
- **Newtonsoft NO es dependencia directa.** Solo aparece en `PackageCache` de forma
  transitiva; no debe usarse sin añadirlo antes a `manifest.json`

### Consecuencias de usar JsonUtility

Condiciona el modelo de datos y no es negociable sin añadir una dependencia:

- Solo serializa **campos públicos** de clases `[Serializable]`. Nada de propiedades
- No admite `Dictionary`, ni arrays en la raíz, ni polimorfismo
- **No puede omitir campos**: siempre emite todos, con su valor por defecto
- Sí serializa campos **heredados**, siempre que el campo se declare con el tipo concreto
- Una clase **sin campos** se emite como `{}` — es lo que mantiene `escenario3` vacío

## 3. Mapa de subsistemas

### Programación por bloques — `Scripts/Block Programming System/`

| Archivo | Responsabilidad |
|---|---|
| `BlockNode.cs` | Base abstracta. Snap contra `Socket`, gestión de agarre, `InstructionLabel` |
| `GridBlock.cs` | Única implementación concreta. Enum + switch de acciones |
| `StartBlock.cs` | Bloque inicial. `Clear()` está roto (ver §11) |
| `Socket.cs` | Un hueco. Encadenado por `Next`, guarda `CurrentBlock` |
| `SocketRow.cs` | Genera N sockets por `Lerp` entre `start` y `end`, los encadena |
| `ProgramRunner.cs` | Recorre la cadena de sockets ejecutando cada bloque |
| `ProgramTrigger.cs` | Botón de ejecutar. Registra el intento en telemetría |
| `BlockResetter.cs` | Devuelve los bloques a su posición inicial sin instanciar ni destruir |
| `BlockGenerator.cs` | **Obsoleto.** Sustituido por `BlockResetter` |

### Escenario 3 — `Scripts/Scenario 3/`

`RoboticArm.cs`, `ArmSlot.cs`, `ArmBlock.cs`, `Scenario3Controller.cs`. Ver §7.

### Escenario 4 — `Scripts/Scenario 4/`

`ShapeData.cs`, `ShapeChip.cs`, `ShapeSocket.cs`, `Scenario4Controller.cs`. Ver §7b.

### Narración en pantalla

`Story/ScreenNarrator.cs` — `IStepAction` que escribe texto progresivamente desde un CSV
(`ID_Texto, Texto_Narrativa`) y bloquea el paso hasta terminar. Va en paralelo con `Narrator`
en el mismo Step (modo `All`): la voz suena mientras el texto se escribe.

Trae parser de CSV propio, porque `Split(',')` parte las frases con comas. **Exportar el CSV
como "CSV UTF-8"**, o los acentos llegan rotos.

`Story/VRInteractEvents.cs` — dispara por menú contextual los eventos de un
`InteractableUnityEventWrapper`, para probar botones pulsables sin visor.

### Nivel de rejilla — `Scripts/Grid Level/`

| Archivo | Responsabilidad |
|---|---|
| `Bot.cs` | Movimiento relativo: avanzar, girar izq/der, usar. Debug por teclado solo en editor |
| `LevelManager.cs` | Grid numérico + grid de objetos, validación de movimiento, estado de completado |
| `LevelLoader.cs` | Instancia el nivel desde JSON. `TileType` vive aquí |
| `Exit.cs` | Meta. Implementa `IInteractable` |
| `Scenario1Controller.cs` | Puente con Director y telemetría |
| `IInteractable.cs` | Interfaz de interacción del bot (corrutina, no void) |
| `Point.cs`, `Interactable.cs`, `UseBlock.cs` | **Código muerto** del sistema de puntos eliminado |

### Escenario 2 — `Scripts/Scenario 2/`

| Archivo | Responsabilidad |
|---|---|
| `ModuleData.cs` | ScriptableObject: problema, opciones, índice correcto |
| `SystemModule.cs` | Pantalla + botones de un módulo. Valida la elección |
| `ModuleOptionButton.cs` | Un botón. Expone `Press()`, sin dependencia de Oculus.Interaction |
| `Scenario2Controller.cs` | Puente con Director y telemetría. Cierra al reparar todos |

### Sesión — `Scripts/Session/`

| Archivo | Responsabilidad |
|---|---|
| `DifficultySelector.cs` | Pantalla de selección (RI-03). Fija dificultad y carga escena |
| `DifficultyScene.cs` | Declara la dificultad de su escena y la impone si no coincide |

### Narrativa — `Scripts/Story/`

| Archivo | Responsabilidad |
|---|---|
| `Director.cs` | Orquesta escenarios → pasos. Cada paso: eventos instantáneos + acciones a esperar |
| `IStepAction.cs` | Contrato de "acción que bloquea al Director hasta terminar" |
| `Narrator.cs` | Reproduce listas de audio secuencialmente |
| `VRGrabEvents.cs` | Envuelve `GrabInteractable` de Meta en UnityEvents |
| `VRInteractionEvent.cs` | `IStepAction` que espera a un grab/release/hover |
| `Tools/Fader.cs` | Fundido a negro mediante esfera en la cámara |
| `Tools/Timer.cs` | Cuenta atrás con avisos por umbral y evento al agotarse (RF-06) |
| `Tools/Teleporter.cs` | Mueve el `OVRCameraRig` compensando el offset de cabeza |
| `Tools/Waiter.cs`, `Tools/Tools.cs` | Utilidades para cablear en el inspector |

### Telemetría — `Scripts/Telemetry/`

| Archivo | Responsabilidad |
|---|---|
| `TelemetryData.cs` | Modelo serializable del JSON |
| `TelemetryManager.cs` | Singleton persistente. Captura, escritura diferida, utilidades de prueba |
| `CsvUploader.cs` | POST multipart al servidor. **El nombre es un residuo**: ya sube JSON |

## 4. Flujo de sesión

```
Escena "Seleccion"          Escena "Basica"  |  Escena "Intermedia"
─────────────────           ──────────────────────────────────────
DifficultySelector          DifficultyScene (impone la dificultad)
  ├─ SetDifficulty()                │
  └─ LoadScene(...)  ──────────────▶│
                                    ▼
                            Director → Escenario 1 → 2 → 3 → 4
```

`TelemetryManager` es `DontDestroyOnLoad`: se crea en la escena de selección y **sobrevive
al cambio de escena**, manteniendo la misma run y el mismo archivo.

> **Riesgo asumido:** con una escena por dificultad, el entorno de la nave, el Director y toda
> la narrativa quedan duplicados. Cada cambio narrativo hay que hacerlo dos veces.
> `DifficultySelector` admite el mismo nombre de escena en ambos campos por si se unifica.

## 5. Escenario 1 — Secuencialidad

### Flujo de ejecución

```
Jugador agarra bloque
  → BlockNode.OnGrabbed: libera su socket, se desparenta
  → Update: busca el socket libre más cercano dentro de snapDistance
  → OnReleased: revalida y se acopla (AttachTo)

Jugador pulsa Ejecutar
  → ProgramTrigger.OnPlayPressed
     ├─ RegisterAttempt(secuencia, solved: false)   ← ANTES de ejecutar
     └─ ProgramRunner.Run(FirstSocket)
          └─ recorre Socket.Next, Execute() de cada bloque
               └─ GridBlock → Bot.MoveForward / RotateLeft / RotateRight / Use
                    └─ Bot.Use sobre Exit → LevelManager.CompleteLevel()
                         └─ OnLevelCompleted → Scenario1Controller
                              ├─ CompleteChallenge()  ← marca el último intento como resuelto
                              └─ OnLevelFinished (UnityEvent)
```

### Dos decisiones no obvias

**El intento se registra ANTES de ejecutar.** El escenario se completa *dentro* de la
ejecución; el Director avanza y desactiva la estación de bloques, y Unity mata las corrutinas
del objeto desactivado. Registrar al final perdía **siempre** el intento ganador.
`CompleteChallenge()` marca el último intento como `solved: 1`.

**`Scenario1Controller` escucha a `LevelManager`, no a `Exit`.** `ReloadLevel()` destruye e
instancia un `Exit` nuevo, así que suscribirse al `Exit` dejaba la referencia apuntando a un
objeto destruido: tras un reinicio, el bot llegaba a la meta y no pasaba nada.

### Acciones disponibles

`GridActionType`: `MoveForward=0`, `RotateLeft=1`, `RotateRight=2`, `Use=3`, `MoveForwardTwice=4`.

> Los valores se serializan por índice en los bloques de la escena. **Los valores nuevos van
> siempre al final**; insertar en medio reasigna la acción de los bloques existentes.

### Formato de nivel

`TileType`: `Path=0`, `Wall=1`, `Spawn=2`, `Danger=3`, `Point=4`, `Exit=5`, `Void=6`, `Interactable=7`.

Solo `Path` es transitable. `Spawn` y `Point` se convierten a `Path` al cargar. `Point` e
`Interactable` son residuos del sistema de puntos eliminado, pero **los JSON de nivel
existentes todavía usan el tile 4**, así que no se pueden borrar sin re-editarlos.

Editor visual: `Tools → Level Editor`.

## 6. Escenario 2 — Condicionales

Tres módulos de la sala de sistemas. Cada uno muestra una avería y ofrece opciones; el jugador
pulsa un botón. Acierto → módulo reparado. Fallo → se registra y puede reintentar sin límite.

```
PokeInteractable.WhenSelect
  → ModuleOptionButton.Press()
     → SystemModule.ChooseOption(index)
        ├─ OnOptionChosen  ← se dispara ANTES de marcar resuelto
        │    └─ Scenario2Controller → RegisterSelection()
        └─ correcto → IsSolved, apaga botones, OnCorrect
           incorrecto → OnWrong
              └─ Scenario2Controller.CheckCompletion()
                   └─ todos reparados → CompleteChallenge + OnScenarioFinished
```

**`ModuleOptionButton` no conoce `Oculus.Interaction` a propósito.** Solo expone `Press()`,
que el prefab de PokeInteractable llama desde su `InteractableUnityEventWrapper`. Así el
escenario sobrevive a un cambio de primitiva de interacción, y se puede probar sin visor
desde el menú contextual del componente.

El contenido vive en assets `ModuleData` (`Create → Codea → Escenario 2 → Módulo`), no en la
escena, para poder preparar sets por dificultad y que los revise alguien que no toque Unity.

## 7. Escenario 3 — Bucles

**La fila entera es el bucle.** No hay bloque contenedor: `SocketRow` tiene `Repetitions`
(por defecto 1, así el resto de escenarios no se entera) y `ProgramRunner.Run(start, N)`
recorre la cadena esas veces.

| Archivo | Responsabilidad |
|---|---|
| `RoboticArm.cs` | Gira entre `ArmSlot`, recoge y suelta. Equivalente de `Bot` |
| `ArmSlot.cs` | Una posición con su pila de objetos |
| `ArmBlock.cs` | Recoger, Soltar, Girar Izq/Der. Enum + switch |
| `Scenario3Controller.cs` | Completa cuando `slotToClear` queda vacío |

Bloqueo de edición, que mapea sobre las dos dificultades:

| | `editable` | `initialBlocks` | Qué hace el niño |
|---|---|---|---|
| Básica | ❌ | Secuencia completa | Solo ajusta N |
| Intermedia | ✅ | Vacío o desordenado | Ordena **y** ajusta N |

Bloquear requiere **las dos mitades**: `Socket.AcceptsBlocks = false` (impide meter) y
`BlockNode.SetInteractable(false)` (impide sacar). Solo una deja la fila a medio bloquear.

> `RepeatBlock` fue eliminado. El giro fuerza el sentido pedido en vez de tomar el camino más
> corto: con ciertos `yaw`, "Girar Derecha" giraba visualmente a la izquierda.
>
> ⚠️ La mecánica del brazo es una **propuesta sin validar**. Si cambia, `SocketRow`,
> `ProgramRunner` y la telemetría no se tocan: solo `RoboticArm`, `ArmSlot` y `ArmBlock`.

## 7b. Escenario 4 — Patrones

`ShapeChip` **hereda de `BlockNode`** con `Execute()` vacío, así reutiliza agarre, snap,
reinicio y telemetría sin añadir mecanismos nuevos. `ShapeData` separa identidad (`shapeId`)
de atributo (`sides`): básica compara figura, intermedia compara lados.

El rechazo de una ficha mal colocada se **difiere un momento**: el aviso llega desde
`Socket.Occupy()` y `BlockNode.AttachTo` todavía no ha terminado de enlazar el bloque.

Los cuatro escenarios tienen ya su estructura de telemetría propia.

## 8. Telemetría

### Modelo

Un JSON por participante en `Application.persistentDataPath/{pin}_{sessionId}.json`.

```json
{
  "pin": "0004",
  "sessionId": 187,
  "difficulty": 1,
  "startedUtc": "2026-08-25T07:25:07.838Z",
  "endedUtc": "2026-08-25T07:28:21.260Z",
  "escenario1": {
    "started": true, "completed": true, "totalSeconds": 149.8,
    "startedUtc": "...", "endedUtc": "...",
    "failedAttempts": 2, "blocksGrabbed": 9, "blocksReleased": 9,
    "errorCollisionBot": 1, "errorIncompleteSequence": 0, "errorInvalidCommand": 1,
    "attempts": [
      { "difficulty": 1,
        "sequence": ["Avanzar", "Girar Derecha", "Avanzar 2", "Usar"],
        "solved": 1, "durationSeconds": 38.2, "timestamp": "..." }
    ]
  },
  "escenario2": {
    "started": true, "completed": true, "totalSeconds": 84.2,
    "wrongSelections": 1,
    "selections": [
      { "module": "motores", "option": "Agregar agua", "correct": 0, "timestamp": "..." }
    ]
  },
  "escenario3": {
    "started": true, "completed": true, "totalSeconds": 96.4,
    "failedAttempts": 1,
    "attempts": [
      { "difficulty": 1,
        "sequence": ["Recoger", "Girar Derecha", "Soltar", "Girar Izquierda"],
        "repetitions": 4, "solved": 1, "durationSeconds": 42.7, "timestamp": "..." }
    ]
  },
  "escenario4": {
    "matchMode": "lados", "wrongPlacements": 2,
    "placements": [
      { "socket": "estrella", "chip": "triangulo",
        "chipSides": 3, "expectedSides": 10, "correct": 0, "timestamp": "..." }
    ]
  }
}
```

**Jerarquía de los registros:**

```
ScenarioRecord            started, completed, totalSeconds, startedUtc, endedUtc
├── BlockScenarioRecord   + failedAttempts, blocksGrabbed/Released, 3 contadores de error
│   ├── Scenario1Record   + attempts (secuencia)
│   └── Scenario3Record   + attempts (secuencia + repeticiones)
├── Scenario2Record       + wrongSelections, selections
└── Scenario4Record       + matchMode, wrongPlacements, placements
```

`difficulty` es **1-based**: 1 = básica, 2 = intermedia.

### Puntos de captura activos

| Dato | Se captura en |
|---|---|
| Inicio / fin de escenario | `Scenario1Controller`, `Scenario2Controller`, o eventos del Director |
| Intento (secuencia + resultado) | `ProgramTrigger.OnPlayPressed` |
| Reinicios | Botón Reiniciar → `RegisterReset()` |
| Bloques agarrados / soltados | `VRGrabEvents.onGrabbed` / `onReleased` |
| Colisión del bot / comando inválido | `LevelManager.ValidMovementInGrid`, `Bot.Use` |
| Selecciones del escenario 2 | `Scenario2Controller.HandleOptionChosen` |

### Escritura

El archivo se reescribe entero en cada cambio crítico. Los eventos de alta frecuencia (errores,
agarres) solo marcan `dirty` y se vuelcan cada `saveInterval` segundos, para no provocar una
escritura síncrona por cada choque del bot.

**`CsvUploader.UploadTelemetry()` llama a `Flush()` antes de leer el archivo.** Sin eso subiría
una versión desactualizada.

### Utilidades de prueba

Click derecho sobre `TelemetryManager` → submenú **Test**. Simula una partida de tres intentos
y permite subirla sin ponerse el visor. Solo en editor (`#if UNITY_EDITOR`).

### Servidor

Flask en Raspberry Pi, expuesto por Cloudflare Tunnel en `csv.penginexr.com`. Acepta `.json` y
`.csv`, guarda con prefijo de timestamp en `~/Services/CSVServer/uploads/`.

> El origen del túnel es `http://raspberrypi:8090`. Ese nombre debe resolverse **desde dentro
> del contenedor de cloudflared**; si falla la resolución, Cloudflare devuelve 502 aunque Flask
> esté perfecto. Fijarlo con `extra_hosts: ["raspberrypi:host-gateway"]`.

## 9. Sistema narrativo

`Director` recorre una lista de `Scenario`, cada uno con una lista de `Step`. Cada paso tiene
eventos instantáneos (`UnityEvent`) y acciones a esperar (componentes que implementan
`IStepAction`), con modo `All` o `Any`.

Implementan `IStepAction`: `Waiter`, `Fader`, `Narrator`, `VRInteractionEvent`,
`AutomaticDoor`, `Scenario1Controller`, `Scenario2Controller`.

## 10. Convenciones

- **Enum + switch** en vez de una clase por acción. Aplica a `GridActionType`, `TileType`,
  `LogicErrorType`. Es el patrón del proyecto; mantenerlo por coherencia
- **Los valores de enum serializados van al final.** Insertar en medio corrompe las escenas
- **Cablear por `UnityEvent`** cuando el diseñador deba poder cambiarlo sin tocar código
  (sonidos, luces, transiciones). Cablear **por código** lo que no se puede olvidar sin
  romper los datos (registro de telemetría)
- **No acoplar la lógica de juego a `Oculus.Interaction`.** Los scripts exponen métodos
  públicos sin argumentos y el prefab de interacción los llama
- **Los ids de telemetría son estables.** `escenario1`, `motores`… no se renombran una vez
  empezada la recolección
- Comentar **el porqué**, no el qué. Los comentarios del código explican decisiones
  contraintuitivas, no repiten la línea siguiente

## 11. Deuda técnica y bugs conocidos

### Abiertos

| # | Dónde | Problema | Por qué sigue abierto |
|---|---|---|---|
| 1 | `Main.unity` | **`OnPlayPressed` no está cableado.** El `ProgramTrigger` está bien configurado pero nada lo llama: al pulsar el botón, el robot no se mueve | Se perdió al rehacer los botones. Edición de escena |
| 2 | Escenario 3 | La meta se cumple en el **`Recoger`** del último ciclo, no en el `Soltar`: el escenario se completa con el objeto aún en la pinza | Mejor cambiar la meta a "el destino tiene N" que a "el origen está vacío" |
| 3 | Telemetría | `SecuenciaIncompleta` no se registra nunca | Sin definición operativa: "faltaron instrucciones" y "el orden estaba mal" no se distinguen con la taxonomía actual |
| 4 | Telemetría | `SessionResult` (Completado/Abandonado) declarado pero sin campo en el JSON | Falta decidir qué lo dispara |
| 5 | `AutomaticDoor` | Las puertas abren siempre sobre `Vector3.right` local | Limitación asumida: basta orientar el prefab |

### Resueltos el 02/09/2026

| Dónde | Qué era |
|---|---|
| `SystemModule.ChooseOption` | Avisaba **antes** de poner `IsSolved = true`, así que al acertar el último módulo el controlador lo veía sin resolver: **el Escenario 2 no se completaba nunca** |
| `Director` | Se añadió `StepCompletionType.Sequence`. Al arreglar el modo `Any` se había eliminado la ejecución en serie, que los pasos usaban para encadenar "esperar → abrir puerta → fundir" |
| `Scenario1Controller` | Reintento automático: al terminar la secuencia sin resolver, dispara `OnAttemptFailed` tras un margen |
| `BlockResetter` | Usaba `ForgetSocket()`, que dejaba el socket marcado como ocupado con un bloque ya devuelto a su bandeja |
| `RoboticArm` | El giro tomaba el camino más corto: "Girar Derecha" podía girar visualmente a la izquierda |
| Telemetría | `resets` → `failedAttempts`; fuera el `pin` por intento; nuevo `durationSeconds` (tiempo de preparación, **sin** el de ejecución) |

### Resueltos el 25/08/2026

| Dónde | Qué era |
|---|---|
| `Director.RunStep` | Las `waitActions` corrían en serie por un `yield return StartCoroutine(...)` dentro del bucle, lo que dejaba muertos el contador y el modo `Any`. Ahora se lanzan en paralelo |
| `Narrator.StopAudio` | Anulaba el `AudioSource` y dejaba el componente inservible |
| `Narrator.SetAudioListIndex` | No reasignaba `currentAudioList`: cambiar de lista no tenía efecto |
| `Narrator.Execute` | `PlayAudio(index); index++` sin límites → `IndexOutOfRange` al agotar la lista |
| `LevelManager` | `TelemetryManager.Instance` sin null check, y `grid` sin comprobar entre `ClearLevel()` y `LoadLevel()` |
| `RunRecord` | El JSON no reservaba hueco para el escenario 4 |
| `Bot.Move` | `while (transform.position != target)` con escrituras vía `rb.MovePosition` podía no converger y dejar `isMoving` bloqueado para siempre. Ahora compara por distancia y hace snap final |
| `Bot.Move` | Sin comprobar la casilla destino: un tile `Void` daba NRE |
| `StartBlock.Clear` | `Destroy(transform)` sobre el componente Transform, y `GetComponentInChildren<Transform>()` que devuelve el propio |
| `Timer` | Mostraba `00:-1` al pasarse del límite. Ahora acota en cero, avisa por umbrales y dispara `OnTimeUp` una sola vez (RF-06 ya cableable) |
| `Socket.Release` | No avisaba al bloque: quedaba una referencia colgada que vaciaba el socket ajeno al reagarrar |
| `Socket` | `GetComponent<AudioSource>()` en cada acople, sin `[RequireComponent]` |
| `BlockNode` | `FindObjectsByType<Socket>()` cada frame por bloque agarrado. Ahora usa el registro estático `Socket.Active` |
| `LevelLoader` | `using NUnit.Framework` en runtime; nivel sin `Spawn` daba NRE; `ReloadLevel()` dejaba convivir bot viejo y nuevo durante un frame |
| `Fader` | `Shader.Find` podía strippearse en el build de Quest. Ahora admite material asignado, y valida `Camera.main` |
| `Exit.Interact` | Sin guardia de `levelManager`; además el evento de narrativa ahora se dispara siempre |
| `AutomaticDoor` | `CanInteract()` público que nadie podía llamar |

### Resueltos en la segunda pasada del 25/08/2026

| Dónde | Qué era |
|---|---|
| `SocketRow` | `socketOrientation.gameObject.SetActive(false)` daba NRE si el campo se dejaba vacío, pese a que `SocketRotation` lo trata como opcional. Añadidos guardias de `start`/`end`/`socketPrefab` y del componente `Socket` en el prefab |
| `Fader` | Los `return` tempranos de `Start()` dejaban `mat` a null, y el Director llama a `Execute()` sin pasar por `enabled`. Ahora se salta el fundido con aviso en vez de reventar el paso |
| `CsvUploader` | `TelemetryManager.Instance` sin guardia; mensajes con mojibake por estar el archivo en ANSI |
| `SceneController` | `ChangeScenario()` sin comprobar rango ni elementos nulos |
| `Socket` | El registro estático podía arrastrar sockets de una partida anterior sin domain reload |
| `TelemetryManager` | `StartChallenge(null)` reventaba el diccionario; `RegisterAttempt` con secuencia nula lanzaba `ArgumentNullException` |
| `DifficultySelector` | Cargaba sin comprobar Build Settings: la excepción dejaba al jugador en negro sin salida |
| `VRConsole`, `Interactable`, `Scenario2Controller` | Guardias de nulos que faltaban |

### Código muerto

`UseBlock.cs`, `Point.cs`, `Interactable.cs`, `BlockGenerator.cs`, `TileType.Point`,
`TileType.Interactable`, `LevelManager.pointCounter`.

Se conserva a propósito, por decisión explícita de no borrar nada que pueda reutilizarse.

> No borrar `TileType.Point` sin re-editar antes `reto1.json` y `reto-fix1.json`, que usan
> el tile 4.

### Seguridad

`UPLOAD_API_KEY` hardcodeada en `CsvUploader.cs:10`, versionada en el repo y embebida en el
APK. El token del túnel de Cloudflare está en texto plano en su `docker-compose.yml`.

## 12. Roadmap

**Inmediato**
1. Montar en escena el Escenario 2 y la selección de dificultad (código listo)
2. Prefab de `PokeInteractable` y sustituir los botones actuales, que son agarrables y se
   pueden arrancar de su sitio
3. Borrar el `ProgramTrigger` duplicado
4. Decidir el comportamiento del `Timer` al llegar a 0 (RF-06)

**Corto plazo**
5. Cablear el `Timer` de sesión: avisos a 15/10/5 min y cierre en `OnTimeUp` (RF-06)
6. Definir `SecuenciaIncompleta` y el campo de resultado de sesión
7. HUD diegético (RI-02)
8. Decidir username vs PIN (RF-01) — cuanto más tarde, más caro

**Medio plazo**
9. Escenario 3: `RepeatBlock` y soporte de sub-cadenas en `ProgramRunner`
10. Escenario 4: validación por atributo sobre sockets sin cadena
11. Reinicio supervisado (RF-08), narración multilenguaje (RF-07)
12. Validar RNF-01 y RNF-02 en dispositivo
