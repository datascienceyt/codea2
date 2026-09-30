# Context — Codea VR 2

**Fuente de verdad única del proyecto.** Escrito para que cualquiera —persona o agente— entienda
el sistema completo sin leer las ~8.800 líneas de código ni depender de conversaciones previas.

Verificado contra el código el **30/09/2026**. Si algo aquí contradice al código, manda el
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
| Equipo evaluador | Evaluación técnica |

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

62 scripts en `Assets/_Main/`. Agrupados por responsabilidad:

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
| `Session/SceneLoader.cs` | Carga una escena cerrando antes la run y esperando a que acabe la subida. Para volver con el siguiente niño |
| `Session/TimeUpSequence.cs` | Tiempo agotado: para Director y narración, cierra y sube la run, dice la frase y vuelve |
| `Session/DifficultyScene.cs` | Declara la dificultad de su escena y la impone si no coincide |
| `Telemetry/TelemetryData.cs` | Modelo serializable del JSON |
| `Telemetry/TelemetryManager.cs` | Singleton **por escena**, sin `DontDestroyOnLoad` (los `UnityEvent` de la escena apuntan al suyo). Captura, escritura diferida, pruebas |
| `Telemetry/JSONUploader.cs` | Sube el JSON de la sesión por POST multipart, con reintentos |
| `Map/AutomaticDoor.cs` | Puertas de apertura **vertical**. Con dos paneles, uno baja y otro sube. `IStepAction` |
| `VRConsole.cs` | Consola de errores dentro del visor |
| `Editor/LevelEditorWindow.cs` | `Tools → Level Editor`. Pinta el grid y exporta JSON |
| `Editor/CodeaSceneTools.cs` | `Tools → Codea`: **1** crea la escena `Setup` con el teclado cableado; **2** prepara `Basico`/`Intermedio` (dificultad, tiempo agotado, vuelta a `Setup`). Repetibles |

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

**Niveles** en `Assets/_Main/Levels/`, asignados en `LevelLoader.levelJson`:

| | Archivo | Solución | Paleta · huecos |
|---|---|---|---|
| Básica | `reto.json` (6×5) | 8 bloques: Avanzar · Girar Der · Avanzar 2 · Avanzar · Girar Der · Avanzar 2 · Girar Izq · Usar | 8 · 8, sin sobrantes |
| Intermedia | `escenario1_intermedio.json` (7×5) | 9 bloques: Girar Izq · Avanzar 2 · Girar Der · Avanzar 2 · Girar Izq · Avanzar 2 · Girar Izq · Avanzar · Usar | 12 · 10, con 3 sobrantes |

El robot empieza mirando **abajo** (`direction: 2` del prefab). En intermedia eso es una pared:
el primer bloque tiene que ser un giro. Los peligros están justo donde acaba cada tramo, para
castigar pasarse de largo. La paleta y los huecos de intermedia están por montar.

### Escenario 2 — Condicionales *(montado, sin probar en visor)*

Tres módulos averiados. Cada uno enuncia un problema en texto y ofrece varias acciones. Los
botones **alternan** entre seleccionado y no seleccionado, y el módulo se repara cuando el
conjunto seleccionado coincide **exactamente** con el correcto: ni de menos ni de más.

| | Opciones | Correctas |
|---|---|---|
| Básica | 4 | 1 |
| Intermedia | 4 | 2 |

La dificultad vive en los **datos** (`ModuleData`), no en código, y se cambia **cargando otra
escena**. Hay un asset por módulo y dificultad en `Scripts/Scenario 2/Modulos/`, nombrados
`<Modulo>_Basica` y `<Modulo>_Intermedia`. Los `moduleId` son idénticos entre dificultades
para que la telemetría agregue.

**Cada distractor es una acción correcta en otro módulo.** Motores trata de combustible y
lubricación, energía de electricidad, enfriamiento de temperatura. Así no se puede acertar
reconociendo el texto de la opción: hay que leer el problema y descartar. *"Agregar agua"*
aparece en los tres módulos y solo es correcta en uno.

Estado visual por icono y luz roja/verde; el problema se mantiene textual, y la pantalla lleva
además el **título del sistema**, que sale de `ModuleData.moduleName`.

