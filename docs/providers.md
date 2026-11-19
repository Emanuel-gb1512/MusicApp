# Proveedores de música: qué permite cada uno

Verificado el 19 de noviembre de 2026 contra la documentación pública de cada servicio.
Estas condiciones cambian: vuelve a revisarlas antes de implementar cada proveedor.

## Regla general de TONYX

Solo se implementa lo que cada servicio permite oficialmente. No se extrae audio, no se salta DRM,
no se hace scraping prohibido y no se descargan canciones protegidas. Las capacidades de cada servicio
viven en la tabla `ConnectedProviders` (script 003).

## Resumen

| Proveedor | Buscar | Reproducir | Descargar | Necesita |
|---|---|---|---|---|
| Jamendo | Si | Si (MP3) | Solo pistas con permiso | client_id gratuito (devportal.jamendo.com) |
| Internet Archive | Si | Si | Segun licencia de cada item | Nada |
| Spotify | Si (con limites) | Solo con sus SDK oficiales (Premium) | No | Cuenta Premium |
| YouTube | Si | Solo el reproductor oficial, en primer plano | No | Clave de API de Google |
| YouTube Music | No (sin API publica) | No | No | Solo "abrir en la app oficial" |

## Spotify

- Una app en modo desarrollo admite 5 usuarios autorizados; los demas reciben 403.
- El dueno de la app necesita Spotify Premium; si caduca, la app deja de funcionar.
- Un solo Client ID por desarrollador en apps nuevas.
- El modo de cuota extendida exige empresa registrada y 250.000 usuarios activos mensuales.
- Algunos endpoints se eliminaron o limitaron en 2024-2026 (recomendaciones, analisis de audio...).
- Las busquedas parecen limitadas a 10 resultados desde febrero de 2026 (fuente secundaria: confirmar en la documentacion oficial).
- Documentacion: https://developer.spotify.com/documentation/web-api

## YouTube

- API gratuita, cuota por defecto de 10.000 unidades al dia; cada busqueda cuesta 100 unidades.
- Politicas: prohibido reproducir en segundo plano, descargar videos para verlos sin conexion
  (fuera de YouTube Premium) y separar o aislar el audio.
- Solo se puede usar el reproductor oficial incrustado, con el video visible.
- Documentacion: https://developers.google.com/youtube/terms/developer-policies-guide

## YouTube Music

- No existe una API publica oficial. TONYX solo ofrece "abrir en la app oficial" (enlaces).

## Jamendo

- Requiere un client_id (devportal.jamendo.com). Revisa sus terminos de uso.
- Cada pista indica si su artista permite la descarga (`audiodownload_allowed`).
- Cada pista trae la URL de su licencia Creative Commons; varias son de uso no comercial.
- El unico formato de descarga es MP3 a 192 kbps.
- Documentacion: https://developer.jamendo.com/v3.0

## Internet Archive

- Busqueda (advancedsearch) y metadatos publicos, sin clave ni cuenta.
- Cada item puede traer la URL de su licencia (`licenseurl`).
- Una busqueda devuelve como maximo unos 10.000 resultados.
- Que se pueda descargar depende de la licencia de cada item.