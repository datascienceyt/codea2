# Producto 2 — Recopilación de información (2.1, 2.2, 2.3)

Material de base para redactar el informe del mes 2 con el formato de `Docs/Informe-Mes1.docx`.
Recopilado el **09/10/2026** del código, la escena `Juego`, el historial del repositorio, `Context.md`,
el GDD (`Docs/Codea2_GDD.docx`), la guía de telemetría y las pruebas realizadas.

**Periodo cubierto:** desde la entrega del Producto 1 (13/08/2026) hasta el 09/10/2026.

**Datos que faltan y solo puede aportar el equipo** (marcados como ⟨pendiente⟩ abajo):

- Nombre exacto del Producto 2 y fecha de entrega
- Pruebas con usuarios clave: fechas, quiénes participaron (rol, número), dónde, y observaciones
  literales si se anotaron. Confirmar si las sesiones `0101` y `0102` del 30/09 las jugó el equipo
- Fotos o capturas desde el visor (la escena se puede capturar desde el editor; ver "Evidencias")

---

## 2.1 Implementación de funcionalidades principales

> Descripción y evidencia del desarrollo de las funcionalidades centrales del módulo, incluyendo
> navegación, interacciones, elementos educativos y lógica de funcionamiento.

### Resumen

CODEA 2 es un escape room educativo en realidad virtual para Meta Quest 3S que trabaja el
pensamiento computacional en niños y adolescentes de 8 a 17 años. El jugador es un astronauta cuya
nave atravesó una tormenta espacial; con ayuda de Roki, el robot de mantenimiento, repara cuatro
sistemas en 15 minutos. Cada sala trabaja un pilar: **secuencialidad, condicionales, bucles y
patrones**, en dos dificultades (básica e intermedia). Un supervisor teclea el PIN del participante y
la dificultad antes de entregar el visor; la sesión se registra en un JSON y se sube sola al
terminar si hay red.

Estado al 09/10/2026: **experiencia jugable completa** de principio a fin (tutorial, cuatro salas,
cierre y vuelta a la pantalla del supervisor), en las dos dificultades, con narración grabada y
telemetría. Presentada el 30/09/2026; desde entonces, en fase de ajustes con el feedback recibido.

### Cronología del desarrollo (historial del repositorio, rama `dev`)

| Fecha | Hito |
|---|---|
| 17/06 – 27/07/2026 | Generador y editor de niveles, movimiento del robot, sistema de bloques (previo al Producto 1) |
| 03/08 – 12/08/2026 | Primer sistema de telemetría, prefabs, scripts de mapa e interacciones VR; entrega del Producto 1 |
| 02/09/2026 | Escenarios 2–4, telemetría en JSON y sistema de sesión |
| 08/09/2026 | Narrador nuevo, Escenario 2 con selección múltiple, pantalla del supervisor (PIN y dificultad) |
| 09–11/09/2026 | Interfaz con TextMeshPro, luces e iconos de estado del Escenario 2, pantalla de narración que acompaña al jugador |
| 14/09/2026 | Montaje del Escenario 2, prefabs de botón y voces de la narración |
| 15/09/2026 | Escenarios 3 y 4 con su telemetría |
| 22/09/2026 | **Versión beta 1**: nuevo entorno y experiencia jugable completa; primer APK |
| 29/09/2026 | Sesión por PIN y dificultad, cierre por tiempo agotado; cambios acordados en la reunión de revisión |
| 30/09/2026 | Escena `Setup`, narrativa nueva, escena intermedia y APK de la presentación |
| 01/10/2026 | Pedidos de cambio recibidos; plan de prueba guionizado de la telemetría |
| 06/10/2026 | Formato nuevo de telemetría, tutorial inicial, botón del Escenario 2 que se suelta solo |
| 09/10/2026 | Las dos dificultades en una sola escena; revisión completa con herramientas de comprobación; Escenario 3 básico sin instrucciones a la vista; textos del Escenario 2 |

