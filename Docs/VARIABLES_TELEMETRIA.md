# Variables de telemetría — Codea VR 2

> Documento para el equipo de evaluación. Describe **qué registra el juego** en cada escenario
> y qué permite observar cada variable.
>
> Estado verificado contra el código el **23/09/2026**.

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
| 2 — Condicionales | ✅ Completo | 🟡 Montado en escena, **pendiente de probar** |
| 3 — Bucles y parámetros | ✅ Completo, **rediseñado** | 🟡 Montado en escena, **pendiente de probar** |
| 4 — Patrones | ✅ Completo | 🟡 Montado en escena, **pendiente de probar** |

Las variables de los escenarios 2, 3 y 4 **están implementadas en código**, pero ese código
todavía no se ha ejecutado en una sesión real. Son un compromiso firme de qué se va a
recoger, no datos disponibles hoy.

---

## Cómo llegan los datos

- Un **archivo JSON por participante**, nombrado `{pin}_{sessionId}.json`
- Se guarda en el visor y se conserva siempre, aunque no haya red
- El supervisor lo envía al servidor con un botón dedicado (no es automático)
- Todas las marcas de tiempo son **ISO-8601 en UTC**
- Los indicadores por registro (`solved`, `correct`, `selected`) se guardan como **0 / 1** para
  facilitar el volcado a tabla. Excepción: `started` y `completed` son booleanos de verdad,
  `true` / `false`

---

## Variables comunes de sesión

Se registran una vez por participante.

| Variable | Tipo | Qué es |
|---|---|---|
| `pin` | texto | Identificador del participante, de 4 dígitos. Lo asigna el supervisor |
| `sessionId` | entero | Contador interno que sube en cada arranque. Evita colisiones si se repite un PIN |
| `difficulty` | entero | **1 = básica, 2 = intermedia**. Fija para toda la sesión |
| `startedUtc` | fecha | Inicio de la sesión |
| `endedUtc` | fecha | Cierre de la sesión |

> `pin` **no es un nombre de usuario**, y **hoy todavía no se asigna**: la pantalla del
> supervisor no está montada, así que todas las sesiones salen con `pin: "0000"`. Los archivos
> no se pisan porque `sessionId` sí cambia en cada arranque, pero **el emparejamiento con cada
> niño depende de que el supervisor anote qué `sessionId` corresponde a quién**.

## Variables comunes de cada escenario

Presentes en los cuatro.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `started` | `true`/`false` | Si el participante llegó a este escenario |
| `completed` | `true`/`false` | Si lo resolvió |
| `totalSeconds` | decimal | **Tiempo de resolución**: desde que empieza hasta que lo resuelve |
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
| `blocksGrabbed` | entero | Bloques agarrados en total |
| `blocksReleased` | entero | Bloques soltados en total |
| `errorCollisionBot` | entero | Intentos de mover el robot contra un muro o fuera del mapa |
| `errorInvalidCommand` | entero | Órdenes de "usar" sobre una casilla donde no hay nada |
| `errorIncompleteSequence` | entero | ⚠️ **Siempre vale 0** (ver limitaciones) |

## Variables por intento

Cada vez que el niño pulsa el botón de ejecutar se guarda un registro completo:

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `sequence` | lista de textos | **La secuencia exacta que montó**, en lenguaje natural |
| `solved` | 0/1 | Si ese intento resolvió el reto |
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
acciones: el niño tiene que deducir qué falta. Los botones
**alternan** entre seleccionado y no seleccionado, y el módulo se repara cuando el conjunto
elegido coincide **exactamente** con el correcto: ni de menos ni de más. Se puede reintentar
sin límite.

La dificultad vive en los datos, no en el código:

| Dificultad | Opciones | Correctas |
|---|---|---|
| Básica | 2 | 1 |
| Intermedia | 4 | 2 |

Cada distractor es una acción **correcta en otro módulo** —motores trata de combustible y
lubricación, energía de electricidad, enfriamiento de temperatura—, así que no se puede
acertar reconociendo el texto de la opción: hay que leer el problema y descartar. *"Agregar
agua"* aparece en los tres módulos y solo es correcta en uno.

## Variables del escenario

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `wrongSelections` | entero | Cuántas veces **marcó** una acción incorrecta, en todo el escenario |

> Solo cuenta el acto de **marcar** algo incorrecto. Desmarcarlo después no suma otro error:
> es la corrección, no una equivocación nueva.

## Variables por pulsación

Se guarda **cada pulsación**, tanto al marcar como al desmarcar:

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `module` | texto | En qué módulo: `"motores"`, `"generadores"`, `"enfriamiento"` |
| `option` | texto | **Qué acción pulsó**, con su texto literal |
| `selected` | 0/1 | `1` = la marcó · `0` = la desmarcó |
| `correct` | 0/1 | Si esa acción formaba parte de la solución |
| `timestamp` | fecha | Cuándo |

**Por qué se registran también las deselecciones.** Desmarcar una acción es un acto de
autocorrección: el niño la puso, la miró junto a las demás y decidió que no tocaba. Sin el
campo `selected` ese momento sería indistinguible de no haber tocado nunca esa opción, y es
justo la evidencia de razonamiento condicional que interesa medir.

**Utilidad para el diseño pedagógico:** el escenario está construido sobre *fading worked
examples*. Como se guarda el orden y el módulo de cada pulsación, se puede comprobar si los
errores **disminuyen del primer módulo al tercero**, que es exactamente la predicción del
modelo. También permite ver si hay distractores concretos que confunden sistemáticamente y,
gracias a `selected`, distinguir a quien duda y se corrige de quien acierta a la primera.

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
| `blocksGrabbed` / `blocksReleased` | entero | Manipulación de bloques **y de fichas de tipo**. En básica, como la fila está bloqueada, son casi solo cambios de ficha |
| `errorInvalidCommand` | entero | Recoger de una pila vacía, soltar en el lado equivocado, o intentar ejecutar sin ficha |
| `errorCollisionBot`, `errorIncompleteSequence` | entero | No se usan en este escenario. Siempre 0 |

