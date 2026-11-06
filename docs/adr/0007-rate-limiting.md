# ADR-0007 — Rate limiting global por IP: umbral de laboratorio, no barrera de producción

> **Estado:** Aceptada · 2026-11-05
> **Ámbito:** middleware de rate limiting del paquete `LL.Security` (`AddRemoteIpRateLimiting`) configurado en [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs), límites operativos de [`docs/api.md`](../api.md) y tabla de capacidades.
> **Evidencia:** mediciones del 2026-11-05 contra una imagen construida desde el árbol de `b9176b0` (`Program.cs` sin cambios hasta `main` en `2b92c85`): sesión secuencial (130 peticiones: 120 `200` y `429` desde la 121) y sesión de carga con k6 (10 y 50 peticiones por segundo durante 50 s: 119 `200` por ventana, resto `429`, sin `5xx`). Registradas en el portfolio: [`2026-11-05-11-api-rate-limit`](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-05-11-api-rate-limit/README.md) y [`2026-11-05-11-api-rate-limit-carga`](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-05-11-api-rate-limit-carga/README.md).
> **Relacionada con:** [ADR-0005](0005-observabilidad.md) y [ADR-0006](0006-imagen-de-contenedor.md).

Un límite de peticiones es como el aforo de una sala: fija cuántos caben y deja fuera al que insiste, sin prometer que la sala aguante un incendio. Este documento fija que el aforo es de 120 peticiones por minuto y por IP, dónde se aplica y qué NO se puede afirmar con él. Es un control de laboratorio: no sustituye a un gateway ni a un WAF.

## 1. Contexto

La API se expone sin API Management ni WAF delante. El middleware de rate limiting se registra con `AddRemoteIpRateLimiting` del paquete `LL.Security` y se configura en `Program.cs`, pero no tenía decisión escrita: `docs/api.md` lo mencionaba como límite operativo y `RateLimitTests` comprobaba el `429` a través de `WebApplicationFactory`, que valida el comportamiento del middleware y no su efecto sobre un cliente real.

OWASP API4 (Unrestricted Resource Consumption) y el threat model del repositorio lo clasifican como **Parcial**: hay un límite global, no cuotas por usuario ni por endpoint, y no hay calibración. Sin embargo, «existe un limiter» y «el límite corta donde dice» son afirmaciones distintas; hasta el 2026-11-05 solo la primera estaba probada contra un proceso en ejecución.

El coste de la decisión es cero en infraestructura: el limiter vive en el proceso y no necesita servicios externos. Ese es justamente el motivo de que no deba confundirse con protección de producción: el estado se pierde al reiniciar el proceso, no se comparte entre instancias y no distingue usuarios.

## 2. Decisión

1. **Middleware global, no por endpoint.** `AddRateLimiter` define un `GlobalLimiter` y `UseRateLimiter()` se registra **antes** de autenticación y autorización. Las peticiones anónimas consumen cuota igual que las autenticadas, y `GET /health` también cuenta; los preflight CORS quedan fuera porque `UseCors` los responde antes de llegar al limiter.
2. **Partición por IP de conexión.** La clave es `context.Connection.RemoteIpAddress`, con `unknown` como respaldo. No se configura `UseForwardedHeaders`: las cabeceras del cliente no crean particiones nuevas.
3. **Ventana fija de un minuto, 120 peticiones, sin cola.** `FixedWindowRateLimiterOptions` con `PermitLimit = 120`, `Window = 1 min` y `QueueLimit = 0`. La petición 121 se rechaza en el acto; no espera turno.
4. **Rechazo con `429` y cuerpo vacío.** `RejectionStatusCode = 429`. No se añade `OnRejected` ni `Retry-After`: la respuesta sale con `Content-Length: 0` y las cabeceras de seguridad globales.
5. **Etiqueta de laboratorio explícita.** El código y la documentación declaran que el límite no sustituye a un gateway o un WAF. Un valor por defecto de 120/min no se presenta como calibrado.

## 3. Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Token bucket o ventana deslizante | Reparte mejor las ráfagas, pero exige más estado y afinar parámetros; en un laboratorio sin carga real el beneficio no es medible. |
| Cuota por usuario o por endpoint | Necesita identidad resuelta antes del middleware y un mapa de costes por ruta que hoy no existe; daría una apariencia de control más fina sin datos que la sostengan. |
| Cola mayor que cero | Convierte el exceso en latencia: el cliente cree que va bien mientras el sistema acumula trabajo. Ocultar saturación no es protegerla. |
| APIM o WAF desde el primer día | Coste y dependencia de despliegue para un laboratorio que corre en un portátil; entrará cuando exista un entorno desplegado. |
| Límites ponderados por coste de endpoint | Requiere medir el coste de cada operación; hoy 120/min es una cifra única para todas. |

