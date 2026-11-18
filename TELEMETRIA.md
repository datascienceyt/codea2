# Telemetría de CODEA 2 · formato 1

Diccionario del JSON que sube el visor al terminar cada sesión, y respuestas a la revisión de la
sección 8 del documento «Métricas por escenario de CODEA2» (19/10/2026).

- **Formato:** 1, desde el 17/11/2026. La recolección válida empieza con él: un JSON sin
  `formatVersion` es del formato anterior.
- **Código:** `Assets/_Main/Scripts/Telemetry/TelemetryData.cs` (modelo) y `TelemetryManager.cs` (registro).
- **Validación:** `Tools → Codea → Validar un JSON de telemetría` (`Editor/CodeaTelemetryCheck.cs`)
  comprueba un archivo contra todas las reglas de la sección 5. La prueba en Play
  (`Tools → Codea → Probar las dos dificultades en Play`) la pasa sola al final de cada dificultad
  y deja una copia del JSON en `Logs/Codea_PruebaPlay_<dificultad>.json`.
- **Forma de los campos:** `node Docs/Herramientas/validar_json_telemetria.js` comprueba que los
  campos y su orden coinciden con el modelo.
- **Ejemplo:** `Docs/ejemplo_formato1.json`. Está construido a mano con horas coherentes: no es
  una sesión jugada.

**Sin cambios:** el contrato de subida sigue igual.

- `POST /upload` con la cabecera `X-API-Key`.
- Archivo `{pin}_{sessionId}_{deviceId}.json`.
- Una sesión a medias se vuelve a subir con el mismo nombre.

## 1. Reglas generales

| Regla | Cómo se cumple |
|---|---|
| Sin null | Todos los textos se inicializan a `""` y todas las listas a `[]` |
| Dato desconocido | `""` en texto y `-1` en números (`remaining`, `moved`, `solutionPercent`). Nunca un 0 inventado |
| Todos los campos siempre presentes | JsonUtility escribe cada campo declarado, tenga o no valor |
| Horas | UTC ISO 8601 con `Z` y 7 decimales: `2026-11-20T15:00:00.1234567Z` |
| Un solo reloj | Todas las horas y duraciones salen de `DateTime.UtcNow`, y las pausas se restan. Por eso `totalSeconds = endedUtc − challengeStartedUtc − pausedSeconds` se cumple por construcción |
| Banderas | Las que ya existían (`started`, `completed`, `timedOut`, `solved`, `correct`) siguen como `true`/`false`. Las nuevas del formato 1 (`selected`, `phaseCompleted`) son enteros 0/1, como pide el contrato. El dashboard lee las dos formas |
| Contadores derivados | `failedAttempts` (E1 y E2), `wrongSelections` y `wrongPlacements` se recalculan de sus listas en cada guardado, así que no pueden descuadrar con ellas |
| Datos personales | Ninguno. El PIN de 4 cifras es lo único que identifica al estudiante |

## 2. Diccionario

### 2.1 Raíz

| Campo | Tipo | Qué es | Cuándo se escribe |
|---|---|---|---|
| `formatVersion` | int | Versión del formato: **1** (`TelemetryManager.FormatVersion`) | Al abrir la run |
| `appVersion` | texto | `Application.version` del APK (hoy `1.0.0`) | Al abrir la run |
| `pin` | texto | PIN del estudiante, 4 cifras | Al abrir la run |
| `deviceId` | texto | Código del visor, 5 caracteres | Al abrir la run |
| `sessionId` | int | Contador de sesiones del visor | Al abrir la run |
| `difficulty` | int | 1 = básica, 2 = intermedia | Al abrir la run |
| `startedUtc` | hora | Apertura de la run (carga de la escena de juego) | Al abrir la run |
| `endedUtc` | hora | Cierre de la run. `""` mientras sigue abierta | Al cerrar |
| `endReason` | texto | Motivo del cierre (sección 3). `""` mientras sigue abierta | Al cerrar |
| `totalSeconds` | s | Tiempo de sesión sin pausas, desde `startedUtc` hasta `endedUtc` (o hasta el último guardado, con la run abierta) | En cada guardado |
| `escenario1` … `escenario4` | objeto | Un objeto por escenario, con los campos de las secciones 2.2 a 2.6 | Siempre presentes |