**El enunciado describe síntomas, no la causa.** Antes decía `<síntoma>: <causa>` —*"los motores
no encienden: el tanque de combustible está vacío"*—, y la parte tras los dos puntos daba la
respuesta literal. Ahora dice *"los motores hacen ruido pero no arrancan. Su tanque está
vacío"*: hay que deducir qué falta. Tres reglas al escribirlos:

- Ninguna palabra de la solución aparece en el enunciado. Si está, el niño empareja texto en
  vez de razonar
- **Una pista por acción correcta.** En intermedia hay dos síntomas, uno por cada opción que
  hay que marcar, para que el "ni de menos ni de más" sea deducible y no suerte
- Vocabulario de niño de ocho años. *Tanque, luces, botón, vapor, rechinar* — nada de
  *aguja, casco, aspas, medidor de carga*

*"Su tanque está vacío"* aparece igual en Motores y en Enfriamiento a propósito: el síntoma es
idéntico y la respuesta distinta, y lo que decide es la primera frase.

**Al seleccionar una opción incorrecta**, `SystemModule.OnWrongOption` dispara la frase de error
del narrador (sección 6). No salta al deseleccionarla: quitar una opción equivocada es
autocorrección, y regañar por acertar sería justo al revés.

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

**Al poner la ficha de tipo, las repeticiones vuelven a 0**, y con 0 tampoco se ejecuta: cambiar de tipo es preparar otra pasada. La fila del esc. 3 tiene `minRepetitions: 0`.

**El socket se registra solo en su botón** (`ProgramTrigger.AddPrecondition`), buscando el que
tiene `challengeId: escenario3`, y de ahí toma la fila. No depende de la lista `preconditions`
del inspector: en `Basico` esa lista apuntaba a las dos **fichas**, que no son condiciones, y
se ignoraban sin avisar. Mientras duró, el programa corría sin ficha, **`itemType` salía vacío
en todos los intentos** y las repeticiones no volvían a 0. `ProgramTrigger` avisa ahora de
cualquier entrada que no sea una condición, y `Tools → Codea → 2` limpia la lista.

**Sin ficha de tipo el programa no se ejecuta.** `ArmTypeSocket` implementa `IRunPrecondition`,
que `ProgramTrigger` consulta **antes de registrar el intento**: un programa sin su argumento no
probó ninguna solución y no debe contar como intento.

**El botón no se bloquea tras una ejecución.** `ProgramTrigger.allowRepeatedRuns` desactiva el
guardia de `HasRun`. Sin eso, la segunda pasada era imposible y el escenario quedaba sin salida.

**Reintento:** falla la pasada que **no clasificó nada**, no la que dejó el escenario
incompleto — si no, la pasada de barriles perfecta se contaría como fallo porque faltan las
cajas. La que avanzó y se quedó corta dispara `OnAttemptAdvanced` y rearma el runner. El fallo
se registra **desde código**; no cablees `RegisterFailedAttempt` en `OnAttemptFailed`.

**Objeto olvidado en la pinza:** al terminar cada ejecución, `Scenario3Controller` llama a
`RoboticArm.ReturnHeldToOrigin()`, que devuelve a su pila lo que quede agarrado. **No cablees
`ResetArm` en `OnAttemptFailed`**: devuelve *todas* las pilas al inicio y borraría lo ya
clasificado en pasadas anteriores. La herramienta `Tools → Codea → 2` lo quita si lo encuentra.

**Montaje en escena:** raíz `Escenario (3)` con `Scenario3Controller` + `ProgramRunner` +
`ProgramTrigger`. Tres `ArmSlot` en el array del brazo, **en orden [izquierda, frente, derecha]**
con `startSlotIndex = 1` — el orden del array define la rotación, no los valores de `yaw`. El
frente lleva dos `TypedStack` (una por tipo) y cada destino una sola. Cada barril y cada caja
necesita su `ArmItem`. Prefabs: `ArmBlock`, `ArmTypeChipBlock`, `ArmSocketTipo`.

### Escenario 4 — Patrones *(montado, sin probar en visor)*

Fichas con figuras abstractas que encajan en huecos. Varias se parecen mucho entre sí y solo
una es idéntica a la del hueco: el reto es de **discriminación visual**, comparar el detalle en
vez de reconocer una forma conocida. Las figuras salen de un pliego recortado en `Multiple`,
`Scripts/Scenario 4/Figuras/symbols.png` (34 recortes, nombrados `<exterior>_<interior>` o
`<familia>_<variante>`: `circulo_rombo`, `persona_cuadrado_ovalo`, `tresenraya_x1_o9`...).

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