Tamaño actual: **71 scripts C# (≈12.400 líneas)**: 66 de juego y 5 herramientas de editor.

### Navegación y flujo de la sesión

1. **Pantalla del supervisor (`Setup`)**: teclado de PIN (10 dígitos, borrar, siguiente
   correlativo), botones Básica / Intermedia y EMPEZAR. No deja empezar sin PIN completo y sin
   dificultad elegida
2. **Botón INICIAR**: la escena de juego carga en espera; nada empieza hasta que se pulsa, para dar
   tiempo a que el niño se ponga el visor
3. **Tutorial**: frase de bienvenida y cuatro interacciones de práctica (dos botones, un bloque en su
   hueco y una ficha en su hueco). Al completarlas se pasa a la misión
4. **Salas 1 a 4**: el jugador se traslada entre salas con fundido a negro; las puertas se abren al
   resolver cada sala. Dentro de cada sala se mueve por teletransporte
5. **Cierre**: frase final, cierre de la sesión, subida del JSON y vuelta automática a `Setup` con el
   siguiente PIN propuesto
6. **Tiempo agotado** (15 min): se detiene la historia, suena la frase de despedida, se cierra y sube
   la sesión y se vuelve a `Setup`

La secuencia la orquesta el componente **Director**: 5 tramos y 23 pasos, cada uno con acciones
inmediatas y acciones que el paso espera (narración, fundidos, teletransporte, puertas, resolución
de la sala). Espera 20 veces a la narración, una por frase.

### Elementos educativos por sala

| Sala | Pilar | Qué hace el niño | Básica | Intermedia |
|---|---|---|---|---|
| 1 · Dormitorio | Secuencialidad | Ordena bloques (Avanzar, Avanzar 2, Girar Izq/Der, Usar) para que Roki recorra una rejilla y pulse el botón verde que abre la puerta | Nivel 6×5, solución de 8 bloques, 8 huecos, paleta de 8 | Mismo cuarto, solución de 11 bloques por arriba y un camino distractor de 14 por abajo que no cabe en los 11 huecos; paleta de 13 |
| 2 · Sala de control | Condicionales | Tres módulos averiados (motores, energía, enfriamiento) describen síntomas; el niño marca la acción que lo arregla | 4 opciones, 1 correcta | 4 opciones, 2 correctas (un síntoma por acción) |
| 3 · Almacén | Bucles y parámetros | Programa un brazo robótico para clasificar 7 cajas y 3 barriles mezclados; una ficha de tipo es el argumento del programa y un contador fija las repeticiones | Las instrucciones no se ven: solo elige tipo y repeticiones | Además reordena las instrucciones (Soltar · Girar Der · Recoger · Girar Izq llegan desordenadas) |
| 4 · Panel final | Patrones | Encaja fichas en los huecos cuya figura es idéntica; varias figuras se parecen y hay fichas de sobra | 4 huecos, 18 fichas | Igual por ahora; pendiente el alfabeto Yachay en glifos |

Decisiones pedagógicas implementadas (GDD y `Context.md`):

- **Sala 2**: los enunciados describen síntomas, no la causa; ninguna palabra de la respuesta aparece
  en el enunciado, y cada opción incorrecta es la correcta de otro módulo (hay que leer y descartar)
- **Sala 3**: la fila entera es el bucle; la ficha de tipo cambia el dato sin cambiar el programa.
  Hacen falta al menos dos ejecuciones, una por tipo
- **Sala 4**: la figura se compara visualmente con la del hueco (discriminación visual), no por
  nombre; hay fichas distractoras para que el último hueco no se resuelva por eliminación

**Narración**: guion de 21 frases más 2 variantes por dificultad (sala 2 y sala 3), voz grabada,
texto en pantalla emparejado por identificador con un CSV y 5 frases de ánimo ante errores que no
interrumpen la frase en curso. Una lista de 20 entradas por dificultad.

