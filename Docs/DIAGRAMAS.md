# Diagramas del proyecto — Codea VR 2

> Estado verificado contra el código el **26/08/2026**.
>
> **Convención:** borde continuo = montado y funcionando. **Borde discontinuo = código
> completo pero sin montar en escena.**

Para renderizar:

```bash
dot -Tpng diagrama.dot -o diagrama.png
dot -Tsvg diagrama.dot -o diagrama.svg
```

O pegar el código en <https://dreampuf.github.io/GraphvizOnline/>.

---

## 1. Flujo de sesión

*Qué ocurre desde que se enciende el visor hasta que los datos llegan al servidor.*

```dot
digraph flujo_sesion {
  rankdir=TB;
  bgcolor="white";
  compound=true;
  node [shape=box, style="rounded,filled", fontname="Helvetica", fontsize=11,
        margin="0.22,0.14", color="#94a3b8", fillcolor="#f1f5f9"];
  edge [fontname="Helvetica", fontsize=9, color="#64748b"];

  arranque [label="Arranque de la aplicación", fillcolor="#e2e8f0"];

  subgraph cluster_seleccion {
    label="Escena: Seleccion";
    fontname="Helvetica"; fontsize=11; color="#94a3b8"; style="dashed";
    selector [label="DifficultySelector\nel supervisor elige\nbásica o intermedia",
              fillcolor="#dbeafe", color="#60a5fa", style="rounded,filled,dashed"];
  }

  subgraph cluster_juego {
    label="Escena: Basica  |  Intermedia";
    fontname="Helvetica"; fontsize=11; color="#94a3b8"; style="dashed";
    guardia [label="DifficultyScene\nverifica que la dificultad\nregistrada coincide",
             fillcolor="#dbeafe", color="#60a5fa", style="rounded,filled,dashed"];
    director [label="Director\nrecorre escenarios → pasos", fillcolor="#ede9fe", color="#a78bfa"];
  }

  telemetria [label="TelemetryManager\nsobrevive al cambio de escena\nabre la run y el archivo",
              fillcolor="#fef3c7", color="#fbbf24"];

  e1 [label="Escenario 1\nSecuencialidad", fillcolor="#dcfce7", color="#4ade80"];
  e2 [label="Escenario 2\nCondicionales", fillcolor="#dcfce7", color="#4ade80", style="rounded,filled,dashed"];
  e3 [label="Escenario 3\nBucles", fillcolor="#dcfce7", color="#4ade80", style="rounded,filled,dashed"];
  e4 [label="Escenario 4\nPatrones", fillcolor="#dcfce7", color="#4ade80", style="rounded,filled,dashed"];

  fin [label="Fin de sesión", fillcolor="#e2e8f0"];
  json [label="{pin}_{sessionId}.json\nen el visor", shape=note, fillcolor="#fef3c7", color="#fbbf24"];
  push [label="Botón del supervisor\nsubida manual", fillcolor="#fef3c7", color="#fbbf24"];
  servidor [label="Flask en Raspberry Pi\ncsv.penginexr.com", shape=cylinder, fillcolor="#e2e8f0"];

  arranque -> telemetria [label="Awake: BeginRun()"];
  arranque -> selector;
  selector -> guardia [label="SetDifficulty + LoadScene"];
  guardia -> director [label="Play()"];

  director -> e1 [label="Step bloqueante"];
  e1 -> e2 -> e3 -> e4 [style=dashed, label="siguiente"];
  e4 -> fin;

  telemetria -> json [label="escribe en cada evento"];
  fin -> push;
  json -> push [style=dashed];
  push -> servidor [label="HTTP POST"];

  {rank=same; telemetria; director}
}
```

---

## 2. Mapa de subsistemas

*Qué módulos existen y de quién depende cada uno.*