**Hueco resuelto:** la figura del hueco pasa a `solvedColor`, suena `solvedSound` y se dispara `OnSolved`. **Panel completo:** `Scenario4Controller.completedSound` (el arranque de motores, coherente con la frase 18), en `completedSource` o, si está vacío, en la posición del controlador. **Física:** las 18 fichas son dinámicas y con gravedad desde el inicio, y los huecos tienen el collider en trigger. Al **encajar**, `BlockNode.AttachTo` la vuelve cinemática: un Rigidbody dinámico ignora a su padre y la gravedad la sacaba del hueco. Una ficha **incorrecta** se queda encajada `rejectDelay` s y después el hueco la expulsa (`ShapeSocket.Eject`, con el `clipOut` del socket) con un empujón de `rejectSpeed` m/s en la dirección `rejectLocalDirection` (ejes del hueco), y cae con su física: no vuelve a su sitio, para que se vea el rechazo y quede a mano para otro hueco.

**El botón de reiniciar** del escenario llama a `BlockResetter.ResetBlocks`, que **solo devuelve las piezas que siguen siendo agarrables**: las fichas acertadas (bloqueadas al encajar) se quedan en su hueco. Antes las devolvía todas y el hueco quedaba resuelto pero vacío, sin forma de terminar. Esto vale también para los bloques fijos de la fila del Escenario 3. El reinicio completo es `Scenario4Controller.ResetScenario`, que desbloquea antes. Si el niño la saca él mismo durante la espera, no se toca. Al **devolverla** en un reinicio, `BlockResetter` le restaura el estado físico con el que empezó (antes la dejaba cinemática para siempre). Si queda más de `fallenBelow` por debajo de su sitio durante `returnDelay` s, vuelve sola.

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

**El texto aparece entero de golpe** (`revealProgressively` desactivado por defecto), decidido en la reunión del 29/09.

**`Narrator` reproduce voz y escribe texto a la vez** y espera a la más larga de las dos.
Emparejado por ID: cada `NarrationEntry` tiene `clip` + `textId` contra un CSV
(`ID_Texto, Texto_Narrativa`), hoy **`Assets/_Main/Narrativa.csv`**: 20 IDs más 2 variantes
por dificultad, `9.1`/`9.2` (esc. 2) y `14.1`/`14.2` (esc. 3). El `20` es el tiempo agotado.

**`textId` es texto, no número**, precisamente por esas variantes: como entero, `9.1` no
existía y la línea se saltaba en silencio. Los audios se nombran con su ID
(`Sounds/VoiceLines/Victor/9.1.wav`), y el menú contextual del Narrator **"Asignar textId por
nombre del clip"** los rellena solos: escribirlos a mano ya dejó una vez toda la lista
desplazada a partir de la primera variante.

**Una lista por dificultad**, elegida con `listIndex`: `1` = `Victor (Basica)`, `2` =
`Victor (Intermedia)`. La `0` (`Camila`) es la grabación anterior y no se usa. Las dos listas
tienen **19 entradas en el mismo orden** y deben seguir así: el Director reproduce una por cada
vez que un paso espera al Narrator, así que una lista con una línea de más desplaza todas las
siguientes. El Director de `Basico` tiene que esperar al Narrator exactamente 19 veces.

Degrada limpiamente: sin clip solo escribe, sin `Text` o sin CSV solo suena, `textId` vacío o
`0` significa sin texto. **Ni el CSV ni el Text son obligatorios.** Las frases de error
(`e1`–`e5.wav`) van en `errorLines` sin `textId`: no están en el CSV.

> Exportar el CSV como **"CSV UTF-8"**, o los acentos llegan rotos. El parser propio respeta
> comillas, comas internas y saltos de línea; un `Split(',')` partiría las frases.


