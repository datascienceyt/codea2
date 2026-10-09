# Context — Codea VR 2

**Fuente de verdad única del proyecto.** Escrito para que cualquiera —persona o agente— entienda
el sistema completo sin leer las ~12.400 líneas de código ni depender de conversaciones previas.

Verificado contra el código el **09/10/2026**. Si algo aquí contradice al código, manda el
código: avisa y corrige este documento.

---

## 0. Para retomar en un chat nuevo

**Dónde estamos (09/10/2026).** La versión se presentó el 30/09/2026 y desde entonces se ajusta
con el feedback. Todo lo de abajo está **en `dev`, subido a los dos remotos**, y el trabajo sigue
en la rama **`informe-mes2`**, creada desde ese `dev`.

- **06/10/2026**: formato nuevo de la telemetría, botón del Escenario 2 que se suelta solo y cuarto
  del tutorial con su botón INICIAR
- **09/10/2026**: las dos dificultades en **una sola escena, `Juego`** (sección 5, "Dificultad");
  en el Esc. 3 de básica la fila está oculta y funciona sola; "Sistema de motores" y "Agregar
  combustible"; corregido el "Skip Step" del Director; dos herramientas de verificación
  (`Tools → Codea → Comprobar escena de juego` y `Probar las dos dificultades en Play`), las dos
  con **0 fallos**; recopilado el material del informe del mes 2
- **Sin probar en el visor.** El APK de `Build/app.apk` es el del 30/09: formato viejo de
  telemetría, sin tutorial y carga `Basico`/`Intermedio`, que ya no existen como escenas de juego

**Por dónde seguir, en este orden:**

1. **Informe del mes 2 en Word** (`Docs/Informe-Mes2.docx`), con el **mismo formato** que
   `Docs/Informe-Mes1.docx` y los entregables 2.1, 2.2 y 2.3. El contenido ya está reunido en
   `Docs/Entregable-Mes2/Recopilacion_Producto2.md`; lo marcado ⟨pendiente⟩ lo tiene que dar el
   usuario (nombre del Producto 2, fecha, participantes de las pruebas, fotos). Ver "Informe del
   mes 2" en la sección 11 para el plan técnico
2. **En el editor** (lo hace el usuario): abrir `Juego`, `Tools → Codea → Comprobar escena de
   juego`, revisar la paleta del Esc. 1 con `Ver como básica / intermedia`, jugar las dos
   dificultades (`Sesion → DifficultyApplier → Editor Difficulty`, y dejarlo después en `De La
   Pantalla De Setup`) y **borrar `Assets/Scenes/Intermedio.unity`**
3. **Recompilar el APK** (mismo keystore) y pasar en el visor la tabla de la sección 14 y el plan
   de `Docs/Pruebas/`
4. **Pendientes de la sección 11**, uno a uno con plan corto antes: frase 3 de la narración
   (decisión del usuario), pedidos 7, 8 y 10 del Esc. 3, depurar logs del Esc. 1, textos y
   paneles del Esc. 2, indicaciones del Esc. 1, glifos Yachay, perfil de render para Quest

**Cómo se trabaja en este proyecto** (acuerdos con el usuario, no negociables):

- **Commits sin coautor**: nunca añadir `Co-Authored-By` ni firma de IA
- **Cada push va a los dos remotos**, `clousck` y `datascienceyt`, con `dev` idéntico en ambos
- **Si Unity está abierto, no editar escenas ni prefabs a mano** (YAML): Unity los pisa o pide
  recargar. Los cambios de escena van por `Tools → Codea` (sección 3) o los hace el usuario en
  el editor. Comprobar antes con `tasklist | grep -i unity`
- **Compilar antes de dar algo por hecho** (sección 14) y decir siempre si se probó en Play o
  en el visor, o solo compila
- **Respuestas cortas y estructuradas**: listas de acciones y tablas, no párrafos. Cuando hay
  que conectar algo en el inspector, decir objeto → campo → valor
- **No alargar las tareas**: si una búsqueda o un diseño se complica, parar, contar lo que hay
  y preguntar. El diseño de niveles lo decide el usuario; el agente lo comprueba
- **Tras cambiar código, actualizar este documento** en la misma sesión

**Herramientas ya hechas, para no rehacerlas:**

| Para | Usar |
|---|---|
| Montar o reparar las escenas | `Tools → Codea → 1` y `2` en Unity (`Editor/CodeaSceneTools.cs`) |
| Montar y cablear el tutorial | `Tools → Codea → 4`, con la escena de juego abierta |
| Ver en el editor solo las piezas de una dificultad | `Tools → Codea → Ver como básica / Ver como intermedia / Ver todas las piezas` (solo visibilidad, no cambia la escena) |
| Probar una dificultad en Play sin pasar por `Setup` | `Sesion → DifficultyApplier → Editor Difficulty` |
| Revisar la escena de juego entera sin jugarla: llamadas de UnityEvent rotas, scripts perdidos, el Director paso a paso, narración contra el CSV, cada reto en cada dificultad, `Setup` | `Tools → Codea → Comprobar escena de juego` (`Editor/CodeaSceneCheck.cs`). Informe en `Logs/Codea_Comprobacion.txt`. **Pasarla tras tocar la escena o renombrar un método** |
| Jugar las dos dificultades sin visor y recorrer el Director entero | `Tools → Codea → Probar las dos dificultades en Play` (`Editor/CodeaPlayCheck.cs`). Se para antes de subir el JSON; restaura las PlayerPrefs y borra sus JSON. Informe en `Logs/Codea_PruebaPlay.txt` |
| Las dos anteriores sin abrir Unity | `Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod CodeaSceneCheck.CheckBatch` · la de Play igual pero **sin `-quit`** y con `CodeaPlayCheck.RunBatch` (sale sola; tarda ~5 min) |
| Saber si un nivel del Escenario 1 tiene solución y cuántos bloques pide | `node Docs/Herramientas/resolver_nivel.js <nivel.json> <máx>` |
| Regenerar el GDD | `Docs/Herramientas/generar_gdd.js` (ver su `LEEME.md`) |
| Comprobar que un JSON de telemetría tiene el formato del código | `node Docs/Herramientas/validar_json_telemetria.js [archivo]` (sin archivo, los de la documentación) |
| Comprobar la telemetría de punta a punta | `Docs/Pruebas/PLAN_PRUEBA_TELEMETRIA.md` |
| Asignar los `textId` de la narración | Menú contextual del Narrator: "Asignar textId por nombre del clip" |

---

## 1. El proyecto en diez líneas

**CODEA 2** — Escape room educativo en VR para desarrollar **pensamiento computacional** en
niños de 8-17 años de contexto rural en Ecuador. Respaldo de Yachay Tech.
**Presentado el 30 de septiembre de 2026; en fase de ajustes.**

El jugador es un astronauta en una nave que atravesó una tormenta espacial. Con la ayuda de
Roki, el robot de mantenimiento, repara cuatro sistemas en 15 minutos para volver a la Tierra.
Cada sala trabaja un pilar: secuencialidad, condicionales, bucles y patrones. Un supervisor
teclea el PIN y fija la dificultad antes de entregar el visor; los datos se guardan en el
visor y se suben solos al terminar si hay red.

| Persona | Rol |
|---|---|
| Ing. Víctor Echeverría | Desarrollador principal |
| Ph.D. Erick Cuenca | Director del proyecto |
| Equipo evaluador | Evaluación técnica |

Repositorio `github.com/datascienceyt/codea2`, rama `dev`.

## 2. Stack y restricciones no negociables

- Unity **6000.4.1f1**, URP + Shader Graph
- Meta XR All-in-One SDK **203.0.2**, Horizon OS
- **Meta Quest 3S exclusivamente**. Mandos físicos; el proyecto admite también manos
  (`handTrackingSupport: 1`) y todos los bloques y fichas tienen agarre con mano, pero **falta
  verificarlo en el visor**
- Locomoción por teletransporte
- **100 % funcional offline** (RNF-04). La subida de datos es opcional: se lanza sola al
  terminar la sesión o al agotarse el tiempo, y sin red el JSON se queda en el visor
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

63 scripts en `Assets/_Main/`. Agrupados por responsabilidad:

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
| `ModuleOptionButton.cs` | Un botón. Expone `Press()`, sin acoplarse a Oculus. Con `standalone` funciona sin módulo: se alterna solo (tutorial) |
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
| `TutorialController.cs` | Cuarto del tutorial: vigila cuatro interacciones (dos botones, un bloque, una ficha) y termina cuando están todas. `IStepAction`, sin telemetría |
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
| `Difficulty/DifficultyApplier.cs` | Monta la escena en la dificultad de la sesión al cargar, antes que ningún otro script (orden -1000). Antes era `DifficultyScene` (mismo guid) |
| `Difficulty/DifficultyVariant.cs` | Base de lo que cambia en un reto: un juego de datos `basica` y otro `intermedia` |
| `Difficulty/Scenario1Difficulty.cs` · `Scenario2Difficulty.cs` · `Scenario3Difficulty.cs` · `NarrationDifficulty.cs` | Lo que cambia en cada reto y en la narración (sección 5, "Dificultad") |
| `Difficulty/DifficultyOnly.cs` | Marca una pieza que solo existe en una dificultad; en la otra se apaga |
| `Telemetry/TelemetryData.cs` | Modelo serializable del JSON |
| `Telemetry/TelemetryManager.cs` | Singleton **por escena**, sin `DontDestroyOnLoad` (los `UnityEvent` de la escena apuntan al suyo). Captura, escritura diferida, pruebas |
| `Telemetry/JSONUploader.cs` | Sube el JSON de la sesión por POST multipart, con reintentos |
| `Map/AutomaticDoor.cs` | Puertas de apertura **vertical**. Con dos paneles, uno baja y otro sube. `IStepAction` |
| `VRConsole.cs` | Consola de errores dentro del visor |
| `Editor/CodeaSceneCheck.cs` · `Editor/CodeaPlayCheck.cs` | Comprobación de la escena y prueba en Play de las dos dificultades (tabla de herramientas, sección 0) |
| `Editor/LevelEditorWindow.cs` | `Tools → Level Editor`. Pinta el grid y exporta JSON |
| `Editor/CodeaSceneTools.cs` | `Tools → Codea`: **1** crea la escena `Setup` con el teclado cableado; **2** prepara la escena de juego (`DifficultyApplier`, tiempo agotado, vuelta a `Setup`). **3** unificó `Basico` e `Intermedio` en `Juego` (se pasó el 09/10/2026; no hace falta volver a pasarla y se puede quitar al borrar `Intermedio`). **4** monta en el cuarto del tutorial las cuatro interacciones y su `TutorialController`. **Ver como…** oculta en la vista de escena las piezas de la otra dificultad. Repetibles |

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