### 2.2 Campos comunes de cada escenario

| Campo | Tipo | Qué es | Cuándo se escribe |
|---|---|---|---|
| `started` | bool | Se entró en la sala | Al entrar en la sala |
| `completed` | bool | Se resolvió | Al resolverse |
| `timedOut` | bool | Se agotaron sus 5 minutos sin resolverla. Nunca a la vez que `completed` | Al agotarse |
| `endReason` | texto | Ver sección 3. `notStarted` hasta entrar en la sala; `""` mientras está abierta | Al entrar y al cerrar |
| `startedUtc` | hora | Entrada en la sala, con su narración | Al entrar en la sala |
| `challengeStartedUtc` | hora | Desde cuándo el reto acepta acciones. `""` si no llegó a abrirse | Al abrir el reto |
| `endedUtc` | hora | Cierre del escenario | Al cerrar |
| `totalSeconds` | s | Tiempo de reto sin pausas: `endedUtc − challengeStartedUtc − pausedSeconds`. Con el escenario abierto, lo jugado hasta el último guardado. 0 si el reto no llegó a abrirse | En cada guardado |
| `pausedSeconds` | s | Suma de `pauses`, recortadas al tramo del reto | En cada guardado |
| `pauses` | lista | `{startUtc, endUtc, reason}`. `reason` es `headsetRemoved`, `menu` o `focusLost`. `endUtc` es `""` solo en un guardado hecho a mitad de pausa | Al empezar y al acabar cada pausa |

Ventana de eventos: un escenario solo apunta eventos entre `challengeStartedUtc` y `endedUtc`
(sección 4.7).

### 2.3 Escenario 1 · Robot y puerta

| Campo | Tipo | Qué es | Cuándo se escribe |
|---|---|---|---|
| `failedAttempts` | int | Ejecuciones con `solved = false`. Incluye la que está en marcha hasta que se resuelve | Derivado, en cada guardado |
| `blocksGrabbed` | int | Bloques agarrados con el reto abierto | En cada agarre |
| `blocksConnected` | int | Bloques encajados por el jugador en un hueco | En cada encaje |
| `blockResets` | int | Pulsaciones del botón que devuelve los bloques a su sitio | En cada pulsación |
| `errorCollisionBot` | int | Pasos imposibles del robot: salirse del tablero o pisar algo que no es camino. Cuenta cada paso, aunque haya varios en una misma ejecución | En el paso que falla |
| `errorInvalidUse` | int | «Usar» donde no hay nada que usar | En el paso que falla |

Cada entrada de `attempts` es una pulsación de Ejecutar:

| Campo | Tipo | Qué es |
|---|---|---|
| `seq` | int | Orden del evento en la sesión (sección 4.1) |
| `sequence` | lista | Instrucciones de la fila, en orden: `Avanzar`, `Avanzar 2`, `Girar Izquierda`, `Girar Derecha`, `Usar` |
| `solved` | bool | La ejecución llevó al robot a la puerta |
| `failReason` | texto | Ver sección 3. `""` si `solved` |
| `solutionPercent` | % | Parecido con la solución (instrucciones y orden), de 0 a 100. -1 sin solución cargada |
| `durationSeconds` | s | Tiempo de preparación (sección 4.5) |
| `timestamp` | hora | Pulsación de Ejecutar |

### 2.4 Escenario 2 · Reparación de la nave

