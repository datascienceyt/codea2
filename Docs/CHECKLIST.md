# Checklist de proyecto — Codea VR 2

> Estado verificado contra el código el **25/08/2026**. Complementa a `ARQUITECTURA.md`,
> que explica *cómo* funciona cada pieza; esto es *qué falta*.
>
> Deadline del proyecto: **30 de septiembre de 2026**.

---

> **Actualizado el 02/09/2026.** Lo de abajo por secciones sigue siendo válido salvo lo que
> corrige este bloque.

## 0. Estado real — lo primero que hay que hacer

### 🟠 Verificar en la próxima prueba

> El Escenario 1 está **verificado en visor y funcionando**. `OnPlayPressed` se cablea dentro
> de `Button.prefab`, no como override de escena: no lo busques en `Main.unity`.

- [ ] `StartChallenge` solo aparece 2 veces en el Director y hay 3 escenarios. Sin él, el
      escenario se completa igual pero queda `started: false` y `totalSeconds: 0` en el JSON
- [ ] `Scenario3Controller.OnScenarioFinished` está vacío; el del 2 sí tiene `CompleteChallenge`
- [ ] Escenario 3: `slotToClear` debe ser el montón **de origen**. Si apunta al de destino,
      se completa con la primera acción
- [ ] Bloques del Escenario 3: asignar su **`Interaction Root`**, o la fila bloqueada se podrá
      vaciar igualmente

### Cambios de diseño ya aplicados

- El **reinicio es automático** al fallar (`OnAttemptFailed`). El botón de reiniciar queda
  libre para `BlockResetter`
- **`RepeatBlock` eliminado**: ahora es el `SocketRow` el que se repite, con `editable` e
  `initialBlocks` para las dos dificultades
- **Escenario 4 completo en código** (`ShapeData`, `ShapeChip`, `ShapeSocket`, controlador)
- **`ScreenNarrator`**: narración escrita desde CSV, en paralelo con la voz
- Telemetría: `failedAttempts`, `durationSeconds`, sin `pin` por intento

---

## 1. Estado del código

### Sistema de bloques — completo

- [x] `BlockNode`: agarre, snap contra sockets, revalidación al soltar
- [x] `Socket` / `SocketRow`: generación por `Lerp`, encadenado, orientación configurable
- [x] Registro estático `Socket.Active` (sin `FindObjectsByType` por frame)
- [x] `ProgramRunner`: recorrido lineal + `ExecuteChain` reutilizable con tope de anidamiento
- [x] `ProgramTrigger`: ejecución y registro del intento
- [x] `BlockResetter`: los bloques vuelven a su sitio sin instanciar ni destruir
- [x] Guardia anti auto-anidamiento (un bloque no puede acoplarse dentro de sí mismo)

### Escenario 1 — Secuencialidad — completo

- [x] `Bot`: avanzar, girar izq/der, usar. Movimiento relativo
- [x] Depuración por teclado, solo en editor
- [x] 5 acciones: Avanzar, Avanzar 2, Girar Izq, Girar Der, Usar
- [x] `LevelManager` / `LevelLoader` / `Exit`
- [x] Editor visual de niveles (`Tools → Level Editor`)
- [x] `Scenario1Controller` integrado con Director y telemetría
- [x] Ciclo fallar → reiniciar → resolver funcionando

### Escenario 2 — Condicionales — código listo, sin montar

- [x] `ModuleData` (ScriptableObject): problema, opciones, respuesta correcta
- [x] `SystemModule`: pantalla, validación, estados AVERIADO/REPARADO
- [x] `ModuleOptionButton`: expone `Press()`, sin acoplarse a Oculus.Interaction
- [x] `Scenario2Controller`: cierra al reparar todos los módulos
- [x] Telemetría de selecciones (aciertos y fallos)
- [ ] **Montaje en escena**
- [ ] Contenido: 3 assets `ModuleData` (motores, generadores, enfriamiento)
- [ ] Variantes por dificultad (más paneles / más distractores en intermedia)