### Dificultad: una escena, dos versiones de cada reto

Las dos dificultades son la **misma escena, `Juego`** (desde el 09/10/2026; antes eran
`Basico` e `Intermedio`, y cada cambio había que hacerlo dos veces). El supervisor elige la
dificultad en `Setup`, que la deja en PlayerPrefs; al cargar `Juego`, **`DifficultyApplier`**
(en `Sesion`) la lee de ahí —la misma que lee `TelemetryManager`, así que escena y JSON no
pueden discrepar— y monta cada reto antes de que despierte ningún otro script.

Lo que cambia se declara de dos formas:

| Qué cambia | Cómo | Dónde verlo |
|---|---|---|
| **Datos** de un reto (nivel, huecos, módulos, fila, lista de narración) | Un componente `<Reto>Difficulty` junto al controlador del reto, con un bloque `Basica` y otro `Intermedia` en el inspector | `Escenario (1)`, `Escenario (2)`, `Escenario (3)`, `Narrator` |
| **Piezas** que solo existen en una dificultad (bloques de paleta, fichas, huecos) | Componente `DifficultyOnly` en la pieza. En la otra dificultad se apaga al cargar | `Tools → Codea → Ver como básica / intermedia` |

Lo que no lleva nada es común a las dos.

| Reto | Básica | Intermedia |
|---|---|---|
| Narración (`NarrationDifficulty`) | lista 1 | lista 2 (frases 9.2 y 14.2) |
| Esc. 1 (`Scenario1Difficulty`) | `reto.json`, 8 huecos, paleta de 8 | `escenario1_intermedio.json`, 11 huecos, paleta de 13 en otra colocación |
| Esc. 2 (`Scenario2Difficulty`) | `*_Basica` | `*_Intermedia` |
| Esc. 3 (`Scenario3Difficulty`) | Fila **oculta** y bloqueada: Recoger · Girar al destino · Soltar · Volver. Funciona sola: el niño solo pone la ficha y las repeticiones | Visible, editable y desordenada: Soltar · Girar Der · Recoger · Girar Izq |
| Esc. 4 | Las 4 figuras actuales | **Las mismas** hasta el pedido 11 |

**La paleta del Esc. 1 no comparte bloques**: en `Intermedio` estaba recolocada entera, así
que hay 8 bloques con `DifficultyOnly` Básica y 13 con Intermedia, uno encima de otro en la
mesa. Para moverlos, `Ver como …` primero. Los giros de intermedia del Esc. 3 son **bloques
propios** (`… (Girar Izquierda)`, `… (Girar Derecha)`), no los de básica reinterpretados.

**Añadir una diferencia nueva** (p. ej. el pedido 11 del Esc. 4):

- Una pieza de más o de menos: ponerle `DifficultyOnly` con su dificultad. El Esc. 4 ya
  ignora los huecos apagados al comprobar si el panel está completo
- Un dato: añadir el campo al `Settings` del `<Reto>Difficulty` y aplicarlo en su `Apply`. Si
  el reto aún no tiene uno, heredar de `DifficultyVariant<TSettings>`
- El componente que recibe el dato tiene que usarlo en su `Awake`/`Start` o después:
  `DifficultyApplier` corre antes que todos (orden -1000)
- **No activar una pieza con `DifficultyOnly` desde el Director** (`Tools.SetActive`): volvería
  a aparecer en la dificultad que no le toca. Activar su padre

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
| Intermedia | `escenario1_intermedio.json` (6×5) | Un camino de 11 y un distractor de 14; ver abajo | 13 · 11 |

**Los dos niveles comparten la geometría de la sala**: 6×5, Salida en **(0,1)**, que es donde
está el botón verde físico (la casilla Salida solo es lógica; el botón es decorado), y robot en
**(4,2) mirando abajo** (`direction: 2` del prefab; la frase 5 dice que se ve a la derecha).
Un nivel con otra geometría deja la meta lógica lejos del botón. Pasó con la primera versión
del intermedio.

**El botón solo se usa de frente:** desde **(1,1) mirando a la izquierda**. Está en una pared,
así que llegar a (0,0) o (0,2) y girarse hacia la Salida no vale aunque el código lo aceptara.
Los dos niveles terminan siempre con el robot en (1,1).

```
   0 1 2 3 4 5        Intermedia (diseño del 30/09/2026). S = salida · R = robot · ! = peligro
0    ! ! · · ·
1  S · · · █ ·        (1,1) = la casilla de enfrente del botón
2    · █ █ R ·
3    · · · █ ·
4    ! ! · · ·
```

Simétrico arriba y abajo, verificado por búsqueda exhaustiva:

- **Por arriba (la solución, 11 bloques):** Girar Izq · Avanzar · Girar Izq · Avanzar 2 · Girar Izq · Avanzar 2 · Girar Izq · Avanzar · Girar Der · Avanzar 2 · Usar
- **Por abajo (el distractor, 14 bloques):** llega también a (1,1), pero no cabe en los **11 huecos** de la fila

Paleta de 13 (Girar Izq ×4, Girar Der ×2, Avanzar 2 ×4, Avanzar ×2, Usar ×1): los 11 de la
solución más un Girar Derecha y un Avanzar 2 de sobra. Nivel y huecos están en
`Scenario1Difficulty`; los bloques de cada paleta llevan `DifficultyOnly`.

### Escenario 2 — Condicionales *(montado, sin probar en visor)*

Tres módulos averiados. Cada uno enuncia un problema en texto y ofrece varias acciones. Una
acción **correcta** alterna entre seleccionada y no seleccionada; una **incorrecta** se queda
marcada `wrongOptionSeconds` (0,8 s) y se suelta sola. El módulo se repara cuando están
marcadas todas las correctas.

| | Opciones | Correctas |
|---|---|---|
| Básica | 4 | 1 |
| Intermedia | 4 | 2 |

La dificultad vive en los **datos** (`ModuleData`), no en código, y `Scenario2Difficulty`
pone en cada módulo el de la dificultad de la sesión. Hay un asset por módulo y dificultad en `Scripts/Scenario 2/Modulos/`, nombrados
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
del narrador (sección 6) y el botón se suelta solo: el niño ya no la desmarca a mano (pedido
del 06/10/2026). La incorrecta **nunca entra en el conjunto seleccionado** —solo se dibuja
marcada—, así que el módulo puede repararse aunque su corrutina de soltado no haya terminado.
Mientras está marcada ignora otra pulsación.

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

| | `editable` | Fila | Qué hace el niño |
|---|---|---|---|
| Básica | ❌ | **Oculta** (09/10/2026) | Repeticiones + ficha de tipo, sin ver instrucciones. La fila trae `Recoger · Girar al destino · Soltar · Volver` y se ejecuta igual |
| Intermedia | ✅ | Visible | Repeticiones + ficha + **reordenar** `Recoger · Girar Izq · Soltar · Girar Der` |

**Fila oculta en básica.** `Scenario3Difficulty.Settings.showRow` → `SocketRow.SetVisible`: apaga los
*renderers* de la fila y de todo lo que cuelga de ella (huecos, bloques, marcadores), pero la
fila sigue activa, porque una fila apagada no crea sus huecos y no habría nada que ejecutar.
Va siempre con la fila bloqueada (si no, se podrían agarrar bloques invisibles; el informe de
comprobación lo marca como fallo). El rótulo INSTRUCCIONES y el fondo azul de esa columna
(`Escenario (3)/Canvas/Panel/titulo (1)` y `…/Panel/Panel`) llevan `DifficultyOnly` de
intermedia; REPETIR, TIPO y el contador se ven en las dos. Probado en Play: con la fila oculta,
la ficha Caja y 7 repeticiones, el brazo lleva las 7 cajas a su sitio.

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
más simples o más densas. El escenario no declara su dificultad: los huecos y fichas de una
sola dificultad llevan `DifficultyOnly`, y `Scenario4Controller` no cuenta los apagados.

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
(`ID_Texto, Texto_Narrativa`), hoy **`Assets/_Main/Narrativa.csv`**: 21 IDs (el `0` es la frase del tutorial) más 2 variantes
por dificultad, `9.1`/`9.2` (esc. 2) y `14.1`/`14.2` (esc. 3). El `20` es el tiempo agotado.

