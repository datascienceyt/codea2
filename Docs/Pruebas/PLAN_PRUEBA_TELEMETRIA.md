# Plan de prueba de la telemetría — dificultad básica

Una partida guionizada en la que **cada acción tiene un efecto conocido en el JSON**. Al
terminarla, el archivo del visor tiene que coincidir con
[`esperado_prueba_basico.json`](esperado_prueba_basico.json) en todos los campos salvo los
marcados `<VARIABLE>`. Sirve para comprobar qué mide cada parámetro y para detectar cualquier
contador que no se registre.

Preparado contra el código del 06/10/2026 (formato de telemetría nuevo). El significado de cada variable está en
[`../VARIABLES_TELEMETRIA.md`](../VARIABLES_TELEMETRIA.md).

## Reglas para que el resultado sea exacto

1. **Cada agarre cuenta.** Coger un bloque o una ficha suma 1 a `…Grabbed`. Soltarlo ya no
   cuenta: en los escenarios 1 y 3 solo suma `blocksConnected` cuando **encaja en un hueco**.
   Si se te escapa uno o lo recolocas, apúntalo: el contador esperado sube en lo mismo.
2. **No toques nada antes de tiempo.** En el Escenario 1 el reto se abre cuando arranca el
   reloj; lo que hagas antes no se registra. En los escenarios 2–4 se abre al llegar a la sala.
3. **Pulsa Ejecutar solo cuando se indica**, y espera a que el robot o el brazo terminen del
   todo antes de tocar nada.
4. Termina antes de que se agoten los 15 minutos.

## Preparación

| Paso | Acción |
|---|---|
| P1 | Abrir la app. En `Setup`, pulsar **Borrar** hasta vaciar el PIN y teclear **0 5 0 0** |
| P2 | Pulsar **Básica** y después **EMPEZAR** |
| P3 | En el cuarto inicial, pulsar el botón de inicio y completar el tutorial: los dos botones, el bloque en su hueco y la ficha en el suyo |

Demuestra: `pin` = `"0500"` y `difficulty` = `1`. `sessionId` es correlativo y `deviceId` es
el código del visor: no se comparan, pero `deviceId` tiene que ser el mismo en todas las
partidas de ese visor.

Lo que se agarra y se encaja en el tutorial **no se registra**: ocurre antes de abrir ningún
reto. Si el JSON trae agarres de más en el Escenario 1, es que el tutorial los está contando.

## Escenario 1 — Secuencialidad

Paleta de 8 bloques: Avanzar ×2, Avanzar 2 ×2, Girar Derecha ×2, Girar Izquierda ×1, Usar ×1.
El robot sale mirando hacia abajo. Espera a que arranque el reloj antes de tocar bloques.

| Paso | Acción | Agarres | Conexiones | Efecto esperado |
|---|---|---|---|---|
| 1.1 | Poner **Avanzar 2** en el hueco 1 y **Avanzar** en el hueco 2 | +2 | +2 | — |
| 1.2 | **Ejecutar** | — | — | Intento 1. El robot baja 2 casillas y el tercer paso sale del tablero: `errorCollisionBot` +1. No llega: `failedAttempts` +1, frase de error y el nivel se rearma solo |
| 1.3 | Sacar los 2 bloques de la fila y dejarlos en la mesa | +2 | — | Soltar en la mesa no suma nada |
| 1.4 | Poner **Girar Derecha**, **Avanzar** y **Usar** en los huecos 1–3 | +3 | +3 | — |
| 1.5 | **Ejecutar** | — | — | Intento 2. Gira hacia la izquierda; "Avanzar" choca con el peligro de al lado: `errorCollisionBot` +1. "Usar" sobre el peligro: `errorInvalidUse` +1. `failedAttempts` +1 |
| 1.6 | Pulsar el botón de **reiniciar bloques** | — | — | Los 3 bloques vuelven solos a la mesa: `blockResets` +1 |
| 1.7 | Montar: **Avanzar · Girar Derecha · Avanzar 2 · Girar Derecha · Avanzar 2 · Girar Izquierda · Avanzar · Usar** | +8 | +8 | — |
| 1.8 | **Ejecutar** | — | — | Intento 3. Llega al botón: `completed` = true y el intento 3 pasa a `solved` = true |