**Frases de error.** Aparte de los tramos hay un banco `errorLines`, y `PlayErrorLine()` suelta
una al azar sin repetir la anterior. **No avanza el recorrido principal**: equivocarse no hace
progresar la historia, y si lo hiciera un niño que falla mucho se saltaría medio guion. Si ya
hay una línea sonando no la corta y no dice nada — pisar la narración para regañar perdería
justo la instrucción que quizá explicaba cómo acertar.

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
| `TelemetryManager` | `StartChallenge(string)`, `CompleteChallenge(string)`, `EndRun()`, `Flush()`, `RegisterFailedAttempt()`, `RegisterBlockGrabbed/Released()`, `RegisterLogicError(int)`, `ReportIntegrity()`, `IncrementPin()`, `SetPin(int)`, `SetDifficulty(int)` |
| `JSONUploader` | `UploadTelemetry()`, `UploadFile(string)` |
| `ProgramTrigger` | `OnPlayPressed()` |
| `ProgramRunner` | `ResetRunner()` |
| `SocketRow` | `IncreaseRepetitions()`, `DecreaseRepetitions()`, `SetRepetitions(int)`, `Lock()`, `Unlock()`, `SetEditable(bool)`, `ClearRow(bool)`, `PlaceInitialBlocks()` |
| `BlockResetter` | `ResetBlocks()` |
| `LevelLoader` / `LevelManager` | `ReloadLevel()` · `ResetLevel()`, `CompleteLevel()` |
| `SystemModule` / `ModuleOptionButton` | `ToggleOption(int)`, `ResetModule()`, `Refresh()` · `Press()` |
| `RoboticArm` | `ResetArm()`, `ReturnHeldToOrigin()` |
| `ScenarioNController` | `StartScenario()`, `ResetScenario()` |
| `ShapeChip` | `ApplyShape()`, `SetShape(Sprite)`, `ApplyShapeToChild()` |
| `ShapeSocket` | `Refresh()`, `ResetSocket()`, `ApplyShapeToChild()` |
| `Narrator` | `PlayAudio(int/string)`, `PlayErrorLine()`, `StopAudio()`, `Interrupt()`, `SetAudioListIndex(int)`, `ShowLineById(int/string)`, `CompleteInstantly()`, `Clear()` |
| `Timer` | `StartTimer()`, `Pause()`, `Continue()`, `Stop()`, `SetTimeLimit(int)` |
| `Fader` | `TriggerFadeIn()`, `TriggerFadeOut()` |
| `SessionSetup` | `SelectBasic()`, `SelectIntermediate()`, `SelectByIndex(int)`, `StartSession()` |
| `PinEntry` | `AppendDigit(int)`, `DeleteLast()`, `Clear()`, `UseNextPin()`, `SetPin(int)` |
| `SceneLoader` | `Load()`, `Load(string)` |
| `TimeUpSequence` | `Begin()` |
| `Director` | `Play()`, `Stop()` |
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

**`Scenario3Controller` mide la meta contando lo que hay en cada destino, no lo que falta en el
origen.** `ArmSlot.Take()` saca el objeto de la pila en el propio `Recoger`, antes de que el
brazo gire y lo suelte: medir por el origen vacío completaba el nivel con el último objeto
todavía en la pinza, sin que el niño hubiera terminado el ciclo. Cada `SortingGoal` calcula su
`required` una vez en `Start()`, contando ese tipo en **todas** las posiciones del brazo.

**`Socket.Active` es un registro estático.** `BlockNode` lo recorre cada frame por cada bloque
agarrado; una búsqueda global ahí costaba FPS (RNF-01).

**El rechazo de ficha del Escenario 4 se difiere un momento.** El aviso llega desde
`Socket.Occupy()` y `AttachTo` aún no terminó de enlazar el bloque.

**Los contadores de bloques van en código, no en `UnityEvent`.** Ver trampa 1.

**`OnApplicationPause` no cierra la run.** Quitarse el visor pausaba la app y ponía `endedUtc`
a mitad de sesión; como `EndRun` es idempotente, ya no se corregía.


**El Escenario 3 empareja por argumento, no por posición del brazo.** Las dos pilas del frente
comparten posición y se distinguen por el tipo puesto en el socket. Girar para elegir habría
hecho falta una posición por tipo, y el bucle dejaría de ser el mismo programa con otro dato.

**En básica el giro es un bloque propio, no una reinterpretación.** `RotateToDestination` y
`RotateToOrigin` existen para que el brazo resuelva el destino sin que ningún bloque mienta
sobre lo que hace. Reinterpretar "Girar Izquierda" según el tipo reintroduce el bug de
`RotateTowardsSlot`, y en intermedia esos mismos bloques significan izquierda y derecha
literales.