**`textId` es texto, no número**, precisamente por esas variantes: como entero, `9.1` no
existía y la línea se saltaba en silencio. Los audios se nombran con su ID
(`Sounds/VoiceLines/Victor/9.1.wav`), y el menú contextual del Narrator **"Asignar textId por
nombre del clip"** los rellena solos: escribirlos a mano ya dejó una vez toda la lista
desplazada a partir de la primera variante.

**Una lista por dificultad**, que elige `NarrationDifficulty` (en el `Narrator`): `1` =
`Victor (Basica)`, `2` = `Victor (Intermedia)`. La `0` (`Camila`) es la grabación anterior y
no se usa. Las dos listas tienen **20 entradas en el mismo orden** (la frase `0` del tutorial
y las 19 de la historia) y deben seguir así: el Director reproduce una por cada vez que un
paso espera al Narrator, así que una lista con una línea de más desplaza todas las
siguientes. El Director espera al Narrator 20 veces.

Degrada limpiamente: sin clip solo escribe, sin `Text` o sin CSV solo suena, `textId` vacío
significa sin texto. (El `0` también lo significaba hasta el 06/10/2026, resto de cuando el id
era un entero; se quitó porque `0` es ahora el id de la frase del tutorial y salía sin texto.) **Ni el CSV ni el Text son obligatorios.** Las frases de error
(`e1`–`e5.wav`) van en `errorLines` sin `textId`: no están en el CSV.

> Exportar el CSV como **"CSV UTF-8"**, o los acentos llegan rotos. El parser propio respeta
> comillas, comas internas y saltos de línea; un `Split(',')` partiría las frases.


**Frases de error.** Aparte de los tramos hay un banco `errorLines`, y `PlayErrorLine()` suelta
una al azar sin repetir la anterior. **No avanza el recorrido principal**: equivocarse no hace
progresar la historia, y si lo hiciera un niño que falla mucho se saltaría medio guion. Si ya
hay una línea sonando no la corta y no dice nada — pisar la narración para regañar perdería
justo la instrucción que quizá explicaba cómo acertar.

## 7. Telemetría

Un JSON por participante en `Application.persistentDataPath/{pin}_{sessionId}_{deviceId}.json`. Formato
cambiado el 06/10/2026; la tabla de equivalencias con el anterior está al final de
`Docs/VARIABLES_TELEMETRIA.md`.

### Jerarquía de registros

```
ScenarioRecord            started, completed, totalSeconds, startedUtc, endedUtc
├── Scenario1Record       + failedAttempts, blocksGrabbed, blocksConnected, blockResets,
│                           errorCollisionBot, errorInvalidUse, attempts[]  (AttemptRecord)
├── Scenario2Record       + selections[]  (cada una con durationSeconds)
├── Scenario3Record       + failedAttempts, blocksGrabbed, blocksConnected,
│                           errorInvalidCommand, attempts[]  (LoopAttemptRecord: + repetitions, itemType)
└── Scenario4Record       + chipsGrabbed, placements[]  (cada una con durationSeconds)

RunRecord (raíz) ── pin, deviceId, sessionId, difficulty, startedUtc, endedUtc, totalSeconds
                 └─ escenario1, escenario2, escenario3, escenario4
```

**Cada escenario tiene su clase y ninguna comparte base** más allá de `ScenarioRecord`. Los
escenarios 1 y 3 tuvieron una común, `BlockScenarioRecord`, y se quitó el 06/10/2026: cada
pedido para uno arrastraba al otro. Los tres campos que coinciden (`failedAttempts`,
`blocksGrabbed`, `blocksConnected`) están declarados en los dos, y `TelemetryManager` enruta
por tipo concreto (`is Scenario1Record` / `is Scenario3Record`). Para quitar o añadir un campo
a uno basta con tocar su clase y la rama que lo escribe.

`difficulty` es **1-based** (1 = básica, 2 = intermedia) y va **solo en la raíz**: es de toda
la sesión, no de cada intento. **Todas las banderas son booleanas** (`started`, `completed`,
`solved`, `correct`). Los tiempos se llaman `totalSeconds` (la sesión o un escenario) y
`durationSeconds` (una acción: preparar un intento, o el tiempo desde la selección o
colocación anterior).

**No hay contadores que repitan una lista.** `wrongSelections` y `wrongPlacements` se quitaron:
son las entradas con `correct: false`. Tampoco se cuentan las sueltas: `blocksConnected` suma
solo cuando el jugador **encaja** un bloque en un hueco (`BlockNode.AttachTo` con
`byPlayer`), no cuando `SocketRow` monta la fila por código. En el Escenario 4 no hay
contador de conexiones porque cada ficha encajada ya es una entrada de `placements[]`.

**Errores de lógica.** El destino lo decide primero el reto activo. En el Escenario 1,
`LogicErrorType.UsoInvalido` va a `errorInvalidUse` ("Usar" donde no hay nada) y cualquier
otro tipo a `errorCollisionBot` (salirse o chocar). En el 3 todo va a `errorInvalidCommand`.

**El Escenario 2 solo registra lo que el niño marca.** Las deselecciones no van al JSON —ni la
automática de una incorrecta ni la manual de una correcta— y por eso `SelectionRecord` no
tiene campo `selected`.

### Identificador del visor y nombre del archivo

`deviceId` son los **5 primeros caracteres** de `SystemInfo.deviceUniqueIdentifier`, en
mayúsculas (`TelemetryManager.GetDeviceId()`, estático; el largo es `DeviceIdLength`). En
Quest deriva del `ANDROID_ID`: sobrevive a reinicios, actualizaciones y borrado de datos de la
app. **Cambia con un restablecimiento de fábrica y con un APK firmado con otra clave** (Android
da un `ANDROID_ID` por clave de firma): compilar siempre con el mismo keystore. Si el sistema
no da identificador, se genera uno y se guarda en PlayerPrefs. Con 5 caracteres hexadecimales
hay un millón de códigos: de sobra para distinguir los visores del proyecto, pero conviene
apuntarlos al empezar y comprobar que no coinciden dos.

El archivo es `{pin}_{sessionId}_{deviceId}.json`. El `sessionId` hace que repetir un PIN en
el mismo visor no pise nada, y el código, que dos visores no se pisen en el servidor.

`totalSeconds` de la raíz se actualiza en cada escritura mientras la run está abierta y se
congela en `EndRun()`: un visor que se apaga sin cerrar deja el tiempo hasta el último guardado.

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
| Inicio / fin de escenario | Escenarios 2–4: el **primer paso** de su escenario en el Director llama a `StartScenario`, antes de la narración, porque el niño manipula mientras escucha. El controlador lo repite después; `StartChallenge` es idempotente y **gana la primera llamada**. Escenario 1: arranca con el temporizador. `totalSeconds` incluye por tanto la narración de la sala |
| Intento (secuencia + repeticiones + tipo) | `ProgramTrigger.OnPlayPressed`. El `itemType` lo aporta la `IRunPrecondition`, así que sin el socket de tipo en `preconditions` sale vacío |
| Piezas agarradas | `BlockNode.OnGrabbed`, **desde código**. Enruta a `blocksGrabbed` (esc. 1 y 3) o a `chipsGrabbed` (esc. 4) según el reto activo |
| Piezas conectadas | `BlockNode.AttachTo(socket, byPlayer: true)`, **desde código**. Solo esc. 1 y 3. Una vez por frame: el prefab cablea además `TryAtattch` en `onReleased` y una suelta puede pasar dos veces |
| Reinicio de bloques (esc. 1) | `BlockResetter.ResetBlocks`, **desde código**. `TelemetryManager` solo lo cuenta con el Escenario 1 activo |
| Error de lógica | `LevelManager.ValidMovementInGrid` (esc. 1 → `errorCollisionBot`), `Bot.Use` (esc. 1 → `errorInvalidUse`) · `RoboticArm.OnInvalidAction` y `ArmTypeSocket.CanRun` (esc. 3 → `errorInvalidCommand`) |
| Intentos fallidos (esc. 1) | `Scenario1Controller.OnAttemptFailed` — **cableado en escena** |
| Intentos fallidos (esc. 3) | `Scenario3Controller.HandleRunFinished` — **desde código** |
| Selecciones (esc. 2) | `Scenario2Controller`. Solo al **marcar**; ninguna deselección se registra |
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
`{pin}_{sessionId}_{deviceId}.json`, sin prefijo de fecha: el nombre ya es único.

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
| `TelemetryManager` | `StartChallenge(string)`, `CompleteChallenge(string)`, `EndRun()`, `Flush()`, `RegisterFailedAttempt()`, `RegisterBlockGrabbed()`, `RegisterBlockConnected()`, `RegisterBlockReset()`, `RegisterLogicError(int)`, `ReportIntegrity()`, `IncrementPin()`, `SetPin(int)`, `SetDifficulty(int)` (también la guarda como la de Setup) · desde código: `SetRunDifficulty(Difficulty)` (solo la run) · estáticos: `GetDeviceId()`, `GetPreparedDifficulty()` |
| `JSONUploader` | `UploadTelemetry()`, `UploadFile(string)` |
| `ProgramTrigger` | `OnPlayPressed()`, `AddPrecondition(IRunPrecondition)` (desde código) |
| `ProgramRunner` | `ResetRunner()` |
| `SocketRow` | `IncreaseRepetitions()`, `DecreaseRepetitions()`, `SetRepetitions(int)`, `Lock()`, `Unlock()`, `SetEditable(bool)`, `SetVisible(bool)`, `ClearRow(bool)`, `PlaceInitialBlocks()` · desde código, antes de `Awake`: `SetSocketsQuantity(int)`, `SetInitialBlocks(…)` |
| `BlockResetter` | `ResetBlocks()` (no mueve las piezas bloqueadas), `ReturnBlock(BlockNode)` |
| `LevelLoader` / `LevelManager` | `ReloadLevel()` · `ResetLevel()`, `CompleteLevel()` |
| `SystemModule` / `ModuleOptionButton` | `ToggleOption(int)`, `ResetModule()`, `Refresh()`, `SetData(ModuleData)` · `Press()` |
| `RoboticArm` | `ResetArm()`, `ReturnHeldToOrigin()` |
| `ScenarioNController` | `StartScenario()`, `ResetScenario()` |
| `ShapeChip` | `ApplyShape()`, `SetShape(Sprite)`, `ApplyShapeToChild()`, `DropInPlace(Vector3)` |
| `ShapeSocket` | `Refresh()`, `ResetSocket()`, `Eject()`, `ApplyShapeToChild()` |
| `Narrator` | `PlayAudio(int/string)`, `PlayErrorLine()`, `StopAudio()`, `Interrupt()`, `SetAudioListIndex(int)`, `ShowLineById(int/string)`, `CompleteInstantly()`, `Clear()` |
| `Timer` | `StartTimer()`, `Pause()`, `Continue()`, `Stop()`, `SetTimeLimit(int)` |
| `Fader` | `TriggerFadeIn()`, `TriggerFadeOut()` |
| `SessionSetup` | `SelectBasic()`, `SelectIntermediate()`, `SelectByIndex(int)`, `StartSession()` |
| `PinEntry` | `AppendDigit(int)`, `DeleteLast()`, `Clear()`, `UseNextPin()`, `SetPin(int)` |
| `SceneLoader` | `Load()`, `Load(string)` |
| `TimeUpSequence` | `Begin()` |
| `Director` | `Play()`, `Stop()` |
| `TutorialController` | Eventos: `OnProgramButtonPressed`, `OnOptionButtonPressed`, `OnBlockPlaced`, `OnChipPlaced`, `OnTutorialFinished`. Es acción de paso del Director |
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