| Campo | Tipo | Qué es |
|---|---|---|
| `failedAttempts` | int | Probar que no repararon su módulo |
| `wrongSelections` | int | Selecciones con `selected = 1` y `correct = false` |
| `modulesRequired` | lista | Ids de los módulos de esta versión: `enfriamiento`, `generadores`, `motores` (las dos dificultades). Se escribe al entrar en la sala |

Cada entrada de `selections` es una pulsación de un botón de acción:

| Campo | Tipo | Qué es |
|---|---|---|
| `seq` | int | Orden del evento en la sesión |
| `module` | texto | Id del módulo |
| `option` | texto | Texto de la acción, p. ej. «Agregar gasolina» |
| `selected` | 0/1 | 1 = la marcó; 0 = la desmarcó (deselección) |
| `correct` | bool | Si la acción es de la solución. También se escribe en las deselecciones |
| `durationSeconds` | s | Desde la pulsación anterior de esta lista |
| `timestamp` | hora | Momento de la pulsación |

Cada entrada de `attempts` es una pulsación de Probar con algo marcado. Lleva `seq`, `module`,
`selection` (lo marcado, en el orden de los botones), `correctSelected`, `wrongSelected`,
`solved`, `solutionPercent` (aciertos frente a la unión de lo marcado y lo correcto),
`durationSeconds` (desde el Probar anterior) y `timestamp`.

### 2.5 Escenario 3 · Brazo robótico

| Campo | Tipo | Qué es |
|---|---|---|
| `blocksGrabbed` | int | Bloques y fichas de tipo agarrados con el reto abierto |
| `blocksConnected` | int | Bloques y fichas de tipo encajados por el jugador |
| `errorInvalidCommand` | int | Una de estas situaciones (sección 4.3): Recoger con la pinza llena o de una pila vacía, Soltar sin nada en la pinza o en el lado equivocado, o Ejecutar sin ficha de tipo, con 0 repeticiones o con huecos vacíos en la fila |
| `errorCollisionBot` | int | Lo pide el contrato, pero el brazo gira en su sitio y no puede chocar. **Siempre 0 en esta versión** |
| `itemsRequired` | lista | `{itemType, count}` por tipo. Se escribe al entrar en la sala |
| — | — | **Sin `failedAttempts`** (formato 1) |

Cada entrada de `attempts` es una pulsación de Ejecutar que llegó a ejecutarse:

| Campo | Tipo | Qué es |
|---|---|---|
| `seq` | int | Orden del evento en la sesión |
| `sequence` | lista | La fila, que es el cuerpo del bucle |
| `repetitions` | int | Repeticiones elegidas |
| `itemType` | texto | Ficha de tipo puesta: `Caja` o `Barril`. `""` si no había |
| `remaining` | int | Objetos de ese tipo que faltaban por trasladar **antes** de ejecutar. -1 sin tipo |
| `moved` | int | Objetos de ese tipo que esta ejecución dejó en su destino. -1 sin tipo. Se actualiza tras cada acción del brazo |
| `phaseCompleted` | 0/1 | 1 si quedaba alguno de ese tipo antes de ejecutar y ninguno después |
| `solved` | bool | true solo en la ejecución que completa el escenario |
| `solutionPercent` | % | Parecido con la solución de ese tipo. No mide repeticiones. -1 sin solución cargada |
| `durationSeconds` | s | Tiempo de preparación (sección 4.5) |
| `timestamp` | hora | Pulsación de Ejecutar |

Hoy, sin tipo no se puede ejecutar: `ArmTypeSocket` lo impide. Por eso `itemType` siempre viene
lleno; el `""` y el -1 quedan para el contrato.

**Tipos.** Se comprobó en las dos dificultades con la prueba en Play del 17/11/2026, y no cambian
con la dificultad:

| `itemType` | Color | Cantidad |
|---|---|---|
| `Caja` | azul o celeste | 7 |
| `Barril` | rojo | 3 |

Los rótulos de la sala dicen «DERECHA · cajas azules» e «IZQUIERDA · barriles rojos».