**Falla la pasada que no clasificó nada, no la que dejó el escenario incompleto.** Con dos
tipos hacen falta al menos dos ejecuciones, así que la pasada de barriles perfecta terminaría
con las cajas sin mover: contarla como fallida inflaría `failedAttempts` midiendo el diseño del
reto en vez del error del niño.

**`ArmSlot` reparte los objetos iniciales por el `ArmItem` de cada uno, no por la lista en la
que estén escritos.** El tipo de un objeto es una sola cosa y la lleva él; que además hubiera
que acertar la lista era una segunda fuente de verdad, y en cuanto discreparon el brazo cogía
de la pila equivocada sin que nada avisara.

**`ProgramTrigger.allowRepeatedRuns` existe porque `HasRun` solo vale donde una ejecución agota
el reto.** Donde el reto se resuelve en varias pasadas, exigir un reinicio entre ellas deja el
botón muerto a mitad de partida y sin nada que explique por qué.

**`TelemetryManager` es uno por escena, sin `DontDestroyOnLoad`.** Lo tuvo, y desde el segundo
niño se perdían datos: al recargar la escena sobrevivía el anterior y el nuevo se autodestruía,
pero los `UnityEvent` de la escena apuntan al de *su* escena, y Unity se salta en silencio las
llamadas a un objeto destruido. La run se cierra en su `OnDestroy`.

**`SceneLoader` espera a la subida antes de cargar.** La corrutina de subida vive en la escena
que se descarga; cargar a mitad la cortaba y el JSON de la sesión recién terminada no llegaba.

**Lo que queda en la pinza vuelve a su pila al acabar cada ejecución; no se reinicia el
brazo.** Una secuencia que recoge sin soltar bloqueaba todas las siguientes (pinza ocupada).
`ResetArm` lo resolvía, pero devolviendo también lo ya clasificado: castigaba un error puntual
borrando el trabajo bueno de pasadas anteriores.

**`textId` es texto.** Las variantes por dificultad del CSV (`9.1`, `14.2`) no caben en un
entero, y el parser las descartaba sin avisar.

**El informe de integridad corre en `EndRun()`.** Esta telemetría no falla reventando: falla
saliendo a cero, y un JSON válido y vacío solo se descubre semanas después. Ver sección 7.

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

## 11. Estado voluble — 30/09/2026

> Esta sección caduca. Todo lo anterior es estable.

**Escenas:** `Setup` (pantalla del supervisor), `Basico` e `Intermedio`, las tres en Build
Settings en ese orden. `Intermedio` es una copia antigua de `Basico` y **se va a regenerar
duplicando `Basico`** cuando esta esté terminada: no tiene nada propio salvo los módulos del
Escenario 2 con datos `*_Intermedia`. Quedan además `Main 2.unity`, `Tests.unity` y 7
respaldos en `_Recovery/`, que no son escenas activas.

| Subsistema | Estado |
|---|---|
| Escena `Setup` | ✅ Montada con la herramienta: 15 teclas cableadas, sin `TelemetryManager` |
| Sesión en `Basico` | ✅ `DifficultyScene`, `SceneLoader`, `TimeUpSequence` (frase `20`) y paso final "Volver a Setup" |
| Escenario 1 | ✅ Verificado en visor. Nivel intermedio escrito (`escenario1_intermedio.json`), sin montar |
| Escenario 2 | ✅ 4 opciones en básica, `OnWrongOption → PlayErrorLine` en los 3 módulos. Sin probar en visor |
| Escenario 3 | ✅ `preconditions` corregida (apuntaba a las fichas), pinza arreglada, repeticiones a 0 al poner la ficha. Sin probar en visor |
| Escenario 4 | ✅ Fichas con física, rechazo con empujón (`rejectSpeed` 4) y sonido, reinicio que respeta las acertadas, sonido de motores al completar. Sin probar en visor |
| Narrativa | ✅ CSV y audios nuevos, listas Básica/Intermedia de 19 frases, 5 frases de error. El Director espera al Narrator exactamente 19 veces |
| Telemetría | ✅ Un `TelemetryManager` por escena, PIN desde `Setup` |
| Servidor | ✅ `~/Services/JSONServer` en la Pi |
| Temporizador | ✅ Tiempo agotado cableado. `alerts` vacíos (no hay frases de aviso en el CSV) |
| HUD diegético (RI-02), reinicio supervisado (RF-08) | ❌ Sin implementar |

**Decidido el 08/09/2026:** las dificultades se cambian **cargando escenas distintas**.

