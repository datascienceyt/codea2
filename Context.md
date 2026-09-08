# Context — Codea VR 2

**Fuente de verdad única del proyecto.** Escrito para que cualquiera —persona o agente— entienda
el sistema completo sin leer los 5.570 líneas de código ni depender de conversaciones previas.

Verificado contra el código el **02/09/2026**. Si algo aquí contradice al código, manda el
código: avisa y corrige este documento.

---

## 1. El proyecto en diez líneas

**CODEA 2** — Escape room educativo en VR para desarrollar **pensamiento computacional** en
niños de 8-17 años de contexto rural en Ecuador. Respaldo de Yachay Tech.
**Deadline: 30 de septiembre de 2026.**

El jugador despierta solo en una nave dañada y debe reparar cuatro sistemas para escapar. Cada
escenario trabaja pilares distintos: secuencialidad, condicionales, bucles y patrones. Un
supervisor fija la dificultad antes de empezar y exporta los datos al terminar.

| Persona | Rol |
|---|---|
| Ing. Víctor Echeverría | Desarrollador principal |
| Ph.D. Erick Cuenca | Director del proyecto |
| Gabriela Galarza, Rolando Armas | Evaluación técnica |

Repositorio `github.com/datascienceyt/codea2`, rama `dev`.

## 2. Stack y restricciones no negociables

- Unity **6000.4.1f1**, URP + Shader Graph
- Meta XR All-in-One SDK **203.0.2**, Horizon OS
- **Meta Quest 3S exclusivamente**, solo mandos físicos (sin hand tracking)
- Locomoción por teletransporte
- **100 % funcional offline** (RNF-04). La subida de datos es manual y opcional
- Objetivo 60 FPS el 90 % del tiempo (RNF-01)
- Serialización con **`JsonUtility`**. Newtonsoft NO es dependencia directa

### Límites de `JsonUtility` que moldean el modelo de datos

Esto explica por qué el JSON tiene la forma que tiene, y no es negociable sin añadir una
dependencia:

- Solo serializa **campos públicos** de clases `[Serializable]`. Nada de propiedades
- No admite `Dictionary`, ni arrays en la raíz, ni polimorfismo
- **No puede omitir campos**: siempre emite todos, con su valor por defecto
- Sí serializa campos **heredados**, si el campo se declara con el tipo concreto
- Una clase **sin campos** se emite como `{}`

## 3. Mapa de archivos

52 scripts en `Assets/_Main/`. Agrupados por responsabilidad:

### `Scripts/Block Programming System/` — núcleo compartido por escenarios 1, 3 y 4

| Archivo | Responsabilidad |
|---|---|
| `BlockNode.cs` | Base abstracta. Agarre, acople por proximidad contra `Socket`, `InstructionLabel` |
| `Socket.cs` | Un hueco. `Next` encadena, `CurrentBlock`, `AcceptsBlocks`, registro estático `Active` |
| `SocketRow.cs` | Genera N sockets por `Lerp`. Repeticiones, bloqueo de edición, bloques iniciales |
| `ProgramRunner.cs` | `Run(inicio, repeticiones)` recorre la cadena. `ExecuteChain` estático |
| `ProgramTrigger.cs` | Botón de ejecutar. Registra el intento **antes** de correr |
| `BlockResetter.cs` | Devuelve bloques a su sitio sin instanciar ni destruir |
| `StartBlock.cs` | Bloque inicial |
| `BlockGenerator.cs` | **Obsoleto.** Sustituido por `BlockResetter` |

### `Scripts/Grid Level/` — Escenario 1

| Archivo | Responsabilidad |
|---|---|
| `Bot.cs` | Robot. Movimiento relativo: avanzar, girar izq/der, usar |
| `GridBlock.cs` | Acciones del bot por enum |
| `LevelManager.cs` | Grid numérico y de objetos, validación, estado de completado |
| `LevelLoader.cs` | Instancia el nivel desde JSON. `TileType` vive aquí |
| `Exit.cs` | Meta. Implementa `IInteractable` |
| `Scenario1Controller.cs` | Puente con Director y telemetría. Reintento automático |
| `Interactable.cs`, `Point.cs` | **Código muerto** del sistema de puntos eliminado |

### `Scripts/Scenario 2/` — Condicionales · `Scripts/Scenario 3/` — Bucles · `Scripts/Scenario 4/` — Patrones