### 2.6 Escenario 4 · Chips y receptáculos

| Campo | Tipo | Qué es |
|---|---|---|
| `chipsGrabbed` | int | Fichas agarradas con el reto abierto |
| `chipsReleased` | int | Fichas soltadas cuyo agarre se contó. Nunca mayor que `chipsGrabbed` |
| `wrongPlacements` | int | Colocaciones con `correct = false` |
| `socketsRequired` | lista | Ids de los huecos de esta versión: 4 en básica y 6 en intermedia. Se escribe al entrar y al abrir el reto |

Cada entrada de `placements` es una ficha encajada en un hueco. Lleva `seq`, `socket`, `chip`,
`correct`, `durationSeconds` (desde la colocación anterior) y `timestamp`.

## 3. Valores posibles

**`endReason` de la raíz**

| Valor | Cuándo |
|---|---|
| `completed` | La sesión terminó como estaba previsto: final de la historia (paso «Completar reto y cambiar escenario» → `TelemetryManager.EndRun`) o 15 minutos de sesión agotados (`TimeUpSequence` → `EndRun`) |
| `quit` | La run se cerró antes de su final dentro de la app: cambio de PIN (`SetPin`), vuelta a Setup a mitad de partida (`SceneLoader.Load` → `QuitRun`) o escena descargada (`OnDestroy`) |
| `appClosed` | La app se cerró (`OnApplicationQuit`), o se pausó y ya no volvió (sección 4.8) |
| `""` | La run sigue abierta: la app se cayó o el archivo se subió a mitad de partida |

**`endReason` de cada escenario**

| Valor | Cuándo |
|---|---|
| `notStarted` | No se llegó a entrar en la sala |
| `completed` | Se resolvió |
| `timedOut` | Se agotaron sus 5 minutos de reto |
| `abandoned` | Se cerró sin resolver ni agotar: se entró en otra sala con esta abierta, o la run se cerró (`completed` o `quit`) con la sala abierta. Por ejemplo, la sala en curso cuando se agotan los 15 minutos de sesión |
| `appClosed` | La app se cerró, o se pausó y no volvió, con la sala abierta |
| `""` | Sin cierre confirmado: la app se cayó y no escribió nada más |

**`failReason` del escenario 1**

| Valor | Cuándo |
|---|---|
| `""` | La ejecución resolvió el nivel |
| `collision` | El primer error de la ejecución fue un choque o una salida del tablero |
| `invalidUse` | El primer error de la ejecución fue un «Usar» sin nada delante |
| `incompleteSequence` | La ejecución terminó sin errores y sin llegar a la puerta |
| `other` | La ejecución no llegó a terminar: tiempo agotado sin esperarla, o app cerrada durante ella. Es también el valor provisional mientras el robot se mueve |

Una ejecución lleva un único `failReason`. Si hubo varios errores, el primero es el que la hizo
fallar; los demás siguen sumando en `errorCollisionBot` y `errorInvalidUse`.

## 4. Revisión de la sección 8 del PDF

### 4.1 Qué crea cada entrada

Cada entrada se crea con un único método de `TelemetryManager`, y ese método tiene un único
llamador. Ningún otro camino crea entradas.