Totales: 3 intentos, `failedAttempts` 2, `blocksGrabbed` 15, `blocksConnected` 13,
`blockResets` 1, `errorCollisionBot` 2, `errorInvalidUse` 1.

## Escenario 2 — Condicionales

Los módulos aparecen en este orden: enfriamiento, generadores, motores. Una opción incorrecta
se queda marcada un instante y **se suelta sola**: no hay que desmarcarla, y ese desmarcado no
se registra.

| Paso | Módulo | Acción | Efecto esperado |
|---|---|---|---|
| 2.1 | Enfriamiento | Pulsar **Agregar combustible** | Selección con `correct` false. Frase de error; el botón se suelta solo en menos de un segundo |
| 2.2 | Enfriamiento | Pulsar **Agregar agua** | `correct` true. Módulo reparado |
| 2.3 | Generadores | Pulsar **Agregar agua** | `correct` false. Se suelta solo |
| 2.4 | Generadores | Pulsar **Conectar una batería cargada** | `correct` true. Módulo reparado: la incorrecta ya no cuenta como marcada |
| 2.5 | Motores | Pulsar **Agregar combustible** | `correct` true. Reparado y escenario completado |

Totales: 5 selecciones, 2 de ellas con `correct` false. Cada una lleva su `durationSeconds`:
el tiempo desde la pulsación anterior (la primera, desde que se abrió el escenario).

## Escenario 3 — Bucles y parámetros

Hay 7 cajas y 3 barriles. La fila viene montada, bloqueada y **oculta** (desde el 09/10/2026):
Recoger · Girar al destino · Soltar · Volver. No se ve, pero se ejecuta y va al JSON igual. Las fichas de tipo están en la mesa; el contador de repeticiones tiene + y −.

| Paso | Acción | Agarres | Conexiones | Efecto esperado |
|---|---|---|---|---|
| 3.1 | **Ejecutar** sin poner ninguna ficha | — | — | No se ejecuta ni cuenta como intento. `errorInvalidCommand` +1 |
| 3.2 | Poner la ficha **Caja** en su hueco | +1 | +1 | El contador pasa a 0 |
| 3.3 | **Ejecutar** con 0 repeticiones | — | — | No se ejecuta ni cuenta como intento. `errorInvalidCommand` +1 |
| 3.4 | Pulsar **+** 3 veces (contador en 3) y **Ejecutar** | — | — | Intento 1 (`Caja`, 3): mueve 3 cajas. Avanzó, así que no es fallido |
| 3.5 | Pulsar **+** 2 veces (contador en 5) y **Ejecutar** | — | — | Intento 2 (`Caja`, 5): mueve las 4 que quedan. En la 5.ª vuelta no hay caja: "Recoger" y "Soltar" fallan, `errorInvalidCommand` +2. Avanzó: no es fallido |
| 3.6 | Pulsar **−** 4 veces (contador en 1) y **Ejecutar** | — | — | Intento 3 (`Caja`, 1): no queda ninguna caja, `errorInvalidCommand` +2, no mueve nada: `failedAttempts` +1 |
| 3.7 | Sacar la ficha **Caja** y dejarla en la mesa | +1 | — | — |
| 3.8 | Poner la ficha **Barril** en su hueco | +1 | +1 | El contador vuelve a 0 |
| 3.9 | Pulsar **+** 3 veces y **Ejecutar** | — | — | Intento 4 (`Barril`, 3): mueve los 3 barriles. Escenario completado: el intento 4 pasa a `solved` = true |

Totales: 4 intentos, `failedAttempts` 1, `blocksGrabbed` 3, `blocksConnected` 2,
`errorInvalidCommand` 6.

