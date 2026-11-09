# Cambiar el servidor de datos de Codea 2

Guía para el equipo de TI de Yachay Tech. Hoy el visor envía los datos a un servidor
provisional, `https://codea2.penginexr.com/upload`. Cuando el servidor de Yachay (laptop con
Docker y el repositorio **Codea2Server**) tenga dirección pública, la aplicación se cambia así.

## Lo que necesita la aplicación del servidor

- `POST /upload`: multipart con el campo `file` y la cabecera `X-API-Key`.
  Responde **200** si guarda el archivo, **400** si no es válido y **401** si la clave es mala.
- `GET /health`: responde `{"status":"alive"}`. Lo usa la aplicación para comprobar la conexión.
- La clave es la `UPLOAD_API_KEY` del servidor y ya está puesta en la aplicación. **El servidor
  nuevo tiene que usar la misma clave**; si cambia, hay que cambiarla también en el `JSONUploader`
  de la escena `Juego` y pasar `Tools → Codea → Poner al día Setup y Juego`, que la copia a `Setup`.

La dirección será `https://DOMINIO/upload` o, si no hay dominio, `http://IP-PÚBLICA:PUERTO/upload`.

## Pasos

1. **Probar la dirección sin tocar nada del proyecto**, con Unity cerrado, desde la carpeta del
   repositorio:

   ```
   "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod CodeaServerCheck.RunBatch -servidor https://DOMINIO/upload
   ```

   El resultado queda en `Logs/Codea_PruebaServidor.txt`: hace el ping a `/health` y sube un
   archivo de prueba, `9999_0_….json`, que puede borrarse del servidor después.
2. Abrir el proyecto en Unity 6000.4.1f1 y pasar `Tools → Codea → Servidor de datos`. Selecciona
   `Assets/_Main/Resources/ServidorDatos.asset`, el **único** sitio donde está la dirección. Poner
   la nueva en `Upload Url`. La dirección del ping (`/health`) se calcula sola.
3. **Solo si la dirección empieza por `http://`:** Project Settings → Player → Android → Other
   Settings → **Allow downloads over HTTP = Always allowed**. El inspector del asset lo avisa y
   tiene un botón que lo pone. Sin eso el visor no envía nada, y la compilación para Android se
   detiene a propósito para que no pase desapercibido. Con `http://` la clave y los datos viajan
   sin cifrar: se recomienda `https://`.
4. `Tools → Codea → Probar el servidor (Ping y JSON de prueba)`: tiene que terminar sin fallos.
5. Compilar el APK (File → Build Profiles → Android) **con la misma clave de firma que la versión
   instalada** e instalarlo en los visores.
6. Prueba en el visor: abrir `Setup` con wifi y jugar una partida corta. `Setup` debe mostrar
   «todo subido» y el archivo debe aparecer en el panel de Codea2Server.

## Antes de cambiar

- **Enviar los datos pendientes de cada visor** con el botón **Subir datos** de `Setup`. Lo
  pendiente se enviará al servidor nuevo, pero si hay que desinstalar la aplicación (por
  ejemplo, porque cambia la clave de firma) los datos no enviados se pierden.
- Lo que ya llegó al servidor provisional **no se vuelve a subir** al nuevo: esas sesiones se
  migran con el ZIP que descarga el panel del servidor.

## Si falla

`JSONUploader` → **Ping al servidor** (en Play) y el log de cada subida fallida dicen en qué capa
está el problema:

| Mensaje | Dónde mirar |
|---|---|
| Sin red | Wifi del visor |
| Cannot resolve destination host | DNS: dirección mal escrita o dominio sin dar de alta |
| Cannot connect to destination host | Nadie atiende en ese puerto: Docker parado, puerto mal publicado o redirección a otra máquina |
| Request timeout | Laptop apagada o dormida, o el firewall de Yachay cierra el puerto |
| Unable to complete SSL connection | Certificado caducado, autofirmado o de otro dominio |
| Insecure connection not allowed | Falta el paso 3 |
| 401 en la subida | La clave no coincide con la `UPLOAD_API_KEY` del servidor |
| 404 | La dirección no apunta a Codea2Server o el proxy la reenvía a otra ruta |
| 500 | Error de la aplicación: `docker logs` en la laptop |
| 502–504 | El proxy de la universidad no alcanza la laptop o el contenedor |

Con 502–504 los datos quedan pendientes en el visor y `Setup` reintenta cada 20 s.
