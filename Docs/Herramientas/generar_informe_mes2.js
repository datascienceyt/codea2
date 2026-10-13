// Genera Docs/Informe-Mes2.docx a partir de Docs/Informe-Mes1.docx: mismo formato (portada,
// estilos, cabeceras, pies, índice), con los entregables del mes 2 (2.1, 2.2 y 2.3).
//
// Uso, desde la raíz del repositorio:  node Docs/Herramientas/generar_informe_mes2.js
//
// No necesita Word: un .docx es un zip de XML. Se reutilizan del informe del mes 1 la portada,
// la hoja de firmas, el índice y las cabeceras, y el cuerpo se escribe con los mismos estilos
// (1173-1175 títulos, 1361 viñetas, 1339 pies de figura y tabla, tablas con cabecera gris).
// Las figuras salen de Docs/Entregable-Mes2/capturas (Tools → Codea → Capturas para informes).

const fs = require("fs");
const path = require("path");
const { topLevel, textOf, esc, readZip, writeZip } = require("./docx_lib.js");

// --- Datos de la portada. Cambiar aquí y volver a generar ---

const FECHA_LARGA = "13 de octubre de 2026";
const FECHA_CORTA = "13/10/2026";
const PRODUCTO = "Informe técnico de desarrollo funcional e integración del módulo inmersivo";
const CABECERA = "Desarrollo funcional e integración del módulo inmersivo";

// La fecha puede cambiar antes de entregar: va subrayada en rojo para no olvidarla.
const FECHA_PROVISIONAL = true;

const ENTREGABLES = [
  ["2.1 Implementación de funcionalidades principales",
   "Descripción y evidencia del desarrollo de las funcionalidades centrales del módulo, incluyendo navegación, interacciones, elementos educativos y lógica de funcionamiento."],
  ["2.2 Integración de recursos 3D e interacciones inmersivas",
   "Detalle de los recursos 3D incorporados, programación de interacciones, configuración de controles y ajustes para su ejecución en dispositivos Meta Quest."],
  ["2.3 Pruebas funcionales preliminares con usuarios clave",
   "Registro de pruebas realizadas con el equipo del proyecto o usuarios clave, incluyendo errores identificados, observaciones de uso, limitaciones técnicas encontradas y ajustes aplicados o priorizados para la siguiente etapa."],
];

const ROOT = path.resolve(__dirname, "..", "..");
const TEMPLATE = path.join(ROOT, "Docs", "Informe-Mes1.docx");
// Otra ruta como argumento si el informe está abierto en Word, que lo bloquea.
const OUTPUT = process.argv[2] ? path.resolve(process.argv[2]) : path.join(ROOT, "Docs", "Informe-Mes2.docx");
const CAPTURAS = path.join(ROOT, "Docs", "Entregable-Mes2", "capturas");

// --- Piezas de formato, copiadas de la plantilla ---

const FONT = '<w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:eastAsia="Arial" w:cs="Arial"/>';

function rPr({ b = false, i = false, sz = 20 } = {}) {
  return `<w:rPr>${FONT}${b ? "<w:b/><w:bCs/>" : ""}${i ? "<w:i/><w:iCs/>" : ""}<w:color w:val="000000"/><w:sz w:val="${sz}"/><w:szCs w:val="${sz}"/></w:rPr>`;
}

/** Texto con **negrita** a runs. */
function runs(text, { sz = 20, b = false } = {}) {
  return String(text).split(/(\*\*[^*]+\*\*)/).filter(s => s.length).map(part => {
    const bold = part.startsWith("**") && part.endsWith("**");
    const t = bold ? part.slice(2, -2) : part;
    return `<w:r>${rPr({ b: b || bold, sz })}<w:t xml:space="preserve">${esc(t)}</w:t></w:r>`;
  }).join("");
}

const blocks = [];      // { xml, lines } en orden, para estimar páginas del índice
const headings = [];    // { level, number, text, bookmark, block }
const media = [];       // { file, rId }
let bookmarkId = 5000, docPrId = 1000, tableNo = 0, figureNo = 0, numId = 900;
const counters = [0, 0, 0];

function add(xml, lines) { blocks.push({ xml, lines }); }

function para(text, { jc = "both", ind = 709, after = 120, sz = 20, keepNext = false } = {}) {
  add(`<w:p><w:pPr>${keepNext ? "<w:keepNext/>" : ""}<w:spacing w:after="${after}" w:line="240" w:lineRule="auto"/><w:ind w:left="${ind}"/><w:jc w:val="${jc}"/>${rPr({ sz })}</w:pPr>${runs(text, { sz })}</w:p>`,
      Math.ceil(String(text).length / 92) + 0.4);
}

function heading(level, text, { newPage = false } = {}) {
  counters[level - 1]++;
  for (let l = level; l < 3; l++) counters[l] = 0;
  const number = counters.slice(0, level).join(".");
  const bookmark = `_TocM2_${bookmarkId}`;
  const id = bookmarkId++;
  const style = ["1173", "1174", "1175"][level - 1];
  const runProps = level === 3 ? '<w:rPr><w:color w:val="000000"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr>' : "";

  headings.push({ level, number, text, bookmark, block: blocks.length });
  if (headingNumbers.has(text)) throw new Error(`Dos títulos iguales: "${text}"`);
  headingNumbers.set(text, number);
  add(`<w:p><w:pPr><w:pStyle w:val="${style}"/>${newPage ? "<w:pageBreakBefore/>" : ""}</w:pPr>` +
      `<w:bookmarkStart w:id="${id}" w:name="${bookmark}"/><w:r>${runProps}<w:t xml:space="preserve">${esc(text)}</w:t></w:r><w:bookmarkEnd w:id="${id}"/></w:p>`,
      [3.2, 2.4, 2][level - 1] + (newPage ? 1000 : 0));
}

function bullet(text) {
  add(`<w:p><w:pPr><w:pStyle w:val="1361"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="7"/></w:numPr><w:spacing w:after="60" w:line="240" w:lineRule="auto"/><w:jc w:val="both"/>${rPr()}</w:pPr>${runs(text)}</w:p>`,
      Math.ceil(String(text).length / 86) + 0.2);
}

const numberedLists = [];
function numbered(items) {
  const id = ++numId;
  numberedLists.push(id);
  for (const text of items)
    add(`<w:p><w:pPr><w:pStyle w:val="1361"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="${id}"/></w:numPr><w:spacing w:after="60" w:line="240" w:lineRule="auto"/><w:jc w:val="both"/>${rPr()}</w:pPr>${runs(text)}</w:p>`,
        Math.ceil(String(text).length / 86) + 0.3);
}

// Número de cada tabla y figura por su título. El texto las cita como {{título}} y el número se
// pone al montar el documento, así añadir una tabla no descuadra las referencias.
const captionNumbers = new Map();
const headingNumbers = new Map();

function resolveReferences(xml) {
  return xml.replace(/\{\{([^}]+)\}\}/g, (match, title) => {
    // {{§Título}} cita una sección por su título; {{Título}}, una tabla o figura.
    if (title.startsWith("§")) {
      const heading = title.slice(1);
      if (!headingNumbers.has(heading)) throw new Error(`Referencia a una sección que no existe: "${heading}"`);
      return headingNumbers.get(heading);
    }
    if (!captionNumbers.has(title)) throw new Error(`Referencia a una tabla o figura que no existe: "${title}"`);
    return String(captionNumbers.get(title));
  });
}

function caption(kind, text) {
  const n = kind === "Tabla" ? ++tableNo : ++figureNo;
  if (captionNumbers.has(text)) throw new Error(`Dos tablas o figuras con el mismo título: "${text}"`);
  captionNumbers.set(text, n);
  add(`<w:p><w:pPr><w:pStyle w:val="1339"/><w:spacing w:after="240"/><w:jc w:val="center"/></w:pPr>` +
      `<w:r><w:t xml:space="preserve">${kind} </w:t></w:r>` +
      `<w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> SEQ ${kind} \\* Arabic </w:instrText></w:r>` +
      `<w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>${n}</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r>` +
      `<w:r><w:t xml:space="preserve"> ${esc(text)}</w:t></w:r></w:p>`, 2);
  return n;
}

/** Tabla con el formato de las del mes 1: rejilla negra fina y cabecera gris EAEAEA. */
function table(columns, rows, { sz = 18, title } = {}) {
  const total = 7795;
  const sum = columns.reduce((n, c) => n + c[1], 0);
  const widths = columns.map(c => Math.round(c[1] / sum * total));
  widths[widths.length - 1] += total - widths.reduce((a, b) => a + b, 0);

  const cell = (text, w, header) => {
    const paras = String(text).split("\n").map(line =>
      `<w:p><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/>${header ? '<w:jc w:val="center"/>' : ""}${rPr({ sz, b: header })}</w:pPr>${runs(line, { sz, b: header })}</w:p>`).join("");
    return `<w:tc><w:tcPr><w:tcW w:w="${w}" w:type="dxa"/>${header ? '<w:shd w:val="clear" w:color="auto" w:fill="EAEAEA"/>' : ""}<w:vAlign w:val="${header ? "center" : "top"}"/></w:tcPr>${paras}</w:tc>`;
  };

  const charsPerLine = w => Math.max(8, Math.floor(w / 95 * (20 / sz)));
  let lines = 1.5;
  const rowXml = (cells, header) => {
    lines += Math.max(...cells.map((c, i) => String(c).split("\n").reduce((n, l) => n + Math.ceil(Math.max(1, l.length) / charsPerLine(widths[i])), 0))) * (sz / 20) + 0.35;
    return `<w:tr>${header ? "<w:trPr><w:tblHeader/></w:trPr>" : "<w:trPr><w:cantSplit/></w:trPr>"}${cells.map((c, i) => cell(c, widths[i], header)).join("")}</w:tr>`;
  };

  const xml = `<w:tbl><w:tblPr><w:tblStyle w:val="1185"/><w:tblW w:w="${total}" w:type="dxa"/><w:tblInd w:w="709" w:type="dxa"/><w:tblLayout w:type="fixed"/>` +
    `<w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="80" w:type="dxa"/><w:bottom w:w="40" w:type="dxa"/><w:right w:w="80" w:type="dxa"/></w:tblCellMar>` +
    `<w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="0" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/></w:tblPr>` +
    `<w:tblGrid>${widths.map(w => `<w:gridCol w:w="${w}"/>`).join("")}</w:tblGrid>` +
    rowXml(columns.map(c => c[0]), true) + rows.map(r => rowXml(r, false)).join("") + `</w:tbl>`;
  add(xml, lines);
  if (title) caption("Tabla", title);
}

