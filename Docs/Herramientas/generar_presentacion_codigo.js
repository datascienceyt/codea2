// Presentación de la capacitación técnica: recorrido general por el código de CODEA 2.
// Para profesores de Computación y estudiantes de TIC (30-45 minutos).
//
//   node Docs/Herramientas/generar_presentacion_codigo.js
//
// Necesita pptxgenjs (npm i -g pptxgenjs). Si está instalado de forma global, el script lo
// encuentra solo. Sale en Docs/Capacitacion-Tecnica-Codigo.pptx, con notas del orador.
const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

// jszip viene con pptxgenjs: se busca junto a él.
function rutaPptxgen() {
  try { return require.resolve("pptxgenjs"); } catch { /* sigue con la instalación global */ }
  return require.resolve(path.join(execSync("npm root -g").toString().trim(), "pptxgenjs"));
}
const RUTA_PPTXGEN = rutaPptxgen();
const pptxgen = require(RUTA_PPTXGEN);

const RAIZ = path.resolve(__dirname, "..", "..");
// Capturas sacadas para esta presentación (Tools → Codea → Capturas para informes con
// CODEA_CAPTURAS=Docs/Capacitacion/capturas y CODEA_CAPACITACION=1), con los listados de la escena.
const CAPT = path.join(RAIZ, "Docs", "Capacitacion", "capturas");
const DIAG = path.join(RAIZ, "Docs", "Diagramas");
const SALIDA = path.join(RAIZ, "Docs", "Capacitacion-Tecnica-Codigo.pptx");

// Colores de los diagramas del proyecto: azul de la nave, naranja de los datos, verde azulado.
const K = {
  noche: "12233A", azul: "1F4466", azulMedio: "35628C", hielo: "E7EEF6", claro: "F3F6FA",
  naranja: "E8862A", naranjaClaro: "FCEBD8", turquesa: "1F8A80", turquesaClaro: "DDF1EE",
  texto: "1E2B38", gris: "5B6B7B", grisClaro: "C9D3DE", blanco: "FFFFFF",
};
const F = { titulo: "Arial", cuerpo: "Calibri", codigo: "Courier New" };
const W = 13.333, H = 7.5, M = 0.6;

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE";
pres.title = "CODEA 2 · Recorrido por el código";
pres.subject = "Capacitación técnica";
pres.author = "Víctor Echeverría";
pres.company = "Yachay Tech";
pres.theme = { headFontFace: F.titulo, bodyFontFace: F.cuerpo };

// ---------- Diseños ----------
const pie = [
  { text: { text: "CODEA 2 · Capacitación técnica", options: { x: M, y: 7.02, w: 6, h: 0.3, fontFace: F.cuerpo, fontSize: 10, color: K.gris, margin: 0 } } },
];
pres.defineSlideMaster({
  title: "CONTENIDO",
  background: { color: K.blanco },
  objects: [
    ...pie,
    { placeholder: { options: { name: "title", type: "title", x: M, y: 0.4, w: W - 2 * M, h: 0.8, fontFace: F.titulo, fontSize: 30, bold: true, color: K.azul, align: "left", valign: "middle", margin: 0 }, text: "" } },
  ],
  slideNumber: { x: W - M - 0.6, y: 7.02, w: 0.6, h: 0.3, fontFace: F.cuerpo, fontSize: 10, color: K.gris, align: "right" },
});
pres.defineSlideMaster({
  title: "SECCION",
  background: { color: K.azul },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: M + 1.9, y: 2.75, w: 9.5, h: 1.1, fontFace: F.titulo, fontSize: 40, bold: true, color: K.blanco, align: "left", valign: "middle", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: M + 1.9, y: 3.9, w: 9.5, h: 0.9, fontFace: F.cuerpo, fontSize: 18, color: "C8D8EA", align: "left", valign: "top", margin: 0 }, text: "" } },
  ],
});
pres.defineSlideMaster({ title: "OSCURA", background: { color: K.noche }, objects: [] });

// ---------- Piezas ----------
const tb = (s, text, o) => s.addText(text, { isTextBox: true, fontFace: F.cuerpo, color: K.texto, margin: 0, valign: "top", ...o });

/** Nombre de clase o archivo, en caja de código. Devuelve el ancho usado. */
function pastilla(s, text, x, y, o = {}) {
  const tam = o.fontSize || 12;
  const w = o.w || Math.max(0.6, text.length * tam * 0.0085 + 0.3);
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h: o.h || 0.34, rectRadius: 0.06, fill: { color: o.fill || K.hielo }, line: { color: o.fill || K.hielo } });
  tb(s, text, { x, y, w, h: o.h || 0.34, fontFace: F.codigo, fontSize: tam, color: o.color || K.azul, align: "center", valign: "middle", bold: !!o.bold });
  return w;
}
function pastillas(s, lista, x, y, o = {}) {
  let cx = x;
  for (const t of lista) cx += pastilla(s, t, cx, y, o) + 0.12;
}
const tarjeta = (s, x, y, w, h, fill = K.claro) =>
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.1, fill: { color: fill }, line: { color: fill } });
function circulo(s, txt, x, y, d = 0.55, fill = K.naranja, color = K.blanco, tam = 16) {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill }, line: { color: fill } });
  tb(s, txt, { x, y, w: d, h: d, fontFace: F.titulo, fontSize: tam, bold: true, color, align: "center", valign: "middle" });
}
function flecha(s, x1, y1, x2, y2, color = K.grisClaro) {
  s.addShape(pres.shapes.LINE, {
    x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1), h: Math.abs(y2 - y1),
    flipH: x2 < x1, flipV: y2 < y1, line: { color, width: 2, endArrowType: "triangle" },
  });
}
/** Imagen encajada en un marco, sin deformar. */
function imagen(s, archivo, x, y, w, h, ancho, alto, alt) {
  const r = Math.min(w / ancho, h / alto);
  const iw = ancho * r, ih = alto * r;
  s.addImage({ path: archivo, x: x + (w - iw) / 2, y: y + (h - ih) / 2, w: iw, h: ih, altText: alt });
  return { x: x + (w - iw) / 2, y: y + (h - ih) / 2, w: iw, h: ih };
}
function captura(s, nombre, x, y, w, alt) {
  const h = w * 9 / 16;
  s.addImage({ path: path.join(CAPT, nombre), x, y, w, h, altText: alt, rounding: false });
  return h;
}
/** Lista de puntos con título en negrita. */
function puntos(s, items, x, y, w, h, tam = 15) {
  const runs = [];
  items.forEach((it, i) => {
    const ultimo = i === items.length - 1;
    if (Array.isArray(it)) {
      runs.push({ text: it[0] + " ", options: { bold: true, color: K.azul, bullet: { indent: 16 }, paraSpaceAfter: 9 } });
      runs.push({ text: it[1], options: { breakLine: !ultimo } });
    } else runs.push({ text: it, options: { bullet: { indent: 16 }, paraSpaceAfter: 9, breakLine: !ultimo } });
  });
  tb(s, runs, { x, y, w, h, fontSize: tam });
}

let seccion = "";
const nueva = (master) => pres.addSlide({ masterName: master, sectionTitle: seccion });
function divisoria(num, titulo, sub, notas) {
  seccion = titulo;
  pres.addSection({ title: titulo });
  const s = nueva("SECCION");
  circulo(s, String(num), M + 0.3, 2.8, 1.0, K.naranja, K.blanco, 32);
  s.addText(titulo, { placeholder: "title" });
  s.addText(sub, { placeholder: "body" });
  s.addNotes(notas);
  return s;
}
const contenido = (titulo) => { const s = nueva("CONTENIDO"); s.addText(titulo, { placeholder: "title" }); return s; };

// =====================================================================
// 1. Portada
// =====================================================================
seccion = "Inicio";
pres.addSection({ title: "Inicio" });
{
  const s = nueva("OSCURA");
  s.addImage({ path: path.join(CAPT, "sala1_general.jpg"), x: 0, y: 0, w: W, h: W * 9 / 16, altText: "Sala 1 de CODEA 2: Roki sobre la cuadrícula" });
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: H, fill: { color: K.noche, transparency: 18 }, line: { color: K.noche, transparency: 18 } });
  tb(s, "CAPACITACIÓN TÉCNICA", { x: M + 0.2, y: 1.9, w: 8, h: 0.4, fontFace: F.titulo, fontSize: 16, bold: true, color: K.naranja, charSpacing: 3 });
  tb(s, "CODEA 2", { x: M + 0.2, y: 2.35, w: 10, h: 1.3, fontFace: F.titulo, fontSize: 66, bold: true, color: K.blanco });
  tb(s, "Recorrido general por el código", { x: M + 0.2, y: 3.65, w: 10, h: 0.7, fontFace: F.titulo, fontSize: 30, color: "D5E2F0" });
  tb(s, "Escape room educativo en realidad virtual · Unity 6 · Meta Quest 3S", { x: M + 0.2, y: 4.45, w: 10, h: 0.45, fontSize: 18, color: "B9CBDD" });
  tb(s, "Ing. Víctor Echeverría · Yachay Tech · Noviembre de 2026", { x: M + 0.2, y: 6.45, w: 10, h: 0.4, fontSize: 14, color: "B9CBDD" });
  s.addNotes("Bienvenida. Esta sesión es para entender cómo está construido el código de CODEA 2, no para aprender a jugarlo: eso viene en la siguiente capacitación, con el resto de estudiantes. La idea es que al salir cada uno sepa dónde está cada parte y qué documento abrir para profundizar.");
}

// 2. Agenda
{
  const s = contenido("Qué vamos a ver");
  const items = [
    ["El proyecto en cinco minutos", "Qué es, para quién y con qué está hecho"],
    ["Arquitectura general", "Escenas, subsistemas y carpetas"],
    ["El sistema de bloques", "El núcleo que comparten las salas 1, 3 y 4"],
    ["Las cuatro salas", "Una mecánica y unas clases por sala"],
    ["Orquestación", "Director, narración, dificultad y relojes"],
    ["Los datos", "Telemetría, archivo JSON y servidor"],
    ["Trabajar con el proyecto", "Herramientas, cambios frecuentes y documentación"],
  ];
  const colW = 5.8, filaH = 1.12;
  items.forEach(([t, d], i) => {
    const col = i < 4 ? 0 : 1, fila = i < 4 ? i : i - 4;
    const x = M + col * (colW + 0.5), y = 1.55 + fila * filaH;
    circulo(s, String(i + 1), x, y + 0.08, 0.6, col ? K.turquesa : K.azul);
    tb(s, t, { x: x + 0.8, y: y, w: colW - 0.8, h: 0.4, fontFace: F.titulo, fontSize: 18, bold: true, color: K.texto });
    tb(s, d, { x: x + 0.8, y: y + 0.42, w: colW - 0.8, h: 0.35, fontSize: 14, color: K.gris });
  });
  tarjeta(s, M + colW + 0.5, 1.55 + 3 * filaH, colW, 0.85, K.naranjaClaro);
  tb(s, "Duración: unos 45 minutos, con preguntas al final", { x: M + colW + 0.75, y: 1.55 + 3 * filaH, w: colW - 0.4, h: 0.85, fontSize: 15, color: K.texto, valign: "middle" });
  s.addNotes("Siete bloques. Los dos primeros dan el mapa; el sistema de bloques es la pieza central y por eso va antes de las salas; luego lo que coordina todo, los datos y, al final, lo práctico: cómo se modifica y dónde está la documentación.");
}