**Retroalimentación** (GDD 5.6): frase de felicitación al terminar cada sala; cambios visibles en el
mundo (la puerta se abre, los módulos pasan a verde con luz e icono, las cajas despejan el paso, los
motores arrancan con sonido); en la sala 4 cada hueco resuelto se ilumina y queda fijo, y una ficha
equivocada sale expulsada con sonido; ante un error, frase breve de ánimo.

### Lógica de funcionamiento

| Subsistema | Cómo funciona |
|---|---|
| Programación por bloques (salas 1, 3 y tutorial) | Cada bloque se acopla por proximidad al hueco libre más cercano; la fila genera sus huecos y los encadena; al pulsar Ejecutar se recorre la cadena y cada bloque ejecuta su acción sobre el robot o el brazo. El intento se registra **antes** de ejecutar para no perder nunca el intento ganador |
| Sala 1 | Rejilla lógica cargada desde JSON (editor de niveles propio); el robot se mueve en relativo (avanzar, girar); chocar, salirse o usar donde no hay nada cuenta como error; el nivel se rearma solo tras un intento fallido |
| Sala 2 | Cada módulo carga sus datos de un asset (problema, opciones, cuáles son correctas); una opción incorrecta queda marcada 0,8 s y se suelta sola |
| Sala 3 | El brazo gira entre tres posiciones y resuelve el destino según la ficha de tipo; la sala se completa contando lo entregado en cada destino; lo que quede en la pinza vuelve a su pila al acabar cada ejecución |
| Sala 4 | Cada hueco valida al soltar; la ficha correcta se fija, la incorrecta se expulsa con física; un reinicio no mueve las acertadas |
| Dificultad | Una sola escena para las dos: al cargar, cada sala se configura con los datos de la dificultad elegida y las piezas que solo existen en una se apagan en la otra |
| Sesión | PIN y dificultad desde `Setup`; tiempo límite con cierre automático; vuelta a `Setup` esperando a que termine la subida |
| Telemetría | Un JSON por participante (`{pin}_{sesión}_{visor}.json`) con tiempos, intentos, secuencias, selecciones, colocaciones, agarres, conexiones y errores por sala; escritura diferida para no provocar tirones; informe de integridad al cerrar; subida por HTTP POST con reintentos; 100 % funcional sin red |

### Cambios respecto al diseño del Producto 1

| Diseño del Producto 1 | Implementado | Motivo |
|---|---|---|
| 3 escenarios | 4 (se añade Patrones) | Cubrir el reconocimiento de patrones como sala propia |
| Ingreso de username (RF-01) | PIN numérico tecleado por el supervisor | Más rápido y sin errores de escritura para niños; decidido con el equipo |
| Registro en `.csv` (RF-05) | JSON por participante | Estructura jerárquica por sala e intento, que un CSV no representa bien |
| Temporizador de 20 min (RF-06) | 15 min, con 5 relojes en la nave | Duración ajustada a la experiencia completa |
| Sala 1 básica con instrucciones macro | Rejilla con movimientos relativos en las dos dificultades; la intermedia aumenta longitud y añade un camino distractor | Mantener la misma mecánica y dificultar con el problema |
| Sala 3 con bloque contenedor "Repetir" | La fila entera es el bucle + ficha de tipo como argumento | Introducir parámetros además de repetición |
| Sala 4 intermedia por número de lados | Figuras visualmente parecidas; alfabeto Yachay en preparación | Discriminación visual en lugar de conteo |
| Sin hand tracking | Mandos como entrada principal; manos habilitadas en todas las piezas | Pendiente de validar en el visor |

### Evidencias sugeridas (figuras)

- Pantalla del supervisor (`Setup`) y botón INICIAR del tutorial
- Cada sala en básica e intermedia (en la sala 1 se ve la paleta distinta; en la sala 3, la fila
  oculta en básica y visible en intermedia)