```dot
digraph subsistemas {
  rankdir=LR;
  bgcolor="white";
  node [shape=box, style="filled", fontname="Helvetica", fontsize=10,
        margin="0.16,0.09", color="#94a3b8", fillcolor="#f8fafc"];
  edge [fontname="Helvetica", fontsize=8, color="#94a3b8"];

  subgraph cluster_narrativa {
    label="Narrativa";
    fontname="Helvetica"; color="#a78bfa"; style="rounded"; bgcolor="#faf5ff";
    Director; IStepAction [shape=hexagon, fillcolor="#ede9fe"];
    Narrator; Fader; Timer; Teleporter; Waiter;
  }

  subgraph cluster_bloques {
    label="Sistema de bloques  (compartido por escenarios 1, 3 y 4)";
    fontname="Helvetica"; color="#4ade80"; style="rounded"; bgcolor="#f0fdf4";
    BlockNode [shape=hexagon, fillcolor="#dcfce7"];
    Socket; SocketRow; ProgramRunner; ProgramTrigger; BlockResetter; VRGrabEvents;
  }

  subgraph cluster_e1 {
    label="Escenario 1 — Secuencialidad";
    fontname="Helvetica"; color="#60a5fa"; style="rounded"; bgcolor="#eff6ff";
    GridBlock; Bot; LevelManager; LevelLoader; Exit; Scenario1Controller;
  }

  subgraph cluster_e2 {
    label="Escenario 2 — Condicionales";
    fontname="Helvetica"; color="#60a5fa"; style="rounded,dashed"; bgcolor="#eff6ff";
    ModuleData; SystemModule; ModuleOptionButton; Scenario2Controller;
  }

  subgraph cluster_e3 {
    label="Escenario 3 — Bucles";
    fontname="Helvetica"; color="#60a5fa"; style="rounded,dashed"; bgcolor="#eff6ff";
    RepeatBlock; ArmBlock; RoboticArm; ArmSlot; Scenario3Controller;
  }

  subgraph cluster_e4 {
    label="Escenario 4 — Patrones";
    fontname="Helvetica"; color="#60a5fa"; style="rounded,dashed"; bgcolor="#eff6ff";
    ShapeData; ShapeChip; ShapeSocket; Scenario4Controller;
  }

  subgraph cluster_telemetria {
    label="Telemetría";
    fontname="Helvetica"; color="#fbbf24"; style="rounded"; bgcolor="#fffbeb";
    TelemetryManager; TelemetryData; CsvUploader;
  }

  subgraph cluster_sesion {
    label="Sesión";
    fontname="Helvetica"; color="#fbbf24"; style="rounded,dashed"; bgcolor="#fffbeb";
    DifficultySelector; DifficultyScene;
  }

  BlockNode -> VRGrabEvents [label="agarre"];
  BlockNode -> Socket [label="snap"];
  SocketRow -> Socket [label="genera"];
  ProgramTrigger -> ProgramRunner;
  ProgramRunner -> BlockNode [label="Execute()"];
  BlockResetter -> BlockNode [label="devuelve a su sitio"];

  GridBlock -> BlockNode [style=dashed, label="hereda"];
  ArmBlock -> BlockNode [style=dashed, label="hereda"];
  RepeatBlock -> BlockNode [style=dashed, label="hereda"];
  ShapeChip -> BlockNode [style=dashed, label="hereda"];

  GridBlock -> Bot;
  ArmBlock -> RoboticArm;
  RoboticArm -> ArmSlot;
  RepeatBlock -> ProgramRunner [label="sub-cadena"];
  ShapeSocket -> Socket [label="valida"];
  SystemModule -> ModuleData;
  ShapeChip -> ShapeData;

  Bot -> LevelManager;
  LevelLoader -> LevelManager;
  Exit -> LevelManager;
  Scenario1Controller -> LevelManager;

  Scenario1Controller -> IStepAction [style=dashed];
  Scenario2Controller -> IStepAction [style=dashed];
  Scenario3Controller -> IStepAction [style=dashed];
  Scenario4Controller -> IStepAction [style=dashed];
  Director -> IStepAction [label="espera"];

  ProgramTrigger -> TelemetryManager [color="#fbbf24"];
  Scenario1Controller -> TelemetryManager [color="#fbbf24"];
  Scenario2Controller -> TelemetryManager [color="#fbbf24"];
  Scenario3Controller -> TelemetryManager [color="#fbbf24"];
  Scenario4Controller -> TelemetryManager [color="#fbbf24"];
  TelemetryManager -> TelemetryData;
  CsvUploader -> TelemetryManager;
  DifficultySelector -> TelemetryManager [color="#fbbf24"];
}
```

