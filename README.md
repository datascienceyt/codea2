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
- [Estado del desarrollo](#estado-del-desarrollo)
- [Requisitos técnicos del entorno](#requisitos-técnicos-del-entorno)
- [Evolución previsible del sistema](#evolución-previsible-del-sistema)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Equipo](#equipo)
- [Licencia](#licencia)

---

## Descripción

El jugador es un astronauta a bordo de una nave que ha atravesado una tormenta espacial. La tormenta desordenó los sistemas y dejó pequeñas fallas por toda la nave. Guiado por una voz narradora y ayudado por Roki, el robot de mantenimiento, recorre **4 salas** y resuelve en cada una un problema mediante **programación por bloques**, para que la nave pueda continuar su viaje de regreso a la Tierra en menos de 15 minutos. Cada escenario corresponde a un pilar distinto de pensamiento computacional:

| Escenario | Categoría | Pilar(es) de CT | Mecánica |
|---|---|---|---|
| 1 — Dormitorio | Secuencialidad | Diseño de algoritmos, Descomposición, Abstracción | Ordenar bloques de instrucciones para guiar a Roki por una cuadrícula hasta el botón verde que abre la puerta. En intermedia hay un camino tentador que no cabe en la fila de instrucciones |
| 2 — Sala de control | Condicionales | Reconocimiento de patrones, Diseño de algoritmos | Cada módulo describe **síntomas** (*"los motores hacen ruido pero no arrancan"*) y el jugador deduce qué acciones lo reparan. Hay que marcar exactamente las correctas: ni de menos ni de más |
| 3 — Almacén | Bucles y parámetros | Reconocimiento de patrones, Abstracción | Programar un brazo robótico que **clasifica** la carga por color para despejar el paso. La fila de instrucciones es el bucle, y una ficha de color aparte decide qué tipo recoge. En intermedia, además, hay que ordenar la fila |
| 4 — Panel final | Patrones | Reconocimiento de patrones, Abstracción | Encender la nave: encontrar, entre figuras abstractas muy parecidas, la ficha **idéntica** a la que muestra cada hueco. Las fichas incorrectas salen disparadas del panel |

Cada escenario tiene **dos niveles de dificultad** (básica / intermedia) para adaptarse a la amplia diferencia de edad del público objetivo.

## Contexto pedagógico

- Público: estudiantes de 8 a 17 años, mayoritariamente sin experiencia previa en VR ni en mandos espaciales, de contexto rural.
- Sesiones limitadas a bloques de 15–20 minutos, con supervisor presente en todo momento.
- Diseño basado en **fading worked examples** (ejemplos resueltos que se desvanecen progresivamente) para introducir condicionales, en **parametrización** para los bucles —el mismo programa resuelve dos casos cambiando solo un dato— y en **discriminación visual** para el reconocimiento de patrones (Escenario 4).
- Los textos en pantalla usan vocabulario cotidiano de un niño de ocho años, y las piezas combinan siempre icono y texto: parte del público todavía lee con dificultad.
- Restricción de scope: lógica estrictamente secuencial en la ejecución de bloques (sin anidado; el único bucle es la fila entera), locomoción por teletransporte, sin hand tracking.

## Estructura de la experiencia

```
Escena Setup: PIN y dificultad (supervisor)
        │
        ▼
   Escenario 1: Secuencialidad
        │ (desbloquea)
        ▼
   Escenario 2: Condicionales
        │ (desbloquea)
        ▼
   Escenario 3: Bucles y parámetros
        │ (desbloquea)
        ▼
   Escenario 4: Patrones
        │
        ▼
   Fin de sesión y subida de datos
```

El supervisor fija la dificultad antes de entregar el visor al estudiante; una vez seleccionada, queda fija para los 4 escenarios de la sesión y no es modificable por el jugador. Cada dificultad es **una escena distinta** (`Basico.unity` / `Intermedio.unity`). El avance entre escenarios es secuencial y obligatorio: completar el escenario activo es el único disparador para desbloquear el siguiente.

## Arquitectura técnica

- **Motor:** Unity 6, C#
- **SDK:** Meta XR All-in-One SDK
- **Render pipeline:** URP + Shader Graph
- **Sistema de bloques (Escenarios 1, 3 y 4):**
  - `BlockNode` (abstracto) — base común: agarre (`VRGrabEvents`) y acople por proximidad contra `Socket`
  - `Socket` / `SocketRow` — huecos encadenados por `Next`, generados en runtime entre dos puntos. El bloqueo de edición se aplica socket a socket
  - `ProgramRunner` — recorre la cadena ejecutando cada bloque; `Run(inicio, repeticiones)`
  - `ProgramTrigger` — el botón de ejecutar. Registra el intento **antes** de correr y consulta las `IRunPrecondition`, que pueden vetar la ejecución
  - `BlockResetter` — devuelve los bloques a su sitio sin instanciar ni destruir nada
  - `GridBlock` — acciones del robot por enum (Escenario 1)
  - `VRConsole` — consola de errores in-headset
- **Sistema de selección (Escenario 2):** módulos independientes con problema y opciones, sin cadena de bloques. El contenido vive en assets `ModuleData`, así que se puede variar por dificultad sin duplicar objetos en la escena. Una opción incorrecta dispara una frase de error del narrador.
- **Bucles y parámetros (Escenario 3):** no hay bloque contenedor — **la fila entera es el bucle**. Aparte hay un `ArmTypeSocket` donde va una ficha (barril o caja): es el argumento del programa. En básica la fila viene hecha y el propio bloque *"Girar al destino"* resuelve el sentido; en intermedia el niño ordena giros explícitos. Hacen falta al menos dos ejecuciones, una por tipo.
- **Discriminación visual (Escenario 4):** `ShapeChip` hereda de `BlockNode`, así que reutiliza agarre, acople y reinicio. La figura **es** el sprite: ficha y hueco encajan si llevan el mismo recorte del pliego de figuras. Sobran fichas a propósito, para que el último hueco no se resuelva por descarte.
- **Narrativa:** `Director` orquesta escenarios → pasos, con acciones que bloquean el paso hasta terminar (`IStepAction`). `Narrator` reproduce la voz y escribe el texto en pantalla a la vez, emparejados por ID contra un CSV, y espera a la más larga de las dos. Tiene además un banco de frases de error que suenan sin avanzar la historia.
- **Temporizador:** un único `Timer` de sesión (RF-06) que escribe en varias pantallas `TimerDisplay`, una por sala y otra en la muñeca del jugador.

## Sistema de telemetría

Pipeline de persistencia local con exportación remota manual:

1. **`TelemetryManager`** (uno por escena) captura métricas de sesión e interacción por escenario (RF-03, RF-04).
2. Genera un **JSON individual por participante**, nombrado `{pin}_{sessionId}.json` en `Application.persistentDataPath`. Se escribe durante la partida, no al cerrarla: un cierre inesperado del visor no se lleva los datos ya registrados.
3. El respaldo local está siempre disponible vía USB/ADB, independientemente del estado de la red.
4. La exportación remota (HTTP POST al servidor Flask en `csv.penginexr.com`, vía Raspberry Pi + Cloudflare Tunnel) se lanza sola al terminar la sesión o al agotarse el tiempo, y es **opcional**: sin red el archivo simplemente se queda en el visor. Si la conexión falla, reintenta y avisa sin bloquear la experiencia ni borrar el respaldo local.
5. Al cerrar la sesión se imprime un **informe de integridad** por escenario, que avisa de contadores sospechosamente a cero antes de dar los datos por buenos.

Cada escenario tiene su **propia estructura de datos**, porque no miden lo mismo: el 1 registra la secuencia de bloques de cada intento; el 3 además las repeticiones y **con qué tipo** se ejecutó; el 2 cada opción marcada y desmarcada; y el 4 qué figura se intentó encajar en qué hueco.

```json
"escenario3": {
    "started": true, "completed": true, "totalSeconds": 121.7,
    "failedAttempts": 1, "errorInvalidCommand": 3,
    "attempts": [
        { "itemType": "Caja",   "repetitions": 7, "solved": 0,
          "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"] },
        { "itemType": "Barril", "repetitions": 3, "solved": 1,
          "sequence": ["Recoger", "Girar al destino", "Soltar", "Volver"] }
    ]
}
```

El detalle de cada variable y su lectura pedagógica está en [`Docs/VARIABLES_TELEMETRIA.md`](Docs/VARIABLES_TELEMETRIA.md), y hay una sesión completa de ejemplo en [`Docs/ejemplo_run_telemetria.json`](Docs/ejemplo_run_telemetria.json).

## Requisitos de software

Documento formal bajo estándar **IEEE 830** (`Codea2_SRS.pdf`, dentro del Informe Técnico Mes 1), con:

- 5 interfaces (RI-01 a RI-05)
- 8 requisitos funcionales (RF-01 a RF-08)
- 4 requisitos no funcionales (RNF-01 a RNF-04)

Puntos críticos abiertos en el SRS:

- Taxonomía de errores lógicos (RF-04): `secuencia_incompleta` sigue sin definición operativa que distinga "faltaron instrucciones" de "el orden estaba mal".
- Identificación del participante: el SRS pide **username** (RF-01) y el diseño usa un **PIN** asignado por el supervisor. El supervisor lo teclea en la escena `Setup` antes de entregar el visor.

## Estado del desarrollo

| Subsistema | Estado |
|---|---|
| Escenario 1 — Secuencialidad | Funcional en las dos dificultades; básica verificada en visor |
| Escenario 2 — Condicionales | Funcional, probado en el APK (4 opciones en ambas dificultades) |
| Escenario 3 — Bucles y parámetros | Funcional, probado en el APK |
| Escenario 4 — Patrones | Funcional, probado en el APK. Falta el juego de figuras propio de intermedia |
| Sistema narrativo | Funcional, con CSV y voces nuevas por dificultad |
| Telemetría JSON + subida | Funcional de punta a punta |
| Temporizador visible en todas las salas | Implementado, con cierre de sesión al agotarse (RF-06) |
| Selección de dificultad y PIN (RF-01) | Implementado en la escena `Setup` |
| Dificultad intermedia | Escena `Intermedio` generada desde la básica con las herramientas de `Tools → Codea` |
| HUD diegético (RI-02), reinicio supervisado (RF-08) | Sin implementar |

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

- **Sincronización en la nube:** migración del almacenamiento offline (JSON local) a un sistema híbrido en tiempo real mediante base de datos en la nube (ej. Firebase o PostgreSQL vía API REST), activado automáticamente al detectar conexión estable.
- **Generación dinámica de escenarios:** ajuste automático de dificultad en tiempo de ejecución según el desempeño del usuario (tiempo invertido, tasa de errores).
- **Panel de visualización docente (dashboard web):** plataforma externa para cargar los JSON recolectados y generar reportes visuales del desempeño en pensamiento computacional.

## Estructura del repositorio

Repositorio oficial: [github.com/datascienceyt/codea2](https://github.com/datascienceyt/codea2/tree/dev) (rama `dev`)

```
Assets/
├── _Main/                  # Contenido propio del proyecto
│   ├── 2D/                 # Sprites: figuras del Escenario 4, fichas del Escenario 3, UI
│   ├── 3D Models/
│   ├── CustomMaterials/
│   ├── Editor/             # Tools → Codea (montaje de escenas) y Level Editor
│   ├── Levels/             # Niveles del Escenario 1: reto.json (básica), escenario1_intermedio.json
│   ├── Prefabs/
│   ├── Scripts/
│   │   ├── Block Programming System/   # BlockNode, Socket, SocketRow, ProgramRunner…
│   │   ├── Grid Level/                 # Bot, LevelManager, LevelLoader (Escenario 1)
│   │   ├── Scenario 2/                 # Módulos de la sala de control y sus ModuleData
│   │   ├── Scenario 3/                 # Brazo robótico, posiciones, fichas de tipo
│   │   ├── Scenario 4/                 # Fichas, huecos y pliego de figuras
│   │   ├── Session/                    # Selección de dificultad y PIN
│   │   ├── Story/                      # Director, Narrator, Fader, Timer…
│   │   └── Telemetry/                  # TelemetryManager, JSONUploader
│   ├── Narrativa.csv        # Guion: ID_Texto, texto de cada frase
│   └── Sounds/             # Voces (VoiceLines/Victor), efectos y ambiente
├── _Recovery/               # Respaldos de escena (no son las escenas activas)
├── Scenes/
│   ├── Setup.unity          # Pantalla del supervisor: PIN y dificultad (primera escena)
│   ├── Basico.unity         # Sesión completa, dificultad básica
│   └── Intermedio.unity     # Sesión completa, dificultad intermedia
├── Oculus/                  # Integración Meta/Oculus
├── Plugins/
├── Resources/
├── Settings/
├── StreamingAssets/
├── XR/                      # Configuración XR
└── InputSystem_Actions.inputactions
Build/
└── app.apk                  # APK para instalar en el visor (Git LFS). El resto de Build/ no se versiona
Docs/
├── VARIABLES_TELEMETRIA.md  # Qué mide cada variable, para el equipo evaluador
├── ejemplo_run_telemetria.json
├── JSON Samples/            # Runs reales del APK
├── Codea2_GDD.docx          # Game Design Document (30/09/2026)
├── Informe-Mes1.docx        # Informe técnico con el SRS (IEEE 830)
├── Informe-Mes1-firmado.pdf
├── Diagramas/               # Diagramas de flujo y funcionalidad, croquis
└── Investigaciones/
```

> Servidor Flask de telemetría (`csv.penginexr.com`): no versionado, corre en contenedores Docker en el Raspberry Pi del homelab (`~/Services/JSONServer`).

### Instalar en el visor

1. Clonar con Git LFS instalado (`git lfs install` una vez); si ya estaba clonado, `git lfs pull` para bajar el APK real y no un puntero de texto.
2. Activar el modo desarrollador del Quest 3S y conectarlo por USB.
3. `adb install -r Build/app.apk`
4. Abrir la app desde *Biblioteca → Orígenes desconocidos*. Arranca en la pantalla del supervisor.

Los JSON de cada sesión quedan en el visor, en `/sdcard/Android/data/<paquete>/files/`, además de subirse al servidor si hay red.

## Equipo

| Nombre | Rol |
|---|---|
| Ing. Víctor Echeverría | Técnico Especialista (Desarrollador principal) |
| Ph.D. Erick Cuenca | Director del Proyecto |
| Equipo evaluador | Evaluación técnica |

## Licencia

Uso interno — restringido a Yachay Tech.
