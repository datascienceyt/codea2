// Páginas reales de las entradas del índice, maquetando el .docx con el conversor de
// OnlyOffice (x2t, viene con OnlyOffice Desktop Editors). Sin Word no hay quien recalcule los
// PAGEREF al abrir, así que el generador los rellena con lo que sale de aquí.
//
// Cada entrada del índice es un hipervínculo a su título; en el PDF de x2t es una anotación
// /Link cuyo /Dest apunta al objeto de la página del título. Las páginas se numeran por su
// orden en el árbol /Pages, que es el mismo número que imprime el pie ("Pág. N").
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");

const X2T = path.join(process.env.ProgramFiles || "C:\\Program Files", "ONLYOFFICE", "DesktopEditors", "converter", "x2t.exe");
// x2t no encuentra las fuentes por su cuenta: usa el índice que genera el editor al abrirse.
const FONTS = path.join(process.env.LOCALAPPDATA || "", "ONLYOFFICE", "DesktopEditors", "data", "fonts");

const disponible = () => fs.existsSync(X2T) && fs.existsSync(path.join(FONTS, "AllFonts.js"));

/** Convierte con x2t. formato 513 = PDF. */
function convertir(origen, destino, formato = 513) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "x2t-"));
  const tarea = path.join(tmp, "tarea.xml");
  fs.writeFileSync(tarea, `<?xml version="1.0" encoding="utf-8"?><TaskQueueDataConvert>` +
    `<m_sFileFrom>${origen}</m_sFileFrom><m_sFileTo>${destino}</m_sFileTo><m_nFormatTo>${formato}</m_nFormatTo>` +
    `<m_sAllFontsPath>${path.join(FONTS, "AllFonts.js")}</m_sAllFontsPath><m_sFontDir>${FONTS}</m_sFontDir>` +
    `<m_sTempDir>${tmp}</m_sTempDir></TaskQueueDataConvert>`);
  try { execFileSync(X2T, [tarea], { stdio: "pipe" }); }
  finally { fs.rmSync(tmp, { recursive: true, force: true }); }
}

/** Página de destino de cada enlace interno, en orden de lectura (página del enlace, de arriba abajo). */
function paginasDeEnlaces(pdf) {
  const s = fs.readFileSync(pdf).toString("latin1");
  const objetos = new Map();
  for (const m of s.matchAll(/(\d+) 0 obj([\s\S]*?)endobj/g)) objetos.set(Number(m[1]), m[2]);

  // Páginas en orden, recorriendo /Kids desde la raíz /Pages (la que no tiene /Parent).
  const raiz = [...objetos].find(([, o]) => /\/Type\s*\/Pages\b/.test(o) && !/\/Parent/.test(o));
  if (!raiz) throw new Error("El PDF no tiene árbol de páginas");
  const paginas = [];
  (function recorrer(n) {
    const o = objetos.get(n);
    if (/\/Type\s*\/Pages\b/.test(o)) {
      for (const k of o.match(/\/Kids\s*\[([^\]]*)\]/)[1].matchAll(/(\d+) 0 R/g)) recorrer(Number(k[1]));
    } else paginas.push(n);
  })(raiz[0]);
  const numeroDe = obj => paginas.indexOf(obj) + 1;

  const enlaces = [];
  paginas.forEach((p, i) => {
    const annots = (objetos.get(p).match(/\/Annots\s*\[([^\]]*)\]/) || [])[1];
    if (!annots) return;
    for (const a of annots.matchAll(/(\d+) 0 R/g)) {
      const o = objetos.get(Number(a[1])) || "";
      if (!/\/Subtype\s*\/Link/.test(o)) continue;
      let dest = (o.match(/\/Dest\s*(\[[^\]]*\]|\d+ 0 R)/) || [])[1];
      if (!dest) continue;
      if (/^\d+ 0 R$/.test(dest)) dest = objetos.get(Number(dest.split(" ")[0]));
      const y = Number((o.match(/\/Rect\s*\[\s*[\d.]+\s+([\d.]+)/) || [])[1] || 0);
      enlaces.push({ desde: i, y, pagina: numeroDe(Number(dest.match(/(\d+) 0 R/)[1])) });
    }
  });
  return enlaces.sort((a, b) => a.desde - b.desde || b.y - a.y).map(e => e.pagina);
}

/** Páginas reales de las entradas del índice de un .docx. Null si no hay OnlyOffice. */
function paginasDelIndice(docx) {
  if (!disponible()) return null;
  const pdf = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "indice-")), "informe.pdf");
  try {
    convertir(docx, pdf);
    return paginasDeEnlaces(pdf);
  } finally {
    fs.rmSync(path.dirname(pdf), { recursive: true, force: true });
  }
}

module.exports = { disponible, convertir, paginasDeEnlaces, paginasDelIndice };