**Decidido el 15/09/2026:** los retos los abre **siempre el Director**.

**Decidido el 29/09/2026:** PIN y dificultad se eligen en una escena `Setup` propia, en el
visor, antes de pasárselo al niño.

**Decidido el 29/09/2026 (reunión):** texto de narración completo de golpe; 4 opciones en las
dos dificultades del Escenario 2 (1 correcta en básica, 2 en intermedia); repeticiones a 0 al
poner la ficha del Escenario 3; física y aviso de acierto en el Escenario 4.

**Decidido el 30/09/2026:** la ficha rechazada del Escenario 4 sale disparada desde el hueco
en vez de volver a su sitio, y el reinicio no mueve las fichas acertadas.

**Reparto de la narración en el Director de `Basico`** (verificado el 30/09/2026). Si se
añade o quita una frase del CSV, hay que rehacer esta tabla:

| Paso | Esperas al Narrator | Frases |
|---|---|---|
| Esc. 1 · Narrar | 6 | 1–6 |
| Esc. 1 · Esperar a completar | 1 | 7 |
| Esc. 2 · Narrar | 2 | 8, 9.x |
| Esc. 2 · Teletransportar a panel 3 | 1 | 10 |
| Esc. 3 · Narrar | 4 | 11, 12, 13, 14.x |
| Esc. 3 · Esperar a completar | 1 | 15 |
| Esc. 4 · Narrar | 2 | 16, 17 |
| Esc. 4 · Esperar a completar | 1 | 18 |
| Esc. 4 · Completar reto | 1 | 19 |

**Pendiente en `Basico`:**

- Partida completa en Play dos veces seguidas (`Setup → Basico → Setup`), tiempo agotado
  (`TimeUpSequence → Probar tiempo agotado`) y después en visor
- Verificar en visor: manos (agarrar y pulsar), título y botones del Escenario 2, bloques y
  rótulos del Escenario 3 (`BloqueRotarI`/`BloqueRotarD`), física y rechazo del Escenario 4
- Opcional: `solvedSound` del Escenario 4; mover "Iniciar temporizador" tras la frase 3, que
  es la que pide pulsarlo; sprite roto en `ShapeChip.prefab`; salas activas desde el arranque

**Después, al generar `Intermedio`:** duplicar `Basico`, ejecutar `Tools → Codea → 2`, y
cambiar solo: `listIndex` 2; módulos del esc. 2 a `*_Intermedia`; esc. 1 con
`escenario1_intermedio.json`, 10 huecos y paleta de 12 (Girar Izq ×3, Girar Der ×2,
Avanzar 2 ×4, Avanzar ×2, Usar ×1); esc. 3 con `editable` y los bloques Recoger · Girar Izq ·
Soltar · Girar Der sueltos; esc. 4 con el juego de figuras denso.

**Seguridad:** `UPLOAD_API_KEY` está en claro en `JSONUploader.cs` y el token del túnel en su
`docker-compose.yml`. Asumido: servidor privado y temporal.
## 12. Decisiones abiertas

| Qué | Por qué sigue abierto |
|---|---|
| `SecuenciaIncompleta` | Sin definición operativa que separe "faltaron instrucciones" de "el orden estaba mal". Siempre vale 0. Es criterio pedagógico |
| `SessionResult` | Declarado pero sin campo en el JSON. Falta decidir qué dispara "abandonado" |
| Username vs PIN | El SRS pide username (RF-01), el código usa PIN. Divergencia **deliberada**: se decidió corregir el documento. El PIN lo teclea el supervisor en la escena `Setup` |
| Mecánica del brazo | Reescrita con ficha de argumento y dos destinos, sin probar en visor ni con niños. ¿Entienden los niños que la ficha es un parámetro y no una instrucción? |
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
grep -n "m_MethodName: X\|value: X" Assets/Scenes/Basico.unity
```

**Probar sin visor:** casi todo tiene `[ContextMenu]`. `ModuleOptionButton → Press`,
`RoboticArm → Probar/Ciclo completo`, `VRInteractEvents → Invoke Full Press`,
`TelemetryManager → Test/Simular y subir`, y el teclado W/A/D/Espacio del `Bot` en editor.

Los cuatro escenarios avisan al completarse:
`[Escenario N] COMPLETADO · challengeId '...'`. Si uno no aparece, ahí está el corte.