Soltar un objeto en el lado equivocado **no se consuma**: el objeto vuelve a su pila y solo se
registra el error. Así el niño ve la consecuencia sin tener que rescatar el objeto.

## Variables por intento

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `itemType` | texto | **Con qué ficha se ejecutó**: `"Barril"` o `"Caja"` |
| `sequence` | lista de textos | **Las instrucciones del bucle, en orden** |
| `repetitions` | entero | **Cuántas repeticiones eligió** |
| `solved` | 0/1 | 1 solo en la ejecución que completó el escenario |
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
| `wrongPlacements` | entero | Colocaciones incorrectas |
| `socket` | texto | En qué hueco intentó colocar |
| `chip` | texto | Qué figura colocó |
| `correct` | 0/1 | Si encajaba |
| `chipsGrabbed` / `chipsReleased` | entero | Cuántas veces cogió y soltó una ficha: la señal de duda y tanteo |

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
| **Tasa de error por módulo** | Agrupar `selections` por `module` | Validar el efecto del *fading* (Esc. 2) |
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

1. **`errorIncompleteSequence` siempre vale 0.** Está declarado pero nunca se registra: no
   hemos acordado una definición operativa que distinga "faltaron instrucciones" de "el orden
   estaba mal". **Es una decisión pedagógica pendiente**, y si os interesa esa distinción hay
   que definirla antes de recoger datos.

2. **No hay identificación nominal, y el `pin` aún no se asigna.** Todas las sesiones salen
   con `"0000"` y solo las distingue `sessionId`. El emparejamiento con encuestas u otros
   instrumentos depende de un registro externo que lleve el supervisor.

3. **`blocksGrabbed` / `blocksReleased` miden manipulación, no colocaciones válidas.** Incluyen
   agarrar un bloque y volver a dejarlo sin usarlo. Sirven como indicador de exploración o de
   dificultad motriz, no de decisiones lógicas.

4. **No se registra el estado "abandonado".** Si un participante no termina, queda
   `completed: 0` pero no hay un campo que distinga *se acabó el tiempo* de *lo dejó*.

5. **No se registran las pausas.** Quitarse el visor un momento no queda reflejado, así que
   `totalSeconds` incluye ese tiempo.

6. **No hay vídeo, audio, mirada ni posición del jugador.** Solo eventos lógicos.

---

# Ejemplo de archivo real

```json
{
    "pin": "0004",
    "sessionId": 187,
    "difficulty": 1,
    "startedUtc": "2026-08-26T09:12:03.4410000Z",
    "endedUtc": "2026-08-26T09:26:44.8820000Z",

    "escenario1": {
        "started": true,
        "completed": true,
        "totalSeconds": 149.8,
        "failedAttempts": 1,
        "blocksGrabbed": 9,
        "blocksReleased": 9,
        "errorCollisionBot": 1,
        "errorIncompleteSequence": 0,
        "errorInvalidCommand": 1,
        "attempts": [
            {
                "difficulty": 1,
                "sequence": ["Avanzar", "Girar Derecha"],
                "solved": 0,
                "durationSeconds": 21.4,
                "timestamp": "2026-08-26T09:13:41.0000000Z"
            },
            {
                "difficulty": 1,
                "sequence": ["Avanzar", "Girar Derecha", "Avanzar 2", "Usar"],
                "solved": 1,
                "durationSeconds": 38.2,
                "timestamp": "2026-08-26T09:14:32.0000000Z"
            }
        ]
    },

    "escenario2": {
        "started": true,
        "completed": true,
        "totalSeconds": 84.2,
        "wrongSelections": 1,
        "selections": [
            { "module": "motores", "option": "Agregar agua",     "selected": 1, "correct": 0, "timestamp": "..." },
            { "module": "motores", "option": "Agregar agua",     "selected": 0, "correct": 0, "timestamp": "..." },
            { "module": "motores", "option": "Agregar gasolina", "selected": 1, "correct": 1, "timestamp": "..." },
            { "module": "motores", "option": "Agregar aceite",   "selected": 1, "correct": 1, "timestamp": "..." }
        ]
    },

    "escenario3": {
        "started": true,
        "completed": true,
        "totalSeconds": 121.7,
        "failedAttempts": 1,
        "errorInvalidCommand": 3,
        "attempts": [
            { "itemType": "Caja",   "repetitions": 7, "solved": 0, "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." },
            { "itemType": "Caja",   "repetitions": 3, "solved": 0, "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." },
            { "itemType": "Barril", "repetitions": 3, "solved": 1, "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"], "timestamp": "..." }
        ]
    },

    "escenario4": {
        "started": true,
        "completed": true,
        "totalSeconds": 73.9,
        "wrongPlacements": 1,
        "chipsGrabbed": 6,
        "chipsReleased": 6,
        "placements": [
            { "socket": "glifo_04", "chip": "glifo_04", "correct": 1, "timestamp": "..." },
            { "socket": "glifo_11", "chip": "glifo_12", "correct": 0, "timestamp": "..." },
            { "socket": "glifo_11", "chip": "glifo_11", "correct": 1, "timestamp": "..." }
        ]
    }
}
```

En el Escenario 4 se lee de un vistazo lo que mide el escenario: confundió `glifo_12` con
`glifo_11` y acertó al segundo intento. Ese par concreto es lo que interesa, porque señala qué
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
- **Tiempo entre intentos**, para separar el tiempo de reflexión del de manipulación
