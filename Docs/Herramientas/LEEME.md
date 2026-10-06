# Herramientas de apoyo

Scripts de Node que no forman parte del juego. Se ejecutan desde la raíz del repositorio.

## `resolver_nivel.js` — comprobar un nivel del Escenario 1

Prueba **todas** las secuencias de bloques posibles con las reglas de `Bot.cs` y lista los
caminos que llegan a la Salida, cuántos bloques necesita cada uno y si caben en una paleta.

```bash
node Docs/Herramientas/resolver_nivel.js Assets/_Main/Levels/escenario1_intermedio.json 11
node Docs/Herramientas/resolver_nivel.js Assets/_Main/Levels/reto.json 10 1 2 2 2 1
#                                         nivel                          máx  L R A2 A U (paleta, opcional)
```

Reglas que aplica:

- El robot sale de la casilla Spawn (2) **mirando abajo**, como el prefab.
- Solo Path (0) es transitable. "Avanzar 2" son dos pasos y cada uno se valida.
- **La Salida (5) solo se usa de frente: desde la casilla de su derecha, mirando a la
  izquierda.** El código del juego aceptaría usarla desde arriba o abajo, pero el botón físico
  de la sala está en una pared.
- Descarta secuencias con giros que se anulan o que repiten casilla.

Con más de 14 bloques tarda bastante: el número de secuencias crece muy deprisa.

## `generar_gdd.js` — regenerar `Docs/Codea2_GDD.docx`

El GDD se escribe desde este script. Para cambiarlo, editar el texto aquí y regenerar:

```bash
npm install docx@9          # una vez, en cualquier carpeta fuera del proyecto de Unity
node Docs/Herramientas/generar_gdd.js Docs/Codea2_GDD.docx
```

Si `require('docx')` no lo encuentra, copiar el script a la carpeta donde se instaló `docx` y
ejecutarlo desde allí. Al abrir el `.docx`, Word pregunta si actualiza los campos: decir que sí,
así se rellena el índice.