### Escenario 3 — Bucles — código listo, sin montar

- [x] `RepeatBlock`: sub-cadena propia, contador de repeticiones
- [x] `ArmBlock`: Recoger, Soltar, Girar Izq/Der
- [x] `RoboticArm` + `ArmSlot`: posiciones con pila de objetos
- [x] `Scenario3Controller` integrado con Director y telemetría
- [x] Telemetría de bucles: `loopBody` + `repetitions`
- [ ] **Montaje en escena**
- [ ] ⚠️ **Validar la mecánica del brazo** — es una propuesta, no un requisito confirmado
- [ ] Variante intermedia (reordenar instrucciones dentro del bucle)

### Escenario 4 — Patrones — no empezado

- [ ] Chips con figuras geométricas
- [ ] Validación por forma (básico) y por número de lados (intermedio)
- [ ] `Scenario4Record` y sus métricas
- [ ] Decidir si necesita panel-ejemplo introductorio (pendiente de beta testers)

### Sistema narrativo — completo

- [x] `Director`: escenarios → pasos, modos `All` y `Any` (ambos funcionando)
- [x] `Narrator`: listas de audio, cambio de lista, límites
- [x] `Fader`: material configurable, tolerante a fallos de configuración
- [x] `Teleporter`, `Waiter`, `Tools`, `VRInteractionEvent`
- [x] `AutomaticDoor`

### Sesión y dificultad — código listo, sin montar

- [x] `DifficultySelector`: fija la dificultad y carga la escena
- [x] `DifficultyScene`: protege la integridad del dato de dificultad
- [x] `Timer`: cuenta atrás, avisos por umbral, `OnTimeUp` (RF-06)
- [ ] **Escenas `Seleccion` / `Basica` / `Intermedia`**
- [ ] Cableado del temporizador de sesión

### Telemetría — funcional

- [x] Modelo JSON con hueco para los 4 escenarios
- [x] Un archivo por participante: `{pin}_{sessionId}.json`
- [x] Escritura diferida + `Flush()` antes de subir
- [x] Intentos, secuencias en lenguaje natural, reinicios
- [x] Bloques agarrados/soltados **desde código** (no depende de cableado)
- [x] Errores de lógica: colisión y comando inválido
- [x] Selecciones del escenario 2, bucles del escenario 3
- [x] Subida manual al servidor, con reintentos
- [x] Utilidades de simulación en el menú contextual
- [ ] `SecuenciaIncompleta` — **sin definición operativa acordada**
- [ ] `SessionResult` (Completado/Abandonado) — declarado pero sin campo en el JSON
- [ ] Camino de abandono por timeout
- [ ] Métricas del escenario 4

### Servidor

- [x] Flask acepta `.json` y `.csv`, con `secure_filename`
- [x] Túnel Cloudflare estable (`extra_hosts: raspberrypi:host-gateway`)
- [x] Subida verificada de punta a punta
- [ ] Rotar `UPLOAD_API_KEY` y el token del túnel (ambos expuestos)

### Requisitos del SRS sin implementar

- [ ] **RI-01 / RF-01** — Panel de username. Hoy se usa PIN
- [ ] **RI-02** — HUD diegético por escenario
- [ ] **RI-03** — UI de selección de dificultad (el código existe, falta la interfaz)
- [ ] **RF-02** — Desbloqueo secuencial de escenarios
- [ ] **RF-07** — Narración multilenguaje + subtítulos
- [ ] **RF-08** — Reinicio supervisado (thumbstick izq + der simultáneo)
- [ ] **RNF-01 / RNF-02** — Validar 60 FPS y tiempos de carga en dispositivo

---

## 2. Tareas en escena

### Bloqueantes — la telemetría no es fiable hasta hacer esto

