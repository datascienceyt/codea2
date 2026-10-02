# Plan de prueba de la telemetría — dificultad básica

Una partida guionizada en la que **cada acción tiene un efecto conocido en el JSON**. Al
terminarla, el archivo del visor tiene que coincidir con
[`esperado_prueba_basico.json`](esperado_prueba_basico.json) en todos los campos salvo los
marcados `<VARIABLE>`. Sirve para comprobar qué mide cada parámetro y para detectar cualquier
contador que no se registre.

Preparado contra el código del 01/10/2026. El significado de cada variable está en
[`../VARIABLES_TELEMETRIA.md`](../VARIABLES_TELEMETRIA.md).

## Reglas para que el resultado sea exacto

1. **Cada agarre cuenta.** Coger un bloque o una ficha suma 1 a `…Grabbed`, y soltarlo, donde
   sea, suma 1 a `…Released`. Si se te escapa uno o lo recolocas, apúntalo: el contador
   esperado sube en lo mismo.
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

Demuestra: `pin` = `"0500"` y `difficulty` = `1`. `sessionId` es correlativo: no se compara.

## Escenario 1 — Secuencialidad

Paleta de 8 bloques: Avanzar ×2, Avanzar 2 ×2, Girar Derecha ×2, Girar Izquierda ×1, Usar ×1.
El robot sale mirando hacia abajo. Espera a que arranque el reloj antes de tocar bloques.

| Paso | Acción | Agarres | Efecto esperado |
|---|---|---|---|
| 1.1 | Poner **Avanzar 2** en el hueco 1 y **Avanzar** en el hueco 2 | +2 | — |
| 1.2 | **Ejecutar** | — | Intento 1. El robot baja 2 casillas y el tercer paso sale del tablero: `errorCollisionBot` +1. No llega: `failedAttempts` +1, frase de error y el nivel se rearma solo |
| 1.3 | Sacar los 2 bloques de la fila y dejarlos en la mesa | +2 | — |
| 1.4 | Poner **Girar Derecha**, **Avanzar** y **Usar** en los huecos 1–3 | +3 | — |
| 1.5 | **Ejecutar** | — | Intento 2. Gira hacia la izquierda; "Avanzar" choca con el peligro de al lado: `errorInvalidCommand` +1 (no es `errorCollisionBot`: ese solo cuenta salir del tablero). "Usar" sobre el peligro: `errorInvalidCommand` +1. `failedAttempts` +1 |
| 1.6 | Sacar los 3 bloques y dejarlos en la mesa | +3 | — |
| 1.7 | Montar: **Avanzar · Girar Derecha · Avanzar 2 · Girar Derecha · Avanzar 2 · Girar Izquierda · Avanzar · Usar** | +8 | — |
| 1.8 | **Ejecutar** | — | Intento 3. Llega al botón: `completed` = true y el intento 3 pasa a `solved` = 1 |

Totales: 3 intentos, `failedAttempts` 2, agarres 18 / sueltas 18, `errorCollisionBot` 1,
`errorInvalidCommand` 2.

## Escenario 2 — Condicionales

Los módulos aparecen en este orden: enfriamiento, generadores, motores. Pulsar una opción la
marca y volver a pulsarla la desmarca.

| Paso | Módulo | Acción | Efecto esperado |
|---|---|---|---|
| 2.1 | Enfriamiento | Marcar **Agregar gasolina** | Selección `selected` 1, `correct` 0. `wrongSelections` +1. Frase de error |
| 2.2 | Enfriamiento | Desmarcar **Agregar gasolina** | Selección `selected` 0, `correct` 0. No suma a `wrongSelections` |
| 2.3 | Enfriamiento | Marcar **Agregar agua** | `selected` 1, `correct` 1. Módulo reparado |
| 2.4 | Generadores | Marcar **Agregar agua** | `selected` 1, `correct` 0. `wrongSelections` +1 |
| 2.5 | Generadores | Marcar **Conectar una batería cargada** | `selected` 1, `correct` 1. **No se repara**: sigue marcada una opción que sobra |
| 2.6 | Generadores | Desmarcar **Agregar agua** | `selected` 0, `correct` 0. Ahora sí se repara: lo marcado coincide exactamente |
| 2.7 | Motores | Marcar **Agregar gasolina** | `selected` 1, `correct` 1. Reparado y escenario completado |

Totales: 7 selecciones, `wrongSelections` 2.

## Escenario 3 — Bucles y parámetros

Hay 7 cajas y 3 barriles. La fila viene montada y bloqueada: Recoger · Girar al destino ·
Soltar · Volver. Las fichas de tipo están en la mesa; el contador de repeticiones tiene + y −.