// =====================================================================
// Sección 1
// =====================================================================
divisoria(1, "El proyecto en cinco minutos", "Qué es CODEA 2 y con qué está construido",
  "Primero el contexto, para que el código tenga sentido.");

// 3. Qué es
{
  const s = contenido("Un escape room en VR para aprender a programar");
  puntos(s, [
    ["Historia:", "un astronauta repara su nave tras una tormenta espacial, con ayuda de Roki, el robot de mantenimiento."],
    ["Público:", "niños y adolescentes de 8 a 17 años de unidades educativas rurales del Ecuador."],
    ["Pedagogía:", "cada sala trabaja un pilar del pensamiento computacional con programación por bloques."],
    ["Uso:", "un supervisor teclea el PIN y la dificultad, y entrega el visor al estudiante."],
    ["Datos:", "cada sesión genera un archivo con intentos, tiempos y errores para el equipo evaluador."],
  ], M, 1.6, 6.6, 5.1, 18);
  const stats = [["4", "salas"], ["2", "dificultades"], ["15", "minutos por sesión"], ["85", "scripts C# propios"]];
  stats.forEach(([n, l], i) => {
    const x = 7.75 + (i % 2) * 2.55, y = 1.6 + Math.floor(i / 2) * 2.45;
    tarjeta(s, x, y, 2.35, 2.2, i === 3 ? K.naranjaClaro : K.hielo);
    tb(s, n, { x, y: y + 0.25, w: 2.35, h: 1.1, fontFace: F.titulo, fontSize: 58, bold: true, color: i === 3 ? K.naranja : K.azul, align: "center", valign: "middle" });
    tb(s, l, { x: x + 0.15, y: y + 1.4, w: 2.05, h: 0.6, fontSize: 15, color: K.texto, align: "center" });
  });
  tb(s, "≈ 17 500 líneas en Assets/_Main (juego + editor)", { x: 7.75, y: 6.5, w: 4.95, h: 0.35, fontSize: 12, color: K.gris, align: "center" });
  s.addNotes("CODEA 2 es un proyecto de Yachay Tech. El código propio está todo en Assets/_Main: 85 scripts, unas 17 500 líneas contando las herramientas del editor. Lo demás de Assets es del SDK de Meta y paquetes de terceros: no hace falta tocarlo.");
}

// 4. Cuatro salas
{
  const s = contenido("Cuatro salas, cuatro pilares");
  const salas = [
    ["sala1_general.jpg", "1 · Secuencialidad", "Ordenar bloques para llevar a Roki hasta el botón que abre la puerta", "Scenario 1/"],
    ["sala2_general.jpg", "2 · Condicionales", "Leer los síntomas de cada módulo y marcar las acciones que lo reparan", "Scenario 2/"],
    ["sala3_basica.jpg", "3 · Bucles", "Programar un brazo que clasifica barriles y cajas, con repeticiones", "Scenario 3/"],
    ["sala4_panel.jpg", "4 · Patrones", "Encontrar la ficha idéntica a la figura de cada hueco del panel", "Scenario 4/"],
  ];
  const w = (W - 2 * M - 3 * 0.3) / 4;
  salas.forEach(([img, t, d, carpeta], i) => {
    const x = M + i * (w + 0.3), y = 1.55;
    tarjeta(s, x, y, w, 5.15, K.claro);
    tb(s, "Carpeta", { x: x + 0.2, y: y + 4.15, w: w - 0.4, h: 0.3, fontSize: 12, color: K.gris });
    pastilla(s, carpeta, x + 0.2, y + 4.48, { w: w - 0.4, fill: K.blanco });
    captura(s, img, x, y, w, "Captura de la sala " + (i + 1));
    tb(s, t, { x: x + 0.2, y: y + w * 9 / 16 + 0.25, w: w - 0.4, h: 0.45, fontFace: F.titulo, fontSize: 17, bold: true, color: K.azul });
    tb(s, d, { x: x + 0.2, y: y + w * 9 / 16 + 0.8, w: w - 0.4, h: 1.2, fontSize: 15, color: K.texto });
  });
  s.addNotes("Cada sala es un reto independiente con su propio controlador. Las salas 1, 3 y 4 usan el mismo sistema de bloques (agarrar y encajar); la 2 es distinta: botones de selección. Antes de la sala 1 hay un tutorial corto, y al final la nave vuelve a la Tierra.");
}

// 5. Stack
{
  const s = contenido("Con qué está hecho");
  const cajas = [
    ["Unity 6", "Versión 6000.4.1f1. Todo el código es C#."],
    ["Meta XR All-in-One SDK", "Agarrar, pulsar y teletransportarse. Versión 203."],
    ["URP + Shader Graph", "Render ligero para el visor; objetivo de 60 FPS."],
    ["Meta Quest 3S", "Única plataforma. Se instala como APK de Android."],
    ["JsonUtility", "Serialización de Unity, sin librerías externas."],
    ["Primero sin conexión", "Funciona sin red; los datos se suben cuando la hay."],
  ];
  const w = (W - 2 * M - 2 * 0.35) / 3, h = 2.3;
  cajas.forEach(([t, d], i) => {
    const x = M + (i % 3) * (w + 0.35), y = 1.6 + Math.floor(i / 3) * (h + 0.35);
    tarjeta(s, x, y, w, h, i < 3 ? K.hielo : K.turquesaClaro);
    circulo(s, String(i + 1), x + 0.3, y + 0.3, 0.5, i < 3 ? K.azul : K.turquesa, K.blanco, 14);
    tb(s, t, { x: x + 0.3, y: y + 0.95, w: w - 0.6, h: 0.45, fontFace: F.titulo, fontSize: 19, bold: true, color: K.texto });
    tb(s, d, { x: x + 0.3, y: y + 1.45, w: w - 0.6, h: 0.75, fontSize: 15, color: K.gris });
  });
  s.addNotes("Para abrir el proyecto hace falta exactamente esa versión de Unity con el módulo de Android. Lo de JsonUtility importa: explica la forma del archivo de datos (no admite diccionarios ni campos opcionales, así que todos los campos salen siempre). Y 'primero sin conexión' es un requisito: en las escuelas rurales puede no haber red.");
}

// 6. Recorrido de una sesión
{
  const s = contenido("Recorrido de una sesión");
  const pasos = [
    ["Setup", "PIN y dificultad"], ["Tutorial", "4 gestos"], ["Sala 1", "Secuencias"], ["Sala 2", "Condicionales"],
    ["Sala 3", "Bucles"], ["Sala 4", "Patrones"], ["Final", "Regreso y subida"],
  ];
  const n = pasos.length, gap = 0.32, w = (W - 2 * M - (n - 1) * gap) / n, y = 1.75;
  pasos.forEach(([t, d], i) => {
    const x = M + i * (w + gap);
    const extremo = i === 0 || i === n - 1;
    tarjeta(s, x, y, w, 1.45, extremo ? K.naranjaClaro : K.hielo);
    tb(s, t, { x, y: y + 0.22, w, h: 0.45, fontFace: F.titulo, fontSize: 18, bold: true, color: extremo ? K.naranja : K.azul, align: "center" });
    tb(s, d, { x: x + 0.05, y: y + 0.75, w: w - 0.1, h: 0.5, fontSize: 13, color: K.texto, align: "center" });
    if (i < n - 1) flecha(s, x + w + 0.03, y + 0.72, x + w + gap - 0.03, y + 0.72, K.azulMedio);
  });
  // Las dos escenas
  const y2 = 3.75, sw = 3.4, jx = M + sw + 0.35, jw = W - M - jx;
  tarjeta(s, M, y2, sw, 2.95, K.naranjaClaro);
  tb(s, "Setup.unity", { x: M + 0.3, y: y2 + 0.25, w: sw - 0.6, h: 0.4, fontFace: F.codigo, fontSize: 15, bold: true, color: K.naranja });
  puntos(s, ["Pantalla del supervisor: PIN, dificultad y botón Subir datos.", "Al abrirse sube solo lo que quedó pendiente."], M + 0.3, y2 + 0.85, sw - 0.6, 2.0, 14);
  tarjeta(s, jx, y2, jw, 2.95, K.claro);
  tb(s, "Juego.unity", { x: jx + 0.3, y: y2 + 0.25, w: 4, h: 0.4, fontFace: F.codigo, fontSize: 15, bold: true, color: K.azul });
  puntos(s, [
    "Una sola escena para todo el recorrido y para las dos dificultades.",
    "Las salas avanzan en orden: terminar una es lo único que abre la siguiente.",
    "Al acabar (o al agotarse el tiempo) se cierra el archivo, se sube y se vuelve a Setup para el siguiente niño.",
  ], jx + 0.3, y2 + 0.85, jw - 0.6, 2.0, 14);
  s.addNotes("Solo hay dos escenas. Setup es la que maneja el supervisor: teclado de PIN, dificultad y botón de subir datos. Juego contiene el tutorial, las cuatro salas y el final. El paso de una sala a otra no carga escenas: el jugador se teletransporta dentro de la misma.");
}