---

## 3. Sistema de bloques en ejecución

*El mecanismo central. Lo comparten los escenarios 1, 3 y 4.*

```dot
digraph bloques {
  rankdir=TB;
  bgcolor="white";
  node [shape=box, style="rounded,filled", fontname="Helvetica", fontsize=10,
        margin="0.2,0.12", color="#94a3b8", fillcolor="#f8fafc"];
  edge [fontname="Helvetica", fontsize=9, color="#64748b"];

  subgraph cluster_manipulacion {
    label="Manipulación";
    fontname="Helvetica"; color="#4ade80"; style="rounded"; bgcolor="#f0fdf4";

    agarra [label="El jugador agarra una pieza", shape=oval, fillcolor="#dcfce7"];
    ongrab [label="BlockNode.OnGrabbed\n· libera su socket\n· se desparenta\n· cuenta agarre"];
    busca  [label="Update\nbusca el socket libre más cercano\n(registro Socket.Active)"];
    suelta [label="El jugador suelta", shape=oval, fillcolor="#dcfce7"];
    valida [label="¿Socket vacío y no es\nhijo de sí mismo?", shape=diamond,
            fillcolor="#fef9c3", color="#facc15"];
    attach [label="AttachTo\ncopia pose y se acopla"];
    libre  [label="Se queda suelto", fillcolor="#fee2e2", color="#f87171"];
  }

  ocupado [label="Socket.OnOccupied", shape=oval, fillcolor="#e0f2fe", color="#38bdf8"];
  shapesocket [label="ShapeSocket\nvalida figura o nº de lados\n(solo Escenario 4)",
               style="rounded,filled,dashed", fillcolor="#eff6ff", color="#60a5fa"];

  subgraph cluster_ejecucion {
    label="Ejecución";
    fontname="Helvetica"; color="#60a5fa"; style="rounded"; bgcolor="#eff6ff";

    play   [label="El jugador pulsa Ejecutar", shape=oval, fillcolor="#dbeafe"];
    trigger [label="ProgramTrigger\nregistra el intento ANTES de ejecutar"];
    chain  [label="ProgramRunner.ExecuteChain\nrecorre Socket.Next\nhasta el primer hueco vacío"];
    exec   [label="BlockNode.Execute()", shape=diamond, fillcolor="#fef9c3", color="#facc15"];
  }

  grid   [label="GridBlock → Bot\navanzar / girar / usar", fillcolor="#dcfce7", color="#4ade80"];
  arm    [label="ArmBlock → RoboticArm\nrecoger / soltar / girar", fillcolor="#dcfce7",
          color="#4ade80", style="rounded,filled,dashed"];
  repeat [label="RepeatBlock\nitera su sub-cadena N veces", fillcolor="#fef3c7",
          color="#fbbf24", style="rounded,filled,dashed"];
  chip   [label="ShapeChip\nno ejecuta nada", fillcolor="#e2e8f0", style="rounded,filled,dashed"];

  meta [label="Meta alcanzada", shape=oval, fillcolor="#dcfce7"];
  completa [label="ScenarioXController\nCompleteChallenge()", fillcolor="#fef3c7", color="#fbbf24"];

  reinicio [label="Botón Reiniciar", shape=oval, fillcolor="#fee2e2", color="#f87171"];
  resetter [label="BlockResetter\ndevuelve las piezas a su sitio\nsin crear ni destruir nada"];

  agarra -> ongrab -> busca -> suelta -> valida;
  valida -> attach [label="sí"];
  valida -> libre  [label="no"];
  attach -> ocupado;
  ocupado -> shapesocket [style=dashed];

  play -> trigger -> chain -> exec;
  exec -> grid;
  exec -> arm;
  exec -> repeat;
  exec -> chip;

  repeat -> chain [label="recursión\n(máx. 4 niveles)", color="#f59e0b", constraint=false];

  grid -> meta;
  arm  -> meta;
  meta -> completa;

  reinicio -> resetter;
  resetter -> agarra [style=dotted, constraint=false];
}
```

