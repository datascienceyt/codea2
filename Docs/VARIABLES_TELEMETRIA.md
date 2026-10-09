# Variables de telemetría — Codea VR 2

> Documento para el equipo de evaluación. Describe **qué registra el juego** en cada escenario
> y qué permite observar cada variable.
>
> Estado verificado contra el código el **06/10/2026**. Ese día cambió el formato: los
> archivos anteriores usan el antiguo, resumido al final. Las dos muestras de `JSON Samples/`
> son sesiones del 30/09/2026 **convertidas** al formato nuevo: llevan estimados los campos que
> entonces no se registraban (`deviceId`, `blocksConnected`, `blockResets`).

---

## Nota previa sobre terminología

El proyecto está diseñado para evaluar **pensamiento computacional**, no pensamiento
científico. Los cuatro pilares que se trabajan son **descomposición, reconocimiento de
patrones, abstracción y diseño de algoritmos**, y cada escenario está mapeado a uno o varios
de ellos. Conviene fijar el término antes de redactar instrumentos o informes.

---

## ⚠️ Estado real de la recolección

Esto es importante para no planificar un análisis sobre datos que aún no existen:

| Escenario | Código | ¿Ha generado datos reales? |
|---|---|---|
| 1 — Secuencialidad | ✅ Completo | ✅ Sí, verificado en visor |
| 2 — Condicionales | ✅ Completo | ✅ Sí, en el APK del 30/09/2026 |
| 3 — Bucles y parámetros | ✅ Completo | ✅ Sí, en el APK del 30/09/2026 |
| 4 — Patrones | ✅ Completo | ✅ Sí, en el APK del 30/09/2026 |

Esos datos son del **formato anterior**. El formato que describe este documento, del
06/10/2026, está implementado en código pero **todavía no se ha ejecutado en el visor**:
hasta que se pruebe es un compromiso firme de qué se va a recoger, no datos disponibles.

---

## Cómo llegan los datos

- Un **archivo JSON por participante**, nombrado `{pin}_{sessionId}_{deviceId}.json`: el PIN,
  el número de sesión del visor y el código del visor. Repetir un PIN no pisa nada, porque el
  número de sesión siempre cambia
- Se guarda en el visor y se conserva siempre, aunque no haya red
- Se sube solo al servidor al terminar la sesión o al agotarse el tiempo, si hay red. Cerrar la aplicación a mitad de partida no lo sube: queda en el visor
- Todas las marcas de tiempo son **ISO-8601 en UTC**
- Todos los indicadores (`started`, `completed`, `timedOut`, `solved`, `correct`) son
  booleanos: `true` / `false`
- Los tiempos se llaman siempre igual: `totalSeconds` para un total (la sesión o un escenario)
  y `durationSeconds` para lo que tardó una acción concreta

---

## Variables comunes de sesión

Se registran una vez por participante.

| Variable | Tipo | Qué es |
|---|---|---|
| `pin` | texto | Identificador del participante, de 4 dígitos. Lo asigna el supervisor |
| `deviceId` | texto | **Código del visor**, 5 letras y cifras. El mismo en todas las sesiones de ese visor |
| `sessionId` | entero | Contador interno del visor, sube en cada sesión |
| `difficulty` | entero | **1 = básica, 2 = intermedia**. Fija para toda la sesión; por eso no se repite en cada intento |
| `startedUtc` | fecha | Inicio de la sesión |
| `endedUtc` | fecha | Cierre de la sesión |
| `totalSeconds` | decimal | **Tiempo total de juego**, de principio a fin de la sesión. Incluye la narración y los traslados entre salas, así que es mayor que la suma de los cuatro escenarios |

> `pin` **no es un nombre de usuario**: es un número que el supervisor teclea en el visor antes
> de entregárselo al niño. La pantalla propone el siguiente al último usado, así que en una
> ronda de participantes basta con confirmarlo. **Qué niño corresponde a cada PIN lo registra
> el supervisor fuera del visor.**
>
> `deviceId` sale del identificador que Android da a la aplicación en ese visor. No cambia al
> reiniciar, al actualizar la app ni al borrar sus datos. **Sí cambia** si el visor se
> restablece de fábrica o si se instala un APK firmado con otra clave: conviene apuntar el
> código de cada visor al empezar una campaña y comprobar que sigue igual.

## Variables comunes de cada escenario