| Lista | Acción del niño | Camino en el código |
|---|---|---|
| E1 `attempts` | Pulsar Ejecutar y que el programa arranque | `ProgramTrigger.OnPlayPressed` → `RegisterBlockAttempt`. No crean entrada: una pulsación con el programa en marcha, otra sin reiniciar, otra en los 0,75 s tras el reinicio, una con el reto cerrado ni las del tutorial |
| E2 `selections` | Pulsar un botón de acción, tanto al marcar como al desmarcar | `SystemModule.ToggleOption` → evento `OnOptionToggled` → `Scenario2Controller.HandleOptionToggled` → `RegisterSelection`. El desmarcado automático tras un Probar fallido (`SystemModule.ClearSelection`) y `ResetModule` no lanzan el evento. Con el módulo reparado, agotado o desmarcándose, el botón no hace nada |
| E2 `attempts` | Pulsar Probar con algo marcado | `SystemModule.Try` → `OnTried` → `Scenario2Controller.HandleTried` → `RegisterModuleAttempt`. Probar sin nada marcado sale antes (desde el 22/10/2026) |
| E3 `attempts` | Pulsar Ejecutar y que el programa arranque | `ProgramTrigger.OnPlayPressed` → `RegisterBlockAttempt`. Sin ficha, con 0 repeticiones o con huecos vacíos no se ejecuta: suma a `errorInvalidCommand` y no crea entrada |
| E4 `placements` | Encajar una ficha en un hueco | `ShapeSocket.OnChipEvaluated` → `Scenario4Controller.HandleChipEvaluated` → `RegisterPlacement`. La misma ficha en el mismo hueco antes de 0,5 s es el mismo encaje dos veces y se descarta |

Cada evento lleva su `seq`: un entero de la run (`TelemetryManager.NextSeq`) que empieza en 1,
sube de uno en uno y no se repite entre listas. El orden de `seq` es el orden real de las acciones.

### 4.2 Qué significa `solved`

| Escenario | Significado |
|---|---|
| E1 | La ejecución llevó al robot a la puerta. El robot llama a `LevelManager.CompleteLevel`, `Scenario1Controller` llama a `CompleteChallenge` y este marca la última ejecución. La ejecución se registra antes de correr, con `solved = false`, porque su corrutina muere cuando el Director apaga la estación |
| E2 (`attempts`) | Lo marcado era exactamente la solución del módulo: ni de menos ni de más |
| E3 | Solo la ejecución que completa el escenario, es decir, la que deja los dos tipos en su destino (`Scenario3Controller.CheckCompletion` → `CompleteChallenge`). Una ejecución que termina una fase sin cerrar el escenario lleva `solved = false` y `phaseCompleted = 1`. La óptima son dos ejecuciones, una por tipo, cada una con `phaseCompleted = 1` y `repetitions = remaining` (7 y 3); la última de ellas lleva `solved = true` |
| E4 | No hay `solved`. Cada colocación lleva `correct` |

### 4.3 Qué incrementa cada contador

| Contador | Qué lo incrementa y cuándo |
|---|---|
| E1 `failedAttempts` | No se incrementa: se recalcula en cada guardado (`TelemetryManager.RefreshDerived`) como el número de ejecuciones con `solved = false`. Hasta el formato anterior lo sumaba el evento `OnAttemptFailed`, que llega 1,5 s después de terminar la ejecución; si en ese margen se agotaba la sala, el fallo se perdía (anomalía de la sección 6) |
| E2 `failedAttempts` | Igual: los `attempts` con `solved = false` |
| E1 `errorCollisionBot` | `LevelManager.ValidMovementInGrid`, en cada paso del robot que saldría del tablero o pisaría algo que no es camino |
| E1 `errorInvalidUse` | `Bot.Use`, cuando no hay nada delante que se pueda usar |
| E3 `errorInvalidCommand` | Dos caminos. En `RoboticArm`, durante la ejecución: Recoger con la pinza llena, sin ficha de tipo o de una pila vacía, y Soltar sin nada en la pinza o en el lado equivocado. En `ArmTypeSocket.CanRun` y `ProgramTrigger.OnPlayPressed`, al pulsar Ejecutar: sin ficha, con 0 repeticiones o con huecos vacíos |
| E3 `errorCollisionBot` | Nada. Vale 0 |

Todos estos contadores solo suben con su reto abierto.

### 4.4 Cronómetro de cada escenario

