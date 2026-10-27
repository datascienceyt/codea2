// Genera Docs/Informe-Mes3.docx a partir de Docs/Informe-Mes1.docx: mismo formato (portada,
// estilos, cabeceras, pies, índice) que los informes de los meses 1 y 2, con los entregables del
// mes 3 (3.1, 3.2 y 3.3).
//
// Uso, desde la raíz del repositorio:  node Docs/Herramientas/generar_informe_mes3.js
//
// Misma maquinaria que generar_informe_mes2.js. El texto entre ⟨ ⟩ está pendiente de confirmar:
// sale en rojo y entre corchetes, para completarlo antes de entregar. Las figuras salen de
// Docs/Entregable-Mes3/capturas (CODEA_CAPTURAS con esa carpeta y CodeaCaptures.RunBatch).

const fs = require("fs");
const path = require("path");
const { topLevel, textOf, esc, readZip, writeZip } = require("./docx_lib.js");
const { paginasDelIndice } = require("./paginas_onlyoffice.js");

// --- Datos de la portada. Cambiar aquí y volver a generar ---

const FECHA_LARGA = "24 de noviembre de 2026";
const FECHA_CORTA = "24/11/2026";
const PRODUCTO = "Informe técnico de optimización, validación funcional y entrega final del módulo inmersivo";
const CABECERA = "Optimización, validación funcional y entrega del módulo inmersivo";

// La fecha puede cambiar antes de entregar: va subrayada en rojo para no olvidarla.
const FECHA_PROVISIONAL = true;

const ENTREGABLES = [
  ["3.1 Optimización técnica del módulo",
   "Descripción de los ajustes realizados para mejorar estabilidad, rendimiento, navegación, interacciones y experiencia de uso en dispositivos Meta Quest."],
  ["3.2 Validación funcional final con usuarios clave",
   "Registro de pruebas finales con el equipo del proyecto o usuarios clave, errores corregidos, evidencias de funcionamiento y verificación de cumplimiento de los requerimientos técnicos y funcionales definidos."],
  ["3.3 Entrega técnica del módulo inmersivo",
   "Entrega del proyecto Unity, archivos asociados, instrucciones de instalación, ejecución y configuración en dispositivos Meta Quest; documentación técnica del código, estructura del proyecto, escenas, recursos, interacciones implementadas y recomendaciones para mantenimiento o futuras mejoras; y sesión de transferencia técnica al equipo del proyecto sobre el desarrollo realizado, funcionamiento del módulo y consideraciones para realizar ajustes posteriores."],
];

const ROOT = path.resolve(__dirname, "..", "..");
const TEMPLATE = path.join(ROOT, "Docs", "Informe-Mes1.docx");
// Otra ruta como argumento si el informe está abierto en Word, que lo bloquea.
const OUTPUT = process.argv[2] ? path.resolve(process.argv[2]) : path.join(ROOT, "Docs", "Informe-Mes3.docx");
const CAPTURAS = path.join(ROOT, "Docs", "Entregable-Mes3", "capturas");

// --- Piezas de formato, copiadas de la plantilla ---

const FONT = '<w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:eastAsia="Arial" w:cs="Arial"/>';

function rPr({ b = false, i = false, sz = 20, color = "000000" } = {}) {
  return `<w:rPr>${FONT}${b ? "<w:b/><w:bCs/>" : ""}${i ? "<w:i/><w:iCs/>" : ""}<w:color w:val="${color}"/><w:sz w:val="${sz}"/><w:szCs w:val="${sz}"/></w:rPr>`;
}

/** Texto con **negrita** a runs. */
function runs(text, { sz = 20, b = false } = {}) {
  return String(text).split(/(\*\*[^*]+\*\*|⟨[^⟩]+⟩)/).filter(s => s.length).map(part => {
    const bold = part.startsWith("**") && part.endsWith("**");
    // ⟨…⟩: pendiente de confirmar. En rojo y entre corchetes, para encontrarlo antes de entregar.
    const pending = part.startsWith("⟨") && part.endsWith("⟩");
    const t = bold ? part.slice(2, -2) : pending ? `[${part.slice(1, -1)}]` : part;
    return `<w:r>${rPr({ b: b || bold, sz, color: pending ? "C00000" : "000000" })}<w:t xml:space="preserve">${esc(t)}</w:t></w:r>`;
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
  const bookmark = `_TocM3_${bookmarkId}`;
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

  const rId = `rIdM3Img${media.length + 1}`;
  const name = `m3_figura${media.length + 1}.jpeg`;
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
// Tools → Codea → Comprobar escena de juego. Sin informe, el valor del 27/10/2026.
const CONEXIONES = (() => {
  try {
    const log = fs.readFileSync(path.join(ROOT, "Logs", "Codea_Comprobacion.txt"), "utf8");
    const n = [...log.matchAll(/✓ (?:Setup: )?(\d+) llamadas de UnityEvent/g)].map(m => Number(m[1]));
    return n.length === 2 ? n[0] + n[1] : 378;
  } catch { return 378; }
})();

// Tamaño del código, contado al generar.
const CODIGO = (() => {
  const files = [];
  const walk = dir => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) walk(p);
      else if (e.name.endsWith(".cs")) files.push(p);
    }
  };
  walk(path.join(ROOT, "Assets", "_Main"));
  const lines = files.reduce((n, f) => n + fs.readFileSync(f, "utf8").split("\n").length, 0);
  const editor = files.filter(f => f.includes(`${path.sep}Editor${path.sep}`)).length;
  return { total: files.length, editor, juego: files.length - editor, miles: (Math.round(lines / 100) * 100).toLocaleString("es-ES") };
})();

// --- 1. Optimización técnica del módulo ---

heading(1, "Optimización técnica del módulo");