Presentes en los cuatro.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `started` | `true`/`false` | Si el participante llegó a este escenario |
| `completed` | `true`/`false` | Si lo resolvió |
| `timedOut` | `true`/`false` | **Se agotó su tiempo sin resolverlo** y la historia pasó a la sala siguiente. Desde el 09/10/2026 cada sala tiene 5 minutos. Nunca es `true` a la vez que `completed`. Un escenario con `started` en `true`, `completed` y `timedOut` en `false` y sin `endedUtc` es una sesión que se cortó (visor apagado, app cerrada) |
| `totalSeconds` | decimal | **Tiempo de resolución**: desde que empieza hasta que lo resuelve o se agota su tiempo (como mucho 300 s desde el 09/10/2026, más lo que dure el último intento si estaba ejecutándose). En los escenarios 2–4 el reto se abre al llegar a la sala, así que incluye su narración (igual para todos). En las sesiones anteriores al 30/09/2026 por la tarde (p. ej. `0101`, `0102`) los escenarios 3 y 4 se abrían al acabar la narración y su tiempo y contadores tienen un pequeño desfase |
| `startedUtc` / `endedUtc` | fecha | Permiten reconstruir el ritmo de la sesión completa |

---

# Escenario 1 — Secuencialidad

**Pilares de diseño:** diseño de algoritmos, descomposición, abstracción.

El niño ordena bloques de instrucciones para que el robot recorra una rejilla y llegue a la
meta. Puede ejecutar, ver qué pasa, reiniciar y volver a intentarlo cuantas veces quiera.

## Variables del escenario

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `failedAttempts` | entero | Ejecuciones que no resolvieron el reto. Indicador de persistencia y de ensayo-error |
| `blocksGrabbed` | entero | Veces que agarró un bloque |
| `blocksConnected` | entero | Veces que **encajó un bloque en un hueco** de la fila. Soltarlo en la mesa no cuenta |
| `blockResets` | entero | Veces que pulsó el botón que devuelve todos los bloques a su sitio |
| `errorCollisionBot` | entero | Movimientos imposibles del robot: salirse del tablero o chocar con un muro o un peligro. Cada paso cuenta: un "Avanzar 2" que choca en los dos pasos suma 2 |
| `errorInvalidUse` | entero | Órdenes de **"Usar" donde no hay nada que usar**. Va aparte del choque porque es otro error: el robot no se movió mal, usó en el sitio equivocado |

## Variables por intento

Cada vez que el niño pulsa el botón de ejecutar se guarda un registro completo:

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `sequence` | lista de textos | **La secuencia exacta que montó**, en lenguaje natural |
| `solved` | `true`/`false` | Si ese intento resolvió el reto |
| `durationSeconds` | decimal | **Segundos que tardó en preparar este intento**: desde que empezó el reto (o desde el fallo anterior) hasta que pulsó ejecutar |
| `timestamp` | fecha | Cuándo lo ejecutó |

> `durationSeconds` **no incluye** el tiempo que el robot tarda en moverse. Ese tiempo lo
> determina la longitud de la secuencia, no el razonamiento: si se contara, una solución larga
> parecería siempre "más pensada" que una corta. Lo que mide es el tiempo de reflexión y
> manipulación de bloques.

Los valores posibles de `sequence` son: `"Avanzar"`, `"Avanzar 2"`, `"Girar Izquierda"`,
`"Girar Derecha"`, `"Usar"`.

**Esta es la variable más rica del escenario.** No solo dice si acertó: conserva el
razonamiento completo de cada intento, en orden, y permite comparar intentos sucesivos para
ver cómo evoluciona la estrategia.

---

# Escenario 2 — Condicionales

**Pilares de diseño:** reconocimiento de patrones, diseño de algoritmos.

Tres módulos averiados de la nave. Cada uno describe en texto **lo que se observa**, no la
causa (*"Los motores hacen ruido pero no arrancan. Su tanque está vacío"*), y ofrece varias
acciones: el niño tiene que deducir qué falta. El módulo se repara cuando están marcadas
**todas** las acciones correctas. Una acción incorrecta se queda marcada un instante, suena la
frase de error y **se suelta sola**: el niño no tiene que desmarcarla. Se puede reintentar sin
límite.

La dificultad vive en los datos, no en el código:

| Dificultad | Opciones | Correctas |
|---|---|---|
| Básica | 4 | 1 |
| Intermedia | 4 | 2 |

