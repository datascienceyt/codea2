# Context — Codea VR 2

**Fuente de verdad única del proyecto.** Escrito para que cualquiera —persona o agente— entienda
el sistema completo sin leer los 5.570 líneas de código ni depender de conversaciones previas.

Verificado contra el código el **21/09/2026**. Si algo aquí contradice al código, manda el
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

54 scripts en `Assets/_Main/`. Agrupados por responsabilidad:

### `Scripts/Block Programming System/` — núcleo compartido por escenarios 1, 3 y 4

| Archivo | Responsabilidad |
|---|---|
| `BlockNode.cs` | Base abstracta. Agarre, acople por proximidad contra `Socket`, `InstructionLabel` |
| `Socket.cs` | Un hueco. `Next` encadena, `CurrentBlock`, `AcceptsBlocks`, registro estático `Active` |
| `SocketRow.cs` | Genera N sockets por `Lerp`. Repeticiones, bloqueo de edición, bloques iniciales |
| `ProgramRunner.cs` | `Run(inicio, repeticiones)` recorre la cadena. `ExecuteChain` estático |
| `ProgramTrigger.cs` | Botón de ejecutar. Registra el intento **antes** de correr |
| `BlockResetter.cs` | Devuelve bloques a su sitio sin instanciar ni destruir |
| `IRunPrecondition.cs` | Contrato: condición que puede vetar la ejecución y aporta su argumento |
| `StartBlock.cs` | Bloque inicial |
| `BlockGenerator.cs` | **Obsoleto**, sustituido por `BlockResetter`. Solo lo referencian las escenas de `_Recovery/`: borrarlo solo las degradaría más |

### `Scripts/Grid Level/` — Escenario 1

| Archivo | Responsabilidad |
|---|---|
| `Bot.cs` | Robot. Movimiento relativo: avanzar, girar izq/der, usar |
| `GridBlock.cs` | Acciones del bot por enum |
| `LevelManager.cs` | Grid numérico y de objetos, validación, estado de completado |
| `LevelLoader.cs` | Instancia el nivel desde JSON. `TileType` vive aquí |
| `Exit.cs` | Meta. Implementa `IInteractable` |
| `Scenario1Controller.cs` | Puente con Director y telemetría. Reintento automático |
| `Interactable.cs`, `Point.cs` | Vestigios del sistema de puntos, pero **NO borrables**: `LevelLoader` los llama (líneas 84 y 118) y ambos tienen prefab activo. Quitarlos rompe la compilación |

### `Scripts/Scenario 2/` — Condicionales · `Scripts/Scenario 3/` — Bucles · `Scripts/Scenario 4/` — Patrones

| Archivo | Responsabilidad |
|---|---|
| `ModuleData.cs` | ScriptableObject: problema y lista de `ModuleOption` con su `isCorrect` |
| `SystemModule.cs` | Pantalla + botones. Selección múltiple con alternado |
| `ModuleOptionButton.cs` | Un botón. Expone `Press()`, sin acoplarse a Oculus |
| `RoboticArm.cs` | Gira entre `ArmSlot`, recoge y suelta. Equivalente de `Bot` |
| `ArmSlot.cs` | Una posición del brazo. Puede llevar varias pilas, una por tipo |
| `ArmItem.cs` | Marca un barril o una caja con su tipo. El enum `ArmItemType` vive aquí |
| `ArmTypeChip.cs` / `ArmTypeSocket.cs` | La ficha de argumento y su hueco. El socket veta la ejecución si está vacío |
| `ArmBlock.cs` | Recoger, Soltar, Girar Izq/Der, Girar al destino, Volver |
| `ShapeChip.cs` | Ficha. Hereda de `BlockNode` con `Execute()` vacío |
| `ShapeSocket.cs` | Hueco. Encaja la ficha cuyo sprite sea el mismo asset que el esperado |
| `ScenarioNController.cs` | Puente con Director y telemetría. Uno por escenario |

### `Scripts/Story/` — narrativa · `Scripts/Session/` · `Scripts/Telemetry/`