- Inspector del Director con sus pasos, y de un componente de dificultad de sala
- Historial de commits del periodo (GitHub, rama `dev`)
- Un JSON de sesión real (`Docs/JSON Samples/0101_5_00000.json`)
- Informe de la herramienta de comprobación (`Logs/Codea_Comprobacion.txt`)

---

## 2.2 Integración de recursos 3D e interacciones inmersivas

> Detalle de los recursos 3D incorporados, programación de interacciones, configuración de
> controles y ajustes para su ejecución en dispositivos Meta Quest.

### Recursos 3D incorporados (los que usa la escena de juego)

| Origen | Contenido |
|---|---|
| Sci-Fi Styled Modular Pack | 20 modelos FBX (suelos, paredes, ventanales, puertas, escaleras, batería, generador de escudo), 10 prefabs, 9 materiales, 8 texturas, 4 animaciones (puerta) |
| Modelos propios (`_Main/3D Models`) | 11 modelos: robot Roki, bloque de programación, botón rectangular, barril, cajas grande y pequeña, baldosa, luz, Tierra y Sol; 9 materiales y 5 texturas |
| Shader Graph propios | 4: atmósfera y halo de la Tierra, núcleo cercano y lejano |
| Materiales propios (`_Main/CustomMaterials`) | 38 materiales, incluido el vidrio de la sala 3 |
| Efectos (VFXPACK_FIRE_WALLCOEUR) | Fuego y humo negro, 2 prefabs |
| Geometría de ProBuilder | 44 mallas modeladas en el editor (salas, mesas, paneles, cuarto del tutorial) |
| Interfaz 2D | 13 imágenes (iconos de estado, figuras), pliego de 34 figuras del Escenario 4, fuente Consolas |
| Audio | 28 locuciones de la narración actual, 5 frases de error, efectos de botón, puerta, encaje y desencaje, robot, arranque de motores y tarea completada |
| Meta XR SDK | Rig de cámara, interactores de mando y mano, botones de pulsación, puntos de teletransporte (*building blocks*) |

### Programación de interacciones

| Interacción | Implementación | Cantidad en escena |
|---|---|---|
| Pulsar botones | Botones de pulsación del Meta Interaction SDK con eventos cableados a la lógica; cada botón con sonido propio | 23 en la escena de juego, 15 en `Setup` |
| Agarrar y soltar | Componentes de agarre del SDK para mando y para mano en cada pieza; envoltorio propio que traduce agarre/suelta a eventos | 49 piezas agarrables (bloques de las salas 1 y 3, fichas de tipo, fichas de figura, piezas del tutorial) |
| Encajar | Acople propio: al soltar, la pieza busca el hueco libre más cercano (5 cm) en un registro estático, se alinea y suena; la sala 3 y la 4 bloquean lo acertado | Huecos generados por fila y huecos de figura |
| Desplazarse | Teletransporte con puntos fijos y traslados guiados entre salas con fundido | 7 puntos |
| Física | Las fichas de la sala 4 tienen gravedad; se congelan al encajar y vuelven solas si caen al suelo | 18 fichas |
| Mundo reactivo | Puertas automáticas, brazo robótico animado por código, robot que recorre la rejilla, luces e iconos de estado, pantalla de narración que se traslada a cada sala | — |

### Configuración de controles

| Acción | Con mandos | Con manos |
|---|---|---|
| Agarrar / soltar | Botón de agarre (grip) | Cerrar y abrir la mano |
| Pulsar botón | Tocar el botón con el mando | Tocar el botón con el dedo |
| Moverse | Teletransporte | — |

Perfil de entrada OpenXR: Oculus Touch Controller. El seguimiento de manos está habilitado en el
proyecto y todas las piezas tienen agarre con mano; **falta validarlo en el visor**.

### Ajustes para Meta Quest