---

## 4. Telemetría — modelo de datos

*Cómo se organiza el JSON y por qué los escenarios no comparten estructura.*

```dot
digraph modelo_datos {
  rankdir=TB;
  bgcolor="white";
  node [shape=box, style="filled", fontname="Courier", fontsize=10,
        margin="0.18,0.1", color="#fbbf24", fillcolor="#fffbeb"];
  edge [fontname="Helvetica", fontsize=9, color="#94a3b8"];

  Run [label="RunRecord   (raíz del archivo)\l\lpin\lsessionId\ldifficulty  1=básica  2=intermedia\lstartedUtc\lendedUtc\l",
       fillcolor="#fef3c7"];

  Base [label="ScenarioRecord   (base de todos)\l\lstarted\lcompleted\ltotalSeconds\lstartedUtc\lendedUtc\l",
        fillcolor="#f1f5f9", color="#94a3b8"];

  Bloques [label="BlockScenarioRecord   (esc. 1 y 3)\l\lresets\lblocksGrabbed\lblocksReleased\lerrorCollisionBot\lerrorIncompleteSequence\lerrorInvalidCommand\l",
           fillcolor="#f0fdf4", color="#4ade80"];

  E1 [label="Scenario1Record\l\lattempts[]\l", fillcolor="#eff6ff", color="#60a5fa"];
  E2 [label="Scenario2Record\l\lwrongSelections\lselections[]\l", fillcolor="#eff6ff", color="#60a5fa"];
  E3 [label="Scenario3Record\l\lattempts[]\l", fillcolor="#eff6ff", color="#60a5fa"];
  E4 [label="Scenario4Record\l\lmatchMode\lwrongPlacements\lplacements[]\l", fillcolor="#eff6ff", color="#60a5fa"];

  A1 [label="AttemptRecord\l\lsequence[]\lsolved  0/1\ltimestamp\l", fillcolor="#faf5ff", color="#a78bfa"];
  A3 [label="LoopAttemptRecord\l\lsequence[]\lloopBody[]\lrepetitions\lsolved  0/1\ltimestamp\l",
      fillcolor="#faf5ff", color="#a78bfa"];
  S2 [label="SelectionRecord\l\lmodule\loption\lcorrect  0/1\ltimestamp\l", fillcolor="#faf5ff", color="#a78bfa"];
  P4 [label="PlacementRecord\l\lsocket\lchip\lchipSides\lexpectedSides\lcorrect  0/1\ltimestamp\l",
      fillcolor="#faf5ff", color="#a78bfa"];

  Base -> Bloques [label="hereda", style=dashed];
  Bloques -> E1 [label="hereda", style=dashed];
  Bloques -> E3 [label="hereda", style=dashed];
  Base -> E2 [label="hereda", style=dashed];
  Base -> E4 [label="hereda", style=dashed];

  Run -> E1 [label="escenario1"];
  Run -> E2 [label="escenario2"];
  Run -> E3 [label="escenario3"];
  Run -> E4 [label="escenario4"];

  E1 -> A1 [label="uno por ejecución"];
  E3 -> A3 [label="uno por ejecución"];
  E2 -> S2 [label="uno por pulsación"];
  E4 -> P4 [label="uno por ficha"];
}
```

