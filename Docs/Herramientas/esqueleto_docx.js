// Esqueleto de un document.xml: hijos directos de <w:body> con estilo, texto y marcas.
// Uso: node Docs/Herramientas/esqueleto_docx.js <carpeta>/word/document.xml [desde] [hasta]
const fs = require("fs");
const xml = fs.readFileSync(process.argv[2], "utf8");
const from = +(process.argv[3] || 0), to = +(process.argv[4] || 1e9);

const body = xml.slice(xml.indexOf("<w:body>") + 8, xml.lastIndexOf("</w:body>"));

// Separa los hijos de primer nivel contando la profundidad de etiquetas.
function topLevel(s) {
  const out = [];
  let i = 0;
  while (i < s.length) {
    const open = s.indexOf("<", i);
    if (open < 0) break;
    const m = /^<([\w:]+)/.exec(s.slice(open));
    if (!m) { i = open + 1; continue; }
    const tag = m[1];
    const selfClose = s.indexOf(">", open);
    if (s[selfClose - 1] === "/") { out.push({ tag, xml: s.slice(open, selfClose + 1) }); i = selfClose + 1; continue; }
    let depth = 0, j = open;
    const re = new RegExp(`<(/?)${tag}(?=[\\s>/])[^>]*?(/?)>`, "g");
    re.lastIndex = open;
    let mm;
    while ((mm = re.exec(s))) {
      if (mm[2] === "/") continue;
      depth += mm[1] ? -1 : 1;
      if (depth === 0) { j = re.lastIndex; break; }
    }
    out.push({ tag, xml: s.slice(open, j) });
    i = j;
  }
  return out;
}

const items = topLevel(body);
items.forEach((it, n) => {
  if (n < from || n > to) return;
  const style = (/<w:pStyle w:val="([^"]+)"/.exec(it.xml) || [])[1] || "";
  const text = [...it.xml.matchAll(/<w:t(?:\s[^>]*)?>([^<]*)<\/w:t>/g)].map(m => m[1]).join("");
  const marks = [];
  if (it.xml.includes("<w:drawing")) marks.push("IMG");
  if (it.xml.includes("<w:sectPr")) marks.push("SECT");
  if (/instrText[^>]*>\s*TOC/.test(it.xml)) marks.push("TOC");
  if (/instrText[^>]*>\s*SEQ/.test(it.xml)) marks.push("SEQ");
  if (it.xml.includes("w:commentReference")) marks.push("COMMENT");
  if (it.xml.includes("<w:numPr>")) marks.push("NUM" + ((/<w:numId w:val="(\d+)"/.exec(it.xml) || [])[1] || ""));
  if (it.xml.includes('w:type="page"')) marks.push("PAGEBREAK");
  const kind = it.tag === "w:tbl" ? `TABLA(${(it.xml.match(/<w:tr[ >]/g) || []).length} filas)` : it.tag;
  console.log(`${String(n).padStart(4)} ${kind.padEnd(14)} ${style.padEnd(6)} ${marks.join(",").padEnd(18)} ${text.slice(0, 110)}`);
});
console.log("total", items.length);