| Ajuste | Valor |
|---|---|
| Dispositivo | Meta Quest 3S, Horizon OS |
| Motor y SDK | Unity 6000.4.1f1 (URP + Shader Graph), Meta XR All-in-One SDK 203.0.2, OpenXR con Meta XR Feature |
| Paquete | `com.yachaytech.Codea2`, versión 0.0.1 |
| Android | SDK mínimo 32, objetivo 34 |
| Compilación | IL2CPP, ARM64 |
| Gráficos | Vulkan (OpenGL ES 3 de respaldo), espacio de color lineal, renderizado estéreo en una pasada (multiview) |
| Rendimiento | Foveated rendering de Meta y *subsampled layout* activados; registro estático de huecos para no buscar en la escena cada frame; escritura diferida de la telemetría (provocaba tirones) |
| Funciones del visor | Seguimiento de manos activado, *focus aware*, sin passthrough |
| Despliegue | APK por instalación local (`adb install`), versionado con Git LFS; JSON en el almacenamiento de la app, accesible por cable |
| Sin conexión | 100 % funcional offline; la subida es opcional |

Pendientes técnicos de este apartado:

- **Rendimiento (RNF-01, 60 FPS)**: sin medir en el visor. El perfil de calidad de Android apunta a un
  asset de render que ya no existe y Unity usa el de PC (MSAA 4x, HDR, sombras suaves). Crear uno
  propio para Quest y medir
- Validar las manos en el visor

### Evidencias sugeridas (figuras)

- Vista general de la nave y de cada sala (capturas del editor o del visor)
- Inspector de un bloque (componentes de agarre de mando y mano) y de un botón de pulsación
- Ajustes de XR Plug-in Management / OpenXR y Player Settings de Android
- Puntos de teletransporte en la escena

---

## 2.3 Pruebas funcionales preliminares con usuarios clave

> Registro de pruebas realizadas con el equipo del proyecto o usuarios clave, incluyendo errores
> identificados, observaciones de uso, limitaciones técnicas encontradas y ajustes aplicados o
> priorizados para la siguiente etapa.

### Pruebas realizadas

| Fecha | Tipo | Participantes | Qué se probó | Resultado |
|---|---|---|---|---|
| 22/09/2026 | Primera versión jugable (beta 1) en el visor | Equipo de desarrollo ⟨confirmar⟩ | Recorrido completo | Base para la revisión del 29/09 |
| 29/09/2026 | Reunión de revisión | Equipo del proyecto ⟨confirmar asistentes⟩ | Experiencia completa | Cambios acordados (ver "Ajustes aplicados") |
| 30/09/2026 | Sesiones en el visor con el APK, dificultad básica | ⟨confirmar quiénes⟩ (PIN 0101 y 0102) | Las cuatro salas y la telemetría | Las dos completadas; datos abajo |
| 30/09/2026 | Presentación de la versión | Equipo del proyecto / evaluadores ⟨confirmar⟩ | Experiencia completa | Feedback recibido el 01/10 (lista de pedidos) |
| 09/10/2026 | Pruebas automatizadas sin visor | — | Integridad de la escena (366 llamadas de eventos), las dos dificultades en Play y el Director completo | 0 fallos; la sala 3 básica, con la fila oculta, entrega 7 de 7 cajas |

### Datos de las sesiones del 30/09/2026 (telemetría real)

| | Sesión 0101 | Sesión 0102 |
|---|---|---|
| Duración total | 5 min 1 s | 3 min 58 s |
| Sala 1 | 33 s · 1 intento, resuelto · 9 agarres · 0 errores | 19 s · 1 intento, resuelto · 10 agarres · 0 errores |
| Sala 2 | 83 s · 5 selecciones, 2 incorrectas | 35 s · 3 selecciones, 0 incorrectas |
| Sala 3 | 66 s · 2 ejecuciones (cajas ×7, barriles ×3) · 0 errores | 62 s · 2 ejecuciones (cajas ×7, barriles ×3) · 0 errores |
| Sala 4 | 41 s · 7 colocaciones, 3 incorrectas · 17 agarres | 45 s · 8 colocaciones, 4 incorrectas · 17 agarres |