| Escenario | Entrada en la sala (`startedUtc`) | Se abre el reto (`challengeStartedUtc`) |
|---|---|---|
| E1 | Empieza el capítulo «Escenario 1» del Director (`Director.ScenarioStarted`), antes de la narración | Paso «Iniciar temporizador», tras la narración. Los bloques se desbloquean en ese mismo instante |
| E2 | Paso «Narrar escenario 2» → `Scenario2Controller.StartScenario` | El mismo instante: los botones de los módulos responden desde que se enciende la sala |
| E3 | Paso «Narrar escenario 3» → `Scenario3Controller.StartScenario` | `Scenario3Controller.Execute`, al terminar la narración, que es cuando se sueltan las piezas |
| E4 | Paso «Narrar escenario 4» → `Scenario4Controller.StartScenario` | `Scenario4Controller.Execute`, al terminar la narración, que es cuando se sueltan las fichas |

**Qué lo pausa.** Las pausas las detecta `TelemetryManager`. Varias fuentes que llegan a la vez
son una sola pausa, con el motivo más concreto.

| Motivo | Fuente |
|---|---|
| `headsetRemoved` | `OnApplicationPause(true)` (en Quest, quitarse el visor) u `OVRManager.HMDUnmounted` |
| `menu` | `OVRManager.InputFocusLost`: el menú del sistema u otro diálogo del sistema encima de la app. El juego no tiene menú propio |
| `focusLost` | `OnApplicationFocus(false)` sin ninguna de las anteriores |

En el editor no se registran pausas: la ventana del Meta XR Simulator le quita el foco a Unity
cada vez que se usa. Se pueden activar con el campo `Detect Pauses In Editor`.

**Cuándo se detiene.** Al resolverse, al agotarse el tiempo o al abandonarse (sección 3).

**El límite de 5 minutos.** Lo lleva `ScenarioTimeLimit` y lee el reloj de la telemetría
(`TelemetryManager.ChallengeSeconds`). Por eso el límite y `totalSeconds` miden lo mismo: tiempo
real desde que se abre el reto, sin pausas.

**Fórmula.** `totalSeconds = endedUtc − challengeStartedUtc − pausedSeconds` se cumple siempre,
porque las dos partes salen del mismo reloj. La validación del 17/11/2026 dio una diferencia
menor de 1 ms.

**Cambios respecto al formato anterior:**

- **La narración ya no gasta tiempo en E3 y E4.** El reto se abría al empezar la narración; ahora
  se abre al terminarla. Eso vale para el límite de 5 minutos de la sala y para el reloj de
  15 minutos de la sesión, que también arranca o se reanuda con el reto. En E2 no cambia nada.
- **Una sala agotada ya no da 300 s justos.** Da los 300 s del límite más lo que se esperó a
  que terminara la ejecución en curso (como mucho, 90 s). Antes se apuntaban 300 s aunque el
  reloj siguiera corriendo, y eso no cuadraba con `endedUtc`.

### 4.5 Qué mide `durationSeconds`

Es siempre tiempo de reto sin pausas, contado desde el inicio de un ciclo, y nunca empieza antes
de `challengeStartedUtc` (`TelemetryManager.Duration`). El ciclo cae dentro del tramo del reto y
se mide con el mismo reloj que `totalSeconds`, así que **no puede superar al `totalSeconds` de
su escenario**.

| Lista | Desde |
|---|---|
| E1 y E3 `attempts` | Lo último que haya pasado: apertura del reto, la pulsación de Ejecutar anterior o el reinicio tras una ejecución fallida (`RegisterFailedAttempt`). No incluye lo que dura la ejecución |
| E2 `selections` | La pulsación anterior de un botón de acción, de cualquier módulo, o la apertura del reto |
| E2 `attempts` | El Probar anterior, de cualquier módulo, o la apertura del reto |
| E4 `placements` | La colocación anterior o la apertura del reto |

### 4.6 Los contadores no se mezclan entre escenarios