Cada distractor es una acción **correcta en otro módulo** —motores trata de combustible y
lubricación, energía de electricidad, enfriamiento de temperatura—, así que no se puede
acertar reconociendo el texto de la opción: hay que leer el problema y descartar. *"Agregar
agua"* aparece en los tres módulos y solo es correcta en uno.

## Variables por pulsación

El escenario no tiene contadores propios: todo está en la lista `selections`, con una entrada
por cada acción que el niño **marca**.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `module` | texto | En qué módulo: `"motores"`, `"generadores"`, `"enfriamiento"` |
| `option` | texto | **Qué acción marcó**, con su texto literal |
| `correct` | `true`/`false` | Si esa acción formaba parte de la solución |
| `durationSeconds` | decimal | **Segundos desde la acción marcada anterior** del escenario; en la primera, desde que se abrió el escenario |
| `timestamp` | fecha | Cuándo |

**Los errores se cuentan sobre la lista:** son las entradas con `correct: false`. Ya no hay un
contador `wrongSelections` aparte, porque decía lo mismo.

**Las deselecciones no se registran.** Ni la automática de una acción incorrecta, ni la manual
de una correcta (que solo puede darse en intermedia, donde hay dos correctas por módulo). Por
eso tampoco existe ya el campo `selected`: valdría siempre `true`. Si un niño desmarca una
correcta y la vuelve a marcar, esa acción aparece dos veces en la lista.

**Utilidad para el diseño pedagógico:** el escenario está construido sobre *fading worked
examples*. Como se guarda el orden y el módulo de cada pulsación, se puede comprobar si los
errores **disminuyen del primer módulo al tercero**, que es exactamente la predicción del
modelo. También permite ver si hay distractores concretos que confunden sistemáticamente y,
con `durationSeconds`, distinguir a quien lee y decide de quien pulsa por descarte.

---

# Escenario 3 — Bucles y parámetros

**Pilares de diseño:** reconocimiento de patrones, abstracción, parametrización.

El niño programa un brazo robótico que tiene que **clasificar** una pila mezclada: los barriles
rojos a la izquierda y las cajas azules a la derecha. La fila de instrucciones entera es el
bucle, y un contador dice cuántas veces se repite.

Además hay un **hueco aparte** donde se pone una ficha —barril o caja— que decide qué recoge el
brazo. No es una instrucción: es el **dato** con el que se ejecuta el programa. La misma
secuencia sirve para los dos tipos cambiando solo esa ficha, y eso es lo que el escenario
enseña.

Hacen falta **al menos dos ejecuciones**, una por tipo. Se puede resolver en más —muchas
ejecuciones con pocas repeticiones— pero es menos eficiente, y eso queda registrado.

- **Básica:** elige las repeticiones y la ficha. La fila viene hecha y no se puede tocar; el
  giro hacia el lado correcto lo resuelve el propio bloque *"Girar al destino"*.
- **Intermedia:** además tiene que **ordenar** las instrucciones, con giros explícitos a
  izquierda y derecha. Entre una pasada y otra el sentido cambia, así que la fila también.

Sin ficha puesta el programa **no se ejecuta** y tampoco cuenta como intento: se registra solo
como error de lógica.

## Variables del escenario

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `failedAttempts` | entero | Ejecuciones que **no clasificaron ningún objeto**. Una pasada que mueve algunos pero no todos no cuenta como fallida: es menos óptima, no errónea |
| `blocksGrabbed` / `blocksConnected` | entero | Veces que agarró y veces que encajó en un hueco un bloque **o una ficha de tipo**. En básica, como la fila está bloqueada, son casi solo cambios de ficha |
| `errorInvalidCommand` | entero | Recoger de una pila vacía, soltar en el lado equivocado, o intentar ejecutar sin ficha o con 0 repeticiones. Al poner la ficha las repeticiones vuelven a 0, así que ejecutar sin elegirlas cuenta aquí y **no** como intento |

Soltar un objeto en el lado equivocado **no se consuma**: el objeto vuelve a su pila y solo se
registra el error. Así el niño ve la consecuencia sin tener que rescatar el objeto.

## Variables por intento

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `itemType` | texto | **Con qué ficha se ejecutó**: `"Barril"` o `"Caja"` |
| `sequence` | lista de textos | **Las instrucciones del bucle, en orden** |
| `repetitions` | entero | **Cuántas repeticiones eligió** |
| `solved` | `true`/`false` | `true` solo en la ejecución que completó el escenario |
| `durationSeconds` | decimal | Tiempo de preparación del intento |
| `timestamp` | fecha | Cuándo |