/** Imagen centrada, 14 cm de ancho, con su pie. */
function figure(file, title, description) {
  const src = path.join(CAPTURAS, file);
  if (!fs.existsSync(src)) throw new Error(`Falta la captura ${src}. Pasa Tools → Codea → Capturas para informes.`);

  const rId = `rIdM2Img${media.length + 1}`;
  const name = `m2_figura${media.length + 1}.jpeg`;
  media.push({ src, name, rId });

  const { w, h } = jpegSize(fs.readFileSync(src));
  const cx = 5040000, cy = Math.round(cx * h / w);
  const id = docPrId++;
  add(`<w:p><w:pPr><w:keepNext/><w:spacing w:before="120" w:after="0"/><w:jc w:val="center"/></w:pPr><w:r><w:drawing>` +
      `<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="${cx}" cy="${cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/>` +
      `<wp:docPr id="${id}" name="Figura ${media.length}"/><wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" noChangeAspect="1"/></wp:cNvGraphicFramePr>` +
      `<a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">` +
      `<pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:nvPicPr><pic:cNvPr id="${id}" name="${name}"/><pic:cNvPicPr><a:picLocks noChangeAspect="1"/></pic:cNvPicPr></pic:nvPicPr>` +
      `<pic:blipFill><a:blip r:embed="${rId}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>` +
      `<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>` +
      `</a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>`, cy / 360000 / 0.45 + 0.5);
  const n = caption("Figura", title);
  if (description) para(description);
  return n;
}

function jpegSize(buf) {
  let p = 2;
  while (p < buf.length) {
    if (buf[p] !== 0xff) { p++; continue; }
    const marker = buf[p + 1];
    if (marker >= 0xc0 && marker <= 0xc3) return { h: buf.readUInt16BE(p + 5), w: buf.readUInt16BE(p + 7) };
    p += 2 + buf.readUInt16BE(p + 2);
  }
  return { w: 16, h: 9 };
}

// =====================================================================================
// Contenido
// =====================================================================================

// Conexiones de eventos revisadas en las dos escenas, del último informe de
// Tools → Codea → Comprobar escena de juego. Sin informe, el valor del 13/10/2026.
const CONEXIONES = (() => {
  try {
    const log = fs.readFileSync(path.join(ROOT, "Logs", "Codea_Comprobacion.txt"), "utf8");
    const n = [...log.matchAll(/✓ (?:Setup: )?(\d+) llamadas de UnityEvent/g)].map(m => Number(m[1]));
    return n.length === 2 ? n[0] + n[1] : 367;
  } catch { return 367; }
})();

// --- 1. Implementación de funcionalidades principales ---

heading(1, "Implementación de funcionalidades principales");

heading(2, "Propósito");
para("El presente informe técnico documenta el desarrollo del módulo inmersivo Codea VR realizado durante el segundo periodo del proyecto, comprendido entre la entrega del Producto 1 (13 de agosto de 2026) y el 13 de octubre de 2026. Mientras el Producto 1 estableció los requerimientos técnicos y funcionales y el diseño inicial del módulo, este documento presenta la implementación de las funcionalidades principales, la integración de los recursos 3D y de las interacciones inmersivas para Meta Quest, y los resultados de las pruebas funcionales preliminares realizadas con usuarios clave.");
para("Al cierre del periodo, el módulo cuenta con una experiencia jugable completa de principio a fin, en las dos dificultades previstas, que fue presentada el 30 de septiembre de 2026. Desde entonces se encuentra en una fase de ajustes a partir de la retroalimentación recibida, cuyo detalle se recoge en el capítulo 3.");

heading(2, "Descripción general del módulo implementado");
para("Codea VR es un escape room educativo de realidad virtual orientado al desarrollo del pensamiento computacional en estudiantes de 8 a 17 años. El jugador asume el papel de un astronauta cuya nave ha atravesado una tormenta espacial y, con la ayuda de Roki, el robot de mantenimiento, debe reparar cuatro sistemas para volver a la Tierra, con quince minutos en total y un máximo de cinco por sistema. Cada sala de la nave trabaja un pilar del pensamiento computacional: secuencialidad, condicionales, bucles y patrones, en dos niveles de dificultad (básica e intermedia).");
para("Antes de entregar el visor al estudiante, un supervisor introduce un PIN que identifica al participante y elige la dificultad. A partir de ese momento la sesión transcurre de forma autónoma: una narración guía al estudiante sala por sala, el sistema registra su desempeño y, al terminar, los datos se envían al servidor del proyecto si hay conexión. La Tabla {{Ficha técnica del módulo implementado}} resume las características del módulo implementado.");
table([["Campo", 1], ["Valor", 2.6]], [
  ["Tipo de aplicación", "Escape room educativo de realidad virtual, un jugador, vista en primera persona"],
  ["Dispositivo", "Meta Quest 3S (Horizon OS)"],
  ["Motor y lenguaje", "Unity 6000.4.1f1 con Universal Render Pipeline y Shader Graph; C#"],
  ["SDK de realidad virtual", "Meta XR All-in-One SDK 203.0.2 sobre OpenXR"],
  ["Escenas", "Setup (pantalla del supervisor) y Juego (tutorial y cuatro salas, para las dos dificultades)"],
  ["Salas", "Cuatro: secuencialidad, condicionales, bucles y parámetros, y patrones"],
  ["Dificultades", "Básica e intermedia, elegidas por el supervisor"],
  ["Duración máxima", "15 minutos de sesión, visibles en los relojes, que solo corren durante los retos; cada sala tiene además un máximo de 5 minutos, tras los cuales se pasa a la siguiente"],
  ["Entrada", "Mandos Meta Quest Touch; compatibilidad con manos habilitada"],
  ["Locomoción", "Teletransporte y traslados guiados entre salas"],
  ["Registro de datos", "Un archivo JSON por participante, enviado por HTTP POST; funcionamiento completo sin conexión"],
  ["Código fuente", "69 scripts en C# (unas 13 800 líneas); repositorio github.com/datascienceyt/codea2, rama dev"],
], { title: "Ficha técnica del módulo implementado" });

heading(2, "Cronología del desarrollo");
para("El desarrollo se organizó en iteraciones cortas con revisiones periódicas del equipo. La Tabla {{Hitos del desarrollo en el periodo}} resume los hitos del periodo, tomados del historial del repositorio del proyecto.");
table([["Fecha", 1], ["Hito", 4.2]], [
  ["13/08/2026", "Entrega del Producto 1: requerimientos, diseño y prototipos iniciales de los escenarios"],
  ["02/09/2026", "Implementación de los escenarios 2, 3 y 4, registro de datos en JSON y sistema de sesión"],
  ["08/09/2026", "Narrador con voz y texto, módulos de la sala 2 con selección múltiple y pantalla del supervisor con PIN y dificultad"],
  ["09-11/09/2026", "Interfaz con TextMeshPro, luces e iconos de estado de los módulos y pantalla de narración que acompaña al jugador"],
  ["14/09/2026", "Montaje de la sala 2, botones y grabación de la voz de la narración"],
  ["15/09/2026", "Salas 3 y 4 con su registro de datos"],
  ["22/09/2026", "Versión beta 1: nuevo entorno de la nave y experiencia jugable completa; primer APK"],
  ["29/09/2026", "Sesión por PIN y dificultad, cierre por tiempo agotado y cambios acordados en la reunión de revisión"],
  ["30/09/2026", "Escena del supervisor, narrativa definitiva, nivel intermedio de la sala 1 y APK de la presentación"],
  ["06/10/2026", "Formato definitivo del registro de datos, tutorial inicial y ajustes de la sala 2"],
  ["09/10/2026", "Las dos dificultades en una sola escena, sala 3 básica sin instrucciones visibles, tiempo límite de 5 minutos por sala y herramientas de verificación automática"],
  ["12/10/2026", "Voz definitiva de la narración, sala 4 intermedia con el alfabeto de glifos cuadrados, reloj de sesión de 15 minutos con máximo por sala, envío automático de las sesiones pendientes y retirada de escenas, recursos y código sin uso"],
], { title: "Hitos del desarrollo en el periodo" });

heading(2, "Arquitectura del software");
para("El módulo se organiza en subsistemas independientes que se comunican mediante eventos. Esta separación permitió desarrollar y probar cada sala por separado y reutilizar el mismo sistema de bloques en varias de ellas. La Tabla {{Subsistemas del módulo}} describe la responsabilidad de cada subsistema.");
table([["Subsistema", 1.1], ["Responsabilidad", 2.6], ["Componentes principales", 1.7]], [
  ["Sesión", "Pantalla del supervisor (PIN y dificultad), carga de la escena de juego, tiempo de la sesión y máximo de cada sala, cierre y vuelta a la pantalla inicial", "SessionSetup, PinEntry, SceneLoader, ScenarioTimeLimit, TimeUpSequence"],
  ["Director", "Orquesta la experiencia en tramos y pasos: narración, traslados, apertura de puertas y espera de cada reto", "Director, IStepAction"],
  ["Narración", "Reproduce la voz y muestra el texto en pantalla, emparejados por identificador con un guion en CSV", "Narrator"],
  ["Programación por bloques", "Acople de bloques en huecos, filas de instrucciones y ejecución secuencial", "BlockNode, Socket, SocketRow, ProgramRunner, ProgramTrigger"],
  ["Salas", "Lógica de cada reto y su conexión con el registro de datos", "Scenario1Controller a Scenario4Controller, LevelManager, Bot, SystemModule, RoboticArm, ShapeSocket"],
  ["Dificultad", "Configura cada sala según la dificultad elegida, en una sola escena", "DifficultyApplier, DifficultyVariant, DifficultyOnly"],
  ["Telemetría", "Registro del desempeño en JSON, envío al servidor, también de las sesiones que quedaron pendientes, e informe de integridad", "TelemetryManager, TelemetryData, JSONUploader, TelemetryOutbox"],
  ["Herramientas de editor", "Montaje de escenas, editor de niveles, comprobación de la escena y pruebas automáticas", "CodeaSceneTools, CodeaScenario4, LevelEditorWindow, CodeaSceneCheck, CodeaPlayCheck"],
], { title: "Subsistemas del módulo" });