heading(2, "Propósito");
para("El presente informe técnico documenta la tercera y última etapa del desarrollo del módulo inmersivo Codea VR, comprendida entre la entrega del Producto 2 (24 de octubre de 2026) y la entrega final. El Producto 1 estableció los requerimientos técnicos y funcionales y el diseño del módulo, y el Producto 2 documentó su implementación, la integración de los recursos 3D y las interacciones, y las pruebas preliminares con usuarios clave y estudiantes. Este documento recoge los ajustes de optimización aplicados a partir de esas pruebas, la validación funcional final y la entrega técnica del módulo: proyecto, instrucciones de instalación y configuración en Meta Quest, documentación técnica y transferencia al equipo del proyecto.");

heading(2, "Estado del módulo al cierre");
para("Al cierre del proyecto, el módulo es una experiencia completa de principio a fin en sus dos dificultades. La Tabla {{Ficha técnica del módulo entregado}} resume sus características; el detalle de su funcionamiento se describió en el Producto 2 y la documentación de entrega se recoge en el capítulo {{§Entrega técnica del módulo inmersivo}}.");
table([["Campo", 1], ["Valor", 2.6]], [
  ["Tipo de aplicación", "Escape room educativo de realidad virtual, un jugador, vista en primera persona"],
  ["Dispositivo", "Meta Quest 3S (Horizon OS)"],
  ["Motor y lenguaje", "Unity 6000.4.1f1 con Universal Render Pipeline y Shader Graph; C#"],
  ["SDK de realidad virtual", "Meta XR All-in-One SDK 203.0.2 sobre OpenXR"],
  ["Escenas", "Setup (pantalla del supervisor) y Juego (tutorial y cuatro salas, para las dos dificultades)"],
  ["Salas", "Cuatro: secuencialidad, condicionales, bucles y parámetros, y patrones"],
  ["Duración máxima", "15 minutos de sesión y 5 por sala"],
  ["Entrada", "Mandos Meta Quest Touch, con interacción con el cuerpo completo del mando; compatibilidad con manos habilitada"],
  ["Registro de datos", "Un archivo JSON por participante, enviado por HTTP POST; funcionamiento completo sin conexión"],
  ["Código fuente", `${CODIGO.total} scripts en C# (${CODIGO.juego} del módulo y ${CODIGO.editor} herramientas de editor; unas ${CODIGO.miles} líneas)`],
  ["Versión de entrega", "1.0.0, rama dev del repositorio github.com/datascienceyt/codea2"],
], { title: "Ficha técnica del módulo entregado" });

heading(2, "Origen de los ajustes");
para("Los ajustes de esta etapa, aplicados el 27 de octubre de 2026, responden a la retroalimentación del equipo del proyecto tras probar en el visor la versión 0.9.0 ⟨fecha y participantes de la sesión de revisión⟩, que se sumó a las observaciones de las sesiones con estudiantes del 22 de octubre de 2026 recogidas en el Producto 2. Las observaciones se organizaron por sala y se trataron una a una: cada una se confirmó con el equipo antes de aplicarla, y las de diseño (alturas, distancias, colocación de paneles) quedaron a cargo del equipo en el editor. La Tabla {{Observaciones recibidas y respuesta}} resume las observaciones y la respuesta a cada una; las secciones siguientes explican cómo se implementaron.");
table([["Ámbito", 0.8], ["Observación", 2.4], ["Respuesta", 2.8]], [
  ["Tutorial", "Reducir y sintetizar el texto", "Frase del tutorial reducida a un tercio"],
  ["Tutorial", "La ficha con gravedad no vuelve si se cae", "Vuelve sola a su sitio a los 4 segundos, como en la sala 4"],
  ["Sala 1", "Demasiada narración", "Frases más cortas y dos frases retiradas"],
  ["Sala 1", "El robot debe empezar mirando hacia arriba y el recorrido dar una vuelta completa", "Nivel en espejo con el robot mirando hacia arriba y la salida frente al botón físico"],
  ["Sala 2", "Texto del problema más grande y terminado siempre en «¿Qué hacemos?»", "Letra hasta un 33 % mayor, ajustada para caber, y la pregunta en una línea propia"],
  ["Sala 2", "El texto «Probar» aparecía repetido junto al botón", "Rótulo duplicado retirado y botón centrado"],
  ["Sala 2", "Cambiar el color de selección", "Las opciones marcadas pasan a verde"],
  ["Sala 2", "El enunciado de motores («hacen ruido») no ayudaba", "«Los motores no arrancan.», sin incluir la solución"],
  ["Sala 3", "Indicar que las cajas van a la derecha", "Frase 12: los barriles rojos van a la izquierda y las cajas celestes a la derecha"],
  ["Sala 3", "Restar las repeticiones de una en una y no reiniciarlas al cambiar de ficha", "El contador baja una unidad por vuelta terminada hasta llegar a cero"],
  ["Sala 3", "«Empezar» debe decir «Probar»; colores de los botones de sumar y restar", "Botón Probar celeste como el resto; + y − en gris con el mismo material"],
  ["General", "Los huecos deben cambiar a verde al colocar un bloque", "Todo hueco de bloque se tiñe de verde mientras tiene un bloque"],
  ["General", "Reducir el recorrido de los botones", "Recorrido reducido al 40 % en todos los botones de pulsar"],
  ["General", "La interacción con el punto blanco de la punta del mando no es intuitiva", "Se toca y se agarra con el cuerpo completo del mando"],
  ["Sala 4", "Ver la llegada a la Tierra sin animar la nave", "La Tierra se acerca sin detenerse hasta que se cierra la escena"],
  ["Sala 4", "El botón de reinicio no se alcanza", "⟨Ajuste aplicado en el editor⟩"],
  ["Sala 2", "Más retroalimentación o mejor visualización", "⟨Ajuste aplicado en el editor⟩"],
  ["General", "Bajar la altura de todo; mesa de la sala 1 más baja y más cerca; salas 2 y 3 más bajas; acercar el panel del tutorial", "⟨Alturas y distancias ajustadas en el editor⟩"],
], { title: "Observaciones recibidas y respuesta", sz: 17 });