| Archivo | Responsabilidad |
|---|---|
| `Director.cs` | Orquesta escenarios → pasos. `Scenario`, `Step`, `StepCompletionType`. Menú **Skip Step** para probar |
| `IStepAction.cs` | Contrato: acción que bloquea el paso hasta terminar |
| `Narrator.cs` | Voz **y** texto en pantalla, emparejados por ID contra un CSV |
| `VRGrabEvents.cs` | Envuelve `Grabbable` de Meta en UnityEvents |
| `VRInteractEvents.cs` | Dispara por menú contextual los eventos de un `InteractableUnityEventWrapper` |
| `VRInteractionEvent.cs` | `IStepAction` que espera a un grab/release/hover |
| `Tools/Fader.cs` | Fundido mediante esfera en la cámara |
| `Tools/Timer.cs` | Cuenta atrás con avisos por umbral y `OnTimeUp` (RF-06). Reparte la hora entre N pantallas |
| `Tools/TimerDisplay.cs` | Una pantalla donde el Timer escribe. Se registra sola al activarse |
| `Tools/Teleporter.cs` | Mueve el `OVRCameraRig` compensando el offset de cabeza |
| `Tools/Waiter.cs`, `Tools/Tools.cs` | Utilidades para cablear en el inspector |
| `Tools/UiText.cs` | Escribe en etiquetas de UI sean `Text` de uGUI o `TMP_Text`. Los samples de Meta usan TMP |
| `Tools/ScreenTeleporter.cs` | Manda la pantalla de narración a la pose de un Transform, pasado como argumento |
| `Session/SessionSetup.cs` | Pantalla del supervisor: PIN + dificultad, y lanza la escena (RI-03) |
| `Session/PinEntry.cs` | Teclado numérico del PIN. `AppendDigit`, borrar, siguiente correlativo |
| `Session/SceneLoader.cs` | Carga una escena cerrando antes la run. Para volver con el siguiente niño |
| `Session/DifficultyScene.cs` | Declara la dificultad de su escena y la impone si no coincide |
| `Telemetry/TelemetryData.cs` | Modelo serializable del JSON |
| `Telemetry/TelemetryManager.cs` | Singleton persistente. Captura, escritura diferida, pruebas |
| `Telemetry/JSONUploader.cs` | Sube el JSON de la sesión por POST multipart, con reintentos |
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

### Escenario 2 — Condicionales *(básica montada, sin probar)*

Tres módulos averiados. Cada uno enuncia un problema en texto y ofrece varias acciones. Los
botones **alternan** entre seleccionado y no seleccionado, y el módulo se repara cuando el
conjunto seleccionado coincide **exactamente** con el correcto: ni de menos ni de más.

| | Opciones | Correctas |
|---|---|---|
| Básica | 2 | 1 |
| Intermedia | 4 | 2 |

La dificultad vive en los **datos** (`ModuleData`), no en código, y se cambia **cargando otra
escena**. Hay un asset por módulo y dificultad en `Scripts/Scenario 2/Modulos/`, nombrados
`<Modulo>_Basica` y `<Modulo>_Intermedia`. Los `moduleId` son idénticos entre dificultades
para que la telemetría agregue.

**Cada distractor es una acción correcta en otro módulo.** Motores trata de combustible y
lubricación, energía de electricidad, enfriamiento de temperatura. Así no se puede acertar
reconociendo el texto de la opción: hay que leer el problema y descartar. *"Agregar agua"*
aparece en los tres módulos y solo es correcta en uno.

Estado visual por icono y luz roja/verde; el problema se mantiene textual.

### Escenario 3 — Bucles y parametrización *(reescrito, sin probar en visor)*

**La fila entera es el bucle.** No hay bloque contenedor: `SocketRow.Repetitions` (por defecto
1, así el resto de escenarios no se entera) y `ProgramRunner.Run(inicio, N)`.

**Y el programa tiene un argumento.** Aparte de la fila hay un `ArmTypeSocket` —físicamente
separado— donde se mete una ficha `ArmTypeChip`: barril rojo o caja azul. No es una
instrucción: no se ejecuta, decide **de qué pila recoge el brazo**. Es el hueco de argumento de
un bloque de Scratch, y con él la forma del programa se queda igual mientras cambia el dato.