- [ ] **`Block.prefab`: borrar los dos `UnityEvent` de telemetría**
      (`RegisterBlockGrabbed` y `RegisterBlockReleased`). Apuntan a `fileID: 0` y ahora el
      registro se hace desde código. Si se quedan, las instancias con override contarán doble
- [ ] **`TelemetryPush`: mover `IncrementPin` al final de la lista**, o sacarlo a su propio
      botón. Ahora va primero y sube un archivo vacío
- [ ] **`TelemetryPush`: cambiar `onHover` por una pulsación deliberada.** Con hover basta
      acercar la mano para cambiar de PIN y subir
- [ ] **`Scenario1Controller`: `startChallengeOnStart = false`**, porque el Director ya llama
      a `StartChallenge`. Dos llamadas reinician el cronómetro

### Limpieza

- [ ] Borrar el `ProgramTrigger` duplicado (anchor `1862719671`, `challengeId: reto1`).
      No se usa, pero apunta al mismo `SocketRow` que el bueno
- [ ] Asignar el material del `Fader` en el inspector. Vacío funciona en editor pero puede
      fallar en el build de Quest, que es el caso difícil de detectar

### Escenario 1 — completar

- [ ] Añadir el bloque **Avanzar 2**: instancia de `Block.prefab`,
      `Action = MoveForwardTwice`, texto y modelo
- [ ] Colgar todos los bloques como **hijos directos** del objeto con `BlockResetter`
- [ ] Quitar los `BlockGenerator` que queden
- [ ] Revisar los pasos del Director: ahora las `waitActions` corren en paralelo, no en serie

### PokeInteractable

- [ ] Crear el prefab de botón pulsable
- [ ] Sustituir `StartButton` y `ResetButton`, que hoy son agarrables y se pueden arrancar
      de su sitio
- [ ] Sustituir `TelemetryPush`

### Escenario 2

- [ ] Crear los 3 assets `ModuleData` (`Create → Codea → Escenario 2 → Módulo`)
- [ ] Por módulo: objeto con `SystemModule`, sus 3 `Text` y los botones como hijos
- [ ] En cada botón: `ModuleOptionButton` con su `optionIndex` (0, 1, …)
- [ ] En cada PokeInteractable: `WhenSelect → ModuleOptionButton.Press`
- [ ] Objeto padre con `Scenario2Controller`
- [ ] Director: `StartChallenge("escenario2")` + el controlador como `waitAction`
- [ ] Probar sin visor: click derecho en un `ModuleOptionButton` → `Press`

### Escenario 3

- [ ] Montar el brazo: `RoboticArm` con su `pivot` y `holdPoint`
- [ ] Crear los `ArmSlot` con su `yaw` y sus objetos iniciales
- [ ] Prefab del `RepeatBlock`: `SocketRow` hijo para la sub-cadena + botones `+` / `−`
      cableados a `IncreaseRepetitions` / `DecreaseRepetitions`
- [ ] Bloques `ArmBlock` (Recoger, Soltar, Girar Izq, Girar Der)
- [ ] `Scenario3Controller` con el `slotToClear` que define la meta

### Escenas y dificultad

- [ ] Crear `Seleccion`, `Basica` e `Intermedia`
- [ ] **Añadirlas en File → Build Settings** (sin esto `LoadScene` falla)
- [ ] `Seleccion`: `TelemetryManager` + `DifficultySelector` con los nombres exactos
- [ ] Cada escena de juego: un `DifficultyScene` con su valor
- [ ] Cablear el `Timer`: `timeLimit`, avisos a 15/10/5 min, y el cierre en `OnTimeUp`

### Validación

- [ ] Partida completa verificando el JSON en `persistentDataPath`
- [ ] Confirmar que llega al servidor y que el contenido es correcto
- [ ] Medir FPS en dispositivo (RNF-01) y tiempos de carga (RNF-02)

---

## 3. Recomendaciones

### Lo que más riesgo tiene ahora mismo

