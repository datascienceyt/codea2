const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, HeadingLevel, AlignmentType,
  WidthType, ShadingType, BorderStyle, LevelFormat, PageBreak, Footer, PageNumber,
  TableOfContents,
} = require("docx");

const FONT = "Arial";
const WIDTH = 9638; // A4 con márgenes de 2 cm
const ACCENT = "1F3864";
const LIGHT = "DCE3F0";

// --- helpers ---
const p = (text, opts = {}) => new Paragraph({
  spacing: { after: 120 },
  ...opts,
  children: runs(text),
});

function runs(text) {
  // **negrita** en línea
  return String(text).split(/(\*\*[^*]+\*\*)/).filter(Boolean).map(t =>
    t.startsWith("**") ? new TextRun({ text: t.slice(2, -2), bold: true }) : new TextRun(t));
}

const h1 = t => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] });
const h2 = t => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const h3 = t => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(t)] });
const bullet = t => new Paragraph({ numbering: { reference: "bullets", level: 0 }, spacing: { after: 60 }, children: runs(t) });
const num = (t, ref = "steps") => new Paragraph({ numbering: { reference: ref, level: 0 }, spacing: { after: 60 }, children: runs(t) });

const border = { style: BorderStyle.SINGLE, size: 4, color: "B4B4B4" };
const borders = { top: border, bottom: border, left: border, right: border };

function table(header, rows, widths) {
  const total = widths.reduce((a, b) => a + b, 0);
  const cell = (text, w, head) => new TableCell({
    borders,
    width: { size: w, type: WidthType.DXA },
    shading: head ? { fill: LIGHT, type: ShadingType.CLEAR } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: [new Paragraph({ children: head ? [new TextRun({ text, bold: true })] : runs(text) })],
  });
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: [
      new TableRow({ tableHeader: true, children: header.map((t, i) => cell(t, widths[i], true)) }),
      ...rows.map(r => new TableRow({ children: r.map((t, i) => cell(t, widths[i], false)) })),
    ],
  });
}

const kv = rows => table(["Campo", "Valor"], rows, [2600, WIDTH - 2600]);
const gap = () => new Paragraph({ spacing: { after: 80 }, children: [] });