Estar fuera del `SocketRow` no es estético: `SetEditable()` bloquea la fila entera de golpe, y
separando el socket el tipo se puede cambiar entre ejecuciones aunque las instrucciones estén
fijas. Eso es justo lo que pide la básica.

**Clasificar, no trasladar.** Delante del brazo hay **dos pilas mezcladas** en la misma
posición; los barriles van a la izquierda y las cajas a la derecha. Hacen falta **al menos dos
ejecuciones**, una por tipo.

| | `editable` | Qué hace el niño |
|---|---|---|
| Básica | ❌ | Repeticiones + ficha de tipo. La fila trae `Recoger · Girar al destino · Soltar · Volver` |
| Intermedia | ✅ | Repeticiones + ficha + **reordenar** `Recoger · Girar Izq · Soltar · Girar Der` |

En básica el giro lo resuelve el propio bloque: `RotateToDestination` pregunta adónde va el
tipo puesto y gira hacia allí. Son **bloques distintos**, no los de izquierda/derecha
reinterpretados — un bloque que dice una cosa y hace otra es el bug que ya costó arreglar en
`RotateTowardsSlot`, y en intermedia esos mismos bloques significan izquierda y derecha
literales.

Bloquear necesita **las dos mitades**: `Socket.AcceptsBlocks = false` (impide meter) y
`BlockNode.SetInteractable(false)` (impide sacar). Lo hace `SocketRow.ApplyEditable()`, ahora
**socket a socket**: un `InitialBlock` puede venir con `fixedInPlace` o sin bloque, para dejar
huecos en medio de una secuencia ya montada.

**Soltar donde no va se permite intentarlo, pero no se consuma:** el objeto vuelve a la pila de
la que salió y se registra un error de lógica. Dejarlo caer obligaría a rescatarlo, y ese
rescate no es el ejercicio.

**Sin ficha de tipo el programa no se ejecuta.** `ArmTypeSocket` implementa `IRunPrecondition`,
que `ProgramTrigger` consulta **antes de registrar el intento**: un programa sin su argumento no
probó ninguna solución y no debe contar como intento.

**El botón no se bloquea tras una ejecución.** `ProgramTrigger.allowRepeatedRuns` desactiva el
guardia de `HasRun`. Sin eso, la segunda pasada era imposible y el escenario quedaba sin salida.

**Reintento:** falla la pasada que **no clasificó nada**, no la que dejó el escenario
incompleto — si no, la pasada de barriles perfecta se contaría como fallo porque faltan las
cajas. La que avanzó y se quedó corta dispara `OnAttemptAdvanced` y rearma el runner. El fallo
se registra **desde código**; no cablees `RegisterFailedAttempt` en `OnAttemptFailed`.

**Montaje en escena:** raíz `Escenario (3)` con `Scenario3Controller` + `ProgramRunner` +
`ProgramTrigger`. Tres `ArmSlot` en el array del brazo, **en orden [izquierda, frente, derecha]**
con `startSlotIndex = 1` — el orden del array define la rotación, no los valores de `yaw`. El
frente lleva dos `TypedStack` (una por tipo) y cada destino una sola. Cada barril y cada caja
necesita su `ArmItem`. Prefabs: `ArmBlock`, `ArmTypeChipBlock`, `ArmSocketTipo`.

### Escenario 4 — Patrones *(en montaje)*

Fichas con figuras abstractas que encajan en huecos. Varias se parecen mucho entre sí y solo
una es idéntica a la del hueco: el reto es de **discriminación visual**, comparar el detalle en
vez de reconocer una forma conocida. Las figuras salen de un pliego recortado en `Multiple`,
`Scripts/Scenario 4/Figuras/symbols.png` (34 recortes).

