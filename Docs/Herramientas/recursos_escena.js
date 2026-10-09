// Recorre las dependencias (guid) de la escena y de los assets que usa, y agrupa por carpeta.
// Uso (desde la raíz del proyecto): node Docs/Herramientas/recursos_escena.js [escena] [--list carpeta]
const fs = require("fs"), path = require("path");
const scene = process.argv[2] && !process.argv[2].startsWith("--") ? process.argv[2] : "Assets/Scenes/Juego.unity";
const listAt = process.argv.indexOf("--list");
const listFolder = listAt > 0 ? process.argv[listAt + 1] : null;

const guidPath = {};
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    let st; try { st = fs.statSync(p); } catch { continue; }
    if (st.isDirectory()) walk(p);
    else if (f.endsWith(".meta")) {
      const m = fs.readFileSync(p, "utf8").match(/guid: (\w+)/);
      if (m) guidPath[m[1]] = p.slice(0, -5).split(path.sep).join("/");
    }
  }
})("Assets");

const seen = new Set(), queue = [scene];
while (queue.length) {
  const f = queue.pop();
  if (seen.has(f)) continue;
  seen.add(f);
  if (!/\.(unity|prefab|mat|asset|controller|anim|overrideController|mixer)$/i.test(f)) continue;
  let s; try { s = fs.readFileSync(f, "utf8"); } catch { continue; }
  for (const m of s.matchAll(/guid: (\w{32})/g)) {
    const p = guidPath[m[1]];
    if (p && !seen.has(p)) queue.push(p);
  }
}

if (listFolder) {
  for (const f of [...seen].filter(f => f.startsWith(listFolder)).sort()) console.log(f);
  process.exit(0);
}

const groups = {};
for (const f of seen) {
  if (f.endsWith(".unity")) continue;
  const parts = f.split("/");
  const top = parts[1] === "_Main" ? parts.slice(0, 3).join("/") : parts.slice(0, 2).join("/");
  const ext = path.extname(f).toLowerCase() || "(sin extensión)";
  groups[top] = groups[top] || {};
  groups[top][ext] = (groups[top][ext] || 0) + 1;
}
for (const [g, c] of Object.entries(groups).sort())
  console.log(g.padEnd(46), Object.entries(c).map(([e, n]) => e + ":" + n).join(" "));