## 4. Consecuencias

Medido el 2026-11-05 contra el contenedor del commit `b9176b0`, con una ráfaga secuencial y dos pruebas de carga con k6 sobre `GET /health` (un cliente, 50 s por escenario):

| Hecho | Observación |
|---|---|
| Cuota exacta | 120 respuestas `200`; la primera `429` en la petición 121 |
| Reinicio de la ventana | `200` de nuevo tras 62 s |
| Carga a 10 peticiones/s | 119 `200` y 382 `429` en 50 s; p95 de `200` 1,58 ms |
| Carga a 50 peticiones/s | 119 `200` y 2 381 `429` en 50 s; p95 de `200` 1,90 ms |
| Coste del rechazo | p95 del `429` en torno a 1,1 ms, sin cola |
| Cuerpo del rechazo | `429` con `Content-Length: 0`, sin `Retry-After` |
| Clave de partición | Tres `X-Forwarded-For` inventadas siguieron recibiendo `429` |

En las pruebas de carga, 119 = 120 menos la petición de salud que abre la ventana; los totales fueron 501 y 2 500 peticiones, sin estados distintos de `200` o `429`.

Lo anterior confirma el umbral y la clave, no su robustez. Consecuencias asumidas:

- **NAT y IP compartida.** Varios usuarios detrás de la misma salida consumen la misma cuota. Es el precio de no tener identidad antes del limiter.
- **Ventana fija en el borde.** Hasta 120 peticiones al final de un minuto y 120 al principio del siguiente. Es una propiedad de `FixedWindowRateLimiter`, no un fallo; no se midió.
- **Sin `Retry-After`.** El cliente no sabe cuándo reintentar y su reintento automático puede alargar la saturación. Es una brecha de interoperabilidad, no de protección.
- **Estado local.** Cada instancia tendría su propia cuota; escalar horizontalmente multiplica el límite efectivo.
- **Sin cuotas por usuario.** Un token válido puede consumir la cuota de todos.
- **Umbral no calibrado.** La carga se midió a 10 y 50 peticiones por segundo durante 50 s: el número de aceptadas no cambió, pero no hay un SLO de latencia ni un coste por endpoint que diga si 120/min es la cifra adecuada.
- **`/health` incluido.** Un monitor que sondea demasiado rápido también puede recibir `429`.

## 5. Criterio para revisarla

Esta ADR se revisa cuando ocurra cualquiera de estas condiciones:

1. **Hay un proxy inverso o balanceador delante.** Entonces `RemoteIpAddress` deja de ser la IP del cliente: o se configura `UseForwardedHeaders` con orígenes conocidos, o el límite pasa al gateway. Sin ese paso, la decisión deja de ser válida.
2. **Hay tokens reales de Entra ID.** Con identidad disponible antes del limiter, se evalúa cuota por usuario además de por IP.
3. **Aparece un SLO de latencia o cambia el coste por endpoint.** La carga concurrente ya está medida; si el SLO o el coste muestran que 120/min no es adecuado, se recalibra el umbral o se cambia a ventana deslizante/token bucket.
4. **Se despliega en más de una instancia.** El estado local deja de ser suficiente; el límite se mueve a un componente compartido.
5. **Aparece tráfico legítimo rechazado.** Se revisa el valor de 120/min y se añade `Retry-After` o un mecanismo de reintento acordado con el cliente.
6. **Los costes por endpoint divergen.** Si una operación cuesta órdenes de magnitud más que otra, se pondera por ruta en lugar de aplicar un global.

## 6. Referencias

- [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs) — `AddRemoteIpRateLimiting` y `UseRateLimiter`.
- [`docs/api.md`](../api.md) — límites operativos y comportamiento del `429`.
- [`docs/threat-model-appsec.md`](../threat-model-appsec.md) — API4 y denegación de servicio.
- [OWASP API Security Top 10 (2023), API4](https://owasp.org/API-Security/editions/2023/en/0xa4-unrestricted-resource-consumption/)