// Setup y tutorial
{
  const s = contenido("Antes de la misión: Setup y tutorial");
  const iw = (W - 2 * M - 0.4) / 2;
  [
    ["setup_supervisor.jpg", "Setup.unity · pantalla del supervisor", ["SessionSetup", "PinEntry", "JSONUploader"],
      "El supervisor teclea el PIN, elige la dificultad y pulsa Empezar. «Subir datos» envía las sesiones pendientes."],
    ["tutorial_inicio.jpg", "Tutorial · primer cuarto de Juego.unity", ["TutorialController", "ModuleOptionButton"],
      "Se practican los gestos: dos botones, un bloque y una ficha. Termina con los cuatro hechos y no cuenta en la telemetría."],
  ].forEach(([img, t, cl, d], i) => {
    const x = M + i * (iw + 0.4);
    const h = captura(s, img, x, 1.45, iw, t);
    tb(s, t, { x, y: 1.45 + h + 0.15, w: iw, h: 0.4, fontFace: F.titulo, fontSize: 16, bold: true, color: K.azul });
    pastillas(s, cl, x, 1.45 + h + 0.6);
    tb(s, d, { x, y: 1.45 + h + 1.05, w: iw, h: 0.9, fontSize: 14 });
  });
  s.addNotes("Las dos primeras pantallas que ve cada persona. Setup la maneja el supervisor, con el visor puesto o mirando la transmisión: el PIN identifica al estudiante sin datos personales. El tutorial ya está dentro de Juego.unity; es un cuarto aparte donde el niño aprende a pulsar, agarrar y encajar antes de que corra el reloj.");
}

// =====================================================================
// Sección 2
// =====================================================================
divisoria(2, "Arquitectura general", "Subsistemas, cómo se comunican y dónde vive cada uno",
  "Ahora el mapa del código.");

// 7. Arquitectura
{
  const s = contenido("El mapa del sistema");
  imagen(s, path.join(DIAG, "arquitectura.jpg"), M, 1.4, 8.7, 5.45, 2400, 1520, "Diagrama de subsistemas: Sesión, Director, Narración, Tiempo, Salas, Bloques, Dificultad, Interacción, Telemetría y Servidor");
  const ideas = [
    ["El Director manda", "Activa cada sala y espera a que termine. La historia es una lista de pasos."],
    ["Las salas no se conocen", "Cada una tiene su controlador; ninguna llama a otra."],
    ["Todo acaba en la telemetría", "Las salas avisan de cada intento, error y acierto."],
  ];
  ideas.forEach(([t, d], i) => {
    const y = 1.5 + i * 1.8;
    tarjeta(s, 9.6, y, W - M - 9.6, 1.6, i === 2 ? K.naranjaClaro : K.hielo);
    tb(s, t, { x: 9.8, y: y + 0.18, w: W - M - 10, h: 0.4, fontFace: F.titulo, fontSize: 15, bold: true, color: i === 2 ? K.naranja : K.azul });
    tb(s, d, { x: 9.8, y: y + 0.62, w: W - M - 10, h: 0.9, fontSize: 13, color: K.texto });
  });
  s.addNotes("Este es el diagrama más importante. Arriba, el Director recorre la historia: le pide al Narrador que hable y escucha al Tiempo. En el centro, las salas. Abajo, tres servicios que usan las salas: el sistema de bloques, la dificultad y la interacción con Meta. Todo lo que pasa termina en la telemetría, que escribe el JSON y lo manda al servidor. La conexión entre piezas casi siempre es por UnityEvents cableados en el inspector, no por llamadas directas.");
}

// 8. Carpetas
{
  const s = contenido("Dónde está cada cosa: Assets/_Main/Scripts");
  const filas = [
    ["Block Programming System", "7", "Bloques, huecos y ejecución (salas 1, 3, 4)"],
    ["Scenario 1", "8", "Sala 1: cuadrícula, Roki y niveles"],
    ["Scenario 2", "4", "Sala 2: módulos y opciones"],
    ["Scenario 3", "8", "Sala 3: brazo robótico y fichas de tipo"],
    ["Scenario 4", "5", "Sala 4: fichas, huecos y celebración"],
    ["Story (+ Tools)", "16", "Director, Narrator, tutorial, reloj, teletransporte"],
    ["Difficulty", "9", "Básica e intermedia en la misma escena"],
    ["Session", "6", "PIN, dificultad, límites de tiempo"],
    ["Telemetry", "6", "Modelo del JSON, registro y subida"],
    ["Map", "3", "Puertas y regreso a la Tierra"],
    ["../Editor", "12", "Menú Tools → Codea y Level Editor"],
  ];
  const colW = 5.85, fh = 0.86;
  filas.forEach(([c, n, d], i) => {
    const col = i < 6 ? 0 : 1, fila = i < 6 ? i : i - 6;
    const x = M + col * (colW + 0.43), y = 1.5 + fila * fh;
    tarjeta(s, x, y, colW, fh - 0.14, fila % 2 ? K.blanco : K.claro);
    tb(s, c, { x: x + 0.2, y: y + 0.06, w: colW - 1.1, h: 0.3, fontFace: F.codigo, fontSize: 13, bold: true, color: K.azul });
    tb(s, d, { x: x + 0.2, y: y + 0.37, w: colW - 1.1, h: 0.3, fontSize: 13, color: K.gris });
    circulo(s, n, x + colW - 0.72, y + 0.09, 0.54, i === 10 ? K.naranja : K.azulMedio, K.blanco, 13);
  });
  tb(s, "El número es la cantidad de scripts de cada carpeta.", { x: M + colW + 0.43, y: 1.5 + 5 * fh + 0.12, w: colW, h: 0.4, fontSize: 12, color: K.gris, italic: true });
  s.addNotes("La regla es simple: una carpeta por sala más las carpetas transversales. Si algo falla en la sala 3, se mira Scenario 3 y luego Block Programming System. Editor no va en el APK: son herramientas para quien trabaja en Unity.");
}

// La nave desde fuera
{
  const s = contenido("Juego.unity desde fuera: cuatro salas, una escena");
  captura(s, "mapa_nave.jpg", M, 1.45, 8.6, "La nave de Juego.unity vista desde fuera, con sus cuatro módulos");
  puntos(s, [
    ["Enviroment:", "la estructura, en cuatro partes (Part1…Part4), una por sala."],
    ["door_1 a door_3:", "las puertas entre salas (AutomaticDoor). Se abren al resolver cada reto."],
    ["Teleporter:", "lleva al jugador de una sala a otra sin cargar otra escena."],
    ["Pos_earth y Pos_sun:", "lo que se ve por las ventanas; al final, el regreso a la Tierra."],
  ], 9.5, 1.5, W - M - 9.5, 5.2, 14);
  s.addNotes("Así se ve la escena Juego desde fuera, en Play. Cada módulo es una sala; las ventanas del techo dejan ver un poco del interior. El jugador nunca sale de esta escena: el Director lo teletransporta de una sala a la siguiente y abre la puerta correspondiente.");
}

// La jerarquía de Juego.unity, leída de la escena por CodeaCaptures
{
  const s = contenido("Juego.unity por dentro: la jerarquía");
  const filas = fs.readFileSync(path.join(CAPT, "jerarquia_juego.txt"), "utf8").split(/\r?\n/).filter(Boolean).map(l => {
    const [nombre, comps] = l.split("\t");
    return { hijo: nombre.startsWith("  "), nombre: nombre.trim(), comps: comps || "" };
  });
  // Raíces que importan y, de sus hijos, solo los que llevan componentes propios (sin los relojes).
  function bloque(raices) {
    const runs = [];
    for (const raiz of raices) {
      const i = filas.findIndex(f => !f.hijo && f.nombre === raiz);
      if (i < 0) continue;
      const lineas = [filas[i]];
      for (let j = i + 1; j < filas.length && filas[j].hijo; j++)
        if (filas[j].comps && filas[j].nombre !== "Timer") lineas.push(filas[j]);
      for (const f of lineas) {
        runs.push({ text: (f.hijo ? "  └ " : "") + f.nombre, options: { color: f.hijo ? "C8D8EA" : K.blanco, bold: !f.hijo } });
        runs.push({ text: f.comps ? "   " + f.comps : "", options: { color: "8FD3C6", fontSize: 10, breakLine: true } });
      }
    }
    return runs;
  }
  const col = (W - 2 * M - 0.3) / 2;
  [["Sesion", "Director", "TelemetryManager", "TelemetryUpdater", "Player", "Enviroment", "Tutorial", "Pos_earth"],
   ["Escenario (1)", "Escenario (2)", "Escenario (3)", "Escenario (4)"]].forEach((raices, i) => {
    const x = M + i * (col + 0.3);
    tarjeta(s, x, 1.45, col, 5.3, K.noche);
    tb(s, bloque(raices), { x: x + 0.3, y: 1.6, w: col - 0.5, h: 5.0, fontFace: F.codigo, fontSize: 12, lineSpacingMultiple: 1.05 });
  });
  s.addNotes("Esta es la jerarquía real de Juego.unity, sacada de la escena con la herramienta de capturas. En verde, los componentes propios del proyecto. Sesion lleva la dificultad y los límites de tiempo; Director lleva al Narrator; cada Escenario (N) lleva su controlador y lo que necesita: el Escenario 1 su cuadrícula (LevelLoader, LevelManager) y su fila; el 3 el brazo, sus posiciones y las fichas de tipo. Cada sala tiene además un Timer, que aquí no se muestra.");
}

// =====================================================================
// Sección 3
// =====================================================================
divisoria(3, "El sistema de bloques", "La pieza que comparten las salas 1, 3 y 4",
  "Si se entiende esta parte, se entiende la mitad del proyecto.");