**El Escenario 2 no registra deselecciones** (pedido 5 del 01/10/2026). Hasta el 06/10/2026 sí:
las incorrectas se desmarcaban a mano y eso se leía como autocorrección. Ahora se sueltan
solas, y lo único que quedaba —desmarcar a mano una correcta— no era un dato que el equipo
quisiera. Al quitarlo, el campo `selected` valía siempre `true` y se eliminó.

**`RoboticArm` fuerza el sentido de giro** en vez de tomar el camino más corto: con ciertos
`yaw`, "Girar Derecha" giraba visualmente a la izquierda.

**El intento fallido del Escenario 3 se registra ANTES del `retryDelay`, y desde código.** Si se
esperase primero, el Director puede desactivar la estación durante la espera y matar la
corrutina: el fallo se perdería. Y si se cablease por `UnityEvent`, olvidarlo dejaría
`failedAttempts` a 0 sin que nada avise. Solo el aviso visual (`OnAttemptFailed`) va diferido.

**"Skip Step" espera los `WaitUntil` frame a frame.** Casi todas las acciones de paso esperan
con un único `yield return new WaitUntil(...)` (los cuatro escenarios, los módulos del Esc. 2,
el tutorial, el Narrator). El Director miraba el salto entre dos `yield`, y Unity no le
devuelve el control hasta que la condición se cumple: hasta el 09/10/2026 el salto de los pasos
en serie se quedaba esperando a que se resolviera el reto. `Director.Pump` recorre la acción
como lo haría Unity (anidadas en el mismo frame, `WaitUntil` frame a frame) y corta al pedir
el salto. Sin salto, el comportamiento es el mismo.

**Una sola escena para las dos dificultades** (09/10/2026; antes, decisión del 08/09/2026).
Con dos escenas, todo lo común —tutorial, Director, retoques visuales— había que hacerlo dos
veces, y se desincronizaban: el tutorial quedó a medias en `Intermedio` y hacían falta tres
herramientas solo para copiar de una escena a otra. Lo que de verdad cambia entre
dificultades es poco y está declarado en cada reto (sección 5, "Dificultad").

**La dificultad se aplica antes de todo, y apagando, nunca encendiendo.** `DifficultyApplier`
tiene orden de ejecución -1000 porque `SocketRow` crea sus huecos en `Awake` y `LevelLoader`
carga en `Start`: aplicar después dejaría la fila con los huecos de la otra dificultad.
`DifficultyOnly` solo apaga lo de la otra; si además encendiera lo suyo, despertaría piezas
que la escena tiene apagadas a propósito hasta un paso del Director.

**La vista previa del editor es visibilidad, no `SetActive`.** `Ver como …` usa el ojo de la
jerarquía, que no se guarda en la escena. Con `SetActive`, guardar la escena tras mirar la
intermedia dejaría apagadas en la escena las piezas de básica.

**El Escenario 4 no declara su dificultad.** Tuvo un `matchMode` propio mientras el modo de
emparejamiento cambiaba de verdad el comportamiento. Al quedar una sola mecánica se habría
convertido en una etiqueta suelta que duplicaba la dificultad de la sesión, con el riesgo de
contradecirla. Dos indicadores que nadie reconcilia son peores que uno.

**El escenario 4 compara sprites por referencia, no ids por nombre.** Los recortes se pueden
renombrar desde el Sprite Editor; un `shapeId` de texto habría dejado de encajar en silencio.
El nombre solo se usa para la telemetría, y por eso hay que fijarlo antes de recoger datos.

**`Scenario4Record` tiene su propio `chipsGrabbed`.** Sus fichas se agarran igual que los
bloques (`ShapeChip` hereda de `BlockNode`), y `RegisterBlockGrabbed` enruta según el reto
activo. Hubo un tiempo en que ese método solo conocía los escenarios de bloques y **cada
agarre del Escenario 4 se descartaba en silencio**: al añadir un escenario, repasar todas las
ramas `is ScenarioNRecord` de `TelemetryManager`.

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

**`StartChallenge` es idempotente y gana la primera llamada.** Antes, una segunda llamada
reiniciaba la hora de inicio y el cronómetro. Con el reto abierto al acabar la narración, el
niño —que ya manipula mientras escucha— dejaba intentos registrados *antes* del inicio de su
escenario y agarres atribuidos a la sala anterior. Ahora el reto se abre al llegar y la
llamada posterior no hace nada. Queda pendiente dejar un único punto de apertura (sección 11).

**`ResetBlocks` no mueve lo que el juego bloqueó.** Una ficha acertada del Escenario 4 o un
bloque fijo de la fila del Escenario 3 no son agarrables, y devolverlos a su sitio dejaba el
hueco marcado como resuelto pero vacío: el escenario ya no se podía terminar. El reinicio
completo desbloquea antes (`Scenario4Controller.ResetScenario`).

**Encajar congela el Rigidbody; devolver restaura el que tenía.** Un Rigidbody dinámico ignora
a su padre, así que una ficha con física encajada en un hueco se caía de él al siguiente paso
de física. `BlockResetter` guarda en `Awake` si cada pieza era cinemática y con gravedad, y
lo restaura al devolverla.

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

## 11. Estado voluble — 09/10/2026

> Esta sección caduca. Todo lo anterior es estable.