heading(2, "Navegación y flujo de la sesión");
para("La navegación combina los pasos que realiza el supervisor y una secuencia guiada para el estudiante. El recorrido completo es el siguiente:", { keepNext: true });
numbered([
  "**Pantalla del supervisor.** El supervisor introduce el PIN del participante en un teclado numérico, que también permite proponer el siguiente número correlativo, elige la dificultad y pulsa EMPEZAR. El sistema no permite empezar sin un PIN completo y sin dificultad elegida (Figura {{Pantalla del supervisor: teclado del PIN, selección de dificultad y botón EMPEZAR}}). Al abrirse, esta pantalla envía sola los datos de las sesiones anteriores que hubieran quedado sin enviar, y tiene un botón para hacerlo a mano.",
  "**Espera inicial.** La escena de juego se carga en pausa y solo comienza al pulsar el botón INICIAR, lo que da tiempo a que el estudiante se coloque el visor (Figura {{Cuarto del tutorial con el botón INICIAR y la pantalla de narración}}).",
  "**Tutorial.** El narrador da la bienvenida y el estudiante practica las cuatro interacciones de la experiencia: pulsar dos botones, encajar un bloque en su hueco y colocar una ficha en el hueco de su misma figura.",
  "**Salas 1 a 4.** El estudiante es trasladado a cada sala con un fundido a negro. Dentro de cada sala se desplaza por teletransporte, la narración presenta el reto y, al resolverlo, se abre la puerta hacia la siguiente.",
  "**Cierre.** Al completar la sala 4 suena la frase final, se cierra el registro de la sesión, se envía al servidor y la aplicación vuelve sola a la pantalla del supervisor con el siguiente PIN propuesto.",
  "**Tiempo agotado.** Los relojes muestran los quince minutos de la sesión, que solo corren mientras hay un reto abierto. Cada sala tiene además un máximo de cinco minutos: si se agotan sin resolverla, se deja terminar el intento que se esté ejecutando y, si no la resuelve, la historia pasa a la sala siguiente. Si se agotan los quince minutos de la sesión, o los cinco de la última sala, el narrador se despide, se cierra y envía el registro y se vuelve a la pantalla del supervisor.",
]);
para("La secuencia de la escena de juego la controla el componente Director, organizado en cinco tramos y 23 pasos. Cada paso ejecuta acciones inmediatas, como activar objetos, iniciar el temporizador o abrir el registro de una sala, y espera a que terminen otras: frases de la narración, fundidos, traslados, puertas o la resolución del reto. La Tabla {{Tramos de la secuencia de la experiencia}} resume los tramos.");
table([["Tramo", 1], ["Pasos", 0.6], ["Contenido", 4]], [
  ["Inicio", "2", "Tutorial (frase de bienvenida y cuatro interacciones) y traslado a la sala 1"],
  ["Sala 1", "6", "Narración de la misión, inicio del reloj de la sesión, resolución del reto, apertura de la puerta y traslado"],
  ["Sala 2", "6", "Narración, recorrido por los tres módulos (uno por paso) y traslado a la sala 3"],
  ["Sala 3", "4", "Narración, resolución del reto, traslado y cierre de la sala"],
  ["Sala 4", "5", "Narración, resolución del panel, frase final, envío de los datos y vuelta a la pantalla del supervisor"],
], { title: "Tramos de la secuencia de la experiencia" });
figure("setup_supervisor.jpg", "Pantalla del supervisor: teclado del PIN, selección de dificultad y botón EMPEZAR",
  "La pantalla del supervisor es la primera escena de la aplicación. Muestra los cuatro dígitos del PIN, el teclado numérico con las teclas de borrar y de siguiente correlativo, los botones de dificultad, el botón EMPEZAR y el botón que envía los datos pendientes, con su indicación de estado.");
figure("tutorial_inicio.jpg", "Cuarto del tutorial con el botón INICIAR y la pantalla de narración",
  "Al cargar la escena de juego, el estudiante aparece en el cuarto del tutorial. Nada comienza hasta que pulsa INICIAR; a partir de ese momento se activan las piezas de práctica y suena la frase de bienvenida.");

heading(2, "Elementos educativos");
heading(3, "Salas y pilares del pensamiento computacional");
para("Cada sala traduce un concepto de programación a una actividad manipulable dentro de la nave. La dificultad intermedia conserva la mecánica de la básica y aumenta la exigencia del problema, de modo que el estudiante transfiere lo aprendido en lugar de aprender una mecánica nueva. La Tabla {{Elementos educativos por sala y dificultad}} detalla cada sala.");
table([["Sala", 0.9], ["Pilar", 0.9], ["Actividad", 2.1], ["Básica", 1.6], ["Intermedia", 1.7]], [
  ["1. Dormitorio", "Secuencialidad", "Ordenar bloques (Avanzar 1 bloque, Avanzar 2 bloques, Girar izquierda, Girar derecha, Pulsar botón) para que Roki recorra una rejilla y pulse el botón que abre la puerta", "Solución de 8 bloques; 8 huecos y 8 bloques disponibles", "Solución de 11 bloques y un camino alternativo de 14 que no cabe en los 11 huecos; 13 bloques disponibles"],
  ["2. Sala de control", "Condicionales", "Leer los síntomas de tres sistemas averiados (motores, energía y enfriamiento) y marcar la acción que corrige cada uno", "4 opciones, 1 correcta", "4 opciones, 2 correctas (un síntoma por acción)"],
  ["3. Almacén", "Bucles y parámetros", "Programar un brazo robótico para clasificar 7 cajas y 3 barriles: una ficha indica el tipo de objeto y un contador las repeticiones", "Las instrucciones vienen ordenadas y no se muestran; el estudiante elige el tipo y las repeticiones", "El estudiante además reordena las instrucciones, que llegan desordenadas"],
  ["4. Panel final", "Patrones", "Colocar cada ficha en el hueco cuya figura es idéntica, entre figuras muy parecidas", "4 huecos con figuras geométricas y 18 fichas", "6 huecos con las letras de la palabra YACHAY en un alfabeto de glifos cuadrados; las mismas 18 fichas llevan letras de ese alfabeto, entre ellas las seis necesarias"],
], { title: "Elementos educativos por sala y dificultad", sz: 17 });
para("El diseño de cada sala incorpora además decisiones pedagógicas que evitan que el reto se resuelva por reconocimiento superficial:", { keepNext: true });
bullet("**Sala 2:** los enunciados describen síntomas y no la causa, ninguna palabra de la respuesta aparece en el enunciado y cada opción incorrecta es la correcta de otro módulo, por lo que es necesario leer y descartar.");
bullet("**Sala 3:** la fila completa de instrucciones es el bucle y la ficha de tipo actúa como argumento: el mismo programa sirve para los dos tipos de objeto cambiando solo el dato. Son necesarias al menos dos ejecuciones, una por tipo.");
bullet("**Sala 4:** la figura se compara visualmente con la del hueco y hay fichas distractoras, de modo que el último hueco no puede resolverse por eliminación. En la dificultad intermedia los símbolos son letras de un alfabeto que el estudiante no conoce, por lo que solo puede guiarse por la forma.");

heading(3, "Narración y guía");
para("La experiencia está guiada por un narrador con voz grabada y texto en pantalla. El guion consta de 22 frases, dos de ellas con una variante por dificultad (salas 2 y 3). Cada dificultad usa una lista de 20 frases en el mismo orden, y las otras dos se reservan para el tiempo agotado: una al agotarse el tiempo de una sala y otra para el cierre de la sesión. La pantalla de narración se desplaza a la sala en la que se encuentra el estudiante y el texto aparece completo, para no depender de la velocidad de lectura. Ante un error, el narrador dice una de cinco frases breves de ánimo sin interrumpir la frase en curso. Las 24 locuciones del guion y las cinco de ánimo se grabaron con la voz definitiva y se integraron el 12 de octubre de 2026; cada audio se asigna a su frase de forma automática a partir del nombre del archivo.");

heading(3, "Retroalimentación al estudiante");
bullet("Frase de felicitación del narrador al terminar cada sala.");
bullet("Cambios visibles en el entorno: la puerta se abre, los módulos pasan de rojo a verde con su icono de estado, las cajas despejan el paso y los motores arrancan con sonido.");
bullet("En la sala 4, cada hueco resuelto se ilumina y la ficha queda fija; una ficha equivocada sale expulsada con un sonido de desconexión.");
bullet("En la sala 1, el nivel se rearma solo tras un intento fallido, sin que el estudiante tenga que pulsar nada, y unas flechas sobre la fila de huecos indican el sentido en que se leen los bloques.");
bullet("Un reloj visible en cada sala indica el tiempo que queda de la sesión.");

heading(2, "Lógica de funcionamiento");
heading(3, "Sistema de programación por bloques");
para("Las salas 1 y 3 y el tutorial comparten un sistema de programación por bloques físicos. Cada bloque es un objeto que el estudiante agarra; al soltarlo cerca de un hueco libre se acopla a él automáticamente. Los huecos se generan en filas encadenadas y, al pulsar el botón de ejecución, el sistema recorre la cadena en orden y ejecuta la acción de cada bloque sobre el robot o el brazo robótico. En la sala 3 la fila completa se repite tantas veces como indique el contador.");
para("El intento se registra antes de ejecutarse. Esta decisión surgió de las pruebas: el reto se completa durante la ejecución y, cuando el registro se hacía al final, el intento que resolvía la sala se perdía.");