Las tres primeras son las claves del escenario y miden cosas distintas:

- `repetitions` → reconocimiento de patrones: ¿identificó **cuántas veces** hay que repetir?
- `sequence` → abstracción: ¿identificó **cuál** es la unidad que se repite, y en qué orden?
  En básica es siempre la misma; solo es informativa en intermedia.
- `itemType` → parametrización: ¿entendió que el **mismo programa** sirve para otro caso
  cambiando solo el dato?

Sin `itemType`, dos intentos con la misma secuencia y las mismas repeticiones serían
indistinguibles siendo tareas distintas.

**Patrones que vale la pena buscar:**

- **Dos intentos, uno por tipo, con `repetitions` igual al número de objetos de cada uno:** la
  solución óptima. Entendió el bucle y el parámetro a la vez
- **Muchos intentos con `repetitions: 1`:** resuelve, pero no está usando el bucle
- **El mismo `itemType` dos veces seguidas, con el segundo fallido:** ejecutó otra vez sobre
  una pila ya vacía. No relacionó que ya había terminado con ese tipo
- **`errorInvalidCommand` alto con `repetitions` mayor que los objetos:** se pasa de vueltas.
  Cuenta mal, o no relaciona el número con la cantidad de objetos

---

# Escenario 4 — Patrones *(incluido por completitud)*

**Pilares de diseño:** reconocimiento de patrones, abstracción.

Fichas con figuras abstractas que encajan en los huecos de un panel. Varias fichas se parecen
mucho entre sí y solo una es idéntica a la del hueco, así que el reto es de **discriminación
visual**: hay que comparar el detalle, no reconocer una forma conocida.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `chipsGrabbed` | entero | Cuántas veces cogió una ficha: la señal de duda y tanteo |
| `socket` | texto | En qué hueco intentó colocar |
| `chip` | texto | Qué figura colocó |
| `correct` | `true`/`false` | Si encajaba |
| `durationSeconds` | decimal | **Segundos desde la colocación anterior**; en la primera, desde que se abrió el escenario |
| `timestamp` | fecha | Cuándo |

Las tres últimas filas y `socket`/`chip` van en la lista `placements`, una entrada por ficha
encajada. Las colocaciones incorrectas son las que tienen `correct: false`; ya no hay un
contador `wrongPlacements` aparte, ni uno de fichas soltadas: una ficha encajada ya es una
entrada de la lista.

Se guarda **qué figura fue a qué hueco**, no solo el acierto. Ese par es el dato pedagógico
del escenario: saber **cuál confundió con cuál** dice qué detalle no llegó a distinguir, y de
ahí salen las parejas de figuras que resultaron demasiado parecidas para la edad.

> La dificultad de la sesión **no se guarda aquí**. Va en `difficulty`, en la raíz del JSON:
> la mecánica es idéntica en básica e intermedia y lo único que cambia es el juego de figuras,
> más simples o más densas. Hubo un campo `matchMode` mientras el escenario emparejaba también
> por número de lados; ese modo ya no existe y el campo tampoco.

---

# Métricas derivadas de alto valor

Estas no se guardan, pero se calculan directamente desde lo anterior:

| Métrica | Cómo se obtiene | Qué informa |
|---|---|---|
| **Intentos hasta resolver** | Nº de elementos en `attempts` | Persistencia, dificultad percibida |
| **Tiempo hasta el primer intento** | `attempts[0].durationSeconds` | Planificación previa vs ensayo inmediato |
| **Evolución del tiempo por intento** | Serie de `durationSeconds` | Si se acelera (va afinando) o se ralentiza (se atasca) |
| **Eficiencia de la solución** | Longitud de la `sequence` ganadora vs la óptima del nivel | Optimización, abstracción |
| **Uso de "Avanzar 2"** | Buscar `"Avanzar 2"` en la `sequence` | **Abstracción**: agrupar dos pasos en una instrucción |
| **Distancia entre intentos** | Comparar `sequence` de intentos consecutivos | Distingue corrección **sistemática** de ensayo-error aleatorio |
| **Errores del Esc. 2** | Contar en `selections` las entradas con `correct: false` | Sustituye al antiguo `wrongSelections` |
| **Tasa de error por módulo** | Agrupar `selections` por `module` | Validar el efecto del *fading* (Esc. 2) |
| **Tiempo de decisión** | `durationSeconds` de cada selección o colocación | Si el error llega tras pensar o por pulsar rápido (Esc. 2 y 4) |
| **Errores del Esc. 4** | Contar en `placements` las entradas con `correct: false` | Sustituye al antiguo `wrongPlacements` |
| **Convergencia del bucle** | Serie de `repetitions` por intento | Estrategia de aproximación (Esc. 3) |
| **Eficiencia de pasadas** | Nº de intentos del Esc. 3 frente al óptimo de 2, uno por `itemType` | Si usa el bucle o lo sustituye por ejecuciones repetidas (Esc. 3) |
| **Transferencia del parámetro** | Comparar la `sequence` de los intentos con distinto `itemType` | Si entiende que el mismo programa sirve cambiando el dato (Esc. 3, intermedia) |
| **Matriz de confusión de figuras** | Cruzar `chip` × `socket` en los fallos | Qué equivalencias falsas construye (Esc. 4) |