**La idea:** todos los escenarios tienen inicio, fin y duración → eso va en la base. Los
escenarios 1 y 3 se resuelven con bloques → comparten reinicios y contadores. Pero el 2 se
juega pulsando botones y el 4 encajando fichas: **no comparten métricas con nadie**, y por
eso cada uno tiene su propia lista.

---

## 5. Telemetría — captura y persistencia

*Quién registra qué, y cómo acaba en el servidor.*

```dot
digraph captura {
  rankdir=LR;
  bgcolor="white";
  node [shape=box, style="rounded,filled", fontname="Helvetica", fontsize=10,
        margin="0.18,0.1", color="#94a3b8", fillcolor="#f8fafc"];
  edge [fontname="Helvetica", fontsize=8, color="#64748b"];

  subgraph cluster_fuentes {
    label="Puntos de captura";
    fontname="Helvetica"; color="#4ade80"; style="rounded"; bgcolor="#f0fdf4";

    f1 [label="ProgramTrigger\nal pulsar Ejecutar"];
    f2 [label="BlockNode\nagarrar / soltar"];
    f3 [label="LevelManager\nmovimiento inválido"];
    f4 [label="Bot.Use\nusar sobre nada"];
    f5 [label="Botón Reiniciar"];
    f6 [label="Scenario2Controller\nelección en un módulo",
        style="rounded,filled,dashed"];
    f7 [label="Scenario4Controller\nficha colocada",
        style="rounded,filled,dashed"];
    f8 [label="ScenarioXController\ninicio y fin de escenario"];
  }

  TM [label="TelemetryManager", shape=box3d, fillcolor="#fef3c7", color="#fbbf24"];

  inmediato [label="Escritura inmediata\nintentos, inicio/fin,\nreinicios, selecciones",
             fillcolor="#dcfce7", color="#4ade80"];
  diferido  [label="Escritura diferida\nagarres y errores\n(cada saveInterval)",
             fillcolor="#fef9c3", color="#facc15"];

  archivo [label="{pin}_{sessionId}.json\nen el visor", shape=note,
           fillcolor="#fef3c7", color="#fbbf24"];
  flush [label="Flush()", shape=oval, fillcolor="#e0f2fe", color="#38bdf8"];
  uploader [label="CsvUploader\nHTTP POST + reintentos"];
  servidor [label="Flask · Raspberry Pi", shape=cylinder, fillcolor="#e2e8f0"];
  adb [label="Extracción por USB/ADB\nsiempre disponible", shape=note, fillcolor="#f1f5f9"];

  f1 -> TM [label="RegisterBlockAttempt"];
  f2 -> TM [label="RegisterBlockGrabbed\nRegisterBlockReleased"];
  f3 -> TM [label="RegisterLogicError"];
  f4 -> TM [label="RegisterLogicError"];
  f5 -> TM [label="RegisterReset"];
  f6 -> TM [label="RegisterSelection"];
  f7 -> TM [label="RegisterPlacement"];
  f8 -> TM [label="StartChallenge\nCompleteChallenge"];

  TM -> inmediato;
  TM -> diferido [label="MarkDirty"];
  inmediato -> archivo;
  diferido -> archivo [label="al vencer el plazo"];

  archivo -> adb [style=dashed];
  flush -> archivo [label="vuelca lo pendiente"];
  uploader -> flush [label="antes de leer"];
  archivo -> uploader;
  uploader -> servidor;
}
```

**Por qué dos velocidades de escritura:** los agarres y los errores son de alta frecuencia, y
escribir el archivo entero en cada choque del robot provocaba tirones en el visor. Los
intentos y los cambios de estado sí se escriben al instante, así que un cierre brusco nunca
puede costar un intento.

**Por qué `Flush()` antes de subir:** el uploader lee el archivo del disco. Sin vaciar antes
lo pendiente, subiría una versión desactualizada.