// 9. Flujo de bloques
{
  const s = contenido("Agarrar, encajar, ejecutar");
  const etapas = [
    ["Agarrar", "BlockNode", "VRGrabEvents envuelve el agarre de Meta"],
    ["Encajar", "Socket", "Busca el hueco libre más cercano al soltar"],
    ["Probar", "ProgramTrigger", "Comprueba condiciones y registra el intento"],
    ["Ejecutar", "ProgramRunner", "Recorre la cadena de huecos N veces"],
    ["Actuar", "Execute()", "Cada bloque mueve a Roki o al brazo"],
  ];
  const n = etapas.length, gap = 0.38, w = (W - 2 * M - (n - 1) * gap) / n, y = 1.65;
  etapas.forEach(([t, c, d], i) => {
    const x = M + i * (w + gap);
    tarjeta(s, x, y, w, 2.6, K.hielo);
    circulo(s, String(i + 1), x + w / 2 - 0.3, y + 0.25, 0.6, i === 2 ? K.naranja : K.azul);
    tb(s, t, { x, y: y + 0.95, w, h: 0.4, fontFace: F.titulo, fontSize: 18, bold: true, color: K.texto, align: "center" });
    pastilla(s, c, x + 0.15, y + 1.42, { w: w - 0.3, fontSize: 12, fill: K.blanco });
    tb(s, d, { x: x + 0.15, y: y + 1.88, w: w - 0.3, h: 0.7, fontSize: 12, color: K.gris, align: "center" });
    if (i < n - 1) flecha(s, x + w + 0.04, y + 1.3, x + w + gap - 0.04, y + 1.3, K.azulMedio);
  });
  // La cadena
  const y2 = 4.65;
  tb(s, "El programa es la cadena de huecos", { x: M, y: y2, w: 6, h: 0.4, fontFace: F.titulo, fontSize: 17, bold: true, color: K.azul });
  const hx = M, hy = y2 + 0.6;
  const etiquetas = ["Avanzar 2", "Girar Izq", "Avanzar", "Usar", ""];
  etiquetas.forEach((t, i) => {
    const x = hx + i * 1.62;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: hy, w: 1.35, h: 0.62, rectRadius: 0.08, fill: { color: t ? K.turquesaClaro : K.blanco }, line: { color: t ? K.turquesa : K.grisClaro, width: 1.5, dashType: t ? "solid" : "dash" } });
    tb(s, t || "vacío", { x, y: hy, w: 1.35, h: 0.62, fontSize: 13, bold: !!t, color: t ? K.turquesa : K.gris, align: "center", valign: "middle" });
    if (i < etiquetas.length - 1) flecha(s, x + 1.37, hy + 0.31, x + 1.6, hy + 0.31, K.gris);
  });
  tb(s, "Socket.Next une cada hueco con el siguiente. ProgramRunner se para en el primer hueco vacío.", { x: M, y: hy + 0.8, w: 8.0, h: 0.5, fontSize: 13, color: K.gris });
  tarjeta(s, 9.15, y2, W - M - 9.15, 1.6, K.naranjaClaro);
  tb(s, [
    { text: "Detalle importante", options: { bold: true, color: K.naranja, breakLine: true } },
    { text: "El intento se registra antes de ejecutarse: si el visor se cierra a mitad, el intento ya está guardado." },
  ], { x: 9.35, y: y2 + 0.18, w: W - M - 9.55, h: 1.65, fontSize: 14 });
  s.addNotes("Recorrido completo: el niño agarra un BlockNode; al soltarlo, el bloque busca el Socket libre más cercano y se acopla. SocketRow genera los huecos de una fila y los encadena con Next. Al pulsar el botón azul, ProgramTrigger consulta las precondiciones (por ejemplo, en la sala 3, que haya ficha de tipo), registra el intento y le pide a ProgramRunner que ejecute. El runner recorre la cadena y llama a Execute de cada bloque.");
}

// 10. Contrato
{
  const s = contenido("Un mismo contrato, tres salas");
  tarjeta(s, M, 1.5, 6.4, 5.2, K.noche);
  tb(s, [
    { text: "// Block Programming System/BlockNode.cs", options: { color: "7F95AB", breakLine: true } },
    { text: "public abstract class ", options: { color: "7FB6E8" } }, { text: "BlockNode", options: { color: "F2C38B" } },
    { text: " : MonoBehaviour", options: { color: "E3EAF2", breakLine: true } },
    { text: "{", options: { color: "E3EAF2", breakLine: true } },
    { text: "    // agarre, acople y etiqueta: comunes", options: { color: "7F95AB", breakLine: true } },
    { text: "    public abstract ", options: { color: "7FB6E8" } }, { text: "IEnumerator ", options: { color: "8FD3C6" } },
    { text: "Execute();", options: { color: "E3EAF2", breakLine: true } },
    { text: "}", options: { color: "E3EAF2", breakLine: true } },
    { text: " ", options: { breakLine: true } },
    { text: "class ", options: { color: "7FB6E8" } }, { text: "GridBlock", options: { color: "F2C38B" } }, { text: " : BlockNode   // sala 1", options: { color: "E3EAF2", breakLine: true } },
    { text: "class ", options: { color: "7FB6E8" } }, { text: "ArmBlock", options: { color: "F2C38B" } }, { text: "  : BlockNode   // sala 3", options: { color: "E3EAF2", breakLine: true } },
    { text: "class ", options: { color: "7FB6E8" } }, { text: "ShapeChip", options: { color: "F2C38B" } }, { text: " : BlockNode   // sala 4", options: { color: "E3EAF2", breakLine: true } },
    { text: " ", options: { breakLine: true } },
    { text: "// ProgramRunner.cs (resumido)", options: { color: "7F95AB", breakLine: true } },
    { text: "for ", options: { color: "7FB6E8" } }, { text: "(int i = 0; i < repetitions; i++)", options: { color: "E3EAF2", breakLine: true } },
    { text: "  for ", options: { color: "7FB6E8" } }, { text: "(Socket c = start; c != null", options: { color: "E3EAF2", breakLine: true } },
    { text: "       && !c.IsEmpty; c = c.Next)", options: { color: "E3EAF2", breakLine: true } },
    { text: "    yield return ", options: { color: "7FB6E8" } }, { text: "c.CurrentBlock.Execute();", options: { color: "E3EAF2" } },
  ], { x: M + 0.35, y: 1.75, w: 5.8, h: 4.8, fontFace: F.codigo, fontSize: 14 });
  const filas = [
    ["GridBlock", "→ Bot", "Avanzar, Girar, Usar: mueve a Roki casilla a casilla"],
    ["ArmBlock", "→ RoboticArm", "Recoger, Girar, Soltar, Volver"],
    ["ShapeChip", "→ nada", "Solo encaja: el hueco decide si es la figura correcta"],
  ];
  filas.forEach(([c, a, d], i) => {
    const y = 1.5 + i * 1.3;
    tarjeta(s, 7.4, y, W - M - 7.4, 1.12, K.hielo);
    pastilla(s, c, 7.6, y + 0.17, { fill: K.blanco, bold: true });
    tb(s, a, { x: 9.3, y: y + 0.17, w: 2.5, h: 0.34, fontFace: F.codigo, fontSize: 13, color: K.turquesa, valign: "middle", bold: true });
    tb(s, d, { x: 7.6, y: y + 0.6, w: W - M - 7.8, h: 0.4, fontSize: 13, color: K.texto });
  });
  tarjeta(s, 7.4, 5.45, W - M - 7.4, 1.25, K.naranjaClaro);
  tb(s, [
    { text: "Execute() es una corrutina ", options: { bold: true, color: K.naranja } },
    { text: "(IEnumerator): el runner espera a que termine la animación de cada bloque antes de pasar al siguiente." },
  ], { x: 7.6, y: 5.6, w: W - M - 7.8, h: 1.0, fontSize: 14 });
  s.addNotes("Herencia clásica: BlockNode tiene todo lo común y cada sala solo implementa qué hace su bloque. Por eso añadir una instrucción nueva a la sala 1 es añadir un valor al enum de GridBlock y su movimiento en Bot. La ficha de la sala 4 hereda de BlockNode solo para reutilizar agarre, acople y reinicio; su Execute está vacío.");
}

// =====================================================================
// Sección 4
// =====================================================================
divisoria(4, "Las cuatro salas", "Qué hace cada una y qué clases mirar",
  "Una diapositiva por sala: la mecánica, las clases clave y lo que no es obvio.");

function sala(titulo, img, pilar, clases, items, notas) {
  const s = contenido(titulo);
  const iw = 6.9;
  const ih = captura(s, img, M, 1.5, iw, "Captura de " + titulo);
  tarjeta(s, M, 1.5 + ih + 0.2, iw, 0.95, K.hielo);
  tb(s, [{ text: "Pilar: ", options: { bold: true, color: K.azul } }, { text: pilar }], { x: M + 0.25, y: 1.5 + ih + 0.2, w: iw - 0.5, h: 0.95, fontSize: 15, valign: "middle" });
  const x = M + iw + 0.45, w = W - M - x;
  tb(s, "Clases clave", { x, y: 1.5, w, h: 0.35, fontFace: F.titulo, fontSize: 14, bold: true, color: K.gris });
  let fy = 1.95, fx = x;
  for (const c of clases) {
    const cw = Math.max(0.6, c.length * 12 * 0.0085 + 0.3);
    if (fx + cw > x + w) { fx = x; fy += 0.46; }
    pastilla(s, c, fx, fy);
    fx += cw + 0.12;
  }
  puntos(s, items, x, fy + 0.65, w, 6.75 - (fy + 0.65), 14);
  s.addNotes(notas);
}

sala("Sala 1 · Secuencialidad", "sala1_mesa_basica.jpg", "diseño de algoritmos y descomposición",
  ["LevelLoader", "LevelManager", "Bot", "GridBlock", "Scenario1Controller"],
  [
    ["Niveles en JSON:", "Assets/_Main/Levels. LevelLoader los convierte en la cuadrícula de casillas."],
    ["Se dibujan con", "Tools → Level Editor, sin escribir el JSON a mano."],
    ["Reintento automático:", "si Roki no llega, el nivel vuelve a su estado y el niño prueba otra vez."],
    ["resolver_nivel.js", "prueba todas las secuencias y confirma que el nivel tiene solución."],
  ],
  "LevelLoader lee el JSON (una matriz de números: camino, muro, peligro, salida) e instancia las casillas. LevelManager guarda el estado y valida cada movimiento. Bot es Roki: avanzar, girar y usar el botón. Scenario1Controller conecta la sala con el Director y la telemetría. Básica e intermedia son dos JSON distintos con la misma geometría.");