heading(2, "Interacciones");
para("Las interacciones fueron el ámbito con más ajustes. Los estudiantes y el equipo coincidieron en que el mando debía responder como un objeto completo, sin que el estudiante tuviera que aprender a apuntar con una esfera pequeña, y en que cada acción debía dar una respuesta inmediata y visible.");
bullet("**Interacción con el cuerpo del mando.** En el rig de Meta, el punto de pulsación de cada mando es una esfera de 0,5 cm en su punta, marcada con una bolita blanca, y el de agarre, una esfera de 5 cm. Un componente nuevo, que se instala solo al cargar la escena de juego y no modifica el rig del SDK, traslada ambos al centro del modelo del mando y amplía su radio hasta cubrirlo: entre 2 y 5 cm para pulsar y hasta 8 cm para agarrar. La bolita blanca se oculta, porque ya no indica dónde se toca. El ajuste se limita a los mandos y a la escena de juego: las manos no cambian, y en la pantalla del supervisor se mantiene la precisión de la punta, porque sus teclas están juntas.");
bullet("**Recorrido de los botones.** El recorrido que hay que hundir un botón hasta que se activa se redujo al 40 % en los cuatro modelos de botón de pulsar de la experiencia (de 18 a 7,2 mm y de 34,8 a 13,9 mm a la escala del modelo), sin cambiar su aspecto en reposo.");
bullet("**Huecos en verde.** Todo hueco de bloque (tutorial, sala 1 y sala 3) se tiñe de verde mientras tiene un bloque y vuelve a su color al vaciarse, de modo que se ve de un vistazo qué falta. Los huecos de figuras de la sala 4 no cambian, porque allí el verde indica solo la ficha correcta.");
bullet("**Botones Probar unificados.** Todos los botones que inician una comprobación (tutorial y salas 1, 2 y 3) dicen «Probar» y tienen el mismo color celeste.");

heading(2, "Experiencia de uso y navegación");
bullet("**Narración más breve.** Se reescribieron siete frases del tutorial y de la sala 1 y se retiraron dos (Tabla {{Frases de la narración modificadas}}). El narrador omite las frases retiradas sin mostrar la pantalla, y las listas de narración conservan su orden, por lo que el resto de la historia no se ve afectado.");
bullet("**Sala 1 en espejo.** El nivel se rediseñó para que el robot empiece mirando hacia arriba y el recorrido dé la vuelta a la sala antes de llegar al botón (Figura {{Sala 1: el robot empieza mirando hacia arriba}}). La salida no se movió, porque el botón que abre la puerta es un objeto físico de la sala. En la dificultad intermedia, que ya era simétrica, bastó con girar el robot; en la básica se partió del espejo del nivel y se añadió un muro para que el recorrido no se acorte. Con el robot mirando hacia arriba la solución necesita más giros a la izquierda que a la derecha, por lo que la mesa de la dificultad básica ofrece ahora dos bloques «Girar izquierda» y uno «Girar derecha». La dirección inicial del robot forma parte del archivo de cada nivel, y la herramienta que resuelve los niveles la tiene en cuenta.");
bullet("**Sala 2 más legible.** Cada problema termina con la pregunta «¿Qué hacemos?» en una línea propia y su letra crece de 30 a 40 puntos, ajustándose sola para no salirse de la pantalla del módulo (Figura {{Sala 2: problema con la pregunta «¿Qué hacemos?» y botón Probar centrado}}).");
bullet("**Sala 3: el bucle se ve contar.** Cada vuelta terminada resta una repetición del contador, que llega a cero al acabar la pasada; para la pasada siguiente el estudiante vuelve a decidir cuántas repeticiones. Mientras el brazo trabaja, + y − no responden.");
bullet("**Regreso a la Tierra.** Al completar la sala 4, la Tierra se acerca a la nave y no deja de crecer hasta que se cierra la escena, cada vez más despacio, sin llegar a introducir la cámara en su atmósfera. Sustituye a la animación anterior, que se detenía a los 25 segundos.");
table([["Frase", 0.5], ["Antes", 2.6], ["Ahora", 2.4]], [
  ["0", "¡Hola, astronauta! Antes de comenzar la misión, vamos a practicar con las herramientas de la nave. Sobre la mesa hay dos botones: presiona cada uno con tu mano. Después, toma el bloque y suéltalo en el espacio vacío… (69 palabras)", "¡Hola, astronauta! Vamos a practicar: presiona los dos botones, encaja el bloque en su hueco y pon la ficha sobre su misma figura."],
  ["1", "¡Bienvenido, astronauta! Tu misión está a punto de comenzar.", "¡Bienvenido, astronauta! Una tormenta espacial dañó la nave. ¡Necesitamos tu ayuda!"],
  ["2", "Una tormenta espacial desordenó algunas cosas y provocó pequeñas fallas…", "Retirada (su contenido pasa a la frase 1)"],
  ["3", "Tienes cinco minutos para reparar cada sistema. Si se acaba el tiempo, pasaremos al siguiente. ¡Adelante, astronauta!", "Tienes quince minutos para repararla. ¡Adelante!"],
  ["4", "La puerta de tu habitación está cerrada. Para abrirla, debemos presionar el botón verde que está al otro lado de la habitación.", "La puerta está cerrada. Guía a Roki, el robot, hasta el botón verde para abrirla."],
  ["5", "Roki, el robot de mantenimiento te ayudará. Puedes verlo a la derecha. Guíalo hasta el botón verde.", "Retirada (su contenido pasa a la frase 4)"],
  ["6", "Ordena los bloques de la mesa para crear su recorrido. Cuando termines, inicia la secuencia.", "Ordena los bloques de la mesa y presiona el botón azul."],
  ["7", "¡Muy bien, astronauta! Roki llegó al botón y la puerta está abierta. Puedes continuar.", "¡Muy bien! La puerta está abierta."],
  ["12", "Fíjate en el color de las cajas y observa dónde debe ir cada una.", "Fíjate en el color: los barriles rojos van a tu izquierda y las cajas celestes, a tu derecha."],
], { title: "Frases de la narración modificadas", sz: 16 });
para("El tutorial y la introducción de la sala 1 pasan de 179 a 74 palabras. Las nuevas locuciones de las frases modificadas ⟨grabadas el …⟩ se asignan a su frase con la herramienta del editor que coloca cada audio a partir del nombre del archivo; hasta entonces, esas frases se muestran solo como texto.");