heading(3, "Lógica de cada sala");
table([["Sala", 0.8], ["Lógica implementada", 5]], [
  ["Sala 1", "Rejilla lógica cargada desde un archivo JSON creado con un editor de niveles propio. El robot se mueve en relativo (avanzar, girar); chocar, salirse de la rejilla o usar donde no hay nada cuentan como error. Tras un intento fallido el nivel se rearma solo."],
  ["Sala 2", "Cada módulo carga su problema y sus opciones desde un archivo de datos. Una opción correcta se alterna entre marcada y desmarcada; una incorrecta se marca durante 0,8 s y se suelta sola. El módulo se repara cuando están marcadas todas las correctas."],
  ["Sala 3", "El brazo gira entre tres posiciones y resuelve el destino según la ficha de tipo. La sala se completa al contar lo entregado en cada destino. Lo que quede en la pinza al terminar una ejecución vuelve a su pila."],
  ["Sala 4", "Cada hueco valida la ficha al soltarla: la correcta queda fija y la incorrecta se expulsa con física. El reinicio no mueve las fichas ya acertadas. En la dificultad intermedia la palabra repite letras, por lo que hay dos fichas de cada letra repetida y cada hueco se registra con su posición."],
], { title: "Lógica implementada en cada sala" });

heading(3, "Gestión de la dificultad");
para("Las dos dificultades comparten una sola escena. Al cargarla, un componente lee la dificultad elegida por el supervisor y, antes de que se inicialice cualquier otro elemento, configura cada sala con los datos correspondientes: el nivel y el número de huecos de la sala 1, los problemas de los módulos de la sala 2, la fila de instrucciones de la sala 3, las figuras de las fichas de la sala 4 y la lista de narración. Las piezas que solo existen en una dificultad, como los bloques adicionales de la sala 1, el fondo del panel de la sala 3 o los huecos de la sala 4, se desactivan en la otra. La dificultad se toma del mismo dato que se registra y se envía al servidor, por lo que la escena y el registro no pueden discrepar.");
para("Esta organización sustituyó el 9 de octubre de 2026 a la anterior, de una escena por dificultad, que obligaba a repetir en dos escenas cada cambio común: tutorial, narración o entorno.");

heading(3, "Sesión y tiempo límite");
para("La sesión dispone de quince minutos, que se muestran en un reloj en cada sala y solo corren mientras hay un reto abierto: se detienen durante la narración, la apertura de puertas y los traslados. Además, cada sala tiene un máximo de cinco minutos, que no se muestra y cuya función es que ningún estudiante quede detenido en una sala. El máximo por sala se incorporó el 9 de octubre de 2026 en sustitución del límite único de 15 minutos, con el que un estudiante que se detenía en una sala no llegaba a las siguientes; desde el 12 de octubre de 2026 conviven los dos.");
para("Al agotarse el tiempo de una sala, el sistema deja terminar el intento que se esté ejecutando, ya que puede ser el que la resuelve. Si no la resuelve, la sala se registra como no completada por tiempo agotado y la historia continúa: la puerta se abre y el estudiante pasa a la sala siguiente. En lugar de la frase de felicitación, el narrador indica que se acabó el tiempo de la sala y que se continúa con la siguiente. Si se agotan los quince minutos de la sesión, lo que ocurre cuando no se resuelve ninguna de las tres primeras salas, o los cinco de la última sala, una secuencia detiene la historia, reproduce la frase de despedida, cierra y envía el registro, y devuelve la aplicación a la pantalla del supervisor una vez terminado el envío.");

heading(3, "Registro de datos");
para("Cada sesión genera un archivo JSON propio, cuyo nombre combina el PIN, un número de sesión correlativo y un código de cinco caracteres que identifica al visor. El archivo se guarda en el visor durante toda la sesión y se envía al servidor por HTTP POST al terminar, con reintentos. Si no hay conexión, o si la aplicación se cierra a mitad de la sesión, el archivo permanece en el visor y se envía de forma automática la siguiente vez que se abre la pantalla del supervisor; también puede extraerse por cable. El sistema lleva la cuenta de los archivos ya enviados sin moverlos ni borrarlos, y una sesión nueva nunca sobrescribe un archivo existente. La Tabla {{Datos registrados por sesión}} resume los datos registrados.");
table([["Ámbito", 0.9], ["Datos registrados", 5]], [
  ["Sesión", "PIN, código del visor, número de sesión, dificultad, fecha y hora de inicio y fin, duración total"],
  ["Cada sala", "Si se inició, si se completó y si se agotó su tiempo; duración, hora de inicio y de fin"],
  ["Sala 1", "Intentos con la secuencia de bloques, tiempo de preparación de cada uno y si resolvió el reto; intentos fallidos, bloques agarrados y conectados, reinicios, colisiones del robot y usos inválidos"],
  ["Sala 2", "Cada opción marcada: módulo, opción, si era correcta, tiempo desde la anterior y hora"],
  ["Sala 3", "Intentos con la secuencia, las repeticiones y el tipo de objeto; intentos fallidos, bloques agarrados y conectados, órdenes imposibles"],
  ["Sala 4", "Cada colocación: hueco (en intermedia, con su posición), ficha, si era correcta, tiempo desde la anterior y hora; fichas agarradas"],
], { title: "Datos registrados por sesión" });
para("Todos los indicadores de estado (iniciado, completado, tiempo agotado, resuelto, correcto) se registran como valores verdadero o falso. Al cerrar cada sesión, el sistema genera un informe de integridad que advierte de valores que suelen indicar un fallo de configuración, como una sala completada sin intentos registrados.");

heading(2, "Evidencia del desarrollo");
para("Las figuras siguientes muestran el estado del módulo al 13 de octubre de 2026. Son capturas de la escena de juego en ejecución en el editor de Unity, tomadas desde el punto de vista del jugador en cada sala.");
figure("sala1_general.jpg", "Sala 1: rejilla, robot Roki y botón verde que abre la puerta",
  "El estudiante observa la rejilla desde la mesa de programación. El robot debe llegar a la casilla situada frente al botón verde de la pared y pulsarlo para abrir la puerta. El reloj superior indica el tiempo que queda de la sesión.");
figure("sala1_mesa_basica.jpg", "Sala 1, dificultad básica: mesa de programación con 8 bloques",
  "Sobre la mesa están los bloques de instrucciones, la fila de huecos donde se ordenan, con flechas que indican el sentido de lectura, el botón de ejecución y el botón de reinicio.");
figure("sala1_mesa_intermedia.jpg", "Sala 1, dificultad intermedia: 13 bloques para una fila de 11 huecos",
  "La paleta intermedia incluye bloques de más que conducen hacia un camino alternativo, más largo, que no cabe en la fila.");
figure("sala2_general.jpg", "Sala 2: módulos de los sistemas de enfriamiento, energía y motores", null);
figure("sala2_modulo_sin_resolver.jpg", "Módulo sin resolver: título, estado, síntomas y opciones",
  "Cada módulo muestra el título del sistema, su estado con luces roja y verde, los síntomas y cuatro opciones.");
figure("sala2_modulo_resuelto.jpg", "Módulo resuelto: luz verde e indicación «Sistema restablecido»", null);
figure("sala3_basica.jpg", "Sala 3, dificultad básica: el estudiante elige el tipo de objeto y las repeticiones",
  "En la dificultad básica la fila de instrucciones funciona sin mostrarse. El estudiante coloca la ficha del tipo de objeto (caja o barril) en la casilla TIPO, indica las repeticiones con los botones + y − y pulsa Iniciar.");
figure("sala3_intermedia.jpg", "Sala 3, dificultad intermedia: columna de instrucciones que el estudiante debe reordenar", null);
figure("sala4_panel.jpg", "Sala 4, dificultad básica: panel con 4 huecos de figura y 18 fichas", null);
figure("sala4_panel_intermedia.jpg", "Sala 4, dificultad intermedia: seis huecos con las letras de YACHAY en glifos cuadrados",
  "En la dificultad intermedia los huecos muestran las seis letras de la palabra YACHAY en un alfabeto de glifos cuadrados y las fichas llevan letras de ese alfabeto. Entre las dieciocho fichas están las seis necesarias, con dos fichas para cada letra repetida.");

heading(2, "Cambios respecto al diseño inicial");
para("Durante la implementación, algunos elementos del diseño del Producto 1 se ajustaron para mejorar la experiencia o la calidad de los datos. La Tabla {{Cambios respecto al diseño del Producto 1}} los resume con su justificación.");
table([["Diseño del Producto 1", 1.7], ["Implementado", 1.9], ["Motivo", 2]], [
  ["3 escenarios", "4 salas: se añade la sala de patrones", "Trabajar el reconocimiento de patrones en una sala propia"],
  ["Ingreso de un nombre de usuario por el estudiante (RF-01)", "PIN numérico introducido por el supervisor", "Más rápido y sin errores de escritura; el estudiante recibe el visor con la sesión preparada"],
  ["Registro en archivo .csv (RF-05)", "Archivo JSON por participante", "Representa los datos por sala e intento, que en un CSV quedarían aplanados"],
  ["Temporizador de 20 minutos para la sesión (RF-06)", "15 minutos de sesión a la vista, que solo corren durante los retos, y un máximo de 5 minutos por sala, tras los cuales se pasa a la siguiente", "Que ningún estudiante quede detenido en una sala y que la narración no reste tiempo de juego"],
  ["Sala 1 básica con instrucciones de alto nivel", "Rejilla con movimientos relativos en ambas dificultades", "La dificultad crece con el problema y no con una mecánica distinta"],
  ["Sala 3 con un bloque contenedor «Repetir»", "La fila completa es el bucle y una ficha actúa como argumento", "Introduce además el concepto de parámetro"],
  ["Sala 4 intermedia basada en el número de lados", "Seis huecos con las letras de YACHAY en un alfabeto de glifos cuadrados", "Ejercita la discriminación visual con símbolos que el estudiante no conoce"],
  ["Sin seguimiento de manos", "Mandos como entrada principal y manos habilitadas", "Ampliar la accesibilidad; pendiente de validación en el visor"],
], { title: "Cambios respecto al diseño del Producto 1" });