// Sala 1: el nivel JSON dibujado, y la sala desde arriba
{
  const s = contenido("Sala 1: del archivo JSON a la cuadrícula");
  const nivel = JSON.parse(fs.readFileSync(path.join(RAIZ, "Assets", "_Main", "Levels", "reto-espejo.json"), "utf8"));
  const tipos = {
    0: ["Camino", K.hielo, K.azul], 1: ["Muro", "8A96A3", K.blanco], 2: ["Roki", K.turquesa, K.blanco],
    3: ["Peligro", "E05A47", K.blanco], 5: ["Salida", "3BA55C", K.blanco], 6: ["Vacío", K.blanco, K.gris],
  };
  const c = 0.78;
  for (let fila = 0; fila < nivel.height; fila++)
    for (let col = 0; col < nivel.width; col++) {
      const v = nivel.tiles[fila * nivel.width + col];
      const [, fill, color] = tipos[v] || ["?", K.claro, K.texto];
      const x = M + col * c, y = 1.55 + fila * c;
      s.addShape(pres.shapes.RECTANGLE, { x, y, w: c - 0.06, h: c - 0.06, fill: { color: fill }, line: { color: v === 6 ? K.grisClaro : fill, dashType: v === 6 ? "dash" : "solid" } });
      tb(s, v === 2 ? "2 ↑" : String(v), { x, y, w: c - 0.06, h: c - 0.06, fontFace: F.codigo, fontSize: 14, bold: true, color, align: "center", valign: "middle" });
    }
  const lx = M, ly = 1.55 + nivel.height * c + 0.2;
  Object.entries(tipos).forEach(([v, [nombre, fill, color]], i) => {
    const x = lx + (i % 3) * 1.6, y = ly + Math.floor(i / 3) * 0.45;
    s.addShape(pres.shapes.RECTANGLE, { x, y: y + 0.04, w: 0.3, h: 0.3, fill: { color: fill }, line: { color: v === "6" ? K.grisClaro : fill } });
    tb(s, `${v} ${nombre}`, { x: x + 0.4, y, w: 1.2, h: 0.38, fontSize: 13, valign: "middle" });
  });
  const x = 5.6, w = W - M - x;
  const h = captura(s, "sala1_cenital.jpg", x, 1.45, w, "La sala 1 vista desde arriba en Play");
  tarjeta(s, x, 1.45 + h + 0.2, w, 6.75 - (1.45 + h + 0.2), K.noche);
  tb(s, [
    { text: "// Assets/_Main/Levels/" + nivel.name + ".json", options: { color: "7F95AB", breakLine: true } },
    { text: `"width": ${nivel.width}, "height": ${nivel.height}, "spawnDirection": ${nivel.spawnDirection},`, options: { color: "E3EAF2", breakLine: true } },
    { text: `"tiles": [${nivel.tiles.slice(0, 12).join(",")}, …]`, options: { color: "F2C38B" } },
  ], { x: x + 0.25, y: 1.45 + h + 0.35, w: w - 0.5, h: 6.75 - (1.45 + h + 0.5), fontFace: F.codigo, fontSize: 12 });
  s.addNotes("El nivel de básica es este archivo JSON: una lista de números que LevelLoader lee fila a fila (6 de ancho, 5 de alto) y convierte en casillas. El 2 es donde aparece Roki, y spawnDirection 0 hace que mire hacia arriba. A la derecha, la misma sala en Play vista desde arriba: los peligros son las cajas en llamas. Para crear o cambiar un nivel no se escribe el JSON a mano: se pinta en Tools → Level Editor.");
}

sala("Sala 2 · Condicionales", "sala2_modulo_probar.jpg", "reconocimiento de patrones y diseño de algoritmos",
  ["ModuleData", "SystemModule", "ModuleOptionButton", "Scenario2Controller"],
  [
    ["El contenido no está en el código:", "cada módulo es un asset ModuleData con el problema, las opciones y cuáles son correctas."],
    ["Un asset por dificultad:", "Motores_Basica, Motores_Intermedia… Se editan desde el inspector."],
    ["Probar:", "repara el módulo solo si lo marcado es exactamente la solución; si no, error y se desmarca todo."],
    ["Cada opción incorrecta", "es la correcta de otro módulo: obliga a leer el problema."],
  ],
  "Esta sala no usa bloques. ModuleData es un ScriptableObject: un archivo de datos que se edita en Unity sin programar. Un profesor podría cambiar los textos o las opciones correctas sin tocar C#. SystemModule pinta la pantalla y compara lo marcado contra lo correcto al pulsar Probar. ModuleOptionButton es un botón genérico con un método Press, sin depender del SDK de Meta.");

// Sala 2: un ModuleData tal como está en el proyecto
{
  const s = contenido("Sala 2: el contenido vive en un asset");
  const yaml = fs.readFileSync(path.join(RAIZ, "Assets", "_Main", "Scripts", "Scenario 2", "Modulos", "Generadores_Basica.asset"), "utf8");
  // Unity escribe los acentos entre comillas como "\xED": se deshacen.
  const texto = v => {
    v = v.trim();
    if (!v.startsWith('"')) return v;
    return v.slice(1, -1).replace(/\\x([0-9A-Fa-f]{2})/g, (_, h) => String.fromCharCode(parseInt(h, 16))).replace(/\\"/g, '"');
  };
  const campo = n => texto((yaml.match(new RegExp("^  " + n + ": (.*)$", "m")) || [])[1] || "");
  const opciones = [...yaml.matchAll(/- label: (.*)\r?\n\s+isCorrect: (\d)/g)].map(m => [texto(m[1]), m[2] === "1"]);
  const pw = 5.7;
  tarjeta(s, M, 1.45, pw, 5.3, K.claro);
  tb(s, "Generadores_Basica.asset  (ModuleData)", { x: M + 0.25, y: 1.6, w: pw - 0.5, h: 0.4, fontFace: F.codigo, fontSize: 14, bold: true, color: K.azul });
  [["Module Id", campo("moduleId")], ["Module Name", campo("moduleName")], ["Problem", campo("problem")]].forEach(([k, v], i) => {
    const y = 2.2 + i * 0.5;
    tb(s, k, { x: M + 0.25, y, w: 1.6, h: 0.38, fontSize: 13, color: K.gris, valign: "middle" });
    s.addShape(pres.shapes.RECTANGLE, { x: M + 1.9, y, w: pw - 2.15, h: 0.38, fill: { color: K.blanco }, line: { color: K.grisClaro } });
    tb(s, v, { x: M + 2.0, y, w: pw - 2.35, h: 0.38, fontSize: 13, valign: "middle" });
  });
  tb(s, "Options", { x: M + 0.25, y: 3.8, w: 2, h: 0.35, fontSize: 13, color: K.gris });
  opciones.forEach(([label, ok], i) => {
    const y = 4.25 + i * 0.55;
    s.addShape(pres.shapes.RECTANGLE, { x: M + 0.25, y, w: pw - 0.5, h: 0.45, fill: { color: K.blanco }, line: { color: K.grisClaro } });
    tb(s, label, { x: M + 0.4, y, w: pw - 2.2, h: 0.45, fontSize: 13, valign: "middle" });
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: M + pw - 1.55, y: y + 0.07, w: 1.15, h: 0.31, rectRadius: 0.05, fill: { color: ok ? "3BA55C" : K.claro }, line: { color: ok ? "3BA55C" : K.grisClaro } });
    tb(s, ok ? "isCorrect ✓" : "isCorrect", { x: M + pw - 1.55, y: y + 0.07, w: 1.15, h: 0.31, fontSize: 11, bold: ok, color: ok ? K.blanco : K.gris, align: "center", valign: "middle" });
  });
  const x = M + pw + 0.4, w = W - M - x;
  const h = captura(s, "sala2_modulo_sin_resolver.jpg", x, 1.45, w, "El módulo de la sala 2 en el juego");
  puntos(s, [
    ["Seis assets:", "tres módulos × dos dificultades, en Scripts/Scenario 2/Modulos."],
    ["Mismo moduleId", "en las dos dificultades, para que la telemetría los compare."],
  ], x, 1.45 + h + 0.3, w, 6.75 - (1.45 + h + 0.3), 14);
  s.addNotes("A la izquierda, el asset del sistema de energía en básica tal como está en el proyecto: el problema y cuatro opciones, una correcta. SystemModule lo pinta en la pantalla del módulo, a la derecha: es el mismo módulo. Cambiar el texto o la opción correcta es editar este asset en el inspector, sin programar. Fíjense en que las opciones incorrectas son correctas en otros módulos: agregar agua, encender el ventilador, agregar combustible.");
}

sala("Sala 3 · Bucles y parámetros", "sala3_intermedia.jpg", "reconocimiento de patrones y abstracción",
  ["RoboticArm", "ArmBlock", "ArmSlot", "ArmTypeSocket", "ArmTypeChip", "Scenario3Controller"],
  [
    ["La fila entera es el bucle:", "SocketRow.Repetitions y ProgramRunner.Run(inicio, N). No hay bloque “repetir”."],
    ["La ficha de tipo es el argumento:", "barril o caja decide de qué pila recoge el brazo."],
    ["Sin ficha no se ejecuta:", "ArmTypeSocket implementa IRunPrecondition y veta la ejecución."],
    ["Básica:", "fila hecha y oculta. Intermedia: el niño ordena los giros."],
  ],
  "Aquí el mismo programa resuelve dos casos cambiando solo un dato, como el argumento de una función. RoboticArm es el equivalente de Bot: gira entre posiciones (ArmSlot), recoge y suelta. Un detalle de diseño: la ficha de tipo está fuera de la fila a propósito, para poder cambiarla aunque la fila esté bloqueada, que es justo lo que pasa en básica.");

sala("Sala 4 · Patrones", "sala4_panel.jpg", "reconocimiento de patrones y abstracción",
  ["ShapeChip", "ShapeSocket", "Scenario4Controller", "MissionCelebration"],
  [
    ["La figura es el sprite:", "encaja si chip.Shape == expectedShape, comparando la referencia al mismo recorte."],
    ["Sin runner ni botón:", "cada hueco valida al soltar la ficha."],
    ["La ficha incorrecta sale disparada", "con física (ShapeSocket.Eject) y queda a mano para otro hueco."],
    ["Sobran fichas a propósito:", "el último hueco no se resuelve por descarte."],
  ],
  "Las figuras salen de un único pliego de sprites recortado. Se compara por referencia y no por nombre para que renombrar un recorte no rompa nada sin avisar. En intermedia los huecos forman la palabra YACHAY en glifos. Al completar el panel, MissionCelebration lanza confeti y empieza el regreso a la Tierra (EarthApproach).");

// =====================================================================
// Sección 5
// =====================================================================
divisoria(5, "Orquestación", "Director, narración, dificultad y relojes",
  "Lo que hace que las salas formen una sola experiencia.");