**Commitea.** Hay más de 100 archivos sin commitear, incluidos todos los scripts de los
escenarios 2 y 3. Es la mayor exposición del proyecto y no tiene nada que ver con el código.

**Decide username vs PIN cuanto antes.** El SRS pide username (RF-01) y todo el sistema gira
sobre PIN. Cada sesión de datos recogida con PIN encarece el cambio, porque habrá que decidir
qué hacer con lo ya recolectado.

**Fija los ids de telemetría.** `escenario1`, `motores`… no deben renombrarse una vez empezada
la recolección. Ya hubo un cambio de `reto1` a `escenario1` que dejó registros descartándose
en silencio.

### Trampas de Unity que ya han mordido en este proyecto

**Un prefab no puede referenciar un objeto de escena.** Unity anula la referencia al guardar,
sin avisar, y el `UnityEvent` queda apuntando a nada. Pasó con los contadores de bloques.
Regla: si el destino vive en la escena, cablea **desde código**.

**El orden dentro de un `UnityEvent` importa.** Pasó con `IncrementPin` antes del upload.
Cuando una lista mezcle "registrar", "cerrar" y "subir", revisa el orden explícitamente.

**`onHover` no es un botón.** Se dispara al acercar la mano. Para cualquier acción con
consecuencias — subir, cambiar de PIN, reiniciar — usa una pulsación deliberada.

**Los valores de enum serializados van al final.** `GridActionType`, `TileType`,
`ArmActionType`, `LogicErrorType`. Insertar en medio reasigna en silencio la acción de todos
los objetos ya colocados en escena.

**Una corrutina muere si su GameObject se desactiva.** Por eso el intento se registra *antes*
de ejecutar y no después. Si algo tiene que ocurrir sí o sí, no lo pongas al final de una
corrutina que pueda ser interrumpida.

### Criterio para decidir dónde cablear

| Va por `UnityEvent` | Va por código |
|---|---|
| Sonidos, luces, animaciones | Registro de telemetría |
| Transiciones y fundidos | Suscripciones que deben sobrevivir a recargas |
| Contenido que el diseñador ajusta | Cualquier cosa cuyo olvido corrompa los datos |

La regla corta: **si olvidarlo rompe los datos, va en código**.

### Hábitos que ahorran tiempo

**Verifica la telemetría antes de cada sesión de campo.** Play Mode → click derecho en
`TelemetryManager` → `Test/3 - Simular y subir`. Comprueba de un tirón modelo, escritura,
red y servidor, sin ponerse el visor.

**Revisa el JSON después de cada prueba real.** `Test/Mostrar JSON actual en consola`. Los
fallos de telemetría son silenciosos por naturaleza: no hay error, simplemente faltan datos.

**Compila antes de dar nada por terminado.** Varios de los fallos encontrados esta semana
eran errores de compilación que dejaban el proyecto entero sin ejecutar.

**Prueba las mecánicas sin visor cuando se pueda.** `ModuleOptionButton.Press`,
`RepeatBlock.IncreaseRepetitions`, `AutomaticDoor.Open` y el teclado del bot tienen
`[ContextMenu]`. Iterar en el editor es mucho más rápido que ponerse el Quest.

### Decisiones abiertas que conviene cerrar pronto

1. **`SecuenciaIncompleta`** — sin definición operativa. "Faltaron instrucciones" y "el orden
   estaba mal" no se distinguen con la taxonomía actual. Es criterio pedagógico, no técnico
2. **`SessionResult`** — ¿qué dispara "abandonado"? ¿Timeout, reinicio supervisado, ambos?
3. **Mecánica del brazo del escenario 3** — validar la propuesta antes de montarla
4. **Tres escenas o una** — con tres, el entorno y la narrativa se duplican entre básica e
   intermedia, y cada cambio narrativo hay que hacerlo dos veces
5. **Escenario 4** — ¿necesita panel-ejemplo introductorio? Depende de los beta testers