La de **distancia entre intentos** merece atención especial: comparar la secuencia del intento
N con la del N+1 permite distinguir a un niño que corrige un elemento concreto tras observar
el fallo, de otro que reordena todo al azar. Son perfiles cognitivos muy distintos y ninguna
variable simple los separa.

---

# Limitaciones — qué NO se captura

Conviene tenerlas presentes antes de diseñar el instrumento de evaluación:

1. **No hay identificación nominal.** Cada sesión lleva el `pin` que tecleó el supervisor. El
   emparejamiento con encuestas u otros instrumentos depende del registro PIN → participante
   que lleve el supervisor. Las sesiones recogidas antes del 30/09/2026 salieron todas con
   `"0000"` y solo las distingue `sessionId`.

2. **Salirse del tablero y chocar van en el mismo contador.** `errorCollisionBot` suma los dos
   y no se pueden separar después. "Usar" mal sí va aparte, en `errorInvalidUse`.

3. **`blocksGrabbed` mide manipulación, no decisiones.** Incluye agarrar un bloque y volver a
   dejarlo sin usarlo. `blocksConnected` es el que dice cuántas veces puso un bloque en la
   fila; la diferencia entre los dos es lo que cogió y no llegó a colocar.

4. **No se registra el estado "abandonado".** Si un participante no termina, queda
   `completed: false` pero no hay un campo que distinga *se acabó el tiempo* de *lo dejó*.

5. **No se registran las pausas.** Quitarse el visor un momento no queda reflejado. Los
   tiempos (`totalSeconds`, `durationSeconds`) usan el reloj del juego, que se detiene mientras
   la aplicación está en pausa; las fechas (`startedUtc`, `timestamp`) usan el reloj real. Si
   la diferencia entre dos fechas es mayor que los segundos registrados, hubo una pausa.

6. **No hay vídeo, audio, mirada ni posición del jugador.** Solo eventos lógicos.

---

# Ejemplo de archivo real

```json
{
    "pin": "0004",
    "deviceId": "3F9A1",
    "sessionId": 187,
    "difficulty": 1,
    "startedUtc": "2026-08-26T09:12:03.4410000Z",
    "endedUtc": "2026-08-26T09:26:44.8820000Z",
    "totalSeconds": 881.4,

    "escenario1": {
        "started": true,
        "completed": true,
        "timedOut": false,
        "totalSeconds": 149.8,
        "failedAttempts": 1,
        "blocksGrabbed": 9,
        "blocksConnected": 7,
        "blockResets": 1,
        "errorCollisionBot": 1,
        "errorInvalidUse": 1,
        "attempts": [
            {
                "sequence": ["Avanzar", "Girar Derecha"],
                "solved": false,
                "durationSeconds": 21.4,
                "timestamp": "2026-08-26T09:13:41.0000000Z"
            },
            {
                "sequence": ["Avanzar", "Girar Derecha", "Avanzar 2", "Usar"],
                "solved": true,
                "durationSeconds": 38.2,
                "timestamp": "2026-08-26T09:14:32.0000000Z"
            }
        ]
    },

    "escenario2": {
        "started": true,
        "completed": true,
        "timedOut": false,
        "totalSeconds": 84.2,
        "selections": [
            { "module": "motores", "option": "Agregar agua",        "correct": false, "durationSeconds": 17.3, "timestamp": "..." },
            { "module": "motores", "option": "Agregar combustible", "correct": true,  "durationSeconds": 9.1,  "timestamp": "..." },
            { "module": "motores", "option": "Agregar aceite",      "correct": true,  "durationSeconds": 4.6,  "timestamp": "..." }
        ]
    },

    "escenario3": {
        "started": true,
        "completed": true,
        "timedOut": false,
        "totalSeconds": 121.7,
        "failedAttempts": 1,
        "blocksGrabbed": 3,
        "blocksConnected": 2,
        "errorInvalidCommand": 3,
        "attempts": [
            { "itemType": "Caja",   "repetitions": 7, "solved": false, "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." },
            { "itemType": "Caja",   "repetitions": 3, "solved": false, "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." },
            { "itemType": "Barril", "repetitions": 3, "solved": true,  "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." }
        ]
    },

    "escenario4": {
        "started": true,
        "completed": true,
        "timedOut": false,
        "totalSeconds": 73.9,
        "chipsGrabbed": 6,
        "placements": [
            { "socket": "circulo_rombo",    "chip": "circulo_rombo",    "correct": true,  "durationSeconds": 18.3, "timestamp": "..." },
            { "socket": "cuadrado_circulo", "chip": "cuadrado_punto",   "correct": false, "durationSeconds": 17.5, "timestamp": "..." },
            { "socket": "cuadrado_circulo", "chip": "cuadrado_circulo", "correct": true,  "durationSeconds": 13.9, "timestamp": "..." }
        ]
    }
}
```