heading(2, "Herramientas de desarrollo y verificación");
para("Junto al código del módulo se desarrollaron herramientas integradas en el editor de Unity, en el menú Tools → Codea, y scripts de apoyo que automatizan el montaje y la verificación del proyecto. Su objetivo es detectar, antes de llevar una versión al visor, los fallos de configuración que no se ven al jugar, como una conexión de eventos rota tras renombrar un método o una sala configurada con los datos de la otra dificultad. La Tabla {{Herramientas de desarrollo y verificación}} las resume.");
table([["Herramienta", 1.5], ["Función", 4.4]], [
  ["Comprobar escena de juego", "Revisa la escena sin ejecutarla: scripts perdidos, todas las conexiones de eventos (que el objeto y el método existan), la secuencia del Director paso a paso, la narración frente al guion, la configuración de cada sala en cada dificultad (incluido que las fichas de la sala 4 permiten completar el panel), los tiempos límite y la escena del supervisor"],
  ["Probar las dos dificultades en Play", "Ejecuta la escena sin visor en básica e intermedia y comprueba lo que queda configurado en cada sala; ejecuta el programa de la sala 3 con la fila oculta; agota el tiempo de las salas 1 y 3 para verificar que la historia continúa y que se registra; y recorre la secuencia completa del Director. No envía datos al servidor y deja el editor como estaba"],
  ["Capturas para informes", "Fotografía cada sala en ejecución, en las dos dificultades, desde el punto de vista del jugador"],
  ["Preparar escena de juego y montar el tutorial", "Montan y reparan de forma repetible el ciclo de sesión, los tiempos límite y el cuarto del tutorial"],
  ["Poner al día las escenas", "Lleva las dos escenas a lo que el código espera sin rehacerlas: botón de envío de datos, una sola apertura por reto, un fondo de panel por dificultad en la sala 3 y retirada de ajustes sin uso"],
  ["Montar la intermedia de la sala 4", "Nombra el pliego de glifos de la A a la Z, crea un hueco por letra de la palabra y sortea las letras de las fichas garantizando las necesarias"],
  ["Asignar la voz del narrador", "Coloca cada audio en su frase a partir del nombre del archivo, sin arrastrarlos a mano"],
  ["Ver como básica o intermedia", "Muestran en el editor solo las piezas de una dificultad, para colocarlas sin que se solapen"],
  ["Editor de niveles", "Dibuja la rejilla de la sala 1 y la exporta a JSON"],
  ["Resolver nivel (script)", "Calcula todos los caminos de un nivel de la sala 1 y cuántos bloques necesita cada uno"],
  ["Validar JSON de telemetría (script)", "Comprueba que un archivo de datos tiene exactamente el formato que emite el código"],
], { title: "Herramientas de desarrollo y verificación" });
para(`En la versión del 13 de octubre de 2026, la comprobación de la escena revisó ${CONEXIONES} conexiones de eventos de las dos escenas sin encontrar fallos.` + " La prueba en Play completó el recorrido en las dos dificultades sin errores ni excepciones: verificó la configuración de cada sala, ejecutó el programa de la sala 3 con la fila oculta (todos los objetos del tipo elegido llegaron a su destino) y comprobó que, al agotarse el tiempo de una sala, la sala 1 cede el paso a la siguiente de inmediato y la sala 3 espera a que termine el intento en curso, quedando ambas registradas como no completadas por tiempo agotado. El cierre de la sesión al agotarse los quince minutos queda por verificar en el visor.");

// --- 2. Integración de recursos 3D e interacciones inmersivas ---

heading(1, "Integración de recursos 3D e interacciones inmersivas", { newPage: true });

heading(2, "Recursos 3D incorporados");
para("El entorno de la nave combina un paquete de recursos modulares de ciencia ficción con modelos y materiales propios, geometría modelada directamente en el editor y efectos visuales. La Tabla {{Recursos 3D y visuales integrados en la escena de juego}} detalla los recursos que utiliza la escena de juego, obtenidos a partir de las referencias reales de la escena y no del contenido total del proyecto.");
table([["Recurso", 1.3], ["Contenido", 2.6], ["Uso en la experiencia", 2.2]], [
  ["Sci-Fi Styled Modular Pack", "20 modelos FBX, 10 prefabs, 9 materiales, 8 texturas y 4 animaciones", "Suelos, paredes, ventanales, puertas animadas, escaleras y elementos decorativos de la nave"],
  ["Modelos propios", "11 modelos: robot Roki, bloque de programación, botón, barril, cajas, baldosa, luz, Tierra y Sol", "Personaje guía, piezas de los retos y escenario exterior"],
  ["Shader Graph propios", "4 sombreadores: atmósfera y halo de la Tierra, núcleo cercano y lejano", "Vista exterior de la Tierra"],
  ["Materiales propios", "36 materiales, entre ellos el vidrio de la sala 3", "Acabados de salas, mesas y piezas"],
  ["Efectos visuales", "2 efectos: fuego y humo negro", "Daños de la tormenta en la sala 1"],
  ["Geometría de ProBuilder", "44 mallas modeladas en el editor", "Cuarto del tutorial, mesas de programación y paneles de las salas"],
  ["Interfaz 2D", "12 imágenes (iconos de estado, figuras y flechas de la fila de la sala 1), pliego de 34 figuras para la sala 4, pliego de 26 glifos para su dificultad intermedia y tipografía Consolas", "Pantallas de los módulos, fichas y huecos de figuras"],
], { title: "Recursos 3D y visuales integrados en la escena de juego" });
para("El apartado sonoro incluye las 24 locuciones de la narración en la voz definitiva, cinco frases de ánimo ante errores y efectos para botones, puertas, acople y desacople de piezas, movimiento del robot y del brazo robótico, arranque de los motores y tarea completada. Todos los botones de la experiencia tienen sonido propio.");

heading(2, "Programación de interacciones");
para("Las interacciones se construyeron sobre el Meta Interaction SDK, que proporciona los interactores de mando y de mano, y se conectaron a la lógica del módulo mediante eventos. Sobre esa base se programaron los comportamientos propios de cada reto. La Tabla {{Interacciones programadas}} resume las interacciones implementadas.");
table([["Interacción", 1.1], ["Implementación", 3.4], ["Cantidad", 1.4]], [
  ["Pulsar botones", "Botones de pulsación del SDK con eventos conectados a la lógica de cada sala y sonido propio", "23 en la escena de juego y 16 en la pantalla del supervisor"],
  ["Agarrar y soltar", "Componentes de agarre del SDK para mando y para mano en cada pieza, con un adaptador propio que traduce el agarre y la suelta a eventos de la lógica", "49 piezas agarrables"],
  ["Encajar piezas", "Acople propio: en el momento de soltar, la pieza busca el hueco libre más cercano (5 cm), se alinea con él y suena; las piezas acertadas o fijas quedan bloqueadas", "Huecos de las salas 1, 3 y 4 y del tutorial"],
  ["Desplazarse", "Teletransporte a puntos fijos y traslados guiados entre salas con fundido a negro", "7 puntos de teletransporte"],
  ["Física de las fichas", "Las fichas de la sala 4 tienen gravedad, se inmovilizan al encajar y vuelven solas a su sitio si caen al suelo", "18 fichas"],
  ["Entorno reactivo", "Puertas automáticas, brazo robótico animado por código, robot que recorre la rejilla, luces e iconos de estado y pantalla de narración que se traslada a cada sala", "Todas las salas"],
], { title: "Interacciones programadas" });
para("El acople por proximidad consulta un registro de huecos activos en lugar de buscar en toda la escena en cada fotograma, lo que evita un coste de rendimiento elevado en el visor mientras el estudiante sostiene una pieza.");

heading(2, "Configuración de controles");
para("La interacción está diseñada para estudiantes que, en su mayoría, usan realidad virtual por primera vez, por lo que se limitó a tres acciones. Toda la interfaz es diegética: forma parte del entorno y no flota delante de la vista. La Tabla {{Controles de la experiencia}} recoge los controles.");
table([["Acción", 1.2], ["Con mandos", 2], ["Con manos", 2]], [
  ["Agarrar y soltar", "Botón de agarre (grip)", "Cerrar y abrir la mano"],
  ["Pulsar un botón", "Tocar el botón con el mando", "Tocar el botón con el dedo"],
  ["Desplazarse", "Teletransporte", "—"],
], { title: "Controles de la experiencia" });
para("El perfil de entrada de OpenXR configurado corresponde a los mandos Oculus Touch. El seguimiento de manos está habilitado en el proyecto y todas las piezas admiten agarre con la mano; su validación en el visor queda pendiente para la siguiente etapa.");

heading(2, "Ajustes para la ejecución en Meta Quest");
para("La Tabla {{Configuración del proyecto para Meta Quest}} recoge la configuración del proyecto para su ejecución en el visor.");
table([["Ajuste", 1.3], ["Valor", 3]], [
  ["Dispositivo y sistema", "Meta Quest 3S, Horizon OS"],
  ["Integración XR", "OpenXR con Meta XR Feature y perfil de mandos Oculus Touch"],
  ["Identificador del paquete", "com.yachaytech.Codea2, versión 0.0.1"],
  ["Versión de Android", "SDK mínimo 32 y objetivo 34"],
  ["Compilación", "IL2CPP para ARM64"],
  ["Gráficos", "Vulkan, con OpenGL ES 3 como respaldo; espacio de color lineal"],
  ["Renderizado estéreo", "Una sola pasada (multiview)"],
  ["Optimización del visor", "Foveated rendering y subsampled layout de Meta"],
  ["Funciones del visor", "Seguimiento de manos activado, modo focus aware, sin passthrough"],
  ["Conectividad", "Funcionamiento completo sin conexión; envío opcional de datos"],
], { title: "Configuración del proyecto para Meta Quest" });
para("Además de la configuración, se aplicaron ajustes en el código orientados al rendimiento:", { keepNext: true });
bullet("**Escritura diferida de datos:** los eventos frecuentes, como agarres y errores, se guardan en lote cada 10 segundos; la escritura inmediata provocaba tirones perceptibles.");
bullet("**Registro de huecos activos:** el acople de piezas no recorre la escena en cada fotograma.");
bullet("**Texto completo de la narración:** se muestra de una vez, sin animación de escritura.");
para("Quedan pendientes la medición del rendimiento en el visor frente al objetivo de 60 FPS (RNF-01) y la revisión del perfil de calidad de Android. Durante la recopilación de este informe se detectó que dicho perfil apunta a una configuración de render que ya no existe en el proyecto, por lo que se aplica la de escritorio, más exigente para el visor.");