heading(2, "Estabilidad y mantenibilidad");
para("Los errores encontrados en la etapa y su corrección se recogen en la sección {{§Errores corregidos}}. Los cambios de esta etapa que afectan a la escena y a los modelos de botón (materiales, bloques de la mesa, rótulos y recorrido) se aplican con una herramienta del editor repetible, que no los duplica si se vuelve a ejecutar: cada cambio queda documentado en código y se puede rehacer sobre una escena restaurada.");

heading(2, "Rendimiento");
para("Los ajustes de esta etapa se diseñaron para no añadir coste en el visor:", { keepNext: true });
bullet("El verde de los huecos se aplica con bloques de propiedades del material, sin crear materiales nuevos, de modo que no se rompe el agrupamiento de objetos que comparten material al dibujar.");
bullet("El componente que amplía el área de interacción del mando calcula el centro del mando una sola vez, cuando su modelo aparece, y no en cada fotograma.");
bullet("El acercamiento de la Tierra mueve un único objeto por fotograma, sin cambiar su escala ni la de su atmósfera.");
para("Se mantienen las optimizaciones descritas en el Producto 2: escritura diferida del registro de datos, registro de huecos activos para el acople por proximidad y texto de la narración sin animación. ⟨Resultado de la medición de rendimiento en el visor con la versión 1.0.0 (OVR Metrics Tool): fotogramas por segundo medios y mínimos por sala.⟩");

// --- 2. Validación funcional final con usuarios clave ---

heading(1, "Validación funcional final con usuarios clave", { newPage: true });

heading(2, "Metodología");
para("La validación final combinó tres niveles de verificación:", { keepNext: true });
bullet("**Verificación automática** con las herramientas del editor: la comprobación de la escena, que la revisa sin ejecutarla, y la prueba en Play, que recorre las dos dificultades sin visor (sección {{§Herramientas de mantenimiento}}).");
bullet("**Revisión del equipo del proyecto** en el visor, que dio lugar a las observaciones de la Tabla {{Observaciones recibidas y respuesta}}.");
bullet("**Prueba final en el visor** con la versión de entrega, siguiendo una lista de comprobación de 44 pasos que cubre la sesión completa en las dos dificultades, el registro de datos y el envío al servidor.");

heading(2, "Registro de pruebas finales");
para("La Tabla {{Registro de pruebas finales}} recoge las pruebas de la etapa.");
table([["Fecha", 0.9], ["Prueba", 1.6], ["Participantes", 1.3], ["Resultado", 2.4]], [
  ["27/10/2026", "Comprobación automática de la escena", "Equipo de desarrollo", `Sin fallos; ${CONEXIONES} conexiones de eventos revisadas en las dos escenas; porcentaje de solución correcto en todos los casos de prueba`],
  ["27/10/2026", "Prueba automática en Play de las dos dificultades", "Equipo de desarrollo", "Sin fallos ni excepciones: configuración de cada sala, nivel nuevo de la sala 1, botón Probar, bloqueo de la mesa hasta que arranca el reloj, ejecución de la sala 3, tiempo agotado en las salas 1 y 3 y secuencia completa"],
  ["⟨fecha⟩", "Revisión de la versión 0.9.0 en el visor", "⟨participantes⟩", "Observaciones de la Tabla {{Observaciones recibidas y respuesta}}, atendidas en la versión de entrega"],
  ["⟨fecha⟩", "Prueba final en el visor, versión 1.0.0", "⟨participantes⟩", "⟨resultado de la lista de comprobación⟩"],
], { title: "Registro de pruebas finales" });

heading(2, "Errores corregidos");
para("La Tabla {{Errores corregidos en la etapa}} recoge los errores encontrados en esta etapa y su corrección, salvo el texto «Probar» repetido en la sala 2, que ya figura en la Tabla {{Observaciones recibidas y respuesta}}. Se suman a los 34 corregidos durante el desarrollo, documentados en el Producto 2.");
table([["Error", 1], ["Corrección", 1]], [
  ["La ficha del tutorial no volvía a su sitio al caerse: el mecanismo que devuelve las fichas solo reconocía las que colgaban directamente de él", "La ficha recuerda su posición de origen y vuelve sola"],
  ["El botón del tutorial casi no se hundía al pulsarlo (unos 0,5 mm)", "Mismo recorrido que el resto de los botones"],
  ["La frase 3 anunciaba cinco minutos por sistema mientras el reloj mostraba 15", "La frase dice quince minutos"],
  ["Los botones + y − de la sala 3 respondían con el brazo en marcha", "Se bloquean durante la ejecución"],
  ["⟨Errores encontrados en la prueba final en el visor⟩", "⟨Corrección⟩"],
], { title: "Errores corregidos en la etapa" });

