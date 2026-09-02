# Variables de telemetría — Codea VR 2

> Documento para el equipo de evaluación. Describe **qué registra el juego** en cada escenario
> y qué permite observar cada variable.
>
> Estado verificado contra el código el **26/08/2026**.

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
| 3 — Bucles | ✅ Completo | 🟡 Montado en escena, **pendiente de probar** |
| 4 — Patrones | ✅ Completo | ❌ **No. Falta montarlo en escena** |

Las variables de los escenarios 2, 3 y 4 **están implementadas y probadas en código**, pero
todavía no se ha jugado ni una sola sesión con ellos. Son un compromiso firme de qué se va a
recoger, no datos disponibles hoy.

---

## Cómo llegan los datos

- Un **archivo JSON por participante**, nombrado `{pin}_{sessionId}.json`
- Se guarda en el visor y se conserva siempre, aunque no haya red
- El supervisor lo envía al servidor con un botón dedicado (no es automático)
- Todas las marcas de tiempo son **ISO-8601 en UTC**
- Los booleanos se guardan como **0 / 1** para facilitar el volcado a tabla

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

> `pin` **no es un nombre de usuario**. El SRS contempla pedir un username al jugador, pero
> eso todavía no está implementado. Hoy la identificación depende de que el supervisor anote
> qué PIN corresponde a qué participante.

## Variables comunes de cada escenario

Presentes en los cuatro.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `started` | 0/1 | Si el participante llegó a este escenario |
| `completed` | 0/1 | Si lo resolvió |
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

Tres módulos averiados de la nave. Cada uno muestra un problema (*"Falta combustible"*) y
ofrece opciones (*"Agregar agua"* / *"Agregar combustible"*). Si falla puede reintentar sin
límite.

## Variables del escenario

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `wrongSelections` | entero | Total de elecciones incorrectas en todo el escenario |

## Variables por elección

Se guarda **cada pulsación**, acertada o no:

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `module` | texto | En qué módulo: `"motores"`, `"generadores"`, `"enfriamiento"` |
| `option` | texto | **Qué opción eligió**, con su texto literal |
| `correct` | 0/1 | Si era la correcta |
| `timestamp` | fecha | Cuándo |

**Utilidad para el diseño pedagógico:** el escenario está construido sobre *fading worked
examples*. Como se guarda el orden y el módulo de cada elección, se puede comprobar si los
errores **disminuyen del primer módulo al tercero**, que es exactamente la predicción del
modelo. También permite ver si hay distractores concretos que confunden sistemáticamente.

---

# Escenario 3 — Bucles

**Pilares de diseño:** reconocimiento de patrones, abstracción.

El niño programa un brazo robótico con un bloque **Repetir** que envuelve un ciclo del tipo
"Recoger, Girar, Soltar, Girar". Hay pocos bloques sueltos, así que resolverlo sin el bucle
no es viable.

- **Básica:** solo define cuántas repeticiones.
- **Intermedia:** además tiene que ordenar bien las instrucciones dentro del bucle.

## Variables del escenario

Las mismas que el Escenario 1 (`resets`, `blocksGrabbed`, `blocksReleased`, contadores de
error), porque también se resuelve manipulando bloques.

## Variables por intento

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `sequence` | lista de textos | **Las instrucciones del bucle, en orden.** La fila entera es el ciclo que se repite |
| `repetitions` | entero | **Cuántas repeticiones eligió** |
| `solved` | 0/1 | Si resolvió |
| `durationSeconds` | decimal | Tiempo de preparación del intento |
| `timestamp` | fecha | Cuándo |

**`repetitions` y `sequence` son las dos variables clave del escenario**, y miden cosas
distintas:

- `repetitions` → reconocimiento de patrones: ¿identificó *cuántas veces* se repite el ciclo?
- `sequence` → abstracción: ¿identificó *cuál* es la unidad que se repite, y en qué orden?

Un niño puede acertar una y fallar la otra, y eso es información pedagógica valiosa. La
secuencia de `repetitions` a lo largo de los intentos también revela si converge por
aproximación sistemática o probando al azar.

---

# Escenario 4 — Patrones *(incluido por completitud)*

**Pilares de diseño:** reconocimiento de patrones, abstracción.

Fichas con figuras geométricas que encajan en los huecos de un panel.

| Variable | Tipo | Qué permite observar |
|---|---|---|
| `matchMode` | texto | `"forma"` (básica) o `"lados"` (intermedia) |
| `wrongPlacements` | entero | Colocaciones incorrectas |
| `socket` | texto | En qué hueco intentó colocar |
| `chip` | texto | Qué figura colocó |
| `chipSides` | entero | Lados de la figura que colocó |
| `expectedSides` | entero | Lados que esperaba el hueco |
| `correct` | 0/1 | Si encajaba |

Se guardan **los lados de ambos lados de la comparación**, no solo el acierto: en modo
`"lados"` eso permite reconstruir **qué figuras consideró equivalentes** el niño, que es más
informativo que un simple acierto/fallo.

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

2. **No hay identificación nominal.** Solo `pin`. El emparejamiento con encuestas u otros
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
            { "module": "motores", "option": "Agregar agua",        "correct": 0, "timestamp": "..." },
            { "module": "motores", "option": "Agregar combustible", "correct": 1, "timestamp": "..." }
        ]
    },

    "escenario3": {
        "started": true,
        "completed": true,
        "totalSeconds": 96.4,
        "resets": 1,
        "attempts": [
            {
                "sequence": ["Repetir x3"],
                "loopBody": ["Recoger", "Girar Derecha", "Soltar", "Girar Izquierda"],
                "repetitions": 3,
                "solved": 1,
                "timestamp": "..."
            }
        ]
    },

    "escenario4": {}
}
```

---

# Si necesitáis algo más

Añadir una variable nueva es barato **antes** de empezar a recoger datos y caro después,
porque obligaría a migrar los archivos ya recogidos. Si al diseñar el instrumento detectáis
que falta algo, es mejor decirlo ahora.

Dos cosas que ya sabemos que se pueden añadir con poco esfuerzo si os sirven:

- **Marca de abandono** con su motivo (tiempo agotado / reinicio del supervisor)
- **Tiempo entre intentos**, para separar el tiempo de reflexión del de manipulación