**La figura ES el sprite.** No hay asset intermedio: `ShapeChip.shape` y
`ShapeSocket.expectedShape` son campos `Sprite`, y encajar es `chip.Shape == expectedShape`, o
sea igualdad de referencia al mismo recorte. Hubo un `ShapeData` con `shapeId` + `sides`
mientras el escenario emparejaba también por número de lados; al quedar una sola forma de
jugar, el asset se reducía a un envoltorio de un campo y desapareció.

Se compara por **referencia y no por nombre** a propósito: el nombre del recorte se puede
cambiar desde el Sprite Editor, y el emparejamiento se rompería sin que nada avisara.

El hueco **dibuja** la figura que espera en un `SpriteRenderer`, no la escribe. Con figuras
abstractas un rótulo con el nombre resolvería el reto leyendo en vez de mirando.

**Las dos dificultades usan la misma mecánica**; lo único que cambia es el juego de figuras,
más simples o más densas. El escenario no declara su dificultad: eso ya lo hace
`DifficultyScene`, que además corrige la telemetría si la escena y el selector no coinciden.

Ficha y hueco traen un hijo llamado `Sprite` con el `SpriteRenderer`, y ambos exponen
`ApplyShapeToChild()` por menú contextual para engancharlo sin arrastrarlo pieza por pieza.
`Scenario4Controller` lo lanza en lote y añade **"Comprobar figuras del panel"**, que detecta
el fallo invisible: un hueco cuya figura no la lleva ninguna ficha deja el escenario
irresoluble y el Director esperando para siempre.

Hacen falta **fichas distractoras de sobra**. Con tantas fichas como huecos, el último se
resuelve por eliminación sin llegar a comparar.

Aquí **no hay `SocketRow`, ni `ProgramRunner`, ni botón de ejecutar**: cada `ShapeSocket` es
independiente (sin `Next`) y valida al soltar. El escenario termina cuando todos están resueltos.
Prefabs: `Prefabs/Blocks/ShapeChip.prefab` y `Prefabs/Blocks/ShapeSocket.prefab`.

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
(`ID_Texto, Texto_Narrativa`, 26 líneas en `Scripts/Story/Audios Narrativa.csv`; la 26 es la variante en
plural del Escenario 2 para la escena de dificultad intermedia, **pendiente de grabar**).

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
│   └── Scenario3Record   + attempts[]  (LoopAttemptRecord: + repetitions, itemType)
├── Scenario2Record       + wrongSelections, selections[]
└── Scenario4Record       + wrongPlacements, chipsGrabbed/Released, placements[]

RunRecord (raíz) ── pin, sessionId, difficulty, startedUtc, endedUtc
                 └─ escenario1, escenario2, escenario3, escenario4