heading(2, "Evidencias de funcionamiento");
para("Las figuras siguientes muestran el estado del módulo tras los ajustes de la etapa. Son capturas de la escena de juego en ejecución en el editor de Unity, desde el punto de vista del jugador.");
figure("tutorial_inicio.jpg", "Tutorial: botón Probar celeste", null);
figure("sala1_general.jpg", "Sala 1: el robot empieza mirando hacia arriba",
  "Roki empieza de espaldas al jugador, mirando hacia el fondo de la sala. Para llegar al botón verde debe recorrer la parte superior de la rejilla y bajar hasta la casilla situada frente a él.");
figure("sala1_mesa_basica.jpg", "Sala 1, dificultad básica: mesa con dos bloques «Girar izquierda»", null);
figure("sala2_modulo_sin_resolver.jpg", "Sala 2: problema con la pregunta «¿Qué hacemos?» y botón Probar centrado", null);
figure("sala3_basica.jpg", "Sala 3: botones + y − en gris y botón Probar celeste", null);

heading(2, "Verificación de cumplimiento de los requerimientos");
para("La Tabla {{Cumplimiento de los requerimientos del Producto 1}} contrasta el módulo entregado con los requerimientos técnicos y funcionales definidos en la especificación del Producto 1. Los cambios de diseño que dieron lugar a un cumplimiento con cambio se justificaron en el Producto 2.");
table([["Requisito", 1.3], ["Estado", 0.9], ["Cómo se cumple", 3.6]], [
  ["RI-01 Ingreso de usuario", "Cumple con cambio", "El supervisor introduce un PIN numérico en la pantalla Setup antes de entregar el visor, en lugar de un nombre de usuario"],
  ["RI-02 Estado diegético de cada sala", "Cumple con cambio", "El estado se muestra en el propio entorno: puerta cerrada o abierta (sala 1), luz e icono rojo o verde en cada módulo (sala 2), zona obstruida o despejada (sala 3), huecos resueltos iluminados (sala 4) y huecos de bloque en verde. No hay un indicador común de «en proceso»"],
  ["RI-03 Selección de dificultad por el supervisor", "Cumple", "Pantalla Setup, con las dos dificultades; no permite empezar sin elegir una"],
  ["RI-04 Mapeo de mandos", "Cumple con cambio", "Agarre con el botón de agarre (grip); las pulsaciones se hacen tocando el botón con el cuerpo del mando, sin gatillo"],
  ["RI-05 Persistencia y exportación de datos", "Cumple con cambio", "Un archivo JSON por participante en el visor, enviado al servidor; los pendientes se envían solos al abrir Setup"],
  ["RF-01 Validación del usuario", "Cumple con cambio", "PIN de cuatro cifras validado en Setup, con propuesta del siguiente correlativo"],
  ["RF-02 Secuenciación de escenarios", "Cumple", "El Director habilita cada sala al completar o agotar el tiempo de la anterior, con apertura de puertas y traslados"],
  ["RF-03 Métricas temporales", "Cumple", "Inicio, fin y duración de la sesión y de cada sala; duración de cada intento, selección y colocación"],
  ["RF-04 Interacción física y lógica", "Cumple", "Intentos con su secuencia y porcentaje de solución, bloques agarrados y conectados, y errores clasificados por tipo"],
  ["RF-05 Escritura de datos", "Cumple con cambio", "Archivo JSON jerárquico por sesión, sala e intento, en lugar de CSV"],
  ["RF-06 Temporizador", "Cumple con cambio", "15 minutos de sesión, visibles en cada sala, y un máximo de 5 por sala"],
  ["RF-07 Narración por voz", "Cumple", "Narrador con voz y texto en todas las salas; ⟨locuciones nuevas de la etapa grabadas e integradas⟩"],
  ["RF-08 Reinicio supervisado", "No implementado", "Al cerrarse la aplicación la sesión queda guardada y se envía en la siguiente apertura; un reinicio de la sala por el supervisor queda como mejora futura"],
  ["RNF-01 Al menos 60 FPS", "⟨Por medir⟩", "Optimizaciones aplicadas (sección {{§Rendimiento}}); ⟨resultado de la medición en el visor⟩"],
  ["RNF-02 Tiempo de carga y transición", "⟨Por medir⟩", "Una sola escena de juego, cargada al empezar, y traslados entre salas con fundido a negro. ⟨Tiempo de carga medido en el visor⟩"],
  ["RNF-03 Integridad de la telemetría", "Cumple", "Informe de integridad al cerrar cada sesión, herramienta de validación del formato y registro de los archivos ya enviados"],
  ["RNF-04 Funcionamiento sin conexión", "Cumple", "Toda la experiencia funciona sin red; el envío de datos es opcional y se reintenta"],
], { title: "Cumplimiento de los requerimientos del Producto 1", sz: 17 });

// --- 3. Entrega técnica del módulo inmersivo ---

heading(1, "Entrega técnica del módulo inmersivo", { newPage: true });

heading(2, "Contenido de la entrega");
para("La entrega se compone del proyecto de Unity completo, con su historial, y de los archivos asociados de la Tabla {{Contenido de la entrega}}. Todo se encuentra en el repositorio github.com/datascienceyt/codea2, rama dev.");
table([["Elemento", 1.4], ["Ubicación", 1.7], ["Contenido", 3]], [
  ["Proyecto de Unity", "Raíz del repositorio", "Escenas, scripts, modelos, materiales, sonidos y configuración, listo para abrir con Unity 6000.4.1f1"],
  ["Aplicación compilada", "Build/app.apk", "APK de la versión de entrega para instalar en Meta Quest ⟨versión 1.0.0 compilada el …⟩"],
  ["Documentación técnica", "Context.md", "Documento de referencia del sistema completo: arquitectura, sistemas, salas, narración, telemetría, decisiones de diseño, problemas conocidos y comprobaciones"],
  ["Presentación del proyecto", "README.md", "Descripción institucional del módulo y de sus salas"],
  ["Variables de telemetría", "Docs/VARIABLES_TELEMETRIA.md", "Qué mide cada dato del registro, en lenguaje llano, para el equipo evaluador; con un archivo de ejemplo"],
  ["Plan de prueba", "Docs/Pruebas/", "Prueba guionizada del registro de datos con el resultado esperado"],
  ["Documento de diseño", "Docs/Codea2_GDD.docx", "Diseño del juego y guion"],
  ["Informes", "Docs/Informe-Mes1 a Mes3", "Informes técnicos de los tres productos"],
  ["Herramientas de apoyo", "Docs/Herramientas/", "Scripts para resolver niveles, validar el registro de datos, compilar sin abrir Unity y generar los documentos"],
], { title: "Contenido de la entrega" });