heading(2, "Despliegue en el visor");
para("La aplicación se distribuye como un APK que se instala en los visores por carga local. El archivo se versiona en el repositorio del proyecto, y los datos de cada sesión quedan en el almacenamiento de la aplicación, accesibles por cable USB. Para que el código que identifica a cada visor se mantenga entre versiones, todas las compilaciones deben firmarse con la misma clave.");

// --- 3. Pruebas funcionales preliminares con usuarios clave ---

// Las sesiones de prueba con usuarios clave, leídas de su registro de telemetría tal como salió
// del visor (formato de la versión del 30/09/2026: indicadores 0/1, con deselecciones).
const SESIONES = [
  { file: "0105_5.json", participante: "Gabriela Cajamarca" },
  { file: "1101_6.json", participante: "Ingeniero especializado en realidad virtual de la Escuela Superior Politécnica del Litoral (ESPOL)" },
].map(s => ({
  ...s,
  // El visor escribe con BOM (Encoding.UTF8 de .NET); JSON.parse no lo admite.
  run: JSON.parse(fs.readFileSync(path.join(ROOT, "Docs", "JSON Samples", "originales", s.file), "utf8").replace(/^﻿/, "")),
}));

const minSeg = seconds => { const t = Math.round(seconds); return `${Math.floor(t / 60)} min ${t % 60} s`; };
const fecha = iso => iso.slice(8, 10) + "/" + iso.slice(5, 7) + "/" + iso.slice(0, 4);
const duracion = run => (new Date(run.endedUtc) - new Date(run.startedUtc)) / 1000;

function resumen(run) {
  const e1 = run.escenario1, e2 = run.escenario2, e3 = run.escenario3, e4 = run.escenario4;
  const resuelto = e1.attempts.findIndex(a => a.solved === 1 || a.solved === true) + 1;
  const marcadas = e2.selections.filter(s => s.selected === 1 || s.selected === true);
  const desmarcadas = e2.selections.length - marcadas.length;
  const malas2 = marcadas.filter(s => !(s.correct === 1 || s.correct === true)).length;
  const malas4 = e4.placements.filter(p => !(p.correct === 1 || p.correct === true)).length;
  const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;

  return {
    sala1: `${minSeg(e1.totalSeconds)} · ${plural(e1.attempts.length, "intento", "intentos")}` +
           (resuelto > 0 ? `, resuelto en el ${resuelto}.º` : ", sin resolver") +
           ` · ${e1.blocksGrabbed} bloques agarrados · ${plural(e1.errorCollisionBot, "colisión", "colisiones")} y ` +
           `${plural(e1.errorInvalidCommand, "orden inválida", "órdenes inválidas")} del robot`,
    sala2: `${minSeg(e2.totalSeconds)} · ${plural(marcadas.length, "opción marcada", "opciones marcadas")}, ` +
           `${malas2 === 0 ? "todas correctas" : plural(malas2, "incorrecta", "incorrectas")}` +
           (desmarcadas > 0 ? ` · ${plural(desmarcadas, "opción desmarcada", "opciones desmarcadas")} a mano` : ""),
    sala3: `${minSeg(e3.totalSeconds)} · ${plural(e3.attempts.length, "ejecución", "ejecuciones")}: ` +
           e3.attempts.map(a => `${a.itemType === "Caja" ? "cajas" : "barriles"} ×${a.repetitions}`).join(", ") +
           ` · ${e3.errorInvalidCommand === 0 ? "sin órdenes imposibles" : plural(e3.errorInvalidCommand, "orden imposible", "órdenes imposibles")}`,
    sala4: `${minSeg(e4.totalSeconds)} · ${plural(e4.placements.length, "colocación", "colocaciones")}, ` +
           `${plural(malas4, "incorrecta", "incorrectas")} · ${e4.chipsGrabbed} fichas agarradas`,
    salas: minSeg(e1.totalSeconds + e2.totalSeconds + e3.totalSeconds + e4.totalSeconds),
  };
}

heading(1, "Pruebas funcionales preliminares con usuarios clave", { newPage: true });

heading(2, "Metodología");
para("Las pruebas del periodo combinaron tres niveles de verificación:", { keepNext: true });
bullet("**Pruebas internas del equipo de desarrollo,** en el editor de Unity y en el visor, durante la implementación de cada sala.");
bullet("**Sesiones de prueba con usuarios clave,** en las que una persona ajena al desarrollo recorre la experiencia completa en el visor. Cada sesión deja su registro de telemetría y, al terminar, el participante aporta su retroalimentación.");
bullet("**Verificación automática** con las herramientas descritas en la sección {{§Herramientas de desarrollo y verificación}}, que comprueban la configuración de la escena y la recorren sin visor en las dos dificultades.");
para("Los errores se corrigieron a medida que aparecían, y las observaciones de uso se registraron como pedidos de cambio que se trataron uno a uno. Las pruebas con estudiantes de la población objetivo están previstas para la siguiente etapa.");

heading(2, "Registro de pruebas realizadas");
para("La Tabla {{Registro de pruebas realizadas}} recoge las sesiones de prueba con usuarios clave realizadas en el periodo. La fuente de cada fila es el registro de telemetría que generó el visor durante la sesión, que se conserva en el repositorio del proyecto.");
table([["Fecha", 0.85], ["Participante", 1.9], ["Alcance", 1.3], ["Registro", 0.75], ["Resultado", 1.7]], [
  ...SESIONES.map(s => [fecha(s.run.startedUtc), s.participante,
    `Las cuatro salas, dificultad ${s.run.difficulty === 1 ? "básica" : "intermedia"}`, `PIN ${s.run.pin}`,
    `Completó las cuatro salas en ${minSeg(duracion(s.run))} de sesión; aportó retroalimentación`]),
  ["Por definir", "Estudiantes de la población objetivo", "Por definir", "—", "Pendiente"],
], { title: "Registro de pruebas realizadas" });
para("El registro queda abierto: se completará con las pruebas previstas con estudiantes de la población objetivo, que todavía no se han realizado.");

heading(2, "Resultados de las sesiones registradas");
para(`Las dos sesiones se realizaron con la versión del 30 de septiembre de 2026, cuyo registro de datos tenía un formato anterior al definitivo descrito en la sección {{§Registro de datos}}. La Tabla {{Resultados de las sesiones de prueba}} resume lo registrado en cada sala.`);
const resumenes = SESIONES.map(s => resumen(s.run));
table([["Sala", 0.75], ...SESIONES.map(s => [`Sesión ${s.run.pin} (${fecha(s.run.startedUtc)})`, 2.55])], [
  ["Sala 1", ...resumenes.map(r => r.sala1)],
  ["Sala 2", ...resumenes.map(r => r.sala2)],
  ["Sala 3", ...resumenes.map(r => r.sala3)],
  ["Sala 4", ...resumenes.map(r => r.sala4)],
  ["Total en salas", ...resumenes.map(r => r.salas)],
], { title: "Resultados de las sesiones de prueba" });
para("Del registro se desprenden observaciones coherentes con la retroalimentación recibida:", { keepNext: true });
bullet("**Sala 1:** fue la que más tiempo e intentos requirió en ambas sesiones. En la sesión 0105, los tres primeros intentos ejecutaron la misma secuencia incompleta de cuatro bloques, el tercero menos de un segundo después del segundo, lo que apunta a dudas sobre dónde colocar los bloques y a una doble pulsación del botón de ejecución.");
bullet("**Sala 2:** en la versión probada, una opción incorrecta quedaba marcada hasta que el participante la desmarcaba a mano; en la sesión 0105 ocurrió seis veces. En la versión actual, la opción incorrecta se suelta sola.");
bullet("**Sala 3:** en la sesión 0105 las repeticiones no coincidieron con el número de objetos (4 para 3 barriles; 3 y 6 para 7 cajas), lo que produjo órdenes imposibles; en la sesión 1101 coincidieron exactamente y no hubo errores. Contar los objetos es el paso crítico de la sala.");
bullet("**Sala 4:** ambas sesiones tuvieron colocaciones incorrectas, concentradas en figuras parecidas: la ficha «circulo_triangulo» se colocó por error en los huecos «triangulo_circulo», «cuadrado_trianguloinv» y «cuadrado_rombo».");
bullet("**Tiempo por sala:** con el límite de cinco minutos por sala incorporado el 9 de octubre de 2026, la sesión 0105 habría agotado el tiempo de la sala 1 (5 min 41 s) y habría continuado en la sala 2.");

