# Codea VR 2

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
- [Estructura del repositorio](#estructura-del-repositorio)
- [Equipo](#equipo)
- [Licencia](#licencia)

---

## Descripción

El jugador despierta solo en una nave espacial dañada tras el impacto de un meteorito. Para llegar a su cápsula de escape debe atravesar 3 salas, resolviendo en cada una un problema mediante **programación por bloques**. Cada sala corresponde a un pilar distinto de pensamiento computacional:

| Sala | Habilidad | Mecánica |
|---|---|---|
| 1 — Dormitorio | Secuenciación | Ordenar bloques de instrucciones para que un robot (uemy-26) rescate al jugador |
| 2 — Sala de sistemas | Condicionales | Insertar chips físicos `SI [condición] → [acción]` en paneles de temperatura |
| 3 — Almacén | Bucles | Programar un brazo robótico para repetir un ciclo de manipulación de objetos |

Cada reto tiene **dos versiones de dificultad** (básica / avanzada) para adaptarse a la amplia diferencia de edad del público objetivo.

## Contexto pedagógico

- Público: estudiantes de 8 a 17 años, mayoritariamente sin experiencia previa en VR ni en mandos espaciales, de contexto rural.
- Sesiones de 15–20 minutos, con supervisor presente en todo momento.
- Diseño de puzzles basado en **faded worked examples** (ejemplos resueltos que se desvanecen progresivamente) para introducir condicionales, y en **tangible/embodied programming** (chips físicos insertables) como puente hacia conceptos abstractos.
- Restricción de scope: lógica estrictamente secuencial en la ejecución de bloques (sin anidado real), locomoción por teletransporte, sin hand tracking.

## Estructura de la experiencia

```
PIN + Selección de dificultad (supervisor)
        │
        ▼
   Reto 1: Secuenciación
        │ (desbloquea)
        ▼
   Reto 2: Condicionales
        │ (desbloquea)
        ▼
   Reto 3: Bucles
        │
        ▼
   Cápsula de escape / fin de sesión
```

El avance es secuencial y obligatorio: completar el reto activo (en cualquier dificultad) es el único disparador para desbloquear el siguiente.

## Arquitectura técnica

- **Motor:** Unity, C#
- **SDK:** Meta XR All-in-One SDK
- **Render pipeline:** URP + Shader Graph
- **Sistema de bloques:**
  - `BlockNode` (abstracto) — nodo base de lista enlazada con lógica de snap/grab (`VRGrabEvents.cs`)
  - `ProgramRunner` — coroutine walker que ejecuta la secuencia
  - `GridBlock` — acciones basadas en enum para movimiento en grid
  - `LevelContext` — service locator para referencias del bot entre recargas de nivel
  - `VRConsole` — consola de errores in-headset
  - `SceneManager` / `ReloadLevel()` — reinicio de nivel

## Sistema de telemetría

Pipeline end-to-end integrado vía Cloudflare Tunnel:

1. **`TelemetryManager`** (Unity, singleton) captura métricas de sesión y de interacción por reto/dificultad.
2. **`CsvUploader`** con lógica de reintento envía los datos.
3. **Servidor Flask** en Raspberry Pi, expuesto en `csv.penginexr.com`, recibe el upload.
4. Respaldo local en `.csv` (`Application.persistentDataPath`) siempre disponible vía USB/ADB, independiente del estado de red.

## Requisitos de software

Documento formal bajo estándar **IEEE 830** (`Codea2_SRS.pdf`), con:

- 5 interfaces (RI-01 a RI-05)
- 8 requisitos funcionales (RF-01 a RF-08)
- 4 requisitos no funcionales (RNF-01 a RNF-04)

Cobertura por reto documentada internamente (ver `/docs`). Puntos críticos abiertos en el SRS:

- Taxonomía de errores lógicos (RF-04) definida solo para Reto 1; falta extenderla a Reto 2 (chips) y Reto 3 (brazo robótico).
- Estructura exacta de columnas del CSV para el desglose por reto/dificultad.

## Requisitos técnicos del entorno

- Unity `6.4.1f1`
- Meta XR All-in-One SDK `203.0.2`
- Visor: Meta Quest 3S (plataforma exclusiva, sin soporte para otros headsets)
- Mandos físicos únicamente (sin hand tracking)

## Estructura del repositorio

```
Assets/
├── _Main/                  # Contenido propio del proyecto
│   ├── 3D Models/
│   ├── CustomMaterials/
│   ├── Editor/
│   ├── Levels/             # Definiciones de nivel por reto/dificultad
│   ├── Prefabs/
│   ├── Scripts/            # BlockNode, ProgramRunner, GridBlock, TelemetryManager, CsvUploader, etc.
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
| Víctor Echeverría | Desarrollador principal |
| Erick Cuenca | Responsable del proyecto |
| Gabriela Cajamarca | Evaluadora técnica |
| Rolando Armas | Evaluador técnico |

## Licencia

Uso interno — restringido a Yachay Tech.