Lectura: las dos sesiones completan las cuatro salas sin fallos de lógica; la sala 4 es donde más
errores hay (3–4 colocaciones incorrectas), coherente con su diseño de figuras parecidas. Por la
rapidez, son sesiones de personas que ya conocían la experiencia ⟨confirmar⟩.

### Errores identificados y corregidos

| Error | Cómo se detectó | Corrección |
|---|---|---|
| Se perdía el intento que resolvía la sala | Revisión de la telemetría | El intento se registra antes de ejecutar |
| Tras reiniciar el nivel de la sala 1, llegar a la meta no hacía nada | Prueba de reintento | Se escucha al gestor del nivel, que no se destruye al recargar |
| La sala 2 no se completaba nunca | Prueba en editor | El estado se actualiza antes de avisar |
| "Girar Derecha" giraba visualmente a la izquierda | Prueba visual | Sentido de giro forzado en el brazo |
| Un objeto olvidado en la pinza bloqueaba las ejecuciones siguientes | Prueba en el APK | Al terminar cada ejecución lo que quede vuelve a su pila |
| La segunda pasada de la sala 3 era imposible | Prueba en el APK | El botón permite ejecutar varias veces |
| `itemType` vacío y repeticiones sin volver a 0 | Revisión de la telemetría del APK | El hueco de tipo se registra solo en su botón |
| La sala 3 se daba por completa con el último objeto aún en la pinza | Prueba | Se cuenta lo entregado en cada destino |
| Desde el segundo niño se perdían datos | Prueba de sesiones seguidas | Un gestor de telemetría por escena |
| El JSON no llegaba al servidor al volver a `Setup` | Prueba de cierre | La carga espera a que termine la subida |
| Quitarse el visor un momento cerraba la sesión | Prueba en el visor | La pausa solo guarda; no cierra |
| Las frases con variante (9.1, 14.2) no sonaban | Prueba de narración | Identificadores como texto |
| Tras reiniciar la sala 4, un hueco quedaba resuelto pero vacío | Prueba en el APK | El reinicio no mueve las fichas acertadas |
| Las fichas con física se salían del hueco | Prueba en el APK | Se congelan al encajar |
| Un intento de la sala 3 quedó antes de su inicio y un agarre de la sala 4 se contó en la 3 | Sesiones del 30/09 | Cada sala abre su registro al llegar |
| Servidor: errores 502 y 401 en la subida | Pruebas de subida | Puertos, nombre de host y clave en la configuración del contenedor; diagnóstico por capas desde el editor |
| Los agarres de la sala 4 no se contaban | Revisión de la telemetría | Contador propio de la sala 4 |
| Tirones al escribir la telemetría | Prueba en el visor | Escritura diferida cada 10 s |
| "Saltar paso" del Director no saltaba los pasos que esperan una sala | Prueba automatizada del 09/10 | El Director comprueba el salto en cada frame |

### Observaciones de uso (feedback del 01/10/2026 y revisión)

- **Sala 1**: no queda claro dónde colocar los bloques ni que el objetivo es abrir la puerta; mejorar
  las indicaciones visuales; fila de huecos en horizontal; panel sobre la mesa
- **Sala 2**: instrucciones ambiguas y preguntas a simplificar; los paneles son grandes y no se ve el
  estado corregido / sin corregir; el botón incorrecto debería soltarse solo; títulos y textos de
  opciones ("Sistema de motores", "Agregar combustible")
- **Sala 3**: en básica sobran las instrucciones; problemas de centrado / posición inicial del jugador
- **Sala 4**: animación de regreso Tierra → Ecuador → Yachay Tech; glifos del alfabeto Yachay en la
  intermedia