// --- contenido ---
const cover = [
  new Paragraph({ spacing: { before: 2400, after: 240 }, alignment: AlignmentType.CENTER,
    children: [new TextRun({ text: "GAME DESIGN DOCUMENT", bold: true, size: 44, color: ACCENT })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 480 },
    children: [new TextRun({ text: "Codea VR 2", bold: true, size: 64 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 },
    children: [new TextRun({ text: "Escape room educativo en realidad virtual para desarrollar pensamiento computacional", size: 26 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1200 },
    children: [new TextRun({ text: "Proyecto CODEA 2 · Universidad Yachay Tech", size: 24, color: "595959" })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Versión 2.0 · 30 de septiembre de 2026", size: 22 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Desarrollo: Ing. Víctor Echeverría · Dirección: Ph.D. Erick Cuenca", size: 22 })] }),
  new Paragraph({ children: [new PageBreak()] }),
  new Paragraph({ spacing: { after: 240 }, children: [new TextRun({ text: "Contenido", bold: true, size: 32, color: ACCENT })] }),
  new TableOfContents("Contenido", { hyperlink: true, headingStyleRange: "1-2" }),
  new Paragraph({ children: [new PageBreak()] }),
];

const ficha = [
  h1("1. Ficha del juego"),
  kv([
    ["Nombre", "Codea VR 2"],
    ["Género", "Puzles VR – Escape room educativo"],
    ["Público", "Niñas, niños y adolescentes de 8 a 17 años, de contexto rural en Ecuador, mayoritariamente sin experiencia previa en VR"],
    ["Objetivo pedagógico", "Desarrollar pensamiento computacional: secuencialidad, condicionales, bucles con parámetros y reconocimiento de patrones"],
    ["Modalidad", "Un solo jugador · primera persona · sin avatar visible · con supervisor presente durante toda la sesión"],
    ["Duración de una sesión", "5 minutos por sala, con cuenta atrás visible; si se agotan, se pasa a la siguiente sala"],
    ["Dificultades", "Básica e intermedia. La fija el supervisor antes de empezar y no cambia durante la sesión"],
  ]),
  gap(),
  h2("Elementos de juego"),
  p("Lo que el jugador hace, de principio a fin:"),
  bullet("**Programar sin teclado:** agarrar bloques físicos de instrucciones y encajarlos en una fila, en orden."),
  bullet("**Ejecutar y observar:** pulsar un botón y ver a un robot o a un brazo mecánico cumplir exactamente lo programado, incluidos los errores."),
  bullet("**Diagnosticar:** leer síntomas de un sistema averiado y deducir qué acciones lo reparan."),
  bullet("**Comparar con detalle:** encontrar, entre figuras muy parecidas, la idéntica a la que pide cada hueco."),
  bullet("**Escapar contra reloj:** cuatro salas en secuencia, cinco minutos para cada una."),
];

const tecnico = [
  h1("2. Especificaciones técnicas"),
  kv([
    ["Forma", "3D, entorno inmersivo de realidad virtual"],
    ["Vista", "Primera persona (VR)"],
    ["Plataforma", "Horizon OS"],
    ["Dispositivo", "Meta Quest 3S, exclusivamente"],
    ["Motor", "Unity 6000.4.1f1, URP + Shader Graph"],
    ["SDK", "Meta XR All-in-One SDK 203.0.2"],
    ["Lenguaje", "C#"],
    ["Entrada", "Mandos Meta Quest; compatibilidad con manos en curso"],
    ["Locomoción", "Teletransporte. El guion además traslada al jugador entre salas"],
    ["Conectividad", "100 % funcional sin conexión. La subida de datos al servidor es opcional"],
    ["Rendimiento objetivo", "60 FPS el 90 % del tiempo"],
  ]),
];

const jugabilidad = [
  h1("3. Jugabilidad"),
  p("El jugador es un astronauta a bordo de una nave que ha atravesado una tormenta espacial. La tormenta desordenó los sistemas y dejó pequeñas fallas por toda la nave. Guiado por una voz narradora y ayudado por Roki, el robot de mantenimiento, recorre cuatro salas y resuelve en cada una un problema de programación para que la nave pueda continuar su viaje de regreso a la Tierra."),
  p("Cada sala corresponde a un pilar distinto del pensamiento computacional. El avance es secuencial y obligatorio: resolver la sala actual es lo único que abre la siguiente. El tiempo corre desde que el jugador pulsa «Iniciar» y es visible en todas las salas."),
  h2("Esquema del juego"),
  table(["Momento", "Descripción"], [
    ["Apertura", "El supervisor, con el visor puesto, teclea el PIN del participante y elige la dificultad en la pantalla del supervisor. Después entrega el visor al estudiante."],
    ["Opciones", "No hay menú para el jugador. PIN y dificultad son decisiones del supervisor y quedan fijas para toda la sesión."],
    ["Sinopsis", "Tras la tormenta, la nave necesita cuatro reparaciones para volver a la Tierra: abrir la puerta del dormitorio, reconfigurar los sistemas de la sala de control, despejar el paso de cajas y encender los motores."],
    ["Modos", "Un único modo: sesión individual guiada, en dificultad básica o intermedia."],
    ["Elementos", "Bloques de instrucciones, fila de programa, botón de ejecutar, robot Roki, módulos de sistema, brazo robótico, fichas de tipo, contador de repeticiones, fichas de figuras y paneles con huecos."],
    ["Niveles", "Cuatro salas (sección 6), cada una en dos dificultades."],
    ["Controles", "Agarrar y soltar objetos, pulsar botones y teletransportarse (sección 5.7)."],
    ["Victoria", "Resolver las cuatro salas, cada una antes de que se agoten sus cinco minutos."],
    ["Derrota", "Se agota el tiempo. El narrador lo anuncia con un mensaje positivo y la sesión se cierra."],
    ["Fin", "En ambos casos se guardan los datos, se intentan subir al servidor y el visor vuelve a la pantalla del supervisor para el siguiente participante."],
    ["¿Por qué es divertido?", "El jugador ve sus propias instrucciones hacerse realidad, incluidos sus errores, que resultan evidentes y a menudo cómicos. Puede reintentar sin castigo, y la historia y el reloj dan un propósito a cada reto."],
  ], [2400, WIDTH - 2400]),
];

const features = [
  h1("4. Características clave"),
  bullet("**Programación física:** los bloques se agarran y se encajan con las manos. No hay texto que teclear ni menús."),
  bullet("**Errores que se ven:** el programa se ejecuta tal cual. Si falta un giro, Roki choca; si sobra una repetición, el brazo intenta recoger de una pila vacía."),
  bullet("**Reintento sin castigo:** los intentos son ilimitados. Tras un fallo el reto se rearma solo, sin que el niño tenga que pulsar nada."),
  bullet("**Dos dificultades con la misma historia:** la intermedia pide más razonamiento con los mismos escenarios y el mismo guion, adaptado."),
  bullet("**Narración con voz y texto a la vez:** el texto aparece completo mientras suena la voz, para quien todavía lee con dificultad."),
  bullet("**Medición integrada:** cada intento, error y manipulación queda registrado para el equipo evaluador, sin interrumpir el juego."),
  bullet("**Funciona sin conexión:** pensado para escuelas rurales sin red estable."),
];

const diseno = [
  h1("5. Documento de diseño"),
  h2("5.1 Pautas de diseño"),
  bullet("**Vocabulario de un niño de ocho años** en enunciados y opciones: tanque, luces, botón, vapor, rechinar."),
  bullet("**Icono y texto juntos** en cada pieza, porque parte del público todavía lee con dificultad."),
  bullet("**Una sola idea nueva por sala.** Cada sala introduce un concepto y lo practica, sin mezclar dos a la vez."),
  bullet("**Ejemplos resueltos que se desvanecen:** en básica parte de la solución viene dada; en intermedia el niño la construye."),
  bullet("**El error es información, no castigo:** frases de ánimo del narrador y reintento automático."),
  bullet("**Sin mareo:** locomoción por teletransporte y traslados entre salas con fundido."),
  bullet("**Alcance acotado:** ejecución estrictamente secuencial, sin bloques anidados. El único bucle es la fila entera."),
  h2("5.2 Definiciones de juego"),
  table(["Concepto", "Definición"], [
    ["Ganar", "Completar cada sala dentro de sus cinco minutos."],
    ["Perder", "Agotar el tiempo con alguna sala sin resolver."],
    ["Transición", "Al resolver una sala se abre el paso y el guion traslada al jugador a la siguiente."],
    ["Intento", "Cada vez que se pulsa Ejecutar con un programa válido (salas 1 y 3), o cada vez que se encaja una ficha (sala 4)."],
    ["Error", "Chocar, ordenar una acción imposible, marcar una opción incorrecta o encajar una figura equivocada. Se señala, se registra y no penaliza."],
    ["Foco", "Razonar antes de ejecutar: leer, planificar, probar y corregir."],
  ], [2400, WIDTH - 2400]),
  gap(),
  h2("5.3 Diagrama de flujo"),
  num("**Pantalla del supervisor:** PIN y dificultad → Empezar."),
  num("**Introducción:** bienvenida, contexto de la tormenta y el reloj. El jugador pulsa «Iniciar»."),
  num("**Sala 1 · Dormitorio (secuencialidad):** programar a Roki hasta el botón verde → la puerta se abre."),
  num("**Sala 2 · Sala de control (condicionales):** reparar tres sistemas."),
  num("**Sala 3 · Almacén (bucles y parámetros):** clasificar las cajas con el brazo robótico."),
  num("**Sala 4 · Panel final (patrones):** completar el panel de figuras → los motores arrancan."),
  num("**Fin:** «¡Misión cumplida!», o «El tiempo ha terminado» si el reloj llega a cero en cualquier momento."),
  num("**Cierre:** se guardan y suben los datos, y el visor vuelve a la pantalla del supervisor."),
  h2("5.4 Definición del jugador"),
  p("El jugador no tiene salud, armas ni inventario. Su única acción sobre el mundo es manipular objetos y pulsar botones. No hay forma de morir ni de quedarse bloqueado: cualquier error se puede deshacer o se deshace solo."),
  h2("5.5 Propiedades del jugador"),
  table(["Propiedad", "Cómo cambia", "Retroalimentación"], [
    ["Tiempo restante", "Baja desde 05:00 al empezar cada sala", "Reloj visible en todas las salas"],
    ["Sala actual", "Avanza al resolver la sala", "Narración, apertura de puertas, traslado"],
    ["Progreso dentro de la sala", "Sube con cada acierto parcial", "Módulo en verde, objetos en su sitio, hueco iluminado"],
  ], [2600, 3200, WIDTH - 5800]),
  gap(),
  h2("5.6 Recompensas y retroalimentación"),
  bullet("Frase de felicitación del narrador al terminar cada sala."),
  bullet("Cambios visibles en el mundo: la puerta se abre, los módulos pasan a verde, las cajas despejan el paso, los motores suenan."),
  bullet("En la sala 4 cada hueco resuelto se ilumina en verde y queda fijo."),
  bullet("Ante un error, una frase breve de ánimo, sin interrumpir lo que el narrador esté diciendo."),
  h2("5.7 Interfaz y controles"),
  p("Toda la interfaz es **diegética**: forma parte del mundo, no flota delante de los ojos."),
  table(["Elemento", "Dónde", "Función"], [
    ["Pantalla de narración", "Se desplaza a la sala activa", "Texto de la voz narradora"],
    ["Reloj", "Cinco pantallas repartidas por la nave", "Tiempo restante"],
    ["Pantallas de módulo", "Sala 2", "Título del sistema, síntomas y estado"],
    ["Contador de repeticiones", "Sala 3", "Número de veces que se repite la fila, con botones + y −"],
    ["Pantalla del supervisor", "Escena inicial", "Teclado de PIN, dificultad y botón Empezar"],
  ], [2800, 3000, WIDTH - 5800]),
  gap(),
  table(["Acción", "Con mandos", "Con manos"], [
    ["Agarrar / soltar", "Botón de agarre", "Cerrar y abrir la mano"],
    ["Pulsar botón", "Tocar el botón con el mando", "Tocar el botón con el dedo"],
    ["Moverse", "Teletransporte", "En curso"],
  ], [2800, 3400, WIDTH - 6200]),
];

const salas = [
  h1("6. Salas"),

  h2("6.1 Sala 1 — Dormitorio · Secuencialidad"),
  p("**Objetivo:** la puerta del dormitorio está cerrada. Hay que guiar a Roki por una cuadrícula hasta el botón verde y pulsarlo."),
  p("**Mecánica:** el jugador ordena bloques en la fila y pulsa Ejecutar. Roki cumple las instrucciones una a una. Si choca o la secuencia termina sin llegar, el nivel se reinicia solo y el jugador puede corregir."),
  p("**Bloques:** Avanzar · Avanzar 2 · Girar Izquierda · Girar Derecha · Usar."),
  table(["", "Básica", "Intermedia"], [
    ["Tablero", "6 × 5", "6 × 5 (misma sala)"],
    ["Solución", "8 bloques", "11 bloques, por arriba"],
    ["Bloques disponibles", "8, todos necesarios", "13: los de la solución y 2 de sobra"],
    ["Huecos en la fila", "8", "11"],
    ["Dificultad añadida", "—", "El botón solo se pulsa de frente. El tablero es simétrico: por abajo también se llega, pero hacen falta 14 bloques y la fila solo tiene 11. Hay que planificar el camino corto antes de colocar nada"],
  ], [2400, 3200, WIDTH - 5600]),
  gap(),

  h2("6.2 Sala 2 — Sala de control · Condicionales"),
  p("**Objetivo:** tres sistemas averiados —Motores, Energía y Enfriamiento— necesitan la acción adecuada para volver a funcionar."),
  p("**Mecánica:** cada módulo muestra un título grande y describe **síntomas, no la causa**: «los motores hacen ruido pero no arrancan». El jugador marca y desmarca opciones, y el módulo se repara cuando lo marcado coincide exactamente con lo correcto."),
  p("**Diseño de distractores:** cada opción incorrecta es la acción correcta de otro módulo. No se puede acertar reconociendo palabras: hay que leer el síntoma y descartar."),
  table(["", "Básica", "Intermedia"], [
    ["Opciones por módulo", "4", "4"],
    ["Opciones correctas", "1", "2 (un síntoma por cada una)"],
  ], [2400, 3200, WIDTH - 5600]),
  gap(),

  h2("6.3 Sala 3 — Almacén · Bucles y parámetros"),
  p("**Objetivo:** unas cajas bloquean el paso. Hay que usar el brazo robótico para colocar cada una en su lugar según su color."),
  p("**Mecánica:** la fila de instrucciones es el bucle y se repite tantas veces como marque el contador. Aparte hay un hueco para una **ficha de color** (celeste o roja) que indica al brazo qué tipo de caja mover: es el parámetro del programa. El mismo programa sirve para los dos colores cambiando solo la ficha, así que hacen falta al menos dos ejecuciones."),
  p("**Reglas de apoyo:** al poner una ficha las repeticiones vuelven a 0 y el programa no se ejecuta hasta elegir cuántas. Sin ficha tampoco se ejecuta. Si una secuencia recoge una caja y no la suelta, al terminar la caja vuelve sola a su pila."),
  table(["", "Básica", "Intermedia"], [
    ["Fila de instrucciones", "Dada y fija: Recoger · Girar al destino · Soltar · Volver", "El jugador ordena Recoger · Girar Izquierda · Soltar · Girar Derecha"],
    ["Lo que decide el jugador", "Ficha de color y repeticiones", "Orden de los bloques, ficha y repeticiones"],
  ], [2400, 3200, WIDTH - 5600]),
  gap(),

  h2("6.4 Sala 4 — Panel final · Patrones"),
  p("**Objetivo:** encender la nave completando un panel de figuras."),
  p("**Mecánica:** cada hueco del panel dibuja una figura abstracta. El jugador busca entre las fichas la **idéntica** y la encaja. Hay fichas muy parecidas y más fichas que huecos, para que el último hueco no se resuelva por descarte. Una ficha correcta se ilumina en verde y queda fija. Una incorrecta se queda un instante en el hueco y sale disparada hacia el jugador con el sonido de desconexión, así el error se ve y la ficha queda a mano para otro intento. Las fichas tienen física: se apoyan en la mesa y, si caen al suelo, vuelven solas a los pocos segundos. Al reiniciar, solo vuelven a su sitio las fichas que no están bien colocadas. Al completar el panel suena el arranque de los motores."),
  table(["", "Básica", "Intermedia"], [
    ["Figuras", "Juego de figuras más simples", "Juego de figuras más densas y parecidas entre sí"],
    ["Mecánica", "Igual", "Igual"],
  ], [2400, 3200, WIDTH - 5600]),
];

const guion = [
  ["1", "Inicio", "¡Bienvenido, astronauta! Tu misión está a punto de comenzar."],
  ["2", "Inicio", "Una tormenta espacial desordenó algunas cosas y provocó pequeñas fallas. Necesitamos tu ayuda para que la nave continúe su viaje."],
  ["3", "Inicio", "Tienes cinco minutos para reparar cada sistema. Si se acaba el tiempo, pasaremos al siguiente. ¡Adelante, astronauta!"],
  ["4", "Sala 1", "La puerta de tu habitación está cerrada. Para abrirla, debemos presionar el botón verde que está al otro lado de la habitación."],
  ["5", "Sala 1", "Roki, el robot de mantenimiento te ayudará. Puedes verlo a la derecha. Guíalo hasta el botón verde."],
  ["6", "Sala 1", "Ordena los bloques de la mesa para crear su recorrido. Cuando termines, inicia la secuencia."],
  ["7", "Sala 1 · fin", "¡Muy bien, astronauta! Roki llegó al botón y la puerta está abierta. Puedes continuar."],
  ["8", "Sala 2", "Has llegado a la sala de control. La tormenta dañó la configuración de varios sistemas."],
  ["9.1", "Sala 2 · básica", "Cada sistema necesita un elemento para volver a funcionar. Observa las opciones y elige el correcto para cada uno."],
  ["9.2", "Sala 2 · intermedia", "Cada sistema necesita varios elementos para volver a funcionar. Observa las opciones y selecciona todos los elementos correctos para cada uno."],
  ["10", "Sala 2 · fin", "¡Excelente elección! Los sistemas vuelven a funcionar correctamente. La nave está cada vez más cerca de continuar su viaje."],
  ["11", "Sala 3", "Estas cajas están bloqueando el camino. Usa el brazo robótico para colocarlas en el lugar correcto."],
  ["12", "Sala 3", "Fíjate en el color de las cajas y observa dónde debe ir cada una."],
  ["13", "Sala 3", "Puedes ver dos fichas de colores celeste y rojo, elige una y colócala en la casilla. Así, el brazo sabrá qué debe mover."],
  ["14.1", "Sala 3 · básica", "Cuenta las cajas de ese color e indica cuántas veces debe repetir la acción. Después, haz lo mismo con el otro color."],
  ["14.2", "Sala 3 · intermedia", "Ordena los bloques de acción para indicarle qué debe hacer con las cajas de ese color. Después, indica cuántas veces debe repetir la secuencia. Cuando termines, haz lo mismo con el otro color."],
  ["15", "Sala 3 · fin", "¡Muy bien, astronauta! Todas las cajas están en su lugar y el camino quedó despejado."],
  ["16", "Sala 4", "Llegamos a la última sala. En esta sala te encargarás de encender la nave. Presta atención a cómo se hace."],
  ["17", "Sala 4", "Observa las figuras del panel y arrastra cada bloque hasta el espacio que tenga la misma figura."],
  ["18", "Sala 4 · fin", "¿Escuchas eso, astronauta? ¡Los motores están encendidos y todos los sistemas funcionan correctamente!"],
  ["19", "Final", "¡Misión cumplida! La nave puede continuar su viaje de regreso a la Tierra. ¡Excelente trabajo!"],
  ["20", "Tiempo agotado en la última sala", "¡Oh, no! El tiempo ha terminado. No logramos completar la misión, pero hiciste un gran trabajo hasta aquí. ¡Gracias por intentarlo!"],
  ["21", "Tiempo agotado en una sala", "¡Se acabó el tiempo en esta sala! No te preocupes, astronauta: sigamos con la siguiente."],
];

const narrativa = [
  h1("7. Narrativa y guion"),
  p("Una voz narradora acompaña toda la sesión. Cada frase suena y a la vez aparece completa en la pantalla de narración. Las líneas 9 y 14 tienen una variante por dificultad; el resto es común. Además hay un banco de cinco frases breves de ánimo que suenan al cometer un error, sin hacer avanzar la historia."),
  table(["ID", "Momento", "Texto"], guion, [800, 2000, WIDTH - 2800]),
];

const dificultad = [
  h1("8. Resumen de dificultades"),
  table(["Sala", "Básica", "Intermedia"], [
    ["1 · Secuencialidad", "8 bloques exactos, sin sobrantes", "Camino de 11 bloques con 13 disponibles; el camino simétrico de abajo no cabe en la fila"],
    ["2 · Condicionales", "4 opciones, 1 correcta", "4 opciones, 2 correctas"],
    ["3 · Bucles y parámetros", "Fila dada; decide ficha y repeticiones", "Ordena la fila; decide ficha y repeticiones"],
    ["4 · Patrones", "Figuras simples", "Figuras densas y parecidas"],
  ], [2400, 3400, WIDTH - 5800]),
];

const datos = [
  h1("9. Registro de datos"),
  p("Cada sesión genera un archivo JSON en el visor, nombrado con el PIN, un número de sesión y el código del visor. Se escribe durante la partida, de modo que un cierre inesperado no pierde lo ya registrado. Al terminar se intenta subir al servidor del proyecto; sin conexión, el archivo se queda en el visor."),
  table(["Sala", "Qué se registra"], [
    ["Todas", "Inicio, fin, si se completó y el tiempo empleado"],
    ["1 y 3", "Cada intento con su secuencia de bloques, intentos fallidos, piezas agarradas y encajadas, y errores (choques y órdenes imposibles)"],
    ["3", "Además, las repeticiones elegidas y el color con que se ejecutó cada intento"],
    ["2", "Cada opción marcada, si era correcta y el tiempo desde la anterior"],
    ["4", "Cada figura intentada en cada hueco, si encajaba, el tiempo desde la anterior y las fichas agarradas"],
  ], [1800, WIDTH - 1800]),
  gap(),
  p("El detalle de cada variable está en el documento «Variables de telemetría» del repositorio."),
  h1("10. Fuera de alcance"),
  bullet("Bloques anidados y condicionales dentro del programa: la ejecución es estrictamente secuencial."),
  bullet("Ajuste automático de la dificultad según el desempeño, propuesto como trabajo futuro."),
  bullet("Multijugador e identificación nominal: el participante se identifica solo por PIN."),
];

const doc = new Document({
  creator: "Víctor Echeverría",
  title: "Codea VR 2 — Game Design Document",
  styles: {
    default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, font: FONT, color: ACCENT },
        paragraph: { spacing: { before: 360, after: 180 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, font: FONT, color: ACCENT },
        paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1 } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 23, bold: true, font: FONT },
        paragraph: { spacing: { before: 180, after: 100 }, outlineLevel: 2 } },
    ],
  },
  numbering: {
    config: [
      { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 540, hanging: 270 } } } }] },
      { reference: "steps", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 540, hanging: 360 } } } }] },
    ],
  },
  features: { updateFields: true },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
      children: [new TextRun({ text: "Codea VR 2 · GDD · página ", size: 18, color: "7F7F7F" }),
                 new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "7F7F7F" })] })] }) },
    children: [...cover, ...ficha, ...tecnico, ...jugabilidad, ...features, ...diseno, ...salas, ...narrativa, ...dificultad, ...datos],
  }],
});

Packer.toBuffer(doc).then(b => { fs.writeFileSync(process.argv[2], b); console.log("ok", b.length); });