// Director
{
  const s = contenido("El Director: la historia como lista de pasos");
  // Árbol
  const cajaArbol = (t, sub, x, y, w, fill, col) => {
    tarjeta(s, x, y, w, 0.95, fill);
    tb(s, t, { x, y: y + 0.1, w, h: 0.4, fontFace: F.codigo, fontSize: 15, bold: true, color: col, align: "center" });
    tb(s, sub, { x, y: y + 0.5, w, h: 0.35, fontSize: 12, color: col === K.blanco ? "C8D8EA" : K.gris, align: "center" });
  };
  cajaArbol("Director", "recorre la historia", M + 1.6, 1.5, 3.0, K.azul, K.blanco);
  flecha(s, M + 3.1, 2.47, M + 3.1, 2.75, K.azulMedio);
  cajaArbol("Scenario", "Tutorial, Escenario 1…4", M + 1.6, 2.8, 3.0, K.hielo, K.azul);
  flecha(s, M + 3.1, 3.77, M + 3.1, 4.05, K.azulMedio);
  cajaArbol("Step", "un paso de la historia", M + 1.6, 4.1, 3.0, K.hielo, K.azul);
  flecha(s, M + 2.3, 5.07, M + 1.3, 5.4, K.azulMedio);
  flecha(s, M + 3.9, 5.07, M + 4.9, 5.4, K.azulMedio);
  cajaArbol("UnityEvents", "instantáneos", M, 5.45, 2.55, K.claro, K.texto);
  cajaArbol("IStepAction", "se esperan", M + 3.65, 5.45, 2.55, K.naranjaClaro, K.naranja);
  // Derecha
  const x = 7.15, w = W - M - x;
  tb(s, "Acciones que el paso espera (IStepAction)", { x, y: 1.5, w, h: 0.35, fontFace: F.titulo, fontSize: 14, bold: true, color: K.gris });
  pastillas(s, ["Narrator", "Waiter", "Fader", "AutomaticDoor"], x, 1.95);
  pastillas(s, ["Scenario1Controller … Scenario4Controller"], x, 2.41);
  tb(s, "Cómo termina un paso (StepCompletionType)", { x, y: 3.05, w, h: 0.35, fontFace: F.titulo, fontSize: 14, bold: true, color: K.gris });
  [["All", "cuando acaban todas"], ["Any", "con la primera que acaba"], ["Sequence", "una tras otra, en orden"]].forEach(([a, b], i) => {
    pastilla(s, a, x, 3.5 + i * 0.45, { w: 1.3, fill: K.turquesaClaro, color: K.turquesa, bold: true });
    tb(s, b, { x: x + 1.45, y: 3.5 + i * 0.45, w: w - 1.45, h: 0.34, fontSize: 14, valign: "middle" });
  });
  tarjeta(s, x, 5.0, w, 1.55, K.hielo);
  tb(s, [
    { text: "Narrator: ", options: { bold: true, color: K.azul } },
    { text: "voz y texto a la vez, emparejados por ID. El texto está en Narrativa.csv y el audio en Sounds/VoiceLines/Camila/<ID>.wav. Espera a la más larga de las dos." },
  ], { x: x + 0.2, y: 5.15, w: w - 0.4, h: 1.5, fontSize: 14 });
  s.addNotes("El Director no tiene la historia escrita en código: está armada en el inspector de Unity como una lista de escenarios y pasos. Cada paso dispara eventos (encender una luz, teletransportar) y espera acciones: que el narrador termine de hablar, que la puerta se abra o que el niño resuelva la sala. Los controladores de sala son IStepAction: el Director simplemente espera a que terminen. Tiene un menú Skip Step para probar sin jugar todo.");
}

// Los pasos del Director, leídos de la escena
{
  const s = contenido("Los pasos del Director en Juego.unity");
  const capitulos = [];
  for (const l of fs.readFileSync(path.join(CAPT, "director_juego.txt"), "utf8").split(/\r?\n/).filter(Boolean)) {
    if (l.startsWith("# ")) { capitulos.push({ nombre: l.slice(2), pasos: [] }); continue; }
    const [mensaje, , esperas] = l.split("\t");
    const cuenta = {};
    for (const e of (esperas || "").split(", ").filter(Boolean)) cuenta[e] = (cuenta[e] || 0) + 1;
    const resumen = Object.entries(cuenta).map(([n, k]) => k > 1 ? `${n} ×${k}` : n).join(" · ");
    capitulos.at(-1).pasos.push([mensaje, resumen]);
  }
  const n = capitulos.length, gap = 0.22, w = (W - 2 * M - (n - 1) * gap) / n;
  capitulos.forEach((cap, i) => {
    const x = M + i * (w + gap);
    tarjeta(s, x, 1.45, w, 5.3, i === 0 ? K.naranjaClaro : K.hielo);
    tb(s, cap.nombre, { x: x + 0.15, y: 1.55, w: w - 0.3, h: 0.4, fontFace: F.titulo, fontSize: 15, bold: true, color: i === 0 ? K.naranja : K.azul });
    const runs = [];
    cap.pasos.forEach(([m, r], j) => {
      runs.push({ text: `${j + 1}. ${m}`, options: { bold: true, fontSize: 12, color: K.texto, breakLine: true } });
      runs.push({ text: r || "—", options: { fontSize: 10, color: K.gris, breakLine: j < cap.pasos.length - 1, paraSpaceAfter: 6 } });
    });
    tb(s, runs, { x: x + 0.15, y: 2.05, w: w - 0.3, h: 4.6 });
  });
  s.addNotes("Esta es la historia completa tal como está en el inspector del Director: cinco capítulos y sus pasos. Debajo de cada paso, lo que espera (en gris). Por ejemplo, 'Esperar a completar el escenario 1' espera al objeto Escenario (1), que es su Scenario1Controller, luego al Narrator y luego a la puerta door_1. Si se quiere cambiar el orden de la experiencia o añadir una pausa, se hace aquí, sin tocar código.");
}

// Dificultad
{
  const s = contenido("Dificultad: una escena, dos versiones de cada reto");
  const x0 = M, w0 = 4.2;
  tb(s, "Cómo funciona", { x: x0, y: 1.5, w: w0, h: 0.35, fontFace: F.titulo, fontSize: 14, bold: true, color: K.gris });
  const pasos = [
    ["Setup", "guarda la dificultad elegida en PlayerPrefs"],
    ["DifficultyApplier", "la lee al cargar Juego, antes que ningún otro script (orden −1000)"],
    ["<Sala>Difficulty", "pone los datos de cada reto: nivel, módulos, fila"],
    ["DifficultyOnly", "apaga las piezas de la otra dificultad"],
  ];
  pasos.forEach(([c, d], i) => {
    const y = 1.95 + i * 1.18;
    circulo(s, String(i + 1), x0, y + 0.05, 0.48, K.azul, K.blanco, 14);
    tb(s, c, { x: x0 + 0.65, y, w: w0 - 0.65, h: 0.34, fontFace: F.codigo, fontSize: 13, bold: true, color: K.azul });
    tb(s, d, { x: x0 + 0.65, y: y + 0.37, w: w0 - 0.65, h: 0.7, fontSize: 13, color: K.texto });
  });
  // Tabla comparativa
  const tx = 5.3, tw = W - M - tx;
  const filas = [
    ["", "Básica", "Intermedia"],
    ["Sala 1", "8 huecos, paleta de 8", "11 huecos, paleta de 13 y un camino distractor"],
    ["Sala 2", "4 opciones, 1 correcta", "4 opciones, 2 correctas"],
    ["Sala 3", "Fila hecha y oculta", "Fila visible y desordenada"],
    ["Sala 4", "Figuras abstractas", "Glifos: YACHAY"],
    ["Narración", "Lista 1", "Lista 2 (frases 9.2 y 14.2)"],
  ];
  const rows = filas.map((f, i) => f.map((c, j) => ({
    text: c,
    options: {
      bold: i === 0 || j === 0, fontSize: 13, fontFace: F.cuerpo,
      color: i === 0 ? K.blanco : (j === 0 ? K.azul : K.texto),
      fill: { color: i === 0 ? (j === 2 ? K.turquesa : K.azul) : (i % 2 ? K.claro : K.blanco) },
      valign: "middle", margin: [4, 8, 4, 8],
    },
  })));
  s.addTable(rows, { x: tx, y: 1.55, w: tw, colW: [1.3, 2.35, tw - 3.65], rowH: 0.62, border: { type: "solid", pt: 0.75, color: K.grisClaro } });
  tarjeta(s, tx, 5.55, tw, 1.15, K.naranjaClaro);
  tb(s, "Ventaja: cada cambio de la sala se hace una sola vez. Antes había dos escenas y todo había que hacerlo dos veces.", { x: tx + 0.2, y: 5.55, w: tw - 0.4, h: 1.15, fontSize: 14, valign: "middle" });
  s.addNotes("La dificultad la elige el supervisor y queda fija para toda la sesión. No hay dos escenas: una sola escena se monta distinta al cargarse. Lo que cambia son datos (componentes <Sala>Difficulty con un bloque Básica y otro Intermedia) o piezas que solo existen en una dificultad (DifficultyOnly). Para ver en el editor cómo queda cada una: Tools → Codea → Ver como básica / intermedia.");
}

// Dificultad en imágenes
{
  const s = contenido("La misma escena en las dos dificultades");
  const lw = 1.25, gap = 0.25, ih = 2.3, iw = ih * 16 / 9;
  const x0 = (W - (lw + 2 * (iw + gap))) / 2;
  ["Básica", "Intermedia"].forEach((t, i) =>
    tb(s, t, { x: x0 + lw + gap + i * (iw + gap), y: 1.45, w: iw, h: 0.4, fontFace: F.titulo, fontSize: 16, bold: true, color: i ? K.turquesa : K.azul, align: "center" }));
  [["Sala 1", "Paleta de 8 bloques y 8 huecos frente a 13 y 11", "sala1_mesa_basica.jpg", "sala1_mesa_intermedia.jpg"],
   ["Sala 4", "Figuras abstractas frente a YACHAY en glifos", "sala4_panel.jpg", "sala4_panel_intermedia.jpg"]].forEach(([sala, nota, a, b], j) => {
    const y = 1.95 + j * (ih + 0.2);
    tb(s, [{ text: sala, options: { bold: true, fontSize: 16, color: K.azul, breakLine: true } }, { text: nota, options: { fontSize: 11, color: K.gris } }],
      { x: x0, y, w: lw, h: ih, valign: "middle" });
    [a, b].forEach((img, i) => s.addImage({ path: path.join(CAPT, img), x: x0 + lw + gap + i * (iw + gap), y, w: iw, h: ih, altText: `${sala}, ${i ? "intermedia" : "básica"}` }));
  });
  s.addNotes("Las cuatro capturas son de la misma escena, Juego.unity, en Play: solo cambia la dificultad elegida en Setup. En la sala 1, DifficultyOnly apaga una paleta y enciende la otra, y Scenario1Difficulty cambia el nivel y el número de huecos. En la sala 4, los huecos y las fichas de intermedia son las letras de YACHAY.");
}