- **General**: revisar el clic y la respuesta de los botones, el contraste del texto de los botones,
  sonido en cada interacción, reducir el tiempo para comprender cada sala, demostración inicial de
  las interacciones
- **Telemetría**: contar reinicios, tiempo por selección y colocación, tiempo total, código único de
  visor, valores `true`/`false`, unificar y depurar tipos de error, no contar deselecciones

### Limitaciones técnicas encontradas

- **Rendimiento sin medir** en el visor; perfil de render de Android a revisar (ver 2.2)
- **Seguimiento de manos** habilitado pero sin validar
- **Subida al cerrar la app**: no es fiable en Quest (el sistema cierra el proceso sin esperar). La
  sesión queda guardada en el visor; lo viable es subir al arrancar las que queden pendientes
- **Dependencia de la red** para la subida; sin red, el JSON se extrae por cable
- **Código de visor** ligado a la clave de firma del APK: hay que compilar siempre con el mismo keystore
- Sin HUD diegético de estado (RI-02) ni reinicio supervisado (RF-08)

### Ajustes aplicados

| Fecha | Ajuste |
|---|---|
| 29/09/2026 | Texto de la narración completo de golpe; 4 opciones en las dos dificultades de la sala 2; repeticiones a 0 al poner la ficha de tipo; física y aviso de acierto en la sala 4 |
| 30/09/2026 | Ficha rechazada expulsada; reinicio que respeta lo acertado; nivel intermedio de la sala 1 definitivo |
| 06/10/2026 | Formato nuevo de telemetría (reinicios, tiempos por acción, tiempo total, código de visor, `true`/`false`, errores separados); botón incorrecto que se suelta solo; deselecciones fuera de la telemetría; tutorial inicial con botón INICIAR |
| 09/10/2026 | Dificultades en una sola escena; sala 3 básica sin instrucciones a la vista; "Sistema de motores" y "Agregar combustible"; herramientas de comprobación y prueba automática; corrección del salto de pasos del Director |

### Priorizado para la siguiente etapa

1. Compilar el APK nuevo y pasar en el visor el plan de prueba de la telemetría
2. Pruebas con usuarios de la población objetivo ⟨fecha prevista⟩
3. Sala 1: indicaciones de dónde colocar los bloques y del objetivo (puerta)
4. Sala 2: textos más simples y paneles más pequeños
5. Sala 3: repeticiones a 0 tras cada ejecución, quitar intentos fallidos de la telemetría y cantidad
   disponible por intento
6. Sala 4: glifos Yachay en la intermedia y animación de regreso
7. Medir rendimiento, perfil de render para Quest y validación de manos
8. Subir al arrancar los JSON pendientes; depurar los mensajes de consola de la sala 1
9. Frase 3 de la narración: ya no corresponde al flujo (habla de pulsar «Iniciar» para el tiempo)

---

## Fuentes

| Dato | Fuente |
|---|---|
| Arquitectura, decisiones y errores corregidos | `Context.md` (secciones 3–10) |
| Diseño de salas, controles y retroalimentación | `Docs/Codea2_GDD.docx` |
| Variables de telemetría | `Docs/VARIABLES_TELEMETRIA.md` |
| Sesiones reales | `Docs/JSON Samples/` |
| Plan de prueba | `Docs/Pruebas/PLAN_PRUEBA_TELEMETRIA.md` |
| Feedback | Lista del 01/10/2026 y `Docs/Pendientes_2026-10-06.pdf` |
| Cronología | Historial de `dev` en `github.com/datascienceyt/codea2` |
| Configuración Quest | `ProjectSettings/`, `Assets/XR/`, `Assets/Oculus/OculusProjectConfig.asset` |
| Pruebas automatizadas | `Logs/Codea_Comprobacion.txt`, `Logs/Codea_PruebaPlay.txt` (se regeneran con `Tools → Codea`) |