> Si el contador **no** conserva el 3 entre el paso 3.4 y el 3.5, es que ya se aplicó el
> pedido 7 de la sección 11 de `Context.md` (repeticiones a 0 al terminar). En ese caso pulsa
> **+** 5 veces en el 3.5 y **+** 1 vez en el 3.6; el JSON esperado no cambia.

## Escenario 4 — Patrones

Huecos: `circulo_cuadrado` (círculo con un cuadrado dentro), `cuadrado_rombo`,
`cuadrado_trianguloinv` y `triangulo_circulo`. Entre las fichas está `cuadrado_circulo`
(cuadrado con un círculo dentro), que se parece mucho a la primera.

| Paso | Acción | Agarres | Efecto esperado |
|---|---|---|---|
| 4.1 | Poner **cuadrado_circulo** en el hueco **circulo_cuadrado** | +1 | Colocación `correct` false. Sale disparada con sonido. **No vuelvas a cogerla** |
| 4.2 | Poner **circulo_cuadrado** en su hueco | +1 | `correct` true. El hueco se pone verde |
| 4.3 | **cuadrado_rombo** en su hueco | +1 | `correct` true |
| 4.4 | **cuadrado_trianguloinv** en su hueco | +1 | `correct` true |
| 4.5 | **triangulo_circulo** en su hueco | +1 | `correct` true. Motores y escenario completado |

Totales: 5 colocaciones, 1 de ellas con `correct` false, `chipsGrabbed` 5. Cada colocación
lleva su `durationSeconds`, desde la anterior (la primera, desde que se abrió el escenario).

## Cómo comparar

1. Sacar el JSON del visor: `/sdcard/Android/data/<paquete>/files/0500_<sessionId>_<deviceId>.json`.
   Si hay varias con el PIN 0500, la tuya es la de `sessionId` más alto.
2. **Todo lo que no es `<VARIABLE>` tiene que ser idéntico**: valores, número de elementos de
   cada lista y su orden.
3. Para los `<VARIABLE>`, comprobar solo que tienen sentido:
   - Cada `startedUtc` es anterior a su `endedUtc`, y los escenarios van en orden.
   - El `timestamp` de cada intento, selección o colocación es posterior al `startedUtc` de su
     escenario.
   - `totalSeconds` > 0 en los cuatro escenarios, y el de la raíz mayor que la suma de los
     cuatro.
   - Cada `durationSeconds` > 0.
4. Si un contador no coincide, primero repasa si hubo un agarre de más (regla 1). Si no lo
   hubo, esa variable no mide lo que dice la guía: anótalo en la sección 11 de `Context.md`.

## Qué parámetro demuestra cada paso

| Parámetro | Pasos |
|---|---|
| `pin`, `difficulty`, `deviceId` | P1, P2 |
| `attempts[].sequence`, `solved` | 1.2, 1.5, 1.8 · 3.4–3.9 |
| `failedAttempts` | 1.2, 1.5 (esc. 1) · 3.6 (esc. 3; 3.4 y 3.5 no cuentan porque avanzan) |
| `errorCollisionBot` | 1.2 (salir del tablero) · 1.5 (chocar con un peligro) |
| `errorInvalidUse` | 1.5 ("Usar" donde no hay nada) |
| `errorInvalidCommand` | 3.1, 3.3 (vetos) · 3.5, 3.6 (brazo sin objeto) |
| `blocksGrabbed` | 1.1, 1.3, 1.4, 1.7 · 3.2, 3.7, 3.8 |
| `blocksConnected` | 1.1, 1.4, 1.7 · 3.2, 3.8 (1.3 y 3.7 muestran que soltar fuera de un hueco no suma) |
| `blockResets` | 1.6 |
| `selections[]` | 2.1–2.5 (2.1 y 2.3 muestran que la incorrecta se registra una vez y se suelta sola) |
| `attempts[].repetitions`, `itemType` | 3.4–3.9 |
| `placements[]` | 4.1–4.5 |
| `chipsGrabbed` | 4.1–4.5 |
| `durationSeconds` | Cada intento, selección y colocación |
| `completed`, `totalSeconds` | Último paso de cada escenario; el `totalSeconds` de la raíz, al volver a `Setup` |