| Archivo | Responsabilidad |
|---|---|
| `ModuleData.cs` | ScriptableObject: problema y lista de `ModuleOption` con su `isCorrect` |
| `SystemModule.cs` | Pantalla + botones. Selección múltiple con alternado |
| `ModuleOptionButton.cs` | Un botón. Expone `Press()`, sin acoplarse a Oculus |
| `RoboticArm.cs` | Gira entre `ArmSlot`, recoge y suelta. Equivalente de `Bot` |
| `ArmSlot.cs` | Una posición con su pila de objetos |
| `ArmBlock.cs` | Recoger, Soltar, Girar Izq/Der |
| `ShapeData.cs` | ScriptableObject: `shapeId` (identidad) + `sides` (atributo) |
| `ShapeChip.cs` | Ficha. Hereda de `BlockNode` con `Execute()` vacío |
| `ShapeSocket.cs` | Hueco con validación por forma o por lados |
| `ScenarioNController.cs` | Puente con Director y telemetría. Uno por escenario |

### `Scripts/Story/` — narrativa · `Scripts/Session/` · `Scripts/Telemetry/`

| Archivo | Responsabilidad |
|---|---|
| `Director.cs` | Orquesta escenarios → pasos. `Scenario`, `Step`, `StepCompletionType` |
| `IStepAction.cs` | Contrato: acción que bloquea el paso hasta terminar |
| `Narrator.cs` | Voz **y** texto en pantalla, emparejados por ID contra un CSV |
| `VRGrabEvents.cs` | Envuelve `Grabbable` de Meta en UnityEvents |
| `VRInteractEvents.cs` | Dispara por menú contextual los eventos de un `InteractableUnityEventWrapper` |
| `VRInteractionEvent.cs` | `IStepAction` que espera a un grab/release/hover |
| `Tools/Fader.cs` | Fundido mediante esfera en la cámara |
| `Tools/Timer.cs` | Cuenta atrás con avisos por umbral y `OnTimeUp` (RF-06) |
| `Tools/Teleporter.cs` | Mueve el `OVRCameraRig` compensando el offset de cabeza |
| `Tools/Waiter.cs`, `Tools/Tools.cs` | Utilidades para cablear en el inspector |
| `Session/DifficultySelector.cs` | Fija la dificultad y carga la escena (RI-03) |
| `Session/DifficultyScene.cs` | Declara la dificultad de su escena y la impone si no coincide |
| `Telemetry/TelemetryData.cs` | Modelo serializable del JSON |
| `Telemetry/TelemetryManager.cs` | Singleton persistente. Captura, escritura diferida, pruebas |
| `Telemetry/CsvUploader.cs` | POST multipart. **El nombre es residuo**: ya sube JSON |
| `Map/AutomaticDoor.cs` | Puertas correderas. `IStepAction` |
| `VRConsole.cs` | Consola de errores dentro del visor |
| `Editor/LevelEditorWindow.cs` | `Tools → Level Editor`. Pinta el grid y exporta JSON |

## 4. Sistema de bloques

El mecanismo central. Lo comparten los escenarios 1, 3 y 4.

```
El jugador agarra una pieza
  └─ BlockNode.OnGrabbed ── libera su socket, se desparenta, cuenta el agarre
       └─ Update ── busca el socket libre más cercano (registro Socket.Active)
            └─ OnReleased ── ¿vacío, acepta bloques y no es hijo de sí mismo?
                 ├─ sí → AttachTo ── copia pose, se acopla, Socket.OnOccupied
                 └─ no → se queda suelto

El jugador pulsa Ejecutar
  └─ ProgramTrigger.OnPlayPressed
       ├─ RegisterBlockAttempt(secuencia, repeticiones)   ← ANTES de ejecutar
       └─ ProgramRunner.Run(FirstSocket, repeticiones)
            └─ ExecuteChain × N ── recorre Socket.Next hasta el primer hueco vacío
                 └─ BlockNode.Execute()
                      ├─ GridBlock  → Bot        (escenario 1)
                      ├─ ArmBlock   → RoboticArm (escenario 3)
                      └─ ShapeChip  → nada       (escenario 4)
```

## 5. Los cuatro escenarios

### Escenario 1 — Secuencialidad *(funcional, verificado en visor)*

El niño ordena bloques para que el robot recorra una rejilla y llegue a la meta.

`GridActionType`: `MoveForward=0`, `RotateLeft=1`, `RotateRight=2`, `Use=3`, `MoveForwardTwice=4`.
Etiquetas: `"Avanzar"`, `"Girar Izquierda"`, `"Girar Derecha"`, `"Usar"`, `"Avanzar 2"`.