heading(2, "Errores identificados y corregidos");
para("La Tabla {{Errores identificados y corregidos}} recoge los errores encontrados durante las pruebas del periodo y su corrección. Varios de ellos no eran visibles para el jugador, pero falseaban los datos registrados.");
table([["Error identificado", 1], ["Corrección aplicada", 1]], [
  ["Se perdía el intento que resolvía la sala", "El intento se registra antes de ejecutarse"],
  ["Tras reiniciar el nivel de la sala 1, llegar a la meta no tenía efecto", "La sala escucha al gestor del nivel, que no se destruye al recargar"],
  ["La sala 2 no se completaba", "El estado del módulo se actualiza antes de avisar al resto"],
  ["«Girar derecha» giraba visualmente a la izquierda", "Sentido de giro forzado en el brazo"],
  ["Un objeto olvidado en la pinza bloqueaba las ejecuciones siguientes", "Al terminar cada ejecución, lo que quede en la pinza vuelve a su pila"],
  ["La segunda ejecución de la sala 3 era imposible", "El botón permite ejecutar varias veces"],
  ["El tipo de objeto no se registraba y las repeticiones no volvían a cero", "La casilla de tipo se registra sola en su botón de ejecución"],
  ["La sala 3 se daba por completa con el último objeto aún en la pinza", "Se cuenta lo entregado en cada destino"],
  ["Desde el segundo participante se perdían datos", "Un gestor del registro por escena"],
  ["El archivo no llegaba al servidor al volver a la pantalla inicial", "La carga de la escena espera a que termine el envío"],
  ["Quitarse el visor un momento cerraba la sesión", "La pausa solo guarda; no cierra la sesión"],
  ["Las frases con variante por dificultad no sonaban", "Identificadores de frase como texto"],
  ["Tras reiniciar la sala 4, un hueco quedaba resuelto pero vacío", "El reinicio no mueve las fichas acertadas"],
  ["Las fichas con física se salían del hueco", "Se inmovilizan al encajar"],
  ["Un intento de la sala 3 quedaba antes de su inicio y un agarre de la sala 4 se contaba en la 3", "Cada sala abre su registro al llegar"],
  ["Errores 502 y 401 en el envío al servidor", "Corrección de puertos, nombre de host y clave del servidor; diagnóstico por capas desde el editor"],
  ["Los agarres de la sala 4 no se contaban", "Contador propio de la sala 4"],
  ["Tirones al escribir el registro", "Escritura diferida cada 10 s"],
  ["La función de saltar pasos, usada en pruebas, no saltaba los que esperan a un reto", "La secuencia comprueba el salto en cada fotograma"],
  ["Al soltar un bloque, no encajaba hasta apartar la mano", "La suelta se detecta al abrir la mano; pendiente de validar en el visor"],
  ["Cada reto de las salas 2 a 4 se abría dos veces en la secuencia", "Una sola apertura, al llegar a la sala, vigilada por la comprobación automática"],
  ["Al cargar la escena sonaban a la vez los acoples de la fila ya montada", "Los bloques que coloca el sistema encajan en silencio"],
  ["Un fallo al escribir el registro podía detener la experiencia", "El error se anota y la escritura se reintenta sin interrumpir la sesión"],
  ["Una sala podía no cerrarse por tiempo si un intento se quedaba sin terminar", "La espera al intento en curso tiene un tope"],
], { title: "Errores identificados y corregidos", sz: 17 });

heading(2, "Observaciones de uso");
para("La retroalimentación recibida tras las sesiones de prueba y la presentación de la versión se agrupa por sala:", { keepNext: true });
bullet("**Sala 1:** no queda claro dónde colocar los bloques ni que el objetivo es abrir la puerta; se pide mejorar las indicaciones visuales del objetivo, disponer la fila de huecos en horizontal y el panel sobre la mesa.");
bullet("**Sala 2:** las instrucciones resultan ambiguas y conviene simplificar las preguntas; los paneles son grandes y no dejan ver bien el estado corregido o sin corregir; se pide que el botón de una opción incorrecta se desactive solo y ajustar los textos «Sistema de motores» y «Agregar combustible».");
bullet("**Sala 3:** en la dificultad básica sobran las instrucciones visibles; se observan problemas de centrado y de posición inicial del jugador.");
bullet("**Sala 4:** se propone una animación de regreso (Tierra → Ecuador → Yachay Tech) y un juego de figuras basado en el alfabeto Yachay para la dificultad intermedia.");
bullet("**General:** revisar el funcionamiento del clic y la respuesta de los botones, mejorar el contraste del texto de los botones, incorporar sonido en cada interacción, reducir el tiempo necesario para comprender cada sala e incluir una demostración inicial de las interacciones.");
bullet("**Registro de datos:** contar los reinicios, registrar el tiempo de cada selección y colocación y el tiempo total, identificar el visor, usar valores verdadero o falso, separar y depurar los tipos de error y no contar las deselecciones.");

heading(2, "Limitaciones técnicas encontradas");
bullet("**Rendimiento sin medir en el visor:** falta comprobar el objetivo de 60 FPS y corregir el perfil de calidad de Android (sección {{§Ajustes para la ejecución en Meta Quest}}).");
bullet("**Seguimiento de manos:** habilitado, pero sin validar en el visor.");
bullet("**Envío al cerrar la aplicación:** el sistema del visor cierra el proceso sin esperar a que termine un envío, por lo que no es fiable enviar los datos en ese momento. La sesión queda guardada en el visor y, desde el 12 de octubre de 2026, se envía de forma automática la siguiente vez que se abre la pantalla del supervisor.");
bullet("**Dependencia de la red:** el envío requiere conexión; sin ella, los archivos se extraen por cable.");
bullet("**Identificación del visor:** el código del visor depende de la clave con la que se firma la aplicación, por lo que todas las compilaciones deben usar la misma.");
bullet("**Requisitos aún no implementados:** el indicador de estado diegético (RI-02) y el reinicio supervisado (RF-08).");

heading(2, "Ajustes aplicados");
para("La Tabla {{Ajustes aplicados en el periodo}} recoge los ajustes aplicados durante el periodo a partir de la revisión de la versión y de la retroalimentación recibida.");
table([["Fecha", 0.8], ["Ajuste", 5]], [
  ["29/09/2026", "Texto de la narración completo de una vez; cuatro opciones en las dos dificultades de la sala 2; repeticiones a cero al colocar la ficha de tipo en la sala 3; física y aviso de acierto en la sala 4"],
  ["30/09/2026", "La ficha incorrecta sale expulsada; el reinicio respeta las fichas acertadas; nivel intermedio definitivo de la sala 1"],
  ["06/10/2026", "Formato definitivo del registro (reinicios, tiempo por acción, tiempo total, código del visor, valores verdadero o falso, tipos de error separados, sin deselecciones); el botón incorrecto de la sala 2 se suelta solo; tutorial inicial con botón INICIAR; fila de huecos de la sala 1 en horizontal"],
  ["09/10/2026", "Dificultades en una sola escena; sala 3 básica sin instrucciones visibles; textos «Sistema de motores» y «Agregar combustible»; tiempo límite de 5 minutos por sala; herramientas de comprobación y prueba automática de la escena"],
  ["12/10/2026", "Voz definitiva de la narración; sala 4 intermedia con las letras de YACHAY en glifos cuadrados; reloj de sesión de 15 minutos con máximo de 5 por sala; envío automático de las sesiones pendientes y botón de envío en la pantalla del supervisor; un fondo de panel por dificultad en la sala 3; sonidos del brazo robótico; el bloque encaja al soltarlo; una sola apertura por reto; retirada de escenas, recursos y código sin uso"],
], { title: "Ajustes aplicados en el periodo" });

heading(2, "Ajustes priorizados para la siguiente etapa");
para("A partir de las observaciones y limitaciones anteriores, se priorizan los ajustes de la Tabla {{Ajustes priorizados para la siguiente etapa}}.");
table([["Prioridad", 0.7], ["Ajuste", 4.3], ["Ámbito", 1]], [
  ["1", "Compilar la versión actual, probarla en el visor con el plan de prueba del registro de datos, validar el acople al soltar, el cierre por tiempo de sesión y el envío de sesiones pendientes, y medir el rendimiento", "General"],
  ["2", "Pruebas con estudiantes de la población objetivo y su incorporación al registro de pruebas", "General"],
  ["3", "Indicaciones visuales de dónde colocar los bloques y del objetivo de abrir la puerta", "Sala 1"],
  ["4", "Separar los bloques de la paleta intermedia que se superponen entre sí sobre la mesa (Figura {{Sala 1, dificultad intermedia: 13 bloques para una fila de 11 huecos}})", "Sala 1"],
  ["5", "Simplificar los enunciados y reducir el tamaño de los paneles", "Sala 2"],
  ["6", "Repeticiones a cero tras cada ejecución; retirar los intentos fallidos del registro y registrar la cantidad disponible por intento", "Sala 3"],
  ["7", "Animación de regreso (Tierra, Ecuador, Yachay Tech)", "Sala 4"],
  ["8", "Revisar que cada locución coincide con su texto en pantalla, en especial la frase que anuncia el tiempo, tras el cambio del reloj a 15 minutos", "Narración"],
  ["9", "Perfil de render para el visor y validación del seguimiento de manos", "General"],
], { title: "Ajustes priorizados para la siguiente etapa" });


// =====================================================================================
// Montaje sobre la plantilla
// =====================================================================================

const files = readZip(TEMPLATE);
const read = name => files.get(name).toString("utf8");

const docXml = read("word/document.xml");
const bodyStart = docXml.indexOf("<w:body>") + "<w:body>".length;
const bodyEnd = docXml.lastIndexOf("</w:body>");
const items = topLevel(docXml.slice(bodyStart, bodyEnd));

const stripComments = x => x
  .replace(/<w:commentRangeStart [^>]*\/>/g, "")
  .replace(/<w:commentRangeEnd [^>]*\/>/g, "")
  .replace(/<w:r>(?:(?!<\/w:r>).)*?<w:commentReference [^>]*\/>(?:(?!<\/w:r>).)*?<\/w:r>/g, "");

/** Deja el texto en el primer <w:t> del párrafo y vacía los demás. */
function setText(xml, text) {
  let first = true;
  return xml.replace(/<w:t(\s[^>]*)?>[^<]*<\/w:t>/g, () => {
    const out = first ? `<w:t xml:space="preserve">${esc(text)}</w:t>` : `<w:t xml:space="preserve"></w:t>`;
    first = false;
    return out;
  });
}

/**
 * Subraya en rojo el run que contiene exactamente ese texto. El subrayado va detrás del tamaño
 * de letra dentro de <w:rPr>, que es el orden que exige el esquema de Word.
 */