// Relojes
{
  const s = contenido("Dos relojes, un solo componente");
  const cajas = [
    ["15", "minutos de sesión", "Es lo que muestran los relojes de las salas. Al agotarse, termina la experiencia y vuelve a Setup.", K.azul, K.hielo],
    ["5", "minutos por sala", "No se ven. Al agotarse, la historia pasa a la siguiente sala para que nadie se quede atascado.", K.turquesa, K.turquesaClaro],
  ];
  cajas.forEach(([n, l, d, c, f], i) => {
    const x = M + i * 4.1, w = 3.8;
    tarjeta(s, x, 1.55, w, 3.5, f);
    tb(s, n, { x, y: 1.75, w, h: 1.3, fontFace: F.titulo, fontSize: 72, bold: true, color: c, align: "center", valign: "middle" });
    tb(s, l, { x, y: 3.05, w, h: 0.4, fontFace: F.titulo, fontSize: 17, bold: true, color: K.texto, align: "center" });
    tb(s, d, { x: x + 0.3, y: 3.55, w: w - 0.6, h: 1.4, fontSize: 14, color: K.gris, align: "center" });
  });
  const x = M + 8.3, w = W - M - x;
  tb(s, "Clases", { x, y: 1.55, w, h: 0.35, fontFace: F.titulo, fontSize: 14, bold: true, color: K.gris });
  pastillas(s, ["ScenarioTimeLimit"], x, 1.98);
  pastillas(s, ["Timer", "TimerDisplay"], x, 2.44);
  pastillas(s, ["TimeUpSequence"], x, 2.9);
  puntos(s, [
    "Corren solo con un reto abierto: en narraciones y traslados están quietos.",
    "Si Roki o el brazo se están moviendo, se deja terminar el intento (máximo 90 s).",
  ], x, 3.5, w, 2.0, 14);
  tarjeta(s, M, 5.45, W - 2 * M, 1.25, K.claro);
  tb(s, [
    { text: "Para probar sin esperar: ", options: { bold: true, color: K.azul } },
    { text: "clic derecho en ScenarioTimeLimit → “Agotar el tiempo de la sala actual”." },
  ], { x: M + 0.3, y: 5.45, w: W - 2 * M - 0.6, h: 1.25, fontSize: 15, valign: "middle" });
  s.addNotes("Hay dos límites en el mismo componente, ScenarioTimeLimit. El de sesión es el que ve el niño; el de sala existe para que nadie se quede atascado en la sala 1 y no llegue a las demás. El reloj arranca cuando empieza el reto, no cuando empieza la narración, para no castigar el tiempo que se pasa escuchando.");
}

// =====================================================================
// Sección 6
// =====================================================================
divisoria(6, "Los datos", "Telemetría: qué se guarda, dónde y cómo llega al servidor",
  "La parte que interesa al equipo evaluador.");

// Camino
{
  const s = contenido("Los datos no se pierden aunque no haya red");
  imagen(s, path.join(DIAG, "flujo_datos.jpg"), M, 1.35, W - 2 * M, 4.55, 2400, 1040, "Diagrama: lo que hace el niño → TelemetryManager → archivo JSON en el visor → servidor de Yachay Tech; sin red, el archivo queda pendiente");
  const cl = [["TelemetryManager", "registra"], ["TelemetryData", "modelo del JSON"], ["JSONUploader", "envía"], ["TelemetryOutbox", "lleva la cuenta de lo subido"]];
  const w = (W - 2 * M - 3 * 0.3) / 4;
  cl.forEach(([c, d], i) => {
    const x = M + i * (w + 0.3);
    tarjeta(s, x, 6.05, w, 0.75, K.hielo);
    tb(s, [{ text: c, options: { fontFace: F.codigo, bold: true, color: K.azul, breakLine: true } }, { text: d, options: { color: K.gris } }], { x: x + 0.15, y: 6.08, w: w - 0.3, h: 0.7, fontSize: 12, align: "center", valign: "middle" });
  });
  s.addNotes("El archivo se escribe durante la partida, no al final: si el visor se apaga, lo registrado no se pierde. Al terminar se intenta subir. Si no hay red, el archivo queda pendiente y se sube solo la próxima vez que se abre Setup, o con el botón Subir datos. TelemetryOutbox nunca borra ni mueve archivos: solo apunta en subidos.txt qué ya llegó al servidor.");
}

// Qué se guarda
{
  const s = contenido("Un archivo JSON por estudiante");
  tarjeta(s, M, 1.5, 5.9, 5.2, K.noche);
  const k = (t) => ({ text: t, options: { color: "8FD3C6" } });
  const v = (t, br = true) => ({ text: t, options: { color: "F2C38B", breakLine: br } });
  const p = (t, br = false) => ({ text: t, options: { color: "E3EAF2", breakLine: br } });
  tb(s, [
    p("{", true),
    p("  "), k("\"formatVersion\""), p(": "), v("1,"),
    p("  "), k("\"pin\""), p(": "), v("\"0101\","),
    p("  "), k("\"difficulty\""), p(": "), v("1,"),
    p("  "), k("\"totalSeconds\""), p(": "), v("588,"),
    p("  "), k("\"escenario1\""), p(": {", true),
    p("    "), k("\"completed\""), p(": "), v("true,"),
    p("    "), k("\"failedAttempts\""), p(": "), v("2,"),
    p("    "), k("\"attempts\""), p(": [{", true),
    p("      "), k("\"sequence\""), p(": "), v("[\"Avanzar 2\", …],"),
    p("      "), k("\"failReason\""), p(": "), v("\"collision\","),
    p("      "), k("\"solutionPercent\""), p(": "), v("50.0"),
    p("    }, …]", true),
    p("  },", true),
    p("  "), k("\"escenario2\""), p(": { … }, …", true),
    p("}"),
  ], { x: M + 0.3, y: 1.75, w: 5.4, h: 4.8, fontFace: F.codigo, fontSize: 15 });
  const x = 7.0, w = W - M - x;
  tb(s, "Nombre: {pin}_{sesión}_{visor}.json", { x, y: 1.5, w, h: 0.4, fontFace: F.codigo, fontSize: 14, bold: true, color: K.naranja });
  const salas = [
    ["Sala 1", "la secuencia de bloques de cada intento, por qué falló y qué tan cerca estuvo de la solución"],
    ["Sala 2", "qué opciones marcó al pulsar Probar y cuáles eran incorrectas"],
    ["Sala 3", "con qué tipo y cuántas repeticiones ejecutó, y cuántos objetos movió"],
    ["Sala 4", "qué ficha intentó en qué hueco y cuántas colocó mal"],
  ];
  salas.forEach(([t, d], i) => {
    const y = 2.1 + i * 0.95;
    circulo(s, String(i + 1), x, y + 0.05, 0.48, K.turquesa, K.blanco, 14);
    tb(s, [{ text: t + ": ", options: { bold: true, color: K.turquesa } }, { text: d }], { x: x + 0.65, y, w: w - 0.65, h: 0.85, fontSize: 14, valign: "middle" });
  });
  tarjeta(s, x, 6.0, w, 0.7, K.hielo);
  tb(s, [{ text: "Diccionario completo: ", options: { bold: true, color: K.azul } }, { text: "TELEMETRIA.md, en la raíz del repositorio" }], { x: x + 0.2, y: 6.0, w: w - 0.4, h: 0.7, fontSize: 14, valign: "middle" });
  s.addNotes("Cada escenario tiene su propia estructura porque no miden lo mismo. No hay datos personales: solo el PIN de 4 cifras. solutionPercent compara el intento con la solución (la subsecuencia común más larga), para saber si un intento fallido estaba cerca. Todos los campos aparecen siempre, con -1 o vacío cuando no hay dato.");
}

// Servidor
{
  const s = contenido("El servidor de datos");
  const eps = [
    ["POST", "/upload", "Recibe el JSON (multipart, campo file) con la cabecera X-API-Key", "200 guardado · 400 archivo no válido · 401 clave mala"],
    ["GET", "/health", "Comprueba que el servidor responde", "{\"status\":\"alive\"}"],
  ];
  eps.forEach(([m, r, d, res], i) => {
    const y = 1.5 + i * 1.4;
    tarjeta(s, M, y, 7.3, 1.25, K.claro);
    pastilla(s, m, M + 0.25, y + 0.18, { w: 0.95, fill: i ? K.turquesa : K.naranja, color: K.blanco, bold: true, fontSize: 13 });
    tb(s, r, { x: M + 1.35, y: y + 0.18, w: 2.5, h: 0.34, fontFace: F.codigo, fontSize: 16, bold: true, color: K.azul, valign: "middle" });
    tb(s, d, { x: M + 0.25, y: y + 0.6, w: 6.8, h: 0.3, fontSize: 13 });
    tb(s, res, { x: M + 0.25, y: y + 0.9, w: 6.8, h: 0.3, fontFace: F.codigo, fontSize: 11, color: K.gris });
  });
  // La clave: la misma API key en los JSONUploader de Juego y Setup, y UPLOAD_API_KEY en el servidor.
  tarjeta(s, M, 4.3, 7.3, 0.8, K.naranjaClaro);
  tb(s, "Clave (X-API-Key)", { x: M + 0.25, y: 4.3, w: 2.2, h: 0.8, fontSize: 14, bold: true, color: K.naranja, valign: "middle" });
  tb(s, "Codea2YachayTech2026", { x: M + 2.4, y: 4.3, w: 3.6, h: 0.8, fontFace: F.codigo, fontSize: 16, bold: true, color: K.texto, valign: "middle" });
  puntos(s, [
    ["Repositorio aparte:", "Codea2Server, con Docker, en una laptop de Yachay publicada por el servidor de la universidad."],
    ["URL y clave:", "la URL en Resources/ServidorDatos.asset; la clave en los JSONUploader de Juego y Setup y en UPLOAD_API_KEY del servidor."],
  ], M, 5.3, 7.3, 1.5, 14);
  const x = 8.35, w = W - M - x;
  tarjeta(s, x, 1.55, w, 5.15, K.hielo);
  tb(s, "Si no sube, ¿qué capa falló?", { x: x + 0.25, y: 1.75, w: w - 0.5, h: 0.4, fontFace: F.titulo, fontSize: 15, bold: true, color: K.azul });
  const capas = [["Sin red", "Wi-Fi del visor"], ["Cannot resolve host", "DNS o URL mal escrita"], ["Request timeout", "laptop apagada o firewall"], ["401", "clave incorrecta"], ["500", "error del servidor: docker logs"]];
  capas.forEach(([a, b], i) => {
    const y = 2.35 + i * 0.72;
    tb(s, a, { x: x + 0.25, y, w: w - 0.5, h: 0.3, fontFace: F.codigo, fontSize: 12, bold: true, color: K.naranja });
    tb(s, b, { x: x + 0.25, y: y + 0.3, w: w - 0.5, h: 0.3, fontSize: 13 });
  });
  tb(s, "Ping desde JSONUploader o Tools → Codea → Probar el servidor", { x: x + 0.25, y: 6.0, w: w - 0.5, h: 0.55, fontSize: 12, italic: true, color: K.gris });
  s.addNotes("El contrato entre la app y el servidor es muy pequeño: un POST con el archivo y una clave, y un GET para saber si está vivo. Si se cambia de servidor, solo se toca el asset ServidorDatos y se recompila el APK; la guía paso a paso está en Docs/Cambiar-servidor-de-datos.md. Ojo: la URL actual es http, así que Android necesita permitir conexiones sin cifrar (está configurado).");
}