`TileType`: `Path=0`, `Wall=1`, `Spawn=2`, `Danger=3`, `Point=4`, `Exit=5`, `Void=6`, `Interactable=7`.
Solo `Path` es transitable. `Spawn` y `Point` se convierten a `Path` al cargar.
**`Point` e `Interactable` son residuos**, pero los JSON de nivel existentes usan el tile 4.

**Reintento automático:** al terminar la secuencia sin resolver, `Scenario1Controller` dispara
`OnAttemptFailed` tras `retryDelay` segundos. Ahí se cablean `RegisterFailedAttempt`,
`ReloadLevel`, `ResetLevel` y `ResetRunner`. El niño no pulsa nada para reintentar.

### Escenario 2 — Condicionales *(montado, sin probar)*

Tres módulos averiados. Cada uno muestra un problema en texto y varias acciones. Los botones
**alternan** entre seleccionado y no seleccionado. El módulo se repara cuando el conjunto
seleccionado coincide **exactamente** con el correcto: ni de menos ni de más.

| | Opciones | Correctas |
|---|---|---|
| Básica | 2 | 1 |
| Intermedia | 4 | 2 (o 3 y 1) |

La dificultad vive en los **datos** (`ModuleData`), no en código. Estado visual por icono y luz
roja/verde; el problema se mantiene textual.

### Escenario 3 — Bucles *(montado, sin probar · mecánica del brazo SIN VALIDAR)*

**La fila entera es el bucle.** No hay bloque contenedor: `SocketRow.Repetitions` (por defecto
1, así el resto de escenarios no se entera) y `ProgramRunner.Run(inicio, N)`.

El brazo gira entre posiciones fijas (`ArmSlot`), cada una con una pila. `Recoger` toma de la
pila de delante, `Soltar` deja en ella. El ciclo `Recoger · Girar D · Soltar · Girar I` traslada
objetos de un montón a otro.

| | `editable` | `initialBlocks` | Qué hace el niño |
|---|---|---|---|
| Básica | ❌ | Secuencia completa | Solo ajusta N |
| Intermedia | ✅ | Vacío o desordenado | Ordena **y** ajusta N |

Bloquear necesita **las dos mitades**: `Socket.AcceptsBlocks = false` (impide meter) y
`BlockNode.SetInteractable(false)` (impide sacar). Solo una lo deja a medio bloquear.

### Escenario 4 — Patrones *(código completo, SIN MONTAR)*

Fichas que encajan en huecos. `ShapeData` separa **identidad** (`shapeId`) de **atributo**
(`sides`): básica compara la figura, intermedia compara el número de lados. Convención del GDD:
triángulo 3, cuadrado 4, estrella 10, círculo 1.

En modo `lados` la etiqueta del hueco muestra **el número, no la figura** — si mostrara la
figura, el emparejamiento por atributo perdería el sentido.

## 6. Sistema narrativo

`Director` recorre `Scenario` → `Step`. Cada paso tiene eventos instantáneos (`UnityEvent`) y
acciones a esperar (componentes `IStepAction`).

| `StepCompletionType` | Comportamiento |
|---|---|
| `All` (0) | En paralelo. Termina cuando acaban **todas** |
| `Any` (1) | En paralelo. Termina con la **primera**; las demás siguen |
| `Sequence` (2) | **En serie**, una tras otra, en el orden de la lista |

Implementan `IStepAction`: `Waiter`, `Fader`, `Narrator`, `VRInteractionEvent`, `AutomaticDoor`
y los cuatro `ScenarioNController`.

**`Narrator` reproduce voz y escribe texto a la vez** y espera a la más larga de las dos.
Emparejado por ID: cada `NarrationEntry` tiene `clip` + `textId` contra un CSV
(`ID_Texto, Texto_Narrativa`, 25 líneas en `Scripts/Story/Audios Narrativa.csv`).

Degrada limpiamente: sin clip solo escribe, sin `Text` o sin CSV solo suena, `textId = 0`
significa sin texto. **Ni el CSV ni el Text son obligatorios.**

> Exportar el CSV como **"CSV UTF-8"**, o los acentos llegan rotos. El parser propio respeta
> comillas, comas internas y saltos de línea; un `Split(',')` partiría las frases.

## 7. Telemetría

Un JSON por participante en `Application.persistentDataPath/{pin}_{sessionId}.json`.

### Jerarquía de registros