```

`difficulty` es **1-based**: 1 = básica, 2 = intermedia. `started`/`completed` son booleanos de
verdad (`true`/`false`); el resto de banderas (`solved`, `correct`, `selected`) van como **0/1**.

**El escenario 4 NO hereda de `BlockScenarioRecord`** aunque sus fichas también se agarren.
Tiene sus propios `chipsGrabbed`/`chipsReleased`: heredar habría metido `failedAttempts` y los
tres contadores de error como ceros permanentes que nadie puede interpretar, porque ese
escenario no tiene intentos ni ejecución.

### Ejemplo

Hay una run completa y coherente, con los cuatro escenarios resueltos, en
**`Docs/ejemplo_run_telemetria.json`**. Está escrita con el formato exacto que emite
`JsonUtility.ToJson(run, true)`: todos los campos presentes, orden de declaración, campos
heredados primero. Sirve de referencia para el equipo evaluador y para validar el parser de
análisis sin tener que jugar una sesión entera.

> Los `float` reales pueden salir con ruido de precisión (`148.3` → `148.30001`). Es normal en
> `JsonUtility`, no es un error de escritura.

### Puntos de captura

| Dato | Dónde |
|---|---|
| Inicio / fin de escenario | `ScenarioNController`, **desde código** en los cuatro. El Director lo repite por evento; es idempotente |
| Intento (secuencia + repeticiones + tipo) | `ProgramTrigger.OnPlayPressed`. El `itemType` lo aporta la `IRunPrecondition`, así que sin el socket de tipo en `preconditions` sale vacío |
| Piezas agarradas / soltadas | `BlockNode.OnGrabbed` / `OnReleased`, **desde código**. Enruta a `blocksGrabbed` (esc. 1 y 3) o a `chipsGrabbed` (esc. 4) según el reto activo |
| Colisión / comando inválido | `LevelManager.ValidMovementInGrid`, `Bot.Use` (esc. 1) · `RoboticArm.OnInvalidAction` y `ArmTypeSocket.CanRun` (esc. 3) |
| Intentos fallidos (esc. 1) | `Scenario1Controller.OnAttemptFailed` — **cableado en escena** |
| Intentos fallidos (esc. 3) | `Scenario3Controller.HandleRunFinished` — **desde código** |
| Selecciones (esc. 2) | `Scenario2Controller` — registra **también las deselecciones** |
| Colocaciones (esc. 4) | `Scenario4Controller` |

**Todo se atribuye al reto activo** (`_currentChallengeId`, que fija `StartChallenge`). Si el
Director no abre el escenario, los agarres y los errores de lógica se van al último escenario
que sí arrancó. Cuando no hay ninguno activo, `TelemetryManager` avisa una vez en consola.

### Escritura

Dos velocidades. **Inmediata**: intentos, inicio/fin, fallos, selecciones, colocaciones.
**Diferida** (`saveInterval`, 10 s en escena): agarres y errores de lógica, que son de alta
frecuencia y provocaban tirones. `Flush()` fuerza lo pendiente, y `UploadTelemetry()` lo llama
antes de leer el archivo.

### Servidor

Flask en Raspberry Pi vía Cloudflare Tunnel en `csv.penginexr.com` (el nombre es residuo: sube
JSON). Acepta `.json` y `.csv`, y guarda con **el nombre que manda el visor**,
`{pin}_{sessionId}.json`, sin prefijo de fecha: el `sessionId` ya evita las colisiones.

El stack vive en la Pi, en `~/Services/JSONServer` — contenedor `json-uploader`, imagen
`jsonserver-json-uploader`, subidas en `./uploads`. **Ese código no está en este repositorio.**

Tres cosas que pueden romperlo, todas vividas:

- El compose **tiene que publicar `8090:8080`**. Flask escucha en 8080 dentro del contenedor y
  el túnel busca el 8090 del host. Sin la línea `ports`, Cloudflare devuelve **502** con Flask
  perfectamente vivo
- `UPLOAD_API_KEY` tiene que estar en el `environment`, o cae al valor por defecto y todo da 401
- Si `raspberrypi` no se resuelve desde dentro del contenedor de `cloudflared`, otro 502. Se
  fija con `extra_hosts: ["raspberrypi:host-gateway"]`

Para diagnosticar sin adivinar: `JSONUploader` → **Ping al servidor** traduce el resultado a
cuál de las cuatro capas falló (red del visor, DNS, túnel, Flask), y **Subir JSON de prueba**
manda un archivo con el formato real por el mismo camino que una subida de verdad. Los dos
necesitan Play Mode. En la Pi, el traceback está en `docker logs -f json-uploader`.

### Utilidades de prueba

Click derecho en `TelemetryManager` → submenú **Test**: simular partida, subir, y volcar el JSON
a consola. Y **Comprobar integridad de la run**, que también corre solo en cada `EndRun()`.

Ese informe existe porque esta telemetría no falla reventando: falla **saliendo a cero**. Un
escenario que nunca arrancó, un contador que nadie incrementó o un reto resuelto sin marcarse
producen un JSON válido y vacío, y eso se descubre semanas después, al analizar. El informe
lista escenario por escenario lo capturado y avisa de lo que huele a cable olvidado. Son
warnings y no errores: un cero puede ser legítimo y el sistema no puede saberlo.

## 8. API pública cableable

Lo que se conecta desde un `UnityEvent`. Verificado contra el código.

| Componente | Métodos |
|---|---|
| `TelemetryManager` | `StartChallenge(string)`, `CompleteChallenge(string)`, `EndRun()`, `Flush()`, `RegisterFailedAttempt()`, `RegisterBlockGrabbed/Released()`, `RegisterLogicError(int)`, `IncrementPin()`, `SetPin(int)`, `SetDifficulty(int)` |
| `JSONUploader` | `UploadTelemetry()`, `UploadFile(string)` |
| `ProgramTrigger` | `OnPlayPressed()` |
| `ProgramRunner` | `ResetRunner()` |
| `SocketRow` | `IncreaseRepetitions()`, `DecreaseRepetitions()`, `SetRepetitions(int)`, `Lock()`, `Unlock()`, `SetEditable(bool)`, `ClearRow(bool)`, `PlaceInitialBlocks()` |
| `BlockResetter` | `ResetBlocks()` |
| `LevelLoader` / `LevelManager` | `ReloadLevel()` · `ResetLevel()`, `CompleteLevel()` |
| `SystemModule` / `ModuleOptionButton` | `ToggleOption(int)`, `ResetModule()` · `Press()` |
| `RoboticArm` | `ResetArm()` |
| `ScenarioNController` | `StartScenario()`, `ResetScenario()` |
| `ShapeChip` | `ApplyShape()`, `SetShape(Sprite)`, `ApplyShapeToChild()` |
| `ShapeSocket` | `Refresh()`, `ResetSocket()`, `ApplyShapeToChild()` |
| `Narrator` | `PlayAudio(int/string)`, `StopAudio()`, `SetAudioListIndex(int)`, `ShowLineById(int)`, `CompleteInstantly()`, `Clear()` |
| `Timer` | `StartTimer()`, `Pause()`, `Continue()`, `Stop()`, `SetTimeLimit(int)` |
| `Fader` | `TriggerFadeIn()`, `TriggerFadeOut()` |
| `SessionSetup` | `SelectBasic()`, `SelectIntermediate()`, `SelectByIndex(int)`, `StartSession()` |
| `PinEntry` | `AppendDigit(int)`, `DeleteLast()`, `Clear()`, `UseNextPin()`, `SetPin(int)` |
| `SceneLoader` | `Load()`, `Load(string)` |
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

**El intento fallido del Escenario 3 se registra ANTES del `retryDelay`, y desde código.** Si se
esperase primero, el Director puede desactivar la estación durante la espera y matar la
corrutina: el fallo se perdería. Y si se cablease por `UnityEvent`, olvidarlo dejaría
`failedAttempts` a 0 sin que nada avise. Solo el aviso visual (`OnAttemptFailed`) va diferido.

**El Escenario 4 no declara su dificultad; la lee de `difficulty`.** Tuvo un `matchMode` propio
mientras el modo de emparejamiento cambiaba de verdad el comportamiento, y entonces no podía
mentir. Al quedar una sola mecánica se habría convertido en una etiqueta suelta que duplicaba
lo que ya dice `DifficultyScene` —que además **corrige** la telemetría si la escena y el
selector no coinciden—, con el riesgo de contradecirla. Dos indicadores que nadie reconcilia
son peores que uno.

**El escenario 4 compara sprites por referencia, no ids por nombre.** Los recortes se pueden
renombrar desde el Sprite Editor; un `shapeId` de texto habría dejado de encajar en silencio.
El nombre solo se usa para la telemetría, y por eso hay que fijarlo antes de recoger datos.

**`Scenario4Record` no hereda de `BlockScenarioRecord`.** Sus fichas se agarran igual que los
bloques, pero el escenario no tiene intentos ni ejecución: heredar habría emitido
`failedAttempts` y los tres contadores de error como ceros permanentes. Tiene sus propios
`chipsGrabbed`/`chipsReleased`, y `RegisterBlockGrabbed/Released` enruta a uno u otro. Antes el
cast a `BlockScenarioRecord` devolvía null y **cada agarre del Escenario 4 se descartaba en
silencio**.

**`Scenario3Controller` mide la meta por `destinationSlot`, no por `slotToClear.IsEmpty`.**
`ArmSlot.Take()` saca el objeto de la lista en el propio `Recoger`, antes de que el brazo
gire y lo suelte en algún sitio. Medir por el origen vacío completaba el nivel con el último
objeto todavía en la pinza, sin que el niño hubiera terminado el ciclo. `requiredCount` se
calcula una vez en `Start()` sumando el contenido inicial de origen y destino.

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

## 11. Estado voluble — 21/09/2026

> Esta sección caduca. Todo lo anterior es estable.

| Subsistema | Estado |
|---|---|
| Escenario 1 | ✅ Verificado en visor |
| Escenario 2 | 🟡 Montado en básica (3 módulos, 6 botones), sin probar |
| Escenario 3 | 🟡 Montado y cableado entero (4 bloques, fila reordenable, botón de ejecutar, meta y telemetría). Sin probar en visor. Quedan 2 correcciones de montaje |
| Escenario 4 | 🟡 Montado: 4 huecos y 18 fichas (10 figuras distintas) sobre `symbols.png`. Cada figura pedida la lleva 1 ficha y hay 14 distractoras. Sin probar en visor. Falta asignar `chipResetter` |
| Narrativa, subida | ✅ Funcionales |
| Telemetría | ✅ Funcional. Los cuatro escenarios registran; ver sección 7 |
| Selección de dificultad | 🟡 Scripts listos (`SessionSetup`, `PinEntry`, `SceneLoader`); falta montar la escena |
| Temporizador visible en todas las salas | 🟡 `TimerDisplay` listo; falta duplicar el panel por sala y el de la muñeca |
| HUD diegético (RI-02), username (RI-01), reinicio supervisado (RF-08) | ❌ Sin implementar |

**Decidido el 08/09/2026:** las dificultades se cambian **cargando escenas distintas**, no
intercambiando datos en caliente. `SystemModule.data` apunta a un único asset, así que cada
escena lleva su propio juego de `ModuleData`.

**Decidido el 15/09/2026:** los retos los abre **siempre el Director**. Un gestor de retos
dedicado queda como mejora futura; mientras tanto, la atribución de telemetría depende de que
cada paso llame a `StartScenario()`.

**Pendiente inmediato en escena:**

- `ScreenNarrator` sigue en `Main.unity` como **script perdido**: un único `MonoBehaviour` con
  `guid: 6d131647b98e46141a1d89097a26e78a`. La clase se fusionó en `Narrator`. Borrar el
  GameObject y quitarlo de los `waitActions` del Director
- Reconfigurar el `Narrator`: CSV, `Text`, y los clips con su `textId`
- Escenario 2 intermedia: añadir los botones 3 y 4 por módulo, con su `optionIndex`, y
  apuntar cada `SystemModule` a su asset `*_Intermedia`
- Escenario 2: cablear el estado visual nuevo — `statusIcon` con sus dos sprites, el
  `Renderer` de la luz y el `selectedMarker` de cada botón
- **Escenario 3: los textos de los dos bloques de giro están cruzados.** `BloqueRotarI`
  (`action = 2`, gira a la izquierda) muestra "girar derecha"; `BloqueRotarD` (`action = 3`)
  muestra "Girar izquerda", con errata. La lógica y la telemetría son correctas, pero la
  instrucción visible miente
- **Escenario 3: asignar el campo `runner`** de `Scenario3Controller` (el `ProgramRunner` del
  objeto `Escenario (3)`). Sin él no se registran los intentos fallidos y `failedAttempts`
  sale siempre 0
- Escenario 3: el **pivot del brazo debe tener X y Z a cero** (hoy los tiene). `RotateTowardsSlot`
  termina con `Quaternion.Euler(0, yaw, 0)` y aplasta cualquier inclinación en cuanto entras en
  Play. Si el modelo la necesita, ponla en un hijo del pivot
- **Escenario 4: `chipResetter` sigue sin asignar** en `Scenario4Controller`. El `BlockResetter`
  de las fichas ya existe y cuelga de `Escenario (4)`: es arrastrarlo. Sin él, `Awake` coge
  cualquier `BlockResetter` activo —el otro es el del Escenario 1— y las fichas rechazadas se
  quedan clavadas en un hueco que ya quedó libre
- Escenario 4: `ShapeChip.prefab` conserva en `shape` el guid del `Circulo.asset` borrado.
  Unity lo anulará al reimportar y ninguna de las 18 instancias lo usa, pero conviene dejarlo
  vacío a propósito
- **Escenario 4: renombrar los 34 recortes de `symbols.png`** en el Sprite Editor antes de
  recoger datos. Sin `ShapeData`, el id de telemetría es el nombre del recorte: hoy el JSON
  diría `chip: "symbols_17"`, ilegible, y renombrarlo después cambiaría los datos en silencio
- Temporizador: sacar el panel de `Escenario (1)`, convertirlo en prefab con un `TimerDisplay`,
  instanciarlo en las otras tres salas y bajo `LeftHandAnchor` del `Player.prefab`. Vaciar
  entonces el campo `display` de `Tools/Timer`, o ese `Text` recibe la hora por dos caminos
- Temporizador: `alerts` está vacío y `OnTimeUp` sin cablear. RF-06 pide avisos a 15, 10 y 5
  minutos y el cierre de sesión al agotarse
- Servidor: quitar el prefijo de timestamp en `server.py` (`saved_as = filename`).
  **Ese código no vive en este repositorio**

**Otros datos del repositorio:** existe `Assets/Scenes/Tests.unity` además de `Main.unity`, y 7
escenas de respaldo en `Assets/_Recovery/` que **no son las escenas activas** — aparecen en las
búsquedas y confunden.

**Seguridad:** `UPLOAD_API_KEY` está en claro en `JSONUploader.cs` y el token del túnel en su
`docker-compose.yml`. Asumido: servidor privado y temporal.

## 12. Decisiones abiertas

| Qué | Por qué sigue abierto |
|---|---|
| `SecuenciaIncompleta` | Sin definición operativa que separe "faltaron instrucciones" de "el orden estaba mal". Siempre vale 0. Es criterio pedagógico |
| `SessionResult` | Declarado pero sin campo en el JSON. Falta decidir qué dispara "abandonado" |
| Username vs PIN | El SRS pide username (RF-01), el código usa PIN. Divergencia **deliberada**: se decidió corregir el documento. Renombrar rompería los JSON ya recogidos |
| Mecánica del brazo | Montada entera, pero sin probar en visor ni con niños |
| Escenario 4 | ¿Necesita panel-ejemplo introductorio? Depende de los beta testers |
| Tanteo con el contador de repeticiones | Cada pulsación de `+`/`−` del Escenario 3 no se registra; solo queda el valor final del intento. Añadirlo es una métrica nueva, no un arreglo: decisión pedagógica |
| Gestor de retos | Hoy el reto activo lo fija quien llame a `StartChallenge`, y eso lo hace el Director. Un gestor que cada escenario declare al activarse eliminaría el riesgo de atribución, pero no hace falta mientras el Director abra todos los pasos |

## 13. Documentación relacionada

| Archivo | Para quién |
|---|---|
| `Docs/VARIABLES_TELEMETRIA.md` | Equipo evaluador. Qué mide cada variable, en lenguaje llano |
| `Docs/ejemplo_run_telemetria.json` | Run completa de ejemplo, con el formato exacto que emite `JsonUtility`. Para el equipo evaluador y para validar el parser de análisis |
| `Docs/Codea2_GDD.docx` | Game Design Document |
| `Docs/Informe-Mes1.docx` / `.pdf` | Informe técnico entregado, con el SRS (IEEE 830) |
| `Docs/Diagramas/` | Diagramas de flujo y funcionalidad, croquis |
| `Docs/Investigaciones/` | Respaldo académico: *worked examples*, manipulativos físico-digitales |
| `README.md` | Presentación institucional del repositorio |

## 14. Comprobaciones rápidas

```bash
# Compilar sin abrir Unity. Necesita que Unity haya generado el .csproj al menos una vez:
# en un worktree recién clonado no existe todavía
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