Cada escenario tiene su propio objeto en el JSON y su propio estado en memoria
(`TelemetryManager.ScenarioState`): ciclos, pausas y ejecución en curso.

- **Agarres, encajes, reinicios y errores** van al reto abierto. Si no hay ninguno (tutorial,
  narración, traslados entre salas), no van a ningún escenario. Antes iban al último reto abierto
  aunque ya estuviera cerrado.
- **Entrar en una sala con otra abierta** cierra la anterior como `abandoned`.
- **Un reto cerrado** no se vuelve a abrir.

### 4.7 Ningún evento fuera del tramo del reto

`TelemetryManager.TryGetOpen` solo deja apuntar un evento con su reto abierto, entre
`challengeStartedUtc` y `endedUtc`. Lo que llega fuera no se registra, y la consola lo avisa una
vez.

El juego tampoco acepta acciones fuera de ese tramo:

- **Ejecutar (E1 y E3).** `ProgramTrigger` no ejecuta con el reto cerrado.
- **Piezas de E1, E3 y E4.** Están bloqueadas hasta que se abre el reto.
- **Tutorial.** Su botón Ejecutar no se controla ni se registra.

La hora de cada evento sale del mismo reloj que las del escenario, así que no puede caer antes
de que se abra el reto.

### 4.8 Cierre de la app, reposo y sesiones a medias

| Situación | Qué queda en el archivo |
|---|---|
| Final de la historia o sesión agotada | `endReason: completed`. La sala abierta queda `abandoned`, y la que se agotó, `timedOut` |
| Vuelta a Setup, cambio de PIN o escena descargada | `endReason: quit`. Las salas abiertas quedan `abandoned` |
| La app se cierra (`OnApplicationQuit`) | `endReason: appClosed` y `endedUtc`. La sala abierta queda `appClosed` |
| El visor entra en reposo o se quita (`OnApplicationPause(true)`) | Antes de abrir la pausa se escribe una copia como si la run se cerrara en ese instante: raíz y sala abierta `appClosed`, `endedUtc` del momento y tiempos hasta entonces. En memoria la run sigue abierta. Si la app vuelve, se cierra la pausa y el siguiente guardado sobrescribe la copia. Si el sistema la mata en reposo, que es lo habitual en Quest tras un rato, el archivo ya dice cómo y cuándo terminó |
| La app se cae | Queda el último guardado: run y sala con `endReason: ""` y `endedUtc: ""`. El dashboard lo trata como «sin cierre confirmado». La ejecución de E1 en marcha lleva `failReason: other` |

**Guardado parcial.** Se escribe al instante con cada evento de lista y cada cambio de escenario.
Agarres, errores y progreso del brazo se escriben como mucho cada 2 s.

**Subida.** Un archivo sin subir se queda en la cola de `TelemetryOutbox` y se vuelve a subir con
su mismo nombre.

## 5. Reglas que se cumplen siempre

Las comprueba `CodeaTelemetryCheck.Check`.

- `formatVersion = 1`, `appVersion` lleno y `endReason` con un valor de la sección 3.
- Sin null y con todos los campos presentes.
- `seq` mayor que 0, único en la sesión y creciente dentro de cada lista.
- `|totalSeconds − (endedUtc − challengeStartedUtc − pausedSeconds)| ≤ 0,1 s`.
- `pausedSeconds` = suma de `pauses`.
- Ningún `timestamp` antes de `challengeStartedUtc` ni después de `endedUtc`.
- Ningún `durationSeconds` mayor que el `totalSeconds` de su escenario.
- E1:
  - `failedAttempts` = ejecuciones con `solved = false`;
  - `failReason = ""` si y solo si `solved`;
  - una sola ejecución resuelta como mucho.
- E2:
  - `wrongSelections` = selecciones con `selected = 1` y `correct = false`;
  - `failedAttempts` = Probar sin reparar;
  - ningún Probar sin nada marcado;
  - `modulesRequired` lleno si la sala empezó.