```
ScenarioRecord            started, completed, totalSeconds, startedUtc, endedUtc
├── BlockScenarioRecord   + failedAttempts, blocksGrabbed/Released, 3 contadores de error
│   ├── Scenario1Record   + attempts[]  (AttemptRecord)
│   └── Scenario3Record   + attempts[]  (LoopAttemptRecord: + repetitions)
├── Scenario2Record       + wrongSelections, selections[]
└── Scenario4Record       + matchMode, wrongPlacements, placements[]

RunRecord (raíz) ── pin, sessionId, difficulty, startedUtc, endedUtc
                 └─ escenario1, escenario2, escenario3, escenario4
```

`difficulty` es **1-based**: 1 = básica, 2 = intermedia. Los booleanos van como **0/1**.

### Ejemplo

```json
{
  "pin": "0004", "sessionId": 187, "difficulty": 1,
  "startedUtc": "...", "endedUtc": "...",
  "escenario1": {
    "started": true, "completed": true, "totalSeconds": 111.6,
    "failedAttempts": 3, "blocksGrabbed": 12, "blocksReleased": 12,
    "errorCollisionBot": 1, "errorIncompleteSequence": 0, "errorInvalidCommand": 1,
    "attempts": [
      { "difficulty": 1, "sequence": ["Avanzar", "Girar Derecha", "Usar"],
        "solved": 1, "durationSeconds": 7.2, "timestamp": "..." }
    ]
  },
  "escenario2": {
    "wrongSelections": 1,
    "selections": [
      { "module": "motores", "option": "Agregar agua", "selected": 1, "correct": 0, "timestamp": "..." },
      { "module": "motores", "option": "Agregar agua", "selected": 0, "correct": 0, "timestamp": "..." }
    ]
  },
  "escenario3": {
    "attempts": [
      { "sequence": ["Recoger", "Girar Derecha", "Soltar", "Girar Izquierda"],
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

### Puntos de captura

| Dato | Dónde |
|---|---|
| Inicio / fin de escenario | `ScenarioNController` o eventos del Director |
| Intento (secuencia + repeticiones) | `ProgramTrigger.OnPlayPressed` |
| Bloques agarrados / soltados | `BlockNode.OnGrabbed` / `OnReleased`, **desde código** |
| Colisión / comando inválido | `LevelManager.ValidMovementInGrid`, `Bot.Use` |
| Intentos fallidos | `Scenario1Controller.OnAttemptFailed` |
| Selecciones (esc. 2) | `Scenario2Controller` — registra **también las deselecciones** |
| Colocaciones (esc. 4) | `Scenario4Controller` |

### Escritura

Dos velocidades. **Inmediata**: intentos, inicio/fin, fallos, selecciones, colocaciones.
**Diferida** (`saveInterval`, 10 s en escena): agarres y errores de lógica, que son de alta
frecuencia y provocaban tirones. `Flush()` fuerza lo pendiente, y `UploadTelemetry()` lo llama
antes de leer el archivo.

### Servidor

Flask en Raspberry Pi vía Cloudflare Tunnel en `csv.penginexr.com`. Acepta `.json` y `.csv`.
El origen del túnel es `http://raspberrypi:8090`; si ese nombre no se resuelve desde dentro del
contenedor de `cloudflared`, Cloudflare devuelve **502** aunque Flask esté perfecto. Se fija con
`extra_hosts: ["raspberrypi:host-gateway"]`.

### Utilidades de prueba

Click derecho en `TelemetryManager` → submenú **Test**: simular partida, subir, y volcar el JSON
a consola. Solo en editor.

## 8. API pública cableable

Lo que se conecta desde un `UnityEvent`. Verificado contra el código.