| Paso | Acción | Agarres | Efecto esperado |
|---|---|---|---|
| 3.1 | **Ejecutar** sin poner ninguna ficha | — | No se ejecuta ni cuenta como intento. `errorInvalidCommand` +1 |
| 3.2 | Poner la ficha **Caja** en su hueco | +1 | El contador pasa a 0 |
| 3.3 | **Ejecutar** con 0 repeticiones | — | No se ejecuta ni cuenta como intento. `errorInvalidCommand` +1 |
| 3.4 | Pulsar **+** 3 veces (contador en 3) y **Ejecutar** | — | Intento 1 (`Caja`, 3): mueve 3 cajas. Avanzó, así que no es fallido |
| 3.5 | Pulsar **+** 2 veces (contador en 5) y **Ejecutar** | — | Intento 2 (`Caja`, 5): mueve las 4 que quedan. En la 5.ª vuelta no hay caja: "Recoger" y "Soltar" fallan, `errorInvalidCommand` +2. Avanzó: no es fallido |
| 3.6 | Pulsar **−** 4 veces (contador en 1) y **Ejecutar** | — | Intento 3 (`Caja`, 1): no queda ninguna caja, `errorInvalidCommand` +2, no mueve nada: `failedAttempts` +1 |
| 3.7 | Sacar la ficha **Caja** y dejarla en la mesa | +1 | — |
| 3.8 | Poner la ficha **Barril** en su hueco | +1 | El contador vuelve a 0 |
| 3.9 | Pulsar **+** 3 veces y **Ejecutar** | — | Intento 4 (`Barril`, 3): mueve los 3 barriles. Escenario completado: el intento 4 pasa a `solved` = 1 |

Totales: 4 intentos, `failedAttempts` 1, agarres 3 / sueltas 3, `errorInvalidCommand` 6.

> Si el contador **no** conserva el 3 entre el paso 3.4 y el 3.5, es que ya se aplicó el
> pedido 7 de la sección 11 de `Context.md` (repeticiones a 0 al terminar). En ese caso pulsa
> **+** 5 veces en el 3.5 y **+** 1 vez en el 3.6; el JSON esperado no cambia.

## Escenario 4 — Patrones

Huecos: `circulo_cuadrado` (círculo con un cuadrado dentro), `cuadrado_rombo`,
`cuadrado_trianguloinv` y `triangulo_circulo`. Entre las fichas está `cuadrado_circulo`
(cuadrado con un círculo dentro), que se parece mucho a la primera.

| Paso | Acción | Agarres | Efecto esperado |
|---|---|---|---|
| 4.1 | Poner **cuadrado_circulo** en el hueco **circulo_cuadrado** | +1 | Colocación `correct` 0. `wrongPlacements` +1. Sale disparada con sonido. **No vuelvas a cogerla** |
| 4.2 | Poner **circulo_cuadrado** en su hueco | +1 | `correct` 1. El hueco se pone verde |
| 4.3 | **cuadrado_rombo** en su hueco | +1 | `correct` 1 |
| 4.4 | **cuadrado_trianguloinv** en su hueco | +1 | `correct` 1 |
| 4.5 | **triangulo_circulo** en su hueco | +1 | `correct` 1. Motores y escenario completado |

Totales: 5 colocaciones, `wrongPlacements` 1, agarres 5 / sueltas 5.

## Cómo comparar

1. Sacar el JSON del visor: `/sdcard/Android/data/<paquete>/files/0500_<sessionId>.json`.
2. **Todo lo que no es `<VARIABLE>` tiene que ser idéntico**: valores, número de elementos de
   cada lista y su orden.
3. Para los `<VARIABLE>`, comprobar solo que tienen sentido:
   - Cada `startedUtc` es anterior a su `endedUtc`, y los escenarios van en orden.
   - El `timestamp` de cada intento, selección o colocación es posterior al `startedUtc` de su
     escenario.
   - `totalSeconds` > 0 en los cuatro escenarios.
4. Si un contador no coincide, primero repasa si hubo un agarre de más (regla 1). Si no lo
   hubo, esa variable no mide lo que dice la guía: anótalo en la sección 11 de `Context.md`.

## Qué parámetro demuestra cada paso

| Parámetro | Pasos |
|---|---|
| `pin`, `difficulty` | P1, P2 |
| `attempts[].sequence`, `solved` | 1.2, 1.5, 1.8 · 3.4–3.9 |
| `failedAttempts` | 1.2, 1.5 (esc. 1) · 3.6 (esc. 3; 3.4 y 3.5 no cuentan porque avanzan) |
| `errorCollisionBot` | 1.2 (salir del tablero) |
| `errorInvalidCommand` | 1.5 (chocar con un peligro y "Usar" en vacío) · 3.1, 3.3 (vetos) · 3.5, 3.6 (brazo sin objeto) |
| `blocksGrabbed` / `blocksReleased` | 1.1, 1.3, 1.4, 1.6, 1.7 · 3.2, 3.7, 3.8 |
| `selections[]`, `wrongSelections` | 2.1–2.7 (2.2 y 2.6 muestran que las deselecciones se guardan pero no suman) |
| `attempts[].repetitions`, `itemType` | 3.4–3.9 |
| `placements[]`, `wrongPlacements` | 4.1–4.5 |
| `chipsGrabbed` / `chipsReleased` | 4.1–4.5 |
| `completed`, `totalSeconds` | Último paso de cada escenario |