El ejemplo está abreviado (faltan `startedUtc`/`endedUtc` de cada escenario y algunos campos
de los intentos del Escenario 3). El archivo completo, con el formato exacto, es
`ejemplo_run_telemetria.json`.

En el Escenario 4 se lee de un vistazo lo que mide el escenario: confundió `cuadrado_punto` con
`cuadrado_circulo` y acertó al segundo intento. Ese par concreto es lo que interesa, porque señala qué
dos figuras resultaron demasiado parecidas.

En el Escenario 3 se lee la historia de las tres ejecuciones: movió las 7 cajas de una pasada,
**volvió a ejecutar con caja** sobre una pila ya vacía —los 3 `Recoger` fallaron, de ahí
`errorInvalidCommand: 3` y el intento fallido—, y después cambió la ficha a barril y terminó.
Solución casi óptima, con un error concreto: no relacionó que ya había acabado con ese tipo.

---

# Si necesitáis algo más

Añadir una variable nueva es barato **antes** de empezar a recoger datos y caro después,
porque obligaría a migrar los archivos ya recogidos. Si al diseñar el instrumento detectáis
que falta algo, es mejor decirlo ahora.

Dos cosas que ya sabemos que se pueden añadir con poco esfuerzo si os sirven:

- **Marca de abandono** con su motivo (tiempo agotado / reinicio del supervisor)

---

# Cambios de formato del 06/10/2026

Para leer archivos anteriores a esa fecha junto a los nuevos:

| Antes | Ahora |
|---|---|
| Archivo `{pin}_{sessionId}.json` | `{pin}_{sessionId}_{deviceId}.json` |
| — | `deviceId` y `totalSeconds` en la raíz |
| `solved`, `correct` como 0/1 | `true`/`false` |
| `difficulty` en cada intento | Solo en la raíz |
| `blocksReleased` (cada suelta) | `blocksConnected` (solo al encajar en un hueco). No son comparables |
| Esc. 1: `errorCollisionBot` (salirse) y `errorInvalidCommand` (chocar y usar mal) | `errorCollisionBot` (salirse y chocar) y `errorInvalidUse` (usar mal). Los totales de antes y de ahora no son comparables contador a contador, solo su suma |
| `errorIncompleteSequence` (siempre 0) | Eliminado |
| Esc. 3: `errorCollisionBot` (siempre 0) | Eliminado; queda `errorInvalidCommand` |
| — | Esc. 1: `blockResets` |
| Esc. 2: `wrongSelections` | Eliminado: contar `correct: false` en `selections` |
| Esc. 2: la incorrecta se desmarcaba a mano y quedaba registrada | Se desmarca sola |
| Esc. 2: campo `selected`, con las deselecciones registradas | Eliminado: solo se registra lo que se marca |
| — | Esc. 2 y 4: `durationSeconds` en cada selección y colocación |
| Esc. 4: `wrongPlacements`, `chipsReleased` | Eliminados: contar `correct: false` en `placements` |