| Componente | Métodos |
|---|---|
| `TelemetryManager` | `StartChallenge(string)`, `CompleteChallenge(string)`, `EndRun()`, `Flush()`, `RegisterFailedAttempt()`, `RegisterBlockGrabbed/Released()`, `RegisterLogicError(int)`, `IncrementPin()`, `SetPin(int)`, `SetDifficulty(int)` |
| `CsvUploader` | `UploadTelemetry()`, `UploadCsv(string)` |
| `ProgramTrigger` | `OnPlayPressed()` |
| `ProgramRunner` | `ResetRunner()` |
| `SocketRow` | `IncreaseRepetitions()`, `DecreaseRepetitions()`, `SetRepetitions(int)`, `Lock()`, `Unlock()`, `SetEditable(bool)`, `ClearRow(bool)`, `PlaceInitialBlocks()` |
| `BlockResetter` | `ResetBlocks()` |
| `LevelLoader` / `LevelManager` | `ReloadLevel()` · `ResetLevel()`, `CompleteLevel()` |
| `SystemModule` / `ModuleOptionButton` | `ToggleOption(int)`, `ResetModule()` · `Press()` |
| `RoboticArm` | `ResetArm()` |
| `ScenarioNController` | `StartScenario()`, `ResetScenario()` |
| `Narrator` | `PlayAudio(int/string)`, `StopAudio()`, `SetAudioListIndex(int)`, `ShowLineById(int)`, `CompleteInstantly()`, `Clear()` |
| `Timer` | `StartTimer()`, `Pause()`, `Continue()`, `Stop()`, `SetTimeLimit(int)` |
| `Fader` | `TriggerFadeIn()`, `TriggerFadeOut()` |
| `DifficultySelector` | `SelectBasic()`, `SelectIntermediate()`, `SelectByIndex(int)` |
| `Tools` | `SetActive(GameObject)`, `SetInactive(GameObject)`, `DestroyObject(GameObject)` |

## 9. Decisiones no obvias — el porqué

Cada una de estas nació de un bug real. Cambiarlas sin entenderlas los reintroduce.

**El intento se registra ANTES de ejecutar.** El escenario se completa *dentro* de la ejecución
(el bloque `Usar` dispara `Exit.Interact`), el Director avanza y desactiva la estación, y Unity
mata las corrutinas del objeto desactivado. Registrar al final perdía **siempre** el intento
ganador. `CompleteChallenge()` marca el último intento como resuelto.

**`Scenario1Controller` escucha a `LevelManager`, no a `Exit`.** `ReloadLevel()` destruye e
instancia un `Exit` nuevo; la suscripción quedaba apuntando a un objeto destruido y tras un
reinicio llegar a la meta no hacía nada.

**El estado se actualiza antes de disparar eventos.** Al revés, el Escenario 2 no se completaba
nunca: el controlador comprueba el `IsSolved` de todos los módulos y veía el último aún sin
resolver.

**`durationSeconds` excluye el tiempo de ejecución.** Ese tiempo lo determina la longitud de la
secuencia; incluirlo mediría el tamaño de la solución en vez del razonamiento.

**El Escenario 2 registra también las deselecciones** (`selected: 0`). Quitar una opción es
autocorrección, y sin ese campo sería indistinguible de no haberla tocado.

**`RoboticArm` fuerza el sentido de giro** en vez de tomar el camino más corto: con ciertos
`yaw`, "Girar Derecha" giraba visualmente a la izquierda.

**`Socket.Active` es un registro estático.** `BlockNode` lo recorre cada frame por cada bloque
agarrado; una búsqueda global ahí costaba FPS (RNF-01).

**El rechazo de ficha del Escenario 4 se difiere un momento.** El aviso llega desde
`Socket.Occupy()` y `AttachTo` aún no terminó de enlazar el bloque.

**Los contadores de bloques van en código, no en `UnityEvent`.** Ver trampa 1.

**`OnApplicationPause` no cierra la run.** Quitarse el visor pausaba la app y ponía `endedUtc`
a mitad de sesión; como `EndRun` es idempotente, ya no se corregía.

## 10. Trampas de Unity vividas en este proyecto

1. **Un prefab no puede referenciar un objeto de escena.** Unity anula la referencia al guardar,
   en silencio, y el `UnityEvent` queda apuntando a nada. Si el destino vive en la escena,
   cablea **desde código**.
2. **El orden dentro de un `UnityEvent` importa.** `IncrementPin` antes del upload cerraba la
   run y subía un archivo vacío.
3. **`onHover` no es un botón.** Se dispara al acercar la mano. Para subir, reiniciar o cambiar
   de PIN, usa una pulsación deliberada.
4. **Los valores de enum serializados van al final.** Insertar en medio reasigna en silencio la
   acción de todos los objetos ya colocados en escena.
5. **Una corrutina muere si su GameObject se desactiva.** Si algo debe ocurrir sí o sí, no lo
   pongas al final de una corrutina interrumpible.

**Regla corta para decidir dónde cablear:** si olvidarlo rompe los datos, va en código; si es
estética (sonidos, luces, transiciones), va en `UnityEvent`.

## 11. Estado voluble — 02/09/2026

> Esta sección caduca. Todo lo anterior es estable.