heading(2, "Instalación en Meta Quest");
para("La aplicación se instala por carga local, sin pasar por la tienda de Meta. Los pasos son los siguientes:", { keepNext: true });
numbered([
  "**Modo de desarrollador.** En la aplicación Meta Horizon del teléfono vinculado al visor, activar el modo de desarrollador (requiere una cuenta de desarrollador de Meta, gratuita).",
  "**Conexión.** Conectar el visor al ordenador por USB y aceptar en el visor la solicitud de depuración USB.",
  "**Instalación.** Con Meta Quest Developer Hub, arrastrar el APK a la sección de aplicaciones del visor; o, desde una terminal con las herramientas de Android, ejecutar «adb install -r app.apk». La opción -r actualiza una versión anterior conservando los datos.",
  "**Primera ejecución.** En el visor, abrir la biblioteca de aplicaciones, filtrar por orígenes desconocidos y abrir Codea 2. La aplicación arranca en la pantalla del supervisor.",
]);
para("**Actualizaciones.** Una versión nueva solo se instala encima de la anterior si está firmada con la misma clave. La clave determina además el código del visor que se registra en cada sesión, por lo que todas las compilaciones deben firmarse con la misma (sección {{§Compilación desde el código fuente}}).");

heading(2, "Ejecución y configuración");
para("**Uso en una sesión.** El supervisor, con el visor puesto o mirando la vista compartida, introduce el PIN del participante (o pulsa la tecla de siguiente correlativo), elige la dificultad y pulsa EMPEZAR. Después entrega el visor al estudiante, que pulsa Probar en el cuarto del tutorial para comenzar. Al terminar o agotarse el tiempo, la aplicación vuelve sola a la pantalla del supervisor con el siguiente PIN propuesto.");
para("**Configuración.** La Tabla {{Parámetros de configuración}} recoge los parámetros que pueden necesitar ajuste y dónde se cambian. Todos se cambian en el editor de Unity y requieren compilar de nuevo.");
table([["Parámetro", 1.3], ["Dónde", 2], ["Valor actual", 2]], [
  ["Servidor de datos y clave", "Escena Juego, objeto con JSONUploader; la herramienta «Poner al día Setup y Juego» lo copia a Setup", "https://codea2.penginexr.com/upload"],
  ["Tiempo de sesión y por sala", "Escena Juego, Sesion → ScenarioTimeLimit", "900 s y 300 s"],
  ["Narración", "Assets/_Main/Narrativa.csv y audios en Sounds/VoiceLines/Camila", "22 frases y 2 variantes por dificultad"],
  ["Niveles de la sala 1", "Assets/_Main/Levels (editor de niveles) y Escenario (1) → Scenario1Difficulty", "reto-espejo (básica) y escenario1_intermedio"],
  ["Problemas de la sala 2", "Scripts/Scenario 2/Modulos, un archivo por módulo y dificultad", "Tres módulos por dificultad"],
  ["Recorrido de los botones", "Herramienta «Feedback 27-10», constante TravelFactor", "40 % del original"],
], { title: "Parámetros de configuración" });
para("**Datos de las sesiones.** Cada sesión queda en el visor en /sdcard/Android/data/com.yachaytech.Codea2/files/, con el nombre {PIN}_{sesión}_{visor}.json. Con conexión Wi-Fi se envían solos al terminar; los que quedan pendientes se envían al abrir la pantalla del supervisor o con su botón Subir datos. Sin red pueden copiarse por cable con Meta Quest Developer Hub o con «adb pull».");

heading(2, "Compilación desde el código fuente");
numbered([
  "Instalar Unity 6000.4.1f1 con el módulo Android Build Support (incluye OpenJDK, Android SDK y NDK) desde Unity Hub, y abrir la carpeta del repositorio como proyecto.",
  "En File → Build Profiles, seleccionar la plataforma Android y comprobar que las escenas están en este orden: Setup primero y Juego después, las dos marcadas.",
  "Antes de compilar, pasar Tools → Codea → Comprobar escena de juego y Tools → Codea → Probar las dos dificultades en Play; las dos deben terminar sin fallos.",
  "Comprobar en Player Settings la versión (1.0.0) y la clave de firma, y compilar el APK.",
]);
para("⟨Clave de firma de la entrega: ubicación y custodia del keystore del proyecto.⟩ Hasta la versión 0.9.0 las compilaciones usaron la clave de depuración del equipo de desarrollo; se recomienda que el equipo del proyecto conserve una clave propia y la use en todas las compilaciones posteriores.");

