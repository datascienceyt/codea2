// Busca todas las soluciones de un nivel del Escenario 1 con las reglas de Bot.cs:
// solo Path (0) es transitable (Spawn 2 cuenta como Path); Avanzar 2 son dos pasos y cada uno
// se valida; Usar necesita mirar a la Salida (5) desde la casilla contigua.
// Uso: node solve.js <nivel.json> <maxBloques> [L R A2 A U]
const fs = require("fs");
const level = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const maxBlocks = +process.argv[3] || 10;
const palette = process.argv.slice(4, 9).map(Number); // L R A2 A U
const W = level.width, H = level.height, T = (x, y) => (x < 0 || y < 0 || x >= W || y >= H) ? -1 : level.tiles[y * W + x];
const DX = [0, 1, 0, -1], DY = [-1, 0, 1, 0]; // Up Right Down Left
const s = level.tiles.indexOf(2), sx = s % W, sy = Math.floor(s / W);
const walk = t => t === 0 || t === 2;

const results = [];
function step(x, y, d, seq, cells) {
  const tx = x + DX[d], ty = y + DY[d];
  if (T(tx, ty) === 5 && d === 3 && seq.length + 1 <= maxBlocks) results.push({ seq: [...seq, "U"], cells: [...cells] });
  if (seq.length >= maxBlocks - 1) return;
  // girar
  step(x, y, (d + 3) % 4, [...seq, "L"], cells);
  step(x, y, (d + 1) % 4, [...seq, "R"], cells);
  // avanzar 1 y 2 (sin chocar)
  if (walk(T(tx, ty))) {
    step(tx, ty, d, [...seq, "A"], [...cells, `${tx},${ty}`]);
    const ux = tx + DX[d], uy = ty + DY[d];
    if (walk(T(ux, uy))) step(ux, uy, d, [...seq, "A2"], [...cells, `${tx},${ty}`, `${ux},${uy}`]);
  }
}
step(sx, sy, 2, [], [`${sx},${sy}`]);

// Solo secuencias "limpias": sin girar y deshacer, sin cuatro giros seguidos, sin volver a una casilla
const clean = results.filter(r => !/(L R|R L|L L L|R R R)/.test(r.seq.join(" ")) && new Set(r.cells).size === r.cells.length);
const fits = r => { if (!palette.length) return true; const c = { L: 0, R: 0, A2: 0, A: 0, U: 0 }; r.seq.forEach(a => c[a]++); return c.L <= palette[0] && c.R <= palette[1] && c.A2 <= palette[2] && c.A <= palette[3] && c.U <= palette[4]; };

const byRoute = {};
for (const r of clean) { const k = r.cells.join(" "); (byRoute[k] = byRoute[k] || []).push(r); }
const routes = Object.entries(byRoute).map(([k, rs]) => ({ k, min: Math.min(...rs.map(r => r.seq.length)), best: rs.sort((a, b) => a.seq.length - b.seq.length)[0], fit: rs.some(fits) }))
  .sort((a, b) => a.min - b.min);

// mapa
const sym = { 0: "·", 1: "█", 2: "R", 3: "!", 4: "·", 5: "S", 6: " " };
for (let y = 0; y < H; y++) console.log("  " + [...Array(W)].map((_, x) => sym[T(x, y)]).join(" "));
console.log(`\nrutas distintas con <= ${maxBlocks} bloques: ${routes.length}`);
for (const r of routes.slice(0, 12)) console.log(`  ${String(r.min).padStart(2)} bloques ${r.fit ? "✓paleta" : "✗paleta"}  ${r.best.seq.join(" ")}   [${r.k}]`);
