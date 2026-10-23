// Utilidades compartidas para leer y reescribir un document.xml de Word sin Word.
// Las usan esqueleto_docx.js y los scripts que generan informes a partir de una plantilla.
const fs = require("fs");

/** Separa un fragmento XML en sus elementos de primer nivel ({ tag, xml }). */
function topLevel(s) {
  const out = [];
  let i = 0;
  while (i < s.length) {
    const open = s.indexOf("<", i);
    if (open < 0) break;
    const m = /^<([\w:]+)/.exec(s.slice(open, open + 64));
    if (!m) { i = open + 1; continue; }
    const tag = m[1];
    const close = s.indexOf(">", open);
    if (s[close - 1] === "/") { out.push({ tag, xml: s.slice(open, close + 1) }); i = close + 1; continue; }
    const re = new RegExp(`<(/?)${tag}(?=[\\s>/])[^>]*?(/?)>`, "g");
    re.lastIndex = open;
    let depth = 0, end = s.length, mm;
    while ((mm = re.exec(s))) {
      if (mm[2] === "/") continue;
      depth += mm[1] ? -1 : 1;
      if (depth === 0) { end = re.lastIndex; break; }
    }
    out.push({ tag, xml: s.slice(open, end) });
    i = end;
  }
  return out;
}

/** Cuerpo del documento: { head, items, tail } para volver a montarlo con join. */
function readBody(path) {
  const xml = fs.readFileSync(path, "utf8");
  const start = xml.indexOf("<w:body>") + "<w:body>".length;
  const end = xml.lastIndexOf("</w:body>");
  return { head: xml.slice(0, start), items: topLevel(xml.slice(start, end)), tail: xml.slice(end) };
}

/** Texto visible de un fragmento. */
function textOf(xml) {
  return [...xml.matchAll(/<w:t(?:\s[^>]*)?>([^<]*)<\/w:t>/g)].map(m => m[1]).join("");
}

/** Escapa texto para meterlo en <w:t>. */
function esc(text) {
  return String(text).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// --- ZIP mínimo: un .docx es un zip. Sin dependencias, con zlib de Node ---

const zlib = require("zlib");

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** Lee un zip: Map nombre → Buffer, en el orden del directorio central. */
function readZip(path) {
  const buf = fs.readFileSync(path);
  let eocd = buf.length - 22;
  while (eocd >= 0 && buf.readUInt32LE(eocd) !== 0x06054b50) eocd--;
  if (eocd < 0) throw new Error(`${path} no es un zip`);

  const count = buf.readUInt16LE(eocd + 10);
  let p = buf.readUInt32LE(eocd + 16);
  const files = new Map();

  for (let i = 0; i < count; i++) {
    const method = buf.readUInt16LE(p + 10);
    const size = buf.readUInt32LE(p + 20);
    const nameLen = buf.readUInt16LE(p + 28), extraLen = buf.readUInt16LE(p + 30), commentLen = buf.readUInt16LE(p + 32);
    const local = buf.readUInt32LE(p + 42);
    const name = buf.slice(p + 46, p + 46 + nameLen).toString("utf8");

    const lNameLen = buf.readUInt16LE(local + 26), lExtraLen = buf.readUInt16LE(local + 28);
    const data = buf.slice(local + 30 + lNameLen + lExtraLen, local + 30 + lNameLen + lExtraLen + size);
    if (!name.endsWith("/")) files.set(name, method === 8 ? zlib.inflateRawSync(data) : Buffer.from(data));

    p += 46 + nameLen + extraLen + commentLen;
  }
  return files;
}

/** Escribe un zip con las entradas en el orden dado ([Content_Types].xml primero en un docx). */
function writeZip(path, files) {
  const locals = [], centrals = [];
  let offset = 0;

  for (const [name, content] of files) {
    const data = Buffer.isBuffer(content) ? content : Buffer.from(content, "utf8");
    const deflated = zlib.deflateRawSync(data, { level: 9 });
    const nameBuf = Buffer.from(name, "utf8");
    const crc = crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0); local.writeUInt16LE(20, 4); local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(8, 8); local.writeUInt16LE(0, 10); local.writeUInt16LE(0x21, 12);
    local.writeUInt32LE(crc, 14); local.writeUInt32LE(deflated.length, 18); local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuf.length, 26); local.writeUInt16LE(0, 28);
    locals.push(local, nameBuf, deflated);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0); central.writeUInt16LE(20, 4); central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8); central.writeUInt16LE(8, 10); central.writeUInt16LE(0, 12); central.writeUInt16LE(0x21, 14);
    central.writeUInt32LE(crc, 16); central.writeUInt32LE(deflated.length, 20); central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBuf.length, 28); central.writeUInt32LE(offset, 42);
    centrals.push(central, nameBuf);

    offset += 30 + nameBuf.length + deflated.length;
  }

  const centralSize = centrals.reduce((n, b) => n + b.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0); end.writeUInt16LE(files.size, 8); end.writeUInt16LE(files.size, 10);
  end.writeUInt32LE(centralSize, 12); end.writeUInt32LE(offset, 16);

  fs.writeFileSync(path, Buffer.concat([...locals, ...centrals, end]));
}

module.exports = { topLevel, readBody, textOf, esc, readZip, writeZip, crc32 };