| Subsistema | Estado |
|---|---|
| Escenario 1 | ✅ Verificado en visor |
| Escenario 2 | 🟡 Montado (3 módulos, 6 botones), sin probar |
| Escenario 3 | 🟡 Montado (brazo + 2 slots), sin probar. Mecánica sin validar |
| Escenario 4 | ❌ Sin montar |
| Narrativa, telemetría, subida | ✅ Funcionales |
| Selección de dificultad | ❌ Sin montar; no existen las escenas |
| HUD diegético (RI-02), username (RI-01), reinicio supervisado (RF-08) | ❌ Sin implementar |

**Pendiente inmediato en escena:**

- `ScreenNarrator` sigue en `Main.unity` como **script perdido** (la clase se fusionó en
  `Narrator`). Borrar el GameObject y quitarlo de los `waitActions` del Director
- Reconfigurar el `Narrator`: CSV, `Text`, y los clips con su `textId`
- **Los 3 assets `ModuleData` usan la estructura antigua** (`options` como lista de strings +
  `correctOptionIndex`). Hay que rehacerlos con `List<ModuleOption>`
- Añadir botones hasta 4 por módulo para la dificultad intermedia
- Escenario 3: asignar el `Interaction Root` de los bloques, o la fila bloqueada se vacía igual
- Servidor: quitar el prefijo de timestamp en `server.py` (`saved_as = filename`)

**Otros datos del repositorio:** existe `Assets/Scenes/Tests.unity` además de `Main.unity`, y 7
escenas de respaldo en `Assets/_Recovery/` que **no son las escenas activas** — aparecen en las
búsquedas y confunden.

**Seguridad:** `UPLOAD_API_KEY` está en claro en `CsvUploader.cs` y el token del túnel en su
`docker-compose.yml`. Asumido: servidor privado y temporal.

## 12. Decisiones abiertas

| Qué | Por qué sigue abierto |
|---|---|
| `SecuenciaIncompleta` | Sin definición operativa que separe "faltaron instrucciones" de "el orden estaba mal". Siempre vale 0. Es criterio pedagógico |
| `SessionResult` | Declarado pero sin campo en el JSON. Falta decidir qué dispara "abandonado" |
| Username vs PIN | El SRS pide username (RF-01), el código usa PIN. Divergencia **deliberada**: se decidió corregir el documento. Renombrar rompería los JSON ya recogidos |
| Mecánica del brazo | Propuesta sin validar. Si cambia, solo se tiran `RoboticArm`, `ArmSlot` y `ArmBlock` |
| Meta del Escenario 3 | Se cumple en el `Recoger` del último ciclo, no en el `Soltar`: se completa con el objeto en la pinza. Mejor cambiarla a "el destino tiene N" |
| Tres escenas o una | Con una por dificultad, el entorno y la narrativa se duplican |
| Escenario 4 | ¿Necesita panel-ejemplo introductorio? Depende de los beta testers |

## 13. Documentación relacionada

| Archivo | Para quién |
|---|---|
| `Docs/VARIABLES_TELEMETRIA.md` | Equipo evaluador. Qué mide cada variable, en lenguaje llano |
| `Docs/Codea2_GDD.docx` | Game Design Document |
| `Docs/Informe-Mes1.docx` / `.pdf` | Informe técnico entregado, con el SRS (IEEE 830) |
| `Docs/Diagramas/` | Diagramas de flujo y funcionalidad, croquis |
| `Docs/Investigaciones/` | Respaldo académico: *worked examples*, manipulativos físico-digitales |
| `README.md` | Presentación institucional del repositorio |

## 14. Comprobaciones rápidas

```bash
# Compilar sin abrir Unity
dotnet build Assembly-CSharp.csproj -v:q --nologo -t:Rebuild

# Superficie pública (si este documento parece desfasado)
grep -rnE "^\s{4}public\s+(void|IEnumerator|bool|int|string|float)\s+\w+\s*\(" Assets/_Main/Scripts

# Qué llama a un método en la escena (ojo: los overrides de prefab usan 'value:' en vez de 'm_MethodName:')
grep -n "m_MethodName: X\|value: X" Assets/Scenes/Main.unity
```

**Probar sin visor:** casi todo tiene `[ContextMenu]`. `ModuleOptionButton → Press`,
`RoboticArm → Probar/Ciclo completo`, `VRInteractEvents → Invoke Full Press`,
`TelemetryManager → Test/Simular y subir`, y el teclado W/A/D/Espacio del `Bot` en editor.

Los cuatro escenarios avisan al completarse:
`[Escenario N] COMPLETADO · challengeId '...'`. Si uno no aparece, ahí está el corte.