**El 09/10/2026 las dos escenas de juego se unificaron en `Juego`** (sección 5, "Dificultad").
La migración la hizo `Tools → Codea → 3` en modo batch; las escenas de antes quedaron
respaldadas fuera del repositorio en `X:\Backup\codea_unificar_2026-10-09\`, con el log de
Unity. Compila; **sin probar en Play ni en el visor**.

**El 06/10/2026 cambiaron el código de telemetría, el botón del Escenario 2 y las escenas de juego** (cuarto del tutorial y retoques visuales del usuario en materiales y prefabs).
El APK de `Build/app.apk` es el del 30/09 a mediodía: **emite el formato viejo** y sus botones
del Escenario 2 se desmarcan a mano. Hay que recompilarlo antes de recoger datos.

**Cambios de telemetría del 06/10/2026 — compilan; sin probar en Play ni en el visor:**

| Dónde | Cambio |
|---|---|
| General | `deviceId` (5 caracteres) y `totalSeconds` en la raíz · archivo `{pin}_{sessionId}_{deviceId}.json` · todas las banderas `true`/`false` · `difficulty` solo en la raíz |
| Esc. 1 | `blockResets` · `errorCollisionBot` (salirse y chocar) y `errorInvalidUse` ("Usar" mal) en lugar de `errorInvalidCommand` · fuera `errorIncompleteSequence` · `blocksReleased` → `blocksConnected` |
| Esc. 2 | `durationSeconds` por selección · fuera `wrongSelections` y `selected` (no se registran deselecciones) · el botón incorrecto se suelta solo (`SystemModule.wrongOptionSeconds`) |
| Esc. 3 | Clase propia, ya sin base común con el Esc. 1. `blocksConnected`, sin `difficulty` por intento, y solo `errorInvalidCommand`. Los pedidos 7–10 siguen pendientes |
| Esc. 4 | `durationSeconds` por colocación · fuera `wrongPlacements` y `chipsReleased` |

Confirmado por el usuario el 06/10/2026: código del visor de 5 caracteres, nombre
`{pin}_{sessionId}_{deviceId}.json`, `errorInvalidUse` aparte, clases de los escenarios 1 y 3
separadas, y sin deselecciones en el Escenario 2. Queda **sin confirmar** que en el Escenario 4
se quitó `chipsReleased` sin sustituto (cada conexión ya está en `placements[]`).

Restos sin efecto en `Juego`: varios bloques tienen overrides de
`onGrabbed`/`onReleased` con `RegisterBlockGrabbed`/`RegisterBlockReleased`, pero sin tamaño
de lista, así que no llaman a nada. `RegisterBlockReleased` ya no existe.

**Escenas:** `Setup` (pantalla del supervisor) y `Juego` (las dos dificultades), activas en
Build Settings en ese orden. `Intermedio.unity` **sigue en disco pero fuera de Build
Settings**: borrarla cuando `Juego` esté probada. `Main 2.unity` se borró; quedan
`Tests.unity` y 8 respaldos en `_Recovery/`, que no son escenas activas.

**`Juego`, comprobado el 09/10/2026 tras la migración** (log de la herramienta y YAML):

| Qué | Básica | Intermedia |
|---|---|---|
| `DifficultyApplier` | en `Sesion` | igual |
| Narrador | lista 1 · CSV `Narrativa.csv` · texto de golpe · 20 entradas, la `0` delante | lista 2 · igual · 20 entradas |
| Director | 20 esperas al Narrator; la primera es el paso "Iniciar tutorial" | igual (es el mismo) |
| Tiempo agotado | `Timer.OnTimeUp → Begin`, frase 20, vuelta a `Setup` | igual |
| Esc. 1 | `reto.json` · 8 huecos · 8 bloques | `escenario1_intermedio.json` · 11 huecos · 13 bloques, en la colocación que tenían en `Intermedio` |
| Esc. 2 · módulos | `*_Basica` | `*_Intermedia` |
| Esc. 3 · fila | bloqueada: Recoger · Girar al destino · Soltar · Volver | editable y desordenada: Soltar · Girar Der · Recoger · Girar Izq |
| Esc. 3 · condiciones | el socket de tipo (`ArmTypeSocket`) | igual |
| Esc. 4 | `rejectSpeed` 4 · sonido de motores | igual · **mismas figuras que básica** |

| Subsistema | Estado |
|---|---|
| Escena `Setup` | ✅ 15 teclas cableadas, sin `TelemetryManager`. Carga `Juego` (`SessionSetup.gameSceneName`) |
| Dificultad en una escena | ✅ Probada en Play sin visor (herramienta de prueba, modo batch): las dos dificultades montan bien y el Director se recorre entero sin errores. Falta el visor |
| Escenario 1 | ✅ Verificado en visor (básica). Intermedia sin probar en visor |
| Escenario 2 | ✅ Probado en APK (`0101`, `0102`) |
| Escenario 3 | ✅ Probado en APK: `itemType` se registra, repeticiones a 0, pinza arreglada |
| Escenario 4 | ✅ Probado en APK: física, rechazo con empujón y sonido, reinicio que respeta las acertadas, motores |
| Narrativa | ✅ CSV y voces nuevas, dos listas de 20 frases (la `0` del tutorial delante), 5 frases de error |
| Telemetría | ✅ Un `TelemetryManager` por escena, PIN desde `Setup`, retos 2–4 abiertos al llegar |
| Repositorio | ✅ De `Build/` solo se versiona `app.apk` (Git LFS, ~160 MB) |
| Servidor | ✅ `~/Services/JSONServer` en la Pi |
| Temporizador | ✅ Tiempo agotado cableado. `alerts` vacíos (no hay frases de aviso grabadas) |
| HUD diegético (RI-02), reinicio supervisado (RF-08) | ❌ Sin implementar |

**Decidido el 09/10/2026:** los relojes se quedan; en el Esc. 3 de básica la fila se oculta y
funciona sola; "Sistema de motores" y "Agregar combustible".

**Decidido el 09/10/2026:** las dos dificultades en **una sola escena** (sustituye a lo
decidido el 08/09/2026, una escena por dificultad).

**Decidido el 15/09/2026:** los retos los abre **siempre el Director**.

**Decidido el 29/09/2026:** PIN y dificultad se eligen en una escena `Setup` propia, en el
visor, antes de pasárselo al niño.

**Decidido el 29/09/2026 (reunión):** texto de narración completo de golpe; 4 opciones en las
dos dificultades del Escenario 2 (1 correcta en básica, 2 en intermedia); repeticiones a 0 al
poner la ficha del Escenario 3; física y aviso de acierto en el Escenario 4.

**Decidido el 30/09/2026:** la ficha rechazada del Escenario 4 sale disparada desde el hueco
en vez de volver a su sitio, y el reinicio no mueve las fichas acertadas. El nivel intermedio
del Escenario 1 es simétrico: el camino de abajo es el distractor por longitud.

**Reparto de la narración en el Director** (el mismo en las dos dificultades, verificado el
30/09/2026). Si se añade o quita una frase del CSV, hay que rehacer esta tabla:

| Paso | Esperas al Narrator | Frases |
|---|---|---|
| Inicio · Iniciar tutorial (desde el 06/10/2026) | 1 | 0 |
| Esc. 1 · Narrar | 6 | 1–6 |
| Esc. 1 · Esperar a completar | 1 | 7 |
| Esc. 2 · Narrar | 2 | 8, 9.x |
| Esc. 2 · Teletransportar a panel 3 | 1 | 10 |
| Esc. 3 · Narrar | 4 | 11, 12, 13, 14.x |
| Esc. 3 · Esperar a completar | 1 | 15 |
| Esc. 4 · Narrar | 2 | 16, 17 |
| Esc. 4 · Esperar a completar | 1 | 18 |
| Esc. 4 · Completar reto | 1 | 19 |

**Pendiente, por orden de importancia:**

- **Los retos 2–4 se abren dos veces.** El primer paso de cada escenario del Director llama a
  `StartScenario` al llegar a la sala (lo añadió `Tools → Codea → 2`), y el paso "Esperar
  completar" lo vuelve a llamar al terminar la narración, porque el controlador es su acción
  de espera. Hoy es inofensivo: `StartChallenge` es idempotente y gana la primera llamada.
  Pero **debería haber un único punto de apertura**. Opciones: quitar la llamada del paso
  "Esperar completar" (y la de "Teletransportar a panel 1" del esc. 2), o que `Execute()` del
  controlador no abra el reto. En `Juego` hay 6 `StartScenario` en el
  Director; deberían quedar 3. Hacerlo antes de recoger datos reales para no depender de la
  idempotencia
- **Figuras del Escenario 4 en intermedia:** hoy usa las mismas 4 que básica
  (`circulo_cuadrado`, `cuadrado_rombo`, `cuadrado_trianguloinv`, `triangulo_circulo`), así
  que la dificultad no cambia en esa sala. Lo resuelve el pedido 11: huecos y fichas nuevos
  con `DifficultyOnly` Intermedia, y los actuales con `DifficultyOnly` Básica
- **Rótulo del bloque "Volver":** el texto visible dice "Regresar" y la telemetría "Volver". Desde
  que la fila de básica está oculta ya no lo ve nadie; solo importa al leer el JSON
- Probar la intermedia en el visor con la tabla "Prueba del APK" (sección 14), en especial el
  Escenario 1 (solución de 11 bloques) y el Escenario 3 (reordenar la fila)
- Verificar en visor: manos (agarrar y pulsar) y que los 4 botones del Escenario 2 no se
  solapan
- Escenario 3, visual (reunión del 29/09): encerrar los bloques, titular las instrucciones y
  hacer más intuitivo el socket de tipo. Para luz o sonido ya existen
  `ArmTypeSocket.OnTypePlaced` y `OnMissingRepetitions`
- Opcional: `solvedSound` del Escenario 4; mover "Iniciar temporizador" tras la frase 3, que
  es la que pide pulsarlo; `alerts` del temporizador; sprite roto en `ShapeChip.prefab`;
  salas activas desde el arranque

**Pedidos de cambio recibidos el 01/10/2026 (se tratan uno a uno, con plan corto antes de cada cambio):**

| # | Pedido | Tipo | Notas |
|---|---|---|---|
| 1 | Esc. 1 · Depurar logs | Código | Plan listo, ver abajo |
| 2 | Esc. 2 · Título "Sistema de motores" | Datos | ✅ **Hecho el 09/10/2026** en `Motores_Basica` y `Motores_Intermedia` |
| 3 | Esc. 2 · "Agregar combustible" | Datos | ✅ **Hecho el 09/10/2026** en los 5 assets, el plan de prueba, el JSON esperado y el de ejemplo. Las muestras reales de `Docs/JSON Samples` conservan "Agregar gasolina": es lo que se vio el 30/09 |
| 4 | Esc. 2 · El botón se desactiva solo tras un error | Código | ✅ **Hecho el 06/10/2026**: se suelta solo a los 0,8 s, no se bloquea. Falta verlo en el visor |
| 5 | Esc. 2 · No contar deselecciones en telemetría | Código + formato | ✅ **Hecho el 06/10/2026**: `Scenario2Controller` solo registra lo que se marca y `SelectionRecord` ya no tiene `selected`. Sección 9 y `VARIABLES_TELEMETRIA.md` actualizadas. Falta verlo en el visor |
| 6 | Esc. 2 · Reducir el tamaño de los paneles | Escena / prefabs | Hoy no se ve el estado corregido / sin corregir. Revisar en visor qué queda fuera de vista |
| 7 | Esc. 3 · Repeticiones a 0 al terminar cada ejecución | Código | `Scenario3Controller.HandleRunFinished` → `SocketRow.SetRepetitions(0)` |
| 8 | Esc. 3 · Quitar intentos fallidos de la telemetría | Código + formato | `failedAttempts` del esc. 3 (y su registro en `HandleRunFinished`). Desde el 06/10/2026 cada escenario tiene su clase: basta con quitarlo de `Scenario3Record` y de la rama del esc. 3 en `RegisterFailedAttempt`. Actualizar la guía de variables y el informe de integridad |
| 9 | Esc. 3 · `solved: true` al terminar barriles o cajas | Formato | ✅ Aclarado el 09/10/2026: se refería a que `solved` salía como 1/0. Todas las banderas son `true`/`false` desde el 06/10/2026 |
| 10 | Esc. 3 · Cantidad disponible por intento | Código + formato | Campo nuevo en `LoopAttemptRecord`: objetos de ese tipo que quedaban por clasificar al ejecutar. Actualizar ejemplo y guía |
| 11 | Esc. 4 · Intermedia con el alfabeto Yachay en glifos cuadrados | Recursos + escena | Hacen falta las imágenes. Sustituye el pendiente de las "figuras densas" |
| 12 | Tutorial · Botón, chip + socket | Diseño | 🟡 **En curso (06/10/2026)**, ver "Tutorial" abajo |

**Tutorial (pedido 12), estado al 09/10/2026:**

- El cuarto es el objeto raíz `Tutorial` (antes `Start`), con `Room` y `Desk` (ProBuilder) y
  las piezas como **hijas directas**. Es el mismo para las dos dificultades. **La
  experiencia empieza al terminar el tutorial**
- Es una instancia de `Prefabs/Tutorial.prefab`. El prefab servía para llevarlo de una escena
  a otra; con una sola escena ya no hace falta, pero se mantiene
- Cuatro interacciones, cada una con la pieza de su escenario:

  | # | Interacción | Pieza | Cuenta cuando |
  |---|---|---|---|
  | 1 | Botón del Escenario 1 | Copia del botón de ejecutar, **sin** su cable al `ProgramTrigger` | Se pulsa (`WhenSelect`) |
  | 2 | Botón del Escenario 2 | `Prefabs/Scenario2/Button` con `standalone` | Queda marcado (`OnSelected`) |
  | 3 | Bloque y hueco del Escenario 1 | Copia de un bloque "Avanzar" y un `socket` suelto | Encaja cualquier bloque (`Socket.OnOccupied`) |
  | 4 | Ficha y hueco del Escenario 4 | `ShapeSocket` y **una** `ShapeChip`, la que encaja | Encaja la ficha correcta |

- **`TutorialController`**, en la raíz, vigila las cuatro (suscrito desde código) y termina
  cuando están todas, en cualquier orden. No escribe telemetría
- **Arranque (plan del usuario, 06/10/2026):** `StartButton → Director.Play`. El primer paso
  del Director, **"Iniciar tutorial"** (escenario `Inicio`, en serie), apaga `StartButton`,
  enciende las piezas, espera al Narrator (frase `0`, `Sounds/VoiceLines/Victor/0.wav`) y
  después al `Tutorial` (`TutorialController`). Luego sigue "Teletransportar a Escenario 1".
  Montado a mano por el usuario. El botón de inicio es un objeto renombrado a `StartButton`
  dentro de la instancia del prefab
- **`Director.Play` ignora una segunda llamada** mientras el recorrido está en marcha
  (`Director.IsPlaying`). Antes lanzaba otro recorrido en paralelo: bastaba con pulsar dos
  veces el botón de inicio
- `TutorialController.startDirectorOnFinish` solo arranca el Director **si no está ya en
  marcha**. Con el plan actual nunca hace nada (el Director ya corre y está esperando al
  tutorial), así que da igual cómo esté; queda como respaldo por si se usa el tutorial sin
  botón de inicio
- **`BlockResetter` en la raíz `Tutorial`:** es a donde vuelve sola una ficha que se cae al
  suelo. Lista como "piezas" a todos los hijos directos (sala y mesa incluidas), pero solo se
  usa `ReturnBlock`. **No cablear `ResetBlocks` de ese componente a ningún botón**
- **`Scenario4Controller` con `challengeId` vacío: solo si hay una ficha que no encaja.** Su
  único papel en el tutorial es expulsarla, porque el rechazo vive en ese controlador y no en
  `ShapeSocket`. Con una sola ficha sobra, y la herramienta 4 lo quita; si se añade una ficha
  incorrecta, lo vuelve a poner. Mientras haya dos en una escena, nada debe buscarlo por tipo
  sin mirar el `challengeId` (`CodeaSceneTools.FindScenario4`)
- **`Tools → Codea → 4`** (en cualquier escena de juego): no mueve ni cambia de padre nada de
  lo que ya esté; crea solo las piezas que falten y deja el cuarto listo (resetter, sin
  controlador sobrante, sin cable al Director, `playOnStart` apagado)
- **La herramienta 4 compila pero no se ha ejecutado en su versión actual**; el tutorial no
  se ha probado en Play
- Si a `TutorialController` le falta una pieza, avisa con error y da esa interacción por
  hecha, para que el Director no se quede esperando para siempre
- La unificación (herramienta 3) ignoró los bloques del tutorial: no llevan `DifficultyOnly`
- Los agarres del tutorial ocurren sin reto abierto: no se cuentan, y `TelemetryManager`
  avisa una vez en consola de "agarres sin ningún reto activo". En el tutorial es lo esperado
- La frase del tutorial es una sola, id `0`, ya en el CSV y grabada
- **Botón de inicio** (confirmado el 09/10/2026): `Tutorial/StartButton`, rótulo INICIAR, encendido
  al cargar y único que arranca el Director. Sirve para que la partida no empiece hasta que el niño
  tenga el visor puesto. Las piezas del tutorial esperan apagadas hasta que se pulsa
- **La frase 3 no cuadra con el flujo:** dice «Cuando estés listo, presiona «Iniciar» y el tiempo
  comenzará a correr», pero INICIAR se pulsa antes del tutorial y el reloj arranca solo en el paso
  1.2, justo después de las frases 1–6. Cambiar el texto (y regrabar `3.wav`); propuesta: «Tienes 15
  minutos para completar la misión. ¡El tiempo empieza ahora!»

**Feedback completo del usuario, estado al 09/10/2026** (✅ hecho · 🟡 a medias · ❌ pendiente · 👓 solo se puede comprobar en el visor):

| Pedido | Estado |
|---|---|
| Esc. 1 · Dónde poner los bloques | ❌ La frase 6 dice "Ordena los bloques de la mesa" sin decir que van en la fila de huecos |
| Esc. 1 · Fila de huecos horizontal | ✅ En escena (0°). 👓 |
| Esc. 1 · Contar reinicios · unificar errores · quitar errorIncompleteSequence y la dificultad por intento | ✅ (`errorInvalidUse` aparte, confirmado el 06/10) |
| Esc. 1 · Panel de botones sobre la mesa | 👓 Probablemente resuelto con la fila horizontal |
| Esc. 1 · Que se entienda que el objetivo es abrir la puerta · indicaciones visuales | 🟡 Lo dicen las frases 4 y 7; no hay indicador visual |
| Esc. 1 · Depurar logs | ❌ Plan listo, abajo |
| Esc. 2 · Instrucciones ambiguas · simplificar preguntas | ❌ Enunciados y frases 9.1/9.2 |
| Esc. 2 · Tiempo por selección · quitar wrongSelections · duration · no contar deselecciones | ✅ |
| Esc. 2 · Luces de resuelto / sin resolver | ✅ Asignadas en los tres módulos. 👓 que se vean (pedido 6) |
| Esc. 2 · "Sistema de motores" · "Agregar combustible" | ✅ 09/10/2026 |
| Esc. 2 · El botón incorrecto se suelta solo | ✅ 👓 |
| Esc. 2 · Reducir paneles | ❌ Pregunta abierta (pedido 6) |
| Esc. 3 · Quitar instrucciones en básica · eliminar el panel | ✅ 09/10/2026: en básica la fila no se ve y funciona sola; el rótulo INSTRUCCIONES y su fondo solo salen en intermedia. 👓 |
| Esc. 3 · Centrado / posición inicial del jugador | ❌ 👓 (`tp (3)`) |
| Esc. 3 · Pedidos 7, 8 y 10 | ❌ |
| Esc. 3 · `solved` en true/false (pedido 9) | ✅ |
| Esc. 4 · Quitar wrongPlacements · duration | ✅ |
| Esc. 4 · Animación Tierra → Ecuador → Yachay Tech | ❌ Sin empezar; faltan recursos |
| Esc. 4 · Glifos Yachay en intermedia | ❌ Faltan las imágenes. El mecanismo ya está: `DifficultyOnly` en huecos y fichas |
| Tutorial · Botón · chip + socket | ✅ Montado. 👓 |
| General · Quitar temporizadores | Descartado el 09/10/2026: los relojes se quedan |
| General · Tiempo total · código de visor · 0/1 a false/true | ✅ |
| General · Subir antes de cerrar la app | ❌ Al cerrar solo se guarda en el visor. Subir en `OnApplicationQuit` no es fiable en Quest; lo viable es subir al arrancar los JSON que queden pendientes |
| General · Clic, respuesta de las interacciones, contraste del texto | 👓 |
| General · Sonido en cada botón e interacción | 🟡 Todos los botones y los huecos tienen sonido; falta `solvedSound` del Esc. 4. 👓 el resto |
| General · Reducir el tiempo de comprensión | ❌ Diseño |
| General · Demostración inicial | ✅ Es el tutorial |

**Plan del punto 1 (depurar logs del esc. 1), listo para aplicar:**

| Dónde | Hoy | Qué hacer |
|---|---|---|
| `LevelLoader.cs:196` y `LevelManager.cs:94` | Volcado del tablero entero al cargar y en cada reintento | Quitarlos, o dejarlos tras una casilla `Log Grid` desactivada por defecto |
| `LevelManager.cs:45` y `:55` | `print("Out of grid...")`, `print("Ilegal move...")` | Un aviso en español con prefijo: `[Escenario1] Choque en (x,y)` |
| `ProgramTrigger.cs:32` y `:41` | `"Programm is running"` / `"Programm has run"` | En español y con prefijo |
| `StartBlock.cs:11` | `print("Iniciando.")` | Quitar |
| Errores de configuración y `[Escenario 1] COMPLETADO` | — | No tocar: son las señales que usamos para verificar |

**Preguntas abiertas para esa sesión:**

- **Punto 12:** resuelto en parte: el tutorial es el cuarto `Tutorial` de la propia escena y no se registra en la telemetría. Queda: ¿lleva narración propia (frases nuevas en el CSV)?
- **Punto 6:** ¿qué es exactamente lo que no se ve: el icono, la luz o el texto "Sistema restablecido"? ¿Desde dónde mira el jugador?
- **Punto 11:** ¿hay ya imágenes del alfabeto Yachay en glifos cuadrados? ¿Cuántos huecos y fichas tendrá la intermedia?

**Informe del mes 2 (siguiente tarea, rama `informe-mes2`).** Pedido del usuario: `Docs/Informe-Mes2.docx`
con el **mismo formato** que `Docs/Informe-Mes1.docx` y los entregables:

- **2.1 Implementación de funcionalidades principales**: navegación, interacciones, elementos
  educativos y lógica de funcionamiento
- **2.2 Integración de recursos 3D e interacciones inmersivas**: recursos 3D, programación de
  interacciones, controles y ajustes para Meta Quest
- **2.3 Pruebas funcionales preliminares con usuarios clave**: errores, observaciones de uso,
  limitaciones, ajustes aplicados y priorizados

El contenido está en `Docs/Entregable-Mes2/Recopilacion_Producto2.md`; el GDD (`Docs/Codea2_GDD.docx`)
sirve para controles, retroalimentación y salas. Plan técnico, ya estudiado:

- **Usar el informe del mes 1 como plantilla**: un `.docx` es un zip. Desempaquetar con
  `System.IO.Compression` (PowerShell), editar `word/document.xml` con Node y volver a empaquetar.
  **En este equipo no hay Word, LibreOffice, pandoc ni Python**: no se puede pasar a PDF para
  mirarlo; comprobar que el XML está bien formado y leerlo convertido a HTML (`mammoth` de npm)
- `node Docs/Herramientas/esqueleto_docx.js <carpeta>/word/document.xml` lista el cuerpo. En el
  del mes 1 (207 elementos): **0–22 portada** ("Proyecto:", título del proyecto, "Producto 1:" y
  su nombre, "El presente informe contempla…", los tres entregables con su descripción en estilo
  `1361` con viñetas, lugar y fecha, salto de página, tabla de fecha de entrega / elaborado por /
  aprobado por); **23** salto de sección; **24–25** "Contenido" y el índice (`w:sdt` con campo TOC,
  estilos `1348`–`1350`); **26** salto de sección; **27 en adelante, el cuerpo**
- Estilos del cuerpo: `1173` título 1 (numerado solo: 1, 2, 3), `1174` título 2, `1175` título 3,
  `1339` pie de figura o tabla (con campo SEQ), `1361` párrafo de lista. Las tablas y las figuras
  (12 imágenes en `word/media`) se copian como modelo
- **Quitar los comentarios de revisión** del mes 1: marcas `commentRangeStart/End` y
  `commentReference` en el cuerpo, los cuatro `comments*.xml`, sus relaciones y sus entradas en
  `[Content_Types].xml`
- Conservar cabeceras (6, con logos) y pies (2). Poner `<w:updateFields w:val="true"/>` en
  `settings.xml` para que Word rehaga el índice al abrir
- Figuras de evidencia: capturas de cada sala renderizando desde Unity en modo batch (con
  gráficos, sin `-nographics`), o fotos del visor que dé el usuario

**Seguridad:** `UPLOAD_API_KEY` está en claro en `JSONUploader.cs` y el token del túnel en su
`docker-compose.yml`. Asumido: servidor privado y temporal.

## 12. Decisiones abiertas

| Qué | Por qué sigue abierto |
|---|---|
| `SessionResult` | Declarado pero sin campo en el JSON. Falta decidir qué dispara "abandonado" |
| Username vs PIN | El SRS pide username (RF-01), el código usa PIN. Divergencia **deliberada**: se decidió corregir el documento. El PIN lo teclea el supervisor en la escena `Setup` |
| Mecánica del brazo | Reescrita con ficha de argumento y dos destinos. Funciona en el APK, pero no se ha probado con niños. ¿Entienden los niños que la ficha es un parámetro y no una instrucción? |
| Escenario 4 | ¿Necesita panel-ejemplo introductorio? Depende de los beta testers |
| Tanteo con el contador de repeticiones | Cada pulsación de `+`/`−` del Escenario 3 no se registra; solo queda el valor final del intento. Añadirlo es una métrica nueva, no un arreglo: decisión pedagógica |
| Gestor de retos | Hoy el reto activo lo fija quien llame a `StartChallenge`, y eso lo hace el Director. Un gestor que cada escenario declare al activarse eliminaría el riesgo de atribución, pero no hace falta mientras el Director abra todos los pasos |

## 13. Documentación relacionada

| Archivo | Para quién |
|---|---|
| `Docs/VARIABLES_TELEMETRIA.md` | Equipo evaluador. Qué mide cada variable, en lenguaje llano |
| `Docs/ejemplo_run_telemetria.json` | Run completa de ejemplo, con el formato exacto que emite `JsonUtility`. Para el equipo evaluador y para validar el parser de análisis |
| `Docs/JSON Samples/` | Dos runs reales del APK del 30/09/2026 (básica), **convertidas el 06/10/2026 al formato nuevo** y renombradas (`0101_5_00000.json`, `0102_6_00000.json`). Secuencias, tiempos y fechas son los reales; `durationSeconds` de selecciones y colocaciones y el `totalSeconds` de la raíz salen de las fechas. **Estimados, porque el APK de entonces no los registraba:** `deviceId` (`00000`), `blocksConnected` (el mínimo posible: 8 en el esc. 1, 2 en el esc. 3) y `blockResets` (0). No sirven para validar esos tres campos |
| `Docs/Pruebas/` | Plan de prueba guionizado de la telemetría (`PLAN_PRUEBA_TELEMETRIA.md`) y el JSON exacto que debe producir (`esperado_prueba_basico.json`). Rehacerlo si cambia el código de telemetría o algún nivel |
| `Docs/Herramientas/` | Scripts de Node: `resolver_nivel.js` (caminos y bloques de un nivel del Escenario 1) `validar_json_telemetria.js` (comprueba un JSON contra `TelemetryData.cs`), `generar_gdd.js` (regenera el GDD), `recursos_escena.js` (qué recursos usa de verdad una escena) y `esqueleto_docx.js` (estructura de un `.docx`, para usar un informe como plantilla). Uso en su `LEEME.md` |
| `Docs/Codea2_GDD.docx` | Game Design Document, reescrito el 30/09/2026 en español con el diseño y el guion vigentes. Se genera con un script de Node (docx); si cambia el diseño, editar el documento directamente |
| `Docs/Informe-Mes1.docx` / `.pdf` | Informe técnico entregado, con el SRS (IEEE 830) |
| `Docs/Entregable-Mes2/Recopilacion_Producto2.md` | Material reunido el 09/10/2026 para el informe del mes 2 (2.1 funcionalidades, 2.2 recursos 3D e interacciones, 2.3 pruebas). Marca con ⟨pendiente⟩ lo que debe aportar el equipo |
| `Docs/Diagramas/` | Diagramas de flujo y funcionalidad, croquis |
| `Docs/Investigaciones/` | Respaldo académico: *worked examples*, manipulativos físico-digitales |
| `README.md` | Presentación institucional del repositorio |

## 14. Comprobaciones rápidas

### Prueba del APK

Antes de compilar: `Setup` primera en Build Settings, luego `Juego`, las dos marcadas. Con el visor
conectado, `adb logcat -s Unity` muestra los `Debug.Log` en directo; el JSON queda en
`/sdcard/Android/data/<paquete>/files/{pin}_{sessionId}_{deviceId}.json`.

| # | Paso | Debe pasar | Si falla, mirar |
|---|---|---|---|
| 1 | Abrir la app | Arranca en `Setup`, con el PIN siguiente ya propuesto | Orden de Build Settings |
| 2 | Teclear un PIN, borrar una cifra, "Siguiente" | La pantalla sigue cada tecla | Cableado de las teclas |
| 3 | Pulsar EMPEZAR sin dificultad | No arranca; el estado pide la dificultad | `SessionSetup.statusText` |
| 4 | Básica → EMPEZAR | Fundido y carga de `Juego`; en logcat, `[Dificultad] Escena en básica` | Build Settings, `DifficultyApplier` |
| 5 | Tutorial | Al pulsar el botón de inicio suena la frase `0` con su texto; tras pulsar los dos botones y encajar el bloque y la ficha, se pasa al Escenario 1 | Paso "Iniciar tutorial" del Director, `TutorialController` |
| 5b | Introducción | Frases 1–3 con voz y texto completo a la vez; el reloj arranca con «Iniciar» | Listas del Narrator |
| 6 | Esc. 1: fallar a propósito | Choca, frase de error, el nivel se rearma solo | `OnAttemptFailed` |
| 7 | Esc. 1: resolver | Frase 7, puerta abierta, traslado | Director |
| 8 | Esc. 2: opción incorrecta | Frase de error; el botón se suelta solo en menos de un segundo | `OnWrongOption`, `wrongOptionSeconds` |
| 9 | Esc. 2: resolver los 3 módulos | Luces verdes, frase 10 | `ModuleData` |
| 10 | Esc. 3: ejecutar sin ficha | No se mueve nada | `preconditions` |
| 11 | Esc. 3: poner ficha | Contador a 0; ejecutar con 0 no hace nada | `ArmTypeSocket` |
| 12 | Esc. 3: resolver en dos pasadas | Cada tipo a su lado, frase 15 | `SortingGoal` |
| 13 | Esc. 4: ficha incorrecta | Sale disparada con sonido de desconexión | `rejectSpeed`, `rejectLocalDirection` |
| 14 | Esc. 4: tirar una ficha al suelo | Vuelve sola a los 4 s | `fallenBelow`, collider del suelo |
| 15 | Esc. 4: 2 aciertos + Reiniciar | Las acertadas no se mueven ni se agarran | `IsInteractable` |
| 16 | Esc. 4: completar | Motores, frase 18, frase 19 | `completedSound`, Director |
| 17 | Fin | Vuelve sola a `Setup` con el PIN siguiente | `SceneLoader`, paso "Volver a Setup" |
| 18 | Segunda partida seguida (otro PIN) | Todo igual que la primera | `TelemetryManager` por escena |
| 19 | Tercera partida: dejar que se acabe el tiempo | Frase 20 y vuelta a `Setup` | `TimeUpSequence` |
| 20 | Sacar los JSON del visor | Un archivo por partida, `{pin}_{sessionId}_{deviceId}.json`, con el mismo `deviceId` en todos, 4 escenarios con datos e `itemType` en el esc. 3 | Informe de integridad en logcat |
| 21 | Repetir 1–17 con las manos, sin mandos | Agarrar y pulsar funcionan | Interactores de mano del rig |
| 22 | `Setup` → **Intermedia** → EMPEZAR | Carga `Juego`; en logcat, `[Dificultad] Escena en intermedia`; en el JSON, `difficulty: 2` | `DifficultyApplier` |
| 23 | Intermedia · frases 9.2 y 14.2 | Suenan las variantes de intermedia | `listIndex` 2 |
| 23b | Intermedia · Esc. 1 | En la mesa solo hay 13 bloques, en la colocación de intermedia; ninguno de básica | `DifficultyOnly` |
| 24 | Intermedia · Esc. 1 por abajo | No caben los bloques en los 11 huecos | Nivel `escenario1_intermedio.json` |
| 25 | Intermedia · Esc. 1 por arriba (11 bloques) | Roki llega de frente al botón y la puerta se abre | Solución en la sección 5 |
| 26 | Intermedia · Esc. 2 | Cada módulo pide 2 opciones | Módulos `*_Intermedia` |
| 27 | Intermedia · Esc. 3 | La fila llega desordenada y se puede reordenar; cada color necesita sus giros | `Scenario3Difficulty` |
| 28 | JSON: primer intento del esc. 3 | Su `timestamp` es posterior a `escenario3.startedUtc`; `blocksConnected` ≤ `blocksGrabbed` en esc. 1 y 3 | Retos abiertos al llegar |

### Compilar sin abrir Unity

`Assembly-CSharp-Editor.csproj` depende de `Assembly-CSharp.csproj`, así que compilar el de
editor comprueba los dos. **Trampa:** los `.csproj` los genera Unity y listan los archivos uno
a uno; un script creado fuera de Unity **no está** en ellos y `dotnet build` dice "correcto"
sin haberlo compilado. A 09/10/2026 faltan `Session/TimeUpSequence.cs`, `Story/TutorialController.cs`
y los siete de `Difficulty/` (en el de runtime, que además lista `Session/DifficultyScene.cs`,
ya movido), y `CodeaSceneTools.cs`, `CodeaSceneCheck.cs` y `CodeaPlayCheck.cs` (en el de editor): Unity sí los compila, pero el
`.csproj` no se regeneró. Hasta que lo haga (en Unity: *Edit → Preferences → External Tools → Regenerate
project files*), hay que añadirlos a mano a una copia, compilar y restaurar:

```powershell
$r = "X:\Projects\Unity\Codea-2"
Copy-Item "$r\Assembly-CSharp.csproj" "$env:TEMP\ac.bak"; Copy-Item "$r\Assembly-CSharp-Editor.csproj" "$env:TEMP\ace.bak"
try {
  $s = [IO.File]::ReadAllText("$r\Assembly-CSharp.csproj")
  $a = '<Compile Include="Assets\_Main\Scripts\Session\SceneLoader.cs" />'
  if (-not $s.Contains('TimeUpSequence.cs')) { $s = $s.Replace($a, $a + '<Compile Include="Assets\_Main\Scripts\Session\TimeUpSequence.cs" />') }
  if (-not $s.Contains('TutorialController.cs')) { $s = $s.Replace($a, $a + '<Compile Include="Assets\_Main\Scripts\Story\TutorialController.cs" />') }
  $s = $s.Replace('<Compile Include="Assets\_Main\Scripts\Session\DifficultyScene.cs" />', '')
  foreach ($f in Get-ChildItem "$r\Assets\_Main\Scripts\Difficulty\*.cs") { if (-not $s.Contains($f.Name)) { $s = $s.Replace($a, $a + "<Compile Include=`"Assets\_Main\Scripts\Difficulty\$($f.Name)`" />") } }
  [IO.File]::WriteAllText("$r\Assembly-CSharp.csproj", $s)
  $e = [IO.File]::ReadAllText("$r\Assembly-CSharp-Editor.csproj")
  $b = '<Compile Include="Assets\_Main\Editor\LevelEditorWindow.cs" />'
  foreach ($f in Get-ChildItem "$r\Assets\_Main\Editor\*.cs") { if (-not $e.Contains($f.Name)) { $e = $e.Replace($b, $b + "<Compile Include=`"Assets\_Main\Editor\$($f.Name)`" />") } }
  [IO.File]::WriteAllText("$r\Assembly-CSharp-Editor.csproj", $e)
  dotnet build "$r\Assembly-CSharp-Editor.csproj" -v:q --nologo | Select-String "error|Compilaci"
} finally {
  Copy-Item "$env:TEMP\ac.bak" "$r\Assembly-CSharp.csproj" -Force; Copy-Item "$env:TEMP\ace.bak" "$r\Assembly-CSharp-Editor.csproj" -Force
}
```

Cualquier script nuevo creado fuera de Unity necesita además su `.meta` (dos líneas:
`fileFormatVersion: 2` y `guid:` con 32 hexadecimales nuevos), para que su identificador sea el
mismo en todos los equipos.

**Otras trampas del entorno** (Windows, sin Python, pandoc ni LibreOffice; hay Node y
PowerShell): los `.cs` y `.md` son UTF-8 y muchos usan CRLF. `perl -pi` y `sed` **rompen los
acentos** si el patrón o el reemplazo llevan caracteres no ASCII; para esos cambios usar la
herramienta de edición, o un script de Perl con `use utf8; use open qw(:std :encoding(UTF-8));`.
Los assets de Unity escriben los acentos como `"\xED"` dentro de comillas dobles.

### Otras comprobaciones

```bash
# Caminos y bloques de un nivel del Escenario 1 (búsqueda exhaustiva)
node Docs/Herramientas/resolver_nivel.js Assets/_Main/Levels/escenario1_intermedio.json 11

# Los JSON de la documentación (o uno del visor) contra el modelo de TelemetryData.cs
node Docs/Herramientas/validar_json_telemetria.js

# Superficie pública (si este documento parece desfasado)
grep -rnE "^\s{4}public\s+(void|IEnumerator|bool|int|string|float)\s+\w+\s*\(" Assets/_Main/Scripts

# Qué llama a un método en la escena (ojo: los overrides de prefab usan 'value:' en vez de 'm_MethodName:')
grep -n "m_MethodName: X\|value: X" Assets/Scenes/Juego.unity
```

**Probar sin visor:** casi todo tiene `[ContextMenu]`. `ModuleOptionButton → Press`,
`RoboticArm → Probar/Ciclo completo`, `VRInteractEvents → Invoke Full Press`,
`TelemetryManager → Test/Simular y subir`, y el teclado W/A/D/Espacio del `Bot` en editor.

Los cuatro escenarios avisan al completarse:
`[Escenario N] COMPLETADO · challengeId '...'`. Si uno no aparece, ahí está el corte.