// =====================================================================
// Sección 7
// =====================================================================
divisoria(7, "Trabajar con el proyecto", "Herramientas, cambios frecuentes y documentación",
  "Lo práctico: cómo se comprueba, se modifica y se compila.");

// Tools → Codea
{
  const s = contenido("El menú Tools → Codea");
  const tools = [
    ["Comprobar escena de juego", "Revisa cables, referencias y soluciones de la escena", K.azul],
    ["Probar las dos dificultades en Play", "Juega solo las cuatro salas y valida el JSON", K.azul],
    ["Asignar la voz del narrador", "Pone cada audio en su frase según el nombre del archivo", K.turquesa],
    ["Ver como básica / intermedia", "Muestra en el editor solo las piezas de una dificultad", K.turquesa],
    ["Servidor de datos · Probar el servidor", "Cambia la URL y prueba ping y subida", K.naranja],
    ["Validar un JSON de telemetría", "Comprueba un archivo contra las reglas del formato", K.naranja],
    ["Compilar el APK", "Genera Build/app.apk para el visor", K.azul],
    ["Tools → Level Editor", "Dibuja los niveles de la sala 1 y exporta el JSON", K.turquesa],
  ];
  const w = (W - 2 * M - 0.35) / 2, h = 1.08;
  tools.forEach(([t, d, c], i) => {
    const x = M + (i % 2) * (w + 0.35), y = 1.5 + Math.floor(i / 2) * (h + 0.22);
    tarjeta(s, x, y, w, h, K.claro);
    s.addShape(pres.shapes.OVAL, { x: x + 0.25, y: y + 0.36, w: 0.36, h: 0.36, fill: { color: c }, line: { color: c } });
    tb(s, t, { x: x + 0.8, y: y + 0.15, w: w - 1.0, h: 0.4, fontFace: F.titulo, fontSize: 15, bold: true, color: K.texto });
    tb(s, d, { x: x + 0.8, y: y + 0.56, w: w - 1.0, h: 0.4, fontSize: 13, color: K.gris });
  });
  s.addNotes("Estas herramientas están en Assets/_Main/Editor y no van dentro del APK. Las dos primeras son la red de seguridad: antes de compilar, Comprobar escena y Probar las dos dificultades deben dar 0 fallos. Varias se pueden lanzar también sin abrir Unity, en modo batch; está explicado en Context.md, sección 14.");
}

// Recetas
{
  const s = contenido("Cambios frecuentes, paso a paso");
  const recetas = [
    ["Cambiar una frase del narrador", ["Editar el texto en Narrativa.csv (guardar como CSV UTF-8)", "Grabar el audio como <ID>.wav en Sounds/VoiceLines/Camila", "Tools → Codea → Asignar la voz del narrador"]],
    ["Nuevo nivel de la sala 1", ["Dibujarlo en Tools → Level Editor y exportar el JSON", "Comprobarlo con resolver_nivel.js", "Asignarlo en Scenario1Difficulty"]],
    ["Cambiar opciones de la sala 2", ["Abrir el asset ModuleData de ese módulo y dificultad", "Editar problema, opciones e isCorrect", "Sin tocar código"]],
    ["Instalar en un visor", ["Modo desarrollador y cable USB", "adb install -r Build/app.apk", "Abrir desde Orígenes desconocidos"]],
  ];
  const w = (W - 2 * M - 0.35) / 2, h = 2.4;
  recetas.forEach(([t, pasos], i) => {
    const x = M + (i % 2) * (w + 0.35), y = 1.5 + Math.floor(i / 2) * (h + 0.3);
    tarjeta(s, x, y, w, h, i === 3 ? K.naranjaClaro : K.hielo);
    tb(s, t, { x: x + 0.3, y: y + 0.2, w: w - 0.6, h: 0.4, fontFace: F.titulo, fontSize: 17, bold: true, color: i === 3 ? K.naranja : K.azul });
    pasos.forEach((p, j) => {
      const py = y + 0.78 + j * 0.5;
      circulo(s, String(j + 1), x + 0.3, py + 0.02, 0.34, i === 3 ? K.naranja : K.azul, K.blanco, 11);
      tb(s, p, { x: x + 0.8, y: py, w: w - 1.1, h: 0.4, fontSize: 14, valign: "middle", fontFace: /adb|\.js|\.wav/.test(p) ? F.cuerpo : F.cuerpo });
    });
  });
  s.addNotes("Estas son las cuatro cosas que más probablemente haya que hacer después de la entrega. Las tres primeras no requieren programar. Después de cualquier cambio: Comprobar escena, Probar las dos dificultades, Compilar el APK e instalarlo.");
}

// Documentación
{
  const s = contenido("Documentación para seguir");
  const docs = [
    ["Context.md", "Fuente de verdad: arquitectura, decisiones, API de cada componente y trampas conocidas"],
    ["README.md", "Visión general, estructura del repositorio e instalación en el visor"],
    ["TELEMETRIA.md", "Diccionario completo del JSON, campo por campo"],
    ["Docs/Herramientas/LEEME.md", "Scripts de apoyo: compilar sin Unity, resolver niveles, validar JSON"],
    ["Docs/Cambiar-servidor-de-datos.md", "Guía para mover el servidor"],
    ["Docs/Codea2_GDD.docx", "Documento de diseño del juego"],
  ];
  docs.forEach(([d, t], i) => {
    const y = 1.5 + i * 0.86;
    tarjeta(s, M, y, 7.9, 0.72, i === 0 ? K.naranjaClaro : K.claro);
    tb(s, d, { x: M + 0.25, y: y + 0.06, w: 7.4, h: 0.3, fontFace: F.codigo, fontSize: 13, bold: true, color: i === 0 ? K.naranja : K.azul });
    tb(s, t, { x: M + 0.25, y: y + 0.36, w: 7.4, h: 0.3, fontSize: 13, color: K.texto });
  });
  const x = 9.0, w = W - M - x;
  tarjeta(s, x, 1.5, w, 1.75, K.azul);
  tb(s, [
    { text: "Repositorio", options: { bold: true, fontSize: 16, color: K.blanco, breakLine: true } },
    { text: "github.com/datascienceyt/codea2", options: { fontFace: F.codigo, fontSize: 12, color: "F2C38B", breakLine: true } },
    { text: "Rama dev. El APK está en Build/ con Git LFS.", options: { fontSize: 13, color: "C8D8EA" } },
  ], { x: x + 0.25, y: 1.65, w: w - 0.5, h: 1.5, paraSpaceAfter: 6 });
  tarjeta(s, x, 3.5, w, 3.2, K.hielo);
  tb(s, "Antes de compilar", { x: x + 0.25, y: 3.7, w: w - 0.5, h: 0.4, fontFace: F.titulo, fontSize: 16, bold: true, color: K.azul });
  puntos(s, ["Comprobar escena: 0 fallos", "Probar las dos dificultades: 0 fallos", "Probar el servidor", "Probar en el visor"], x + 0.25, 4.3, w - 0.5, 2.3, 15);
  s.addNotes("Si solo se lee un documento, que sea Context.md: está escrito para que cualquiera entienda el sistema sin leer todo el código. Para clonar el repositorio hace falta Git LFS, o el APK baja como un puntero de texto.");
}

// Cierre
seccion = "Cierre";
pres.addSection({ title: "Cierre" });
{
  const s = nueva("OSCURA");
  s.addImage({ path: path.join(CAPT, "sala4_panel.jpg"), x: 0, y: 0, w: W, h: W * 9 / 16, altText: "Panel final de la sala 4" });
  s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: H, fill: { color: K.noche, transparency: 15 }, line: { color: K.noche, transparency: 15 } });
  tb(s, "¿Preguntas?", { x: M + 0.2, y: 2.3, w: 10, h: 1.3, fontFace: F.titulo, fontSize: 60, bold: true, color: K.blanco });
  tb(s, "Siguiente sesión: capacitación en el uso de la aplicación", { x: M + 0.2, y: 3.75, w: 11, h: 0.6, fontFace: F.titulo, fontSize: 24, color: K.naranja });
  tb(s, "Supervisor, PIN y dificultad · Colocar el visor · Acompañar al estudiante · Subir los datos", { x: M + 0.2, y: 4.4, w: 11.5, h: 0.5, fontSize: 17, color: "C8D8EA" });
  s.addNotes("Abrir a preguntas. Recordar que la siguiente capacitación es de uso, con el resto de estudiantes: cómo maneja el supervisor la pantalla de Setup, cómo se coloca el visor y cómo se suben los datos.");
}

// pptxgenjs mete un <a:pPr> en mitad del párrafo cuando un punto lleva título en negrita y texto
// normal (XML inválido: se pierden la viñeta y el espaciado). Se quita al escribir, y de paso se
// marca el texto como español para el corrector.
async function escribir() {
  const JSZip = require(require.resolve("jszip", { paths: [RUTA_PPTXGEN] }));
  const zip = await JSZip.loadAsync(await pres.write({ outputType: "nodebuffer" }));
  for (const nombre of Object.keys(zip.files).filter(n => /^ppt\/(slides|slideLayouts|notesSlides)\/[^/]+\.xml$/.test(n))) {
    const xml = (await zip.file(nombre).async("string"))
      .replace(/<\/a:r><a:pPr indent="0" marL="0"><a:buNone\/><\/a:pPr>/g, "</a:r>")
      .replace(/lang="en-US"/g, 'lang="es-EC"');
    zip.file(nombre, xml);
  }
  fs.writeFileSync(SALIDA, await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" }));
  console.log("Escrito:", SALIDA);
}
escribir();