- E3:
  - sin `failedAttempts`;
  - `itemsRequired` lleno;
  - `0 ≤ moved ≤ remaining`;
  - `phaseCompleted = 1` solo si `moved = remaining`;
  - si se completó, la última ejecución lleva `solved`.
- E4:
  - `wrongPlacements` = colocaciones con `correct = false`;
  - `chipsReleased ≤ chipsGrabbed`;
  - `socketsRequired` lleno si la sala empezó.

## 6. Anomalías vistas en los JSON reales

| Anomalía | Causa | Estado en el formato 1 |
|---|---|---|
| Ejecuciones con hora anterior al inicio de su escenario (hasta 1,77 s en E4 y 0,022 s en E3). Un Ejecutar de noviembre llegó antes de abrirse el reto | El botón Ejecutar del tutorial lleva el id `escenario1` y apuntaba sus pulsaciones en E1. Además, los registros no comprobaban que el reto estuviera abierto, y horas e inicio salían de relojes distintos (`DateTime` y `Time.time`) | **Corregido**: ventana de eventos (4.7), tutorial excluido y un solo reloj. El validador lo comprueba |
| E3 con un `durationSeconds` de 56,51 s y un `totalSeconds` de 29,61 s | El ciclo de preparación era uno solo para toda la run y podía venir de antes de abrirse el reto. Además, `totalSeconds` se medía con otro reloj | **Corregido**: ciclos por escenario, recortados al inicio del reto y con el mismo reloj (4.5) |
| E1 con `failedAttempts = 1` y dos ejecuciones `solved = 0` | El fallo lo sumaba `OnAttemptFailed` 1,5 s después de terminar la ejecución. Si en ese margen se cerraba la sala, o la última ejecución seguía en marcha al agotarse el tiempo, no se contaba | **Corregido**: se recalcula de `attempts` (4.3) |
| `blocksReleased` mayor que `blocksGrabbed`, con agarres atribuidos a otro escenario | Las manipulaciones iban al último reto abierto aunque ya estuviera cerrado. `blocksReleased` ya no existía en el formato anterior | **Corregido**: solo cuentan con el reto abierto. El nuevo `chipsReleased` solo cuenta fichas cuyo agarre se contó |
| Dos colocaciones idénticas con menos de 0,5 s de diferencia | El hueco avisaba dos veces del mismo encaje | Corregido el 22/10/2026 en `Scenario4Controller` (filtro de 0,5 s). Además, el formato 1 da a cada entrada su `seq` |
| Pulsaciones de Probar en E2 sin nada marcado | Segunda pulsación justo después del desmarcado automático | Corregido el 22/10/2026 en `SystemModule.Try`. El validador lo comprueba |
| `solutionPercent` del E1 tomó «Avanzar» como «Avanzar 2» (22/10/2026) | No se reproduce desde el código: la etiqueta sale de la acción del bloque (`GridBlock.InstructionLabel`) y la comparación es exacta (`SolutionScore`). Solo podría pasar con un bloque cuya pieza se vea como «Avanzar» pero tenga la acción `MoveForwardTwice`, o al revés | **Sin confirmar**: revisarlo en un JSON real del formato 1, mirando la `sequence` frente a lo que montó el niño |

## 7. Pendiente

- **Un JSON real por dificultad, jugado completo en el visor.** Hay que copiarlo a
  `Codea2Server/JSON Samples/` con su nombre normal y validarlo antes con
  `Tools → Codea → Validar un JSON de telemetría`. Los de la prueba en Play
  (`Logs/Codea_PruebaPlay_*.json`) son válidos, pero salen de saltar pasos del Director: tienen
  pocos eventos y E4 queda `completed` sin colocar fichas, porque el paso «Completar reto y
  cambiar escenario» lo marca al saltarse la espera.
- **Recompilar el APK.** El formato 1 no está en `Build/app.apk`.
