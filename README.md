# Codea VR 2

**CODEA 2: Fortalecimiento de Entornos Inmersivos con Realidad Virtual e Inteligencia Artificial en Educación**

Escape room educativo en realidad virtual para el desarrollo de pensamiento computacional en niños y adolescentes (8–17 años), desarrollado para visores **Meta Quest 3S**.

Proyecto con respaldo institucional de **Yachay Tech**, dirigido a estudiantes de unidades educativas de sectores rurales del Ecuador.

---

## Tabla de contenidos

- [Descripción](#descripción)
- [Contexto pedagógico](#contexto-pedagógico)
- [Estructura de la experiencia](#estructura-de-la-experiencia)
- [Arquitectura técnica](#arquitectura-técnica)
- [Sistema de telemetría](#sistema-de-telemetría)
- [Requisitos de software](#requisitos-de-software)
- [Requisitos técnicos del entorno](#requisitos-técnicos-del-entorno)
- [Evolución previsible del sistema](#evolución-previsible-del-sistema)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Equipo](#equipo)
- [Licencia](#licencia)

---

## Descripción

El jugador despierta solo en una nave espacial dañada tras el impacto de un meteorito. Para llegar a su cápsula de escape debe atravesar **4 escenarios**, resolviendo en cada uno un problema mediante **programación por bloques**. Cada escenario corresponde a un pilar distinto de pensamiento computacional:

| Escenario | Categoría | Pilar(es) de CT | Mecánica |
|---|---|---|---|
| 1 — Dormitorio | Secuencialidad | Diseño de algoritmos, Descomposición, Abstracción | Ordenar bloques de instrucciones para que el robot uemy-26 controle un incendio y abra la puerta |
| 2 — Sala de sistemas | Condicionales | Reconocimiento de patrones, Diseño de algoritmos | Elegir la opción correcta (ej. "Gasolina" / "Agua") ante un problema mostrado en un panel (ej. "Falta combustible") |
| 3 — Almacén | Bucles | Reconocimiento de patrones, Abstracción | Programar un brazo robótico con un bloque Repetir para despejar barriles y cajas |
| 4 — Panel final | Patrones | Reconocimiento de patrones, Abstracción | Insertar chips con figuras geométricas en el socket correcto, por forma o por número de lados |

Cada escenario tiene **dos niveles de dificultad** (básica / intermedia) para adaptarse a la amplia diferencia de edad del público objetivo.

## Contexto pedagógico

- Público: estudiantes de 8 a 17 años, mayoritariamente sin experiencia previa en VR ni en mandos espaciales, de contexto rural.
- Sesiones limitadas a bloques de 15–20 minutos, con supervisor presente en todo momento.
- Diseño basado en **fading worked examples** (ejemplos resueltos que se desvanecen progresivamente) para introducir condicionales, y en mecánicas de clasificación por atributo para introducir reconocimiento de patrones (Escenario 4).
- Restricción de scope: lógica estrictamente secuencial en la ejecución de bloques (sin anidado real más allá del bloque Repetir), locomoción por teletransporte, sin hand tracking.

## Estructura de la experiencia

```
Selección de dificultad (supervisor)
        │
        ▼
Ingreso de username (jugador)
        │
        ▼
   Escenario 1: Secuencialidad
        │ (desbloquea)
        ▼
   Escenario 2: Condicionales
        │ (desbloquea)
        ▼
   Escenario 3: Bucles
        │ (desbloquea)
        ▼
   Escenario 4: Patrones
        │
        ▼
   Fin de sesión
```

El supervisor fija la dificultad antes de entregar el visor al estudiante; una vez seleccionada, queda fija para los 4 escenarios de la sesión y no es modificable por el jugador. El avance entre escenarios es secuencial y obligatorio: completar el escenario activo (en cualquier dificultad) es el único disparador para desbloquear el siguiente.

## Arquitectura técnica

- **Motor:** Unity 6, C#
- **SDK:** Meta XR All-in-One SDK
- **Render pipeline:** URP + Shader Graph
- **Sistema de bloques (Escenarios 1, 3 y 4):**
  - `BlockNode` (abstracto) — base común: agarre (`VRGrabEvents`) y acople por proximidad contra `Socket`
  - `Socket` / `SocketRow` — huecos encadenados por `Next`, generados en runtime entre dos puntos
  - `ProgramRunner` — recorre la cadena ejecutando cada bloque; `Run(inicio, repeticiones)`
  - `BlockResetter` — devuelve los bloques a su sitio sin instanciar ni destruir nada
  - `GridBlock` — acciones del robot por enum (Escenario 1)
  - `VRConsole` — consola de errores in-headset
- **Sistema de selección (Escenario 2):** módulos independientes con problema y opciones, sin cadena de bloques. El contenido vive en assets `ModuleData`, así que se puede variar por dificultad sin duplicar objetos en la escena.
- **Bucles (Escenario 3):** no hay bloque contenedor — **la fila entera es el bucle**. `SocketRow` expone las repeticiones y un flag de edición que mapea sobre las dos dificultades: en básica la secuencia viene dada y el niño solo ajusta N; en intermedia además la ordena.
- **Emparejamiento por atributo (Escenario 4):** `ShapeChip` hereda de `BlockNode`, así que reutiliza agarre, acople y reinicio. Cada ficha se valida contra un único socket comparando la figura (básico) o su número de lados (intermedio).
- **Narrativa:** `Director` orquesta escenarios → pasos, con acciones que bloquean el paso hasta terminar (`IStepAction`). `Narrator` reproduce la voz y escribe el texto en pantalla a la vez, emparejados por ID contra un CSV, y espera a la más larga de las dos.

## Sistema de telemetría

Pipeline de persistencia local con exportación remota manual:

1. **`TelemetryManager`** (singleton persistente) captura métricas de sesión e interacción por escenario (RF-03, RF-04).
2. Genera un **JSON individual por participante**, nombrado `{pin}_{sessionId}.json` en `Application.persistentDataPath`. Se escribe durante la partida, no al cerrarla: un cierre inesperado del visor no se lleva los datos ya registrados.
3. El respaldo local está siempre disponible vía USB/ADB, independientemente del estado de la red.
4. La exportación remota (HTTP POST al servidor Flask en `csv.penginexr.com`, vía Raspberry Pi + Cloudflare Tunnel) **no es automática**: la activa un botón exclusivo del supervisor. Si la conexión falla, reintenta y avisa sin bloquear la experiencia ni borrar el respaldo local.

Cada escenario tiene su **propia estructura de datos**, porque no miden lo mismo: el 1 y el 3 registran la secuencia de bloques montada en cada intento, el 2 las opciones elegidas en cada módulo y el 4 qué figura se intentó encajar en qué hueco. Los escenarios 1 y 3 comparten además los contadores de manipulación de bloques y de errores de lógica.

```json
"escenario1": {
    "started": true, "completed": true, "totalSeconds": 111.6,
    "failedAttempts": 3, "blocksGrabbed": 12, "blocksReleased": 12,
    "attempts": [
        { "difficulty": 1,
          "sequence": ["Avanzar", "Girar Derecha", "Avanzar 2", "Usar"],
          "solved": 1, "durationSeconds": 7.2, "timestamp": "..." }
    ]
}
```

El detalle de cada variable y su lectura pedagógica está en [`Docs/VARIABLES_TELEMETRIA.md`](Docs/VARIABLES_TELEMETRIA.md).

## Requisitos de software

Documento formal bajo estándar **IEEE 830** (`Codea2_SRS.pdf`, dentro del Informe Técnico Mes 1), con:

- 5 interfaces (RI-01 a RI-05)
- 8 requisitos funcionales (RF-01 a RF-08)
- 4 requisitos no funcionales (RNF-01 a RNF-04)

Puntos críticos abiertos en el SRS:

- Taxonomía de errores lógicos (RF-04): `secuencia_incompleta` sigue sin definición operativa que distinga "faltaron instrucciones" de "el orden estaba mal".
- Identificación del participante: el SRS pide **username** (RF-01) y el sistema funciona hoy con **PIN** asignado por el supervisor.

## Estado del desarrollo

| Subsistema | Estado |
|---|---|
| Escenario 1 — Secuencialidad | Funcional, verificado en visor |
| Escenario 2 — Condicionales | Montado en escena, pendiente de prueba |
| Escenario 3 — Bucles | Montado en escena; mecánica del brazo sin validar |
| Escenario 4 — Patrones | Código completo, sin montar |
| Sistema narrativo | Funcional |
| Telemetría JSON + subida | Funcional de punta a punta |
| Selección de dificultad | Código completo, sin montar |
| HUD diegético (RI-02), username (RI-01), reinicio supervisado (RF-08) | Sin implementar |

**La documentación técnica completa está en [`Context.md`](Context.md)**: arquitectura,
decisiones de diseño, API de cada componente, estado de avance y trampas conocidas. Es la
fuente de verdad para desarrollo.

Para el equipo evaluador, el detalle de qué mide cada variable está en
[`Docs/VARIABLES_TELEMETRIA.md`](Docs/VARIABLES_TELEMETRIA.md).

## Requisitos técnicos del entorno

- Unity `6000.4.1f1`
- Meta XR All-in-One SDK `203.0.2` o superior
- Visor: Meta Quest 3S (plataforma exclusiva, sin soporte para otros headsets)
- Mandos físicos únicamente (sin hand tracking)
- Horizon OS actualizado a su versión más reciente disponible, para garantizar compatibilidad con el SDK

## Evolución previsible del sistema

Mejoras contempladas en la arquitectura para fases futuras, fuera del alcance actual:

- **Sincronización en la nube:** migración del almacenamiento offline (.csv local) a un sistema híbrido en tiempo real mediante base de datos en la nube (ej. Firebase o PostgreSQL vía API REST), activado automáticamente al detectar conexión estable.
- **Generación dinámica de escenarios:** ajuste automático de dificultad en tiempo de ejecución según el desempeño del usuario (tiempo invertido, tasa de errores).
- **Panel de visualización docente (dashboard web):** plataforma externa para cargar los CSV recolectados y generar reportes visuales del desempeño en pensamiento computacional.

## Estructura del repositorio

Repositorio oficial: [github.com/datascienceyt/codea2](https://github.com/datascienceyt/codea2/tree/dev) (rama `dev`)

```
Assets/
├── _Main/                  # Contenido propio del proyecto
│   ├── 3D Models/
│   ├── CustomMaterials/
│   ├── Editor/
│   ├── Levels/             # Definiciones de nivel por escenario/dificultad
│   ├── Prefabs/
│   ├── Scripts/
│   │   ├── Block Programming System/   # BlockNode, Socket, SocketRow, ProgramRunner…
│   │   ├── Grid Level/                 # Bot, LevelManager, LevelLoader (Escenario 1)
│   │   ├── Scenario 2/                 # Módulos de la sala de sistemas
│   │   ├── Scenario 3/                 # Brazo robótico y posiciones
│   │   ├── Scenario 4/                 # Fichas y figuras
│   │   ├── Session/                    # Selección de dificultad
│   │   ├── Story/                      # Director, Narrator, Fader, Timer…
│   │   └── Telemetry/
│   └── Sounds/
├── _Recovery/               # Carpeta de recuperación (Unity)
├── Docs/
│   ├── Investigaciones/
│   ├── Codea2_GDD.docx
│   ├── Codea2_SRS.docx
│   ├── Codea2_SRS.pdf
│   ├── Informe-Mes1.docx
│   └── croquis.png
├── Oculus/                  # Integración Meta/Oculus
├── Plugins/
├── Resources/
├── Scenes/
├── Settings/
├── StreamingAssets/
├── XR/                      # Configuración XR
└── InputSystem_Actions.inputactions
```

> Servidor Flask de telemetría (`csv.penginexr.com`): no versionado, corre directamente en el Raspberry Pi del homelab.

## Equipo

| Nombre | Rol |
|---|---|
| Ing. Víctor Echeverría | Técnico Especialista (Desarrollador principal) |
| Ph.D. Erick Cuenca | Director del Proyecto |
| Gabriela Cajamarca | Evaluadora técnica |
| Rolando Armas | Evaluador técnico |

## Licencia

Uso interno — restringido a Yachay Tech.