heading(2, "Estructura del proyecto");
para("El contenido propio del proyecto está en Assets/_Main; el resto de carpetas de Assets son paquetes de terceros (Meta XR, paquete de recursos de ciencia ficción, TextMeshPro) y configuración de Unity. La Tabla {{Estructura de Assets/_Main}} describe cada carpeta.");
table([["Carpeta", 1.2], ["Contenido", 4]], [
  ["Scripts", "Código del módulo, por subsistema: Block Programming System, Grid Level (sala 1), Scenario 2, Scenario 3, Scenario 4, Story (Director, narración, tutorial y utilidades), Session, Difficulty, Telemetry y Map"],
  ["Editor", "Herramientas del menú Tools → Codea, editor de niveles y comprobaciones automáticas"],
  ["Prefabs", "Piezas reutilizables: bloques, fichas, huecos, botones, módulos de la sala 2, tutorial y jugador"],
  ["Levels", "Niveles de la sala 1 en JSON"],
  ["3D Models, CustomMaterials, 2D", "Modelos, materiales, sombreadores, imágenes de la interfaz y pliegos de figuras"],
  ["Sounds", "Efectos y locuciones de la narración (VoiceLines/Camila)"],
  ["Narrativa.csv", "Guion: identificador y texto de cada frase"],
], { title: "Estructura de Assets/_Main" });
para("Las escenas están en Assets/Scenes. Setup contiene la pantalla del supervisor y el envío de datos pendientes. Juego contiene el jugador, el cuarto del tutorial, las cuatro salas (objetos Escenario (1) a Escenario (4)), la sesión con sus tiempos, el narrador y el Director, que orquesta la experiencia en cinco tramos.");

heading(2, "Documentación del código");
para("El código sigue una organización por subsistemas que se comunican por eventos, descrita en el Producto 2 y en la sección 3 de Context.md. Cada clase y cada decisión no evidente está comentada en el propio código, con el motivo del diseño. La Tabla {{Subsistemas y componentes principales}} resume los subsistemas.");
table([["Subsistema", 1.1], ["Responsabilidad", 2.6], ["Componentes principales", 1.8]], [
  ["Sesión", "PIN y dificultad, carga de la escena, tiempos de sesión y de sala, cierre y vuelta a la pantalla inicial", "SessionSetup, PinEntry, SceneLoader, ScenarioTimeLimit, TimeUpSequence"],
  ["Director", "Orquesta la experiencia en tramos y pasos", "Director, IStepAction"],
  ["Narración", "Voz y texto emparejados por identificador con el guion", "Narrator"],
  ["Programación por bloques", "Acople de bloques en huecos, filas, ejecución y repeticiones", "BlockNode, Socket, SocketRow, ProgramRunner, ProgramTrigger"],
  ["Salas", "Lógica de cada reto y su conexión con el registro", "Scenario1Controller a Scenario4Controller, LevelLoader, Bot, SystemModule, RoboticArm, ArmTypeSocket, ShapeSocket"],
  ["Dificultad", "Configura cada sala según la dificultad, en una sola escena", "DifficultyApplier, DifficultyVariant, DifficultyOnly"],
  ["Interacción", "Agarre y suelta como eventos, teletransporte e interacción con el cuerpo del mando", "VRGrabEvents, Teleporter, ControllerReach"],
  ["Telemetría", "Registro en JSON, envío y sesiones pendientes", "TelemetryManager, TelemetryData, JSONUploader, TelemetryOutbox"],
], { title: "Subsistemas y componentes principales" });

heading(2, "Interacciones implementadas");
para("La Tabla {{Interacciones implementadas en la versión de entrega}} resume las interacciones del módulo; los ajustes de esta etapa (área del mando, recorrido de los botones y huecos en verde) se describen en la sección {{§Interacciones}}.");
table([["Interacción", 1.2], ["Implementación", 4]], [
  ["Pulsar botones", "Botones de pulsación del SDK de Meta con sonido propio; se pulsan con el cuerpo del mando o con el dedo"],
  ["Agarrar y soltar", "Componentes de agarre del SDK para mando y mano"],
  ["Encajar piezas", "Al soltar, la pieza se acopla al hueco libre más cercano; las piezas fijas o acertadas quedan bloqueadas"],
  ["Desplazarse", "Teletransporte a puntos fijos y traslados guiados entre salas con fundido a negro"],
  ["Física", "Las fichas de la sala 4 y del tutorial tienen gravedad y vuelven solas a su sitio si caen"],
  ["Entorno reactivo", "Puertas, robot, brazo robótico, luces e iconos de estado, pantalla de narración que acompaña al jugador y Tierra que se acerca al final"],
], { title: "Interacciones implementadas en la versión de entrega" });

heading(2, "Herramientas de mantenimiento");
para("El proyecto incluye herramientas en el menú Tools → Codea del editor que automatizan el montaje y la verificación. Las de la Tabla {{Herramientas para el mantenimiento}} son las que se usan al hacer ajustes.");
table([["Herramienta", 1.5], ["Cuándo usarla", 4]], [
  ["Comprobar escena de juego", "Tras cualquier cambio en la escena o al renombrar un método. Revisa conexiones de eventos, la secuencia del Director, la narración frente al guion y cada sala en cada dificultad"],
  ["Probar las dos dificultades en Play", "Antes de compilar una versión. Recorre la experiencia sin visor en las dos dificultades"],
  ["Poner al día Setup y Juego", "Tras actualizar el código o cambiar un nivel: aplica las soluciones para el porcentaje de solución y copia la configuración del servidor a Setup"],
  ["Asignar la voz del narrador", "Tras grabar o sustituir un audio: lo coloca en su frase por el nombre del archivo"],
  ["Feedback 27-10", "Rehace los cambios de escena de esta etapa sobre una escena restaurada"],
  ["Ver como básica o intermedia", "Para mover piezas de una sola dificultad sin que se solapen"],
  ["Editor de niveles y resolver_nivel.js", "Para crear un nivel de la sala 1 y comprobar sus soluciones y el número de bloques"],
  ["Capturas para informes", "Para documentar el estado de cada sala"],
], { title: "Herramientas para el mantenimiento" });