function underlineRed(xml, text) {
  const target = `<w:t xml:space="preserve">${esc(text)}</w:t>`;
  const at = xml.indexOf(target);
  if (at < 0) throw new Error(`No encuentro "${text}" para subrayarlo`);

  const runStart = Math.max(xml.lastIndexOf("<w:r>", at), xml.lastIndexOf("<w:r ", at));
  let run = xml.slice(runStart, at);
  const u = '<w:u w:val="single" w:color="FF0000"/>';

  if (run.includes("<w:rPr>")) {
    run = /<w:szCs [^>]*\/>/.test(run) ? run.replace(/(<w:szCs [^>]*\/>)/, `$1${u}`) : run.replace("</w:rPr>", `${u}</w:rPr>`);
  } else {
    run = run.replace(/^<w:r>/, `<w:r><w:rPr>${u}</w:rPr>`);
  }

  return xml.slice(0, runStart) + run + xml.slice(at);
}

const expect = (n, fragment) => {
  if (!textOf(items[n].xml).includes(fragment)) throw new Error(`La plantilla cambió: el elemento ${n} no contiene "${fragment}"`);
};
expect(3, "Producto 1"); expect(7, "1.1"); expect(10, "1.2"); expect(12, "1.3"); expect(16, "agosto"); expect(24, "Contenido");

const cover = [];
for (let n = 0; n <= 23; n++) {
  let x = stripComments(items[n].xml);
  if (n === 3) x = setText(x, "Producto 2: ");
  if (n === 4) x = setText(x, PRODUCTO);
  if (n === 7) x = setText(x, ENTREGABLES[0][0]);
  if (n === 8) x = setText(x, ENTREGABLES[0][1]);
  if (n === 9) continue;
  if (n === 10) x = setText(x, ENTREGABLES[1][0]);
  if (n === 11) x = setText(x, ENTREGABLES[1][1]);
  if (n === 12) x = setText(x, ENTREGABLES[2][0]);
  if (n === 13) x = setText(x, ENTREGABLES[2][1]);
  if (n === 16) {
    x = setText(x, FECHA_LARGA);
    if (FECHA_PROVISIONAL) x = underlineRed(x, FECHA_LARGA);
  }
  if (n === 20) {
    // La fecha está partida en tres runs: "13/0", "8", "/2026".
    x = x.replace(/<w:t(?:\s[^>]*)?>13\/0<\/w:t>/, `<w:t xml:space="preserve">${FECHA_CORTA}</w:t>`)
         .replace(/(<w:t(?:\s[^>]*)?>)8(<\/w:t>)/, "$1$2")
         .replace(/(<w:t(?:\s[^>]*)?>)\/2026(<\/w:t>)/, "$1$2");
    if (FECHA_PROVISIONAL) x = underlineRed(x, FECHA_CORTA);
  }
  cover.push(x);
}
cover.push(stripComments(items[24].xml));

// Índice: mismas piezas que el del mes 1, con las entradas de este informe. Los números de
// página son una estimación; Word los recalcula al abrir (updateFields en settings.xml).
const toc = items[25].xml;
const sdtPr = toc.slice(0, toc.indexOf("<w:sdtContent>"));
const tocInner = topLevel(toc.slice(toc.indexOf("<w:sdtContent>") + "<w:sdtContent>".length, toc.lastIndexOf("</w:sdtContent>")));
const tocClose = tocInner[tocInner.length - 1].xml;

const LINES_PER_PAGE = 50, FIRST_BODY_PAGE = 5;
let page = FIRST_BODY_PAGE, used = 0;
const pageOf = [];
blocks.forEach((b, i) => {
  if (b.lines >= 1000) { if (used > 0) page++; used = 0; b.lines -= 1000; }
  if (used + b.lines > LINES_PER_PAGE && used > 0) { page++; used = 0; }
  pageOf[i] = page;
  used += b.lines;
});

const tocEntries = headings.map((h, i) => {
  const style = ["1348", "1349", "1350"][h.level - 1];
  const tab = [567, 850, 1418][h.level - 1];
  const sz = '<w:rPr><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr>';
  const fieldStart = i === 0
    ? `<w:r><w:rPr><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr><w:fldChar w:fldCharType="begin"/></w:r>` +
      `<w:r><w:rPr><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr><w:instrText xml:space="preserve">TOC \\h \\t &quot;Heading 1,1,Heading 2,2,Heading 3,3,,1&quot; </w:instrText></w:r>` +
      `<w:r><w:rPr><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr><w:fldChar w:fldCharType="separate"/></w:r>`
    : "";
  return `<w:p><w:pPr><w:pStyle w:val="${style}"/><w:tabs><w:tab w:val="left" w:leader="none" w:pos="${tab}"/><w:tab w:val="right" w:leader="dot" w:pos="8494"/></w:tabs>${sz}</w:pPr>${fieldStart}` +
    `<w:hyperlink w:anchor="${h.bookmark}" w:history="1">` +
    `<w:r>${rPr()}<w:t xml:space="preserve">${h.number}</w:t></w:r><w:r>${sz}<w:tab/></w:r>` +
    `<w:r><w:rPr><w:rStyle w:val="1346"/><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr><w:t xml:space="preserve">${esc(h.text)}</w:t></w:r>` +
    `<w:r>${sz}<w:tab/></w:r>` +
    `<w:r>${sz}<w:fldChar w:fldCharType="begin"/></w:r><w:r>${sz}<w:instrText xml:space="preserve"> PAGEREF ${h.bookmark} \\h </w:instrText></w:r>` +
    `<w:r>${sz}<w:fldChar w:fldCharType="separate"/></w:r><w:r>${sz}<w:t>${pageOf[h.block]}</w:t></w:r><w:r>${sz}<w:fldChar w:fldCharType="end"/></w:r>` +
    `</w:hyperlink></w:p>`;
});
const newToc = `${sdtPr}<w:sdtContent>${tocEntries.join("")}${tocClose}</w:sdtContent></w:sdt>`;

// Salto de sección tras el índice y, al final, la sección del cuerpo del mes 1 (cabecera con
// el título del informe y número de página).
const tocSection = items[26].xml;
const bodySectPr = items[170].xml.match(/<w:sectPr[\s\S]*?<\/w:sectPr>/)[0];

const newBody = [...cover, newToc, tocSection, ...blocks.map(b => resolveReferences(b.xml)), bodySectPr].join("");
files.set("word/document.xml", docXml.slice(0, bodyStart) + newBody + docXml.slice(bodyEnd));

// Relaciones: fuera las imágenes del cuerpo del mes 1, su enlace y los comentarios; dentro las
// capturas del mes 2.
const removedTargets = /Target="(media\/image(?:[3-9]|1[0-2])\.png|https:\/\/github\.com[^"]*|comments\.xml|commentsExtended\.xml|commentsExtensible\.xml|commentsIds\.xml|people\.xml)"/;
let rels = read("word/_rels/document.xml.rels");
rels = rels.replace(/<Relationship [^>]*\/>/g, r => (removedTargets.test(r) ? "" : r));
rels = rels.replace("</Relationships>", media.map(m =>
  `<Relationship Id="${m.rId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/${m.name}"/>`).join("") + "</Relationships>");
files.set("word/_rels/document.xml.rels", rels);

for (let i = 3; i <= 12; i++) files.delete(`word/media/image${i}.png`);
for (const name of ["word/comments.xml", "word/commentsExtended.xml", "word/commentsExtensible.xml", "word/commentsIds.xml", "word/people.xml", "word/_rels/comments.xml.rels"])
  files.delete(name);
for (const m of media) files.set(`word/media/${m.name}`, fs.readFileSync(m.src));

let types = read("[Content_Types].xml");
types = types.replace(/<Override\s+PartName="\/word\/(comments|commentsExtended|commentsExtensible|commentsIds|people)\.xml"[^>]*\/>/g, "");
files.set("[Content_Types].xml", types);

// Cabecera del cuerpo: el título del informe.
const header = read("word/header4.xml");
if (!header.includes("Especificación de requisitos de software")) throw new Error("La cabecera de la plantilla cambió");
files.set("word/header4.xml", header.replace("Especificación de requisitos de software", esc(CABECERA)));

// Listas numeradas propias: la plantilla no tiene ninguna libre.
let numbering = read("word/numbering.xml");
const abstractNum = `<w:abstractNum w:abstractNumId="900"><w:multiLevelType w:val="hybridMultilevel"/>` +
  `<w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/><w:lvlJc w:val="left"/>` +
  `<w:pPr><w:ind w:left="1069" w:hanging="360"/></w:pPr><w:rPr>${FONT}</w:rPr></w:lvl></w:abstractNum>`;
const firstNum = numbering.search(/<w:num w:numId=/);
numbering = numbering.slice(0, firstNum) + abstractNum + numbering.slice(firstNum);
const lastNumEnd = numbering.lastIndexOf("</w:num>") + "</w:num>".length;
numbering = numbering.slice(0, lastNumEnd) + numberedLists.map(id =>
  `<w:num w:numId="${id}"><w:abstractNumId w:val="900"/><w:lvlOverride w:ilvl="0"><w:startOverride w:val="1"/></w:lvlOverride></w:num>`).join("") +
  numbering.slice(lastNumEnd);
files.set("word/numbering.xml", numbering);

// Que Word rehaga índice y números de figura al abrir.
let settings = read("word/settings.xml");
if (!settings.includes("<w:updateFields")) settings = settings.replace("<w:footnotePr", '<w:updateFields w:val="true"/><w:footnotePr');
files.set("word/settings.xml", settings);

let core = read("docProps/core.xml");
core = core.replace(/(<dcterms:modified[^>]*>)[^<]*/, `$1${new Date().toISOString().replace(/\.\d+Z$/, "Z")}`);
files.set("docProps/core.xml", core);

// [Content_Types].xml primero, como en cualquier docx.
const ordered = new Map([["[Content_Types].xml", files.get("[Content_Types].xml")]]);
for (const [k, v] of files) if (k !== "[Content_Types].xml") ordered.set(k, v);
writeZip(OUTPUT, ordered);

console.log(`${path.relative(ROOT, OUTPUT)}: ${headings.filter(h => h.level === 1).length} capítulos, ${headings.length} títulos, ` +
            `${tableNo} tablas, ${figureNo} figuras, ${blocks.length} bloques; cuerpo estimado en ${page - FIRST_BODY_PAGE + 1} páginas`);