heading(2, "Recomendaciones de mantenimiento");
bullet("**No editar escenas ni prefabs fuera de Unity con el editor abierto:** Unity sobrescribe los cambios. Los cambios de escena reproducibles se hacen con herramientas del menú Tools → Codea.");
bullet("**Narración:** las dos listas del narrador deben tener las mismas 20 entradas en el mismo orden, porque el Director reproduce una por paso. Para retirar una frase, se deja vacía en el guion y sin audio; para cambiarla, se edita el guion y se graba el audio con el identificador de la frase como nombre.");
bullet("**Niveles de la sala 1:** un nivel nuevo debe mantener la salida en la casilla (0,1), frente al botón físico. Tras cambiarlo, comprobar sus soluciones con resolver_nivel.js, ajustar los bloques de la mesa si hace falta y actualizar las soluciones en CodeaSolutions.");
bullet("**Diferencias por dificultad:** una pieza que solo existe en una dificultad lleva el componente DifficultyOnly; nunca se activa desde el Director, sino su contenedor.");
bullet("**Enumeraciones:** los valores nuevos de una enumeración se añaden al final, porque Unity guarda el número y no el nombre.");
bullet("**Firma y servidor:** firmar siempre con la misma clave y, si cambia el servidor, actualizarlo en la escena Juego y pasar «Poner al día Setup y Juego».");

heading(2, "Mejoras futuras");
para("La Tabla {{Mejoras futuras propuestas}} recoge las mejoras identificadas que quedan fuera del alcance de esta entrega.");
table([["Mejora", 3.2], ["Motivo", 2.6]], [
  ["Reinicio supervisado de una sala (RF-08)", "Permitir al supervisor reiniciar una sala sin cerrar la sesión"],
  ["Perfil de render específico para el visor", "El perfil de calidad de Android hereda la configuración de escritorio, más exigente"],
  ["Validación completa con seguimiento de manos", "Las manos están habilitadas pero no se validaron con estudiantes"],
  ["Indicador común de progreso de la misión", "Completar RI-02 con un estado «en proceso» visible en todas las salas"],
  ["Más niveles por sala y selección aleatoria", "Evitar que estudiantes del mismo grupo se transmitan la solución"],
  ["Panel de resultados para el docente", "Aprovechar el registro de datos durante la sesión, no solo después"],
], { title: "Mejoras futuras propuestas" });

heading(2, "Sesión de transferencia técnica");
para("La transferencia técnica al equipo del proyecto se realizó ⟨el … de … de 2026, con …⟩. La sesión siguió el programa de la Tabla {{Programa de la sesión de transferencia técnica}}.");
table([["Bloque", 1.4], ["Contenido", 4]], [
  ["Funcionamiento del módulo", "Recorrido completo en el visor en las dos dificultades: supervisor, tutorial, cuatro salas, cierre y envío de datos"],
  ["Instalación y configuración", "Modo de desarrollador, instalación del APK, actualización, extracción de datos y parámetros de la Tabla {{Parámetros de configuración}}"],
  ["Estructura y código", "Escenas, carpetas, subsistemas, Director y narración; dónde está cada parte"],
  ["Ajustes habituales", "Cambiar una frase y su audio, un problema de la sala 2, un nivel de la sala 1 y los tiempos; uso de las herramientas de verificación"],
  ["Registro de datos", "Formato del JSON, variables y su interpretación, servidor y sesiones pendientes"],
  ["Preguntas y pendientes", "Recomendaciones de mantenimiento y mejoras futuras"],
], { title: "Programa de la sesión de transferencia técnica" });
para("⟨Acuerdos y observaciones de la sesión.⟩");


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
  if (n === 3) x = setText(x, "Producto 3: ");
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
// página salen primero de una estimación y, si está OnlyOffice, de maquetar el documento con él
// (al final del script). Word además los recalcula al abrir (updateFields en settings.xml).
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

const buildToc = pages => headings.map((h, i) => {
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
    `<w:r>${sz}<w:fldChar w:fldCharType="separate"/></w:r><w:r>${sz}<w:t>${pages[i]}</w:t></w:r><w:r>${sz}<w:fldChar w:fldCharType="end"/></w:r>` +
    `</w:hyperlink></w:p>`;
});
const tocXml = pages => `${sdtPr}<w:sdtContent>${buildToc(pages).join("")}${tocClose}</w:sdtContent></w:sdt>`;

// Salto de sección tras el índice y, al final, la sección del cuerpo del mes 1 (cabecera con
// el título del informe y número de página).
const tocSection = items[26].xml;
const bodySectPr = items[170].xml.match(/<w:sectPr[\s\S]*?<\/w:sectPr>/)[0];

const bodyXml = blocks.map(b => resolveReferences(b.xml)).join("");
const documentXml = pages => docXml.slice(0, bodyStart) + [...cover, tocXml(pages), tocSection, bodyXml, bodySectPr].join("") + docXml.slice(bodyEnd);
files.set("word/document.xml", documentXml(headings.map(h => pageOf[h.block])));

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

// Páginas reales del índice: OnlyOffice maqueta el documento y se vuelve a escribir con ellas.
// Cambiar los números no mueve nada: el índice cabe en su página.
let paginas = "estimadas (sin OnlyOffice)";
const reales = paginasDelIndice(OUTPUT);
if (reales && reales.length === headings.length) {
  ordered.set("word/document.xml", documentXml(reales));
  writeZip(OUTPUT, ordered);
  paginas = `maquetadas con OnlyOffice (último título en la pág. ${reales[reales.length - 1]})`;
} else if (reales) {
  paginas = `estimadas: OnlyOffice dio ${reales.length} enlaces para ${headings.length} títulos`;
}

console.log(`${path.relative(ROOT, OUTPUT)}: ${headings.filter(h => h.level === 1).length} capítulos, ${headings.length} títulos, ` +
            `${tableNo} tablas, ${figureNo} figuras, ${blocks.length} bloques; índice con páginas ${paginas}`);
