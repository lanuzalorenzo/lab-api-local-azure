# ADR-0007 — Rate limiting global por IP: umbral de laboratorio, no barrera de producción

> **Estado:** Aceptada · 2026-11-05
> **Ámbito:** middleware de rate limiting en [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs), límites operativos de [`docs/api.md`](../api.md) y tabla de capacidades.
> **Evidencia:** medición del 2026-11-05 contra la imagen del commit `b9176b0` (130 peticiones: 120 `200` y 10 `429`, recuperación tras reiniciar la ventana), registrada en el portfolio: [`2026-11-05-11-api-rate-limit`](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-05-11-api-rate-limit/README.md).
> **Relacionada con:** [ADR-0005](0005-observabilidad.md) y [ADR-0006](0006-imagen-de-contenedor.md).

Un límite de peticiones es como el aforo de una sala: fija cuántos caben y deja fuera al que insiste, sin prometer que la sala aguante un incendio. Este documento fija que el aforo es de 120 peticiones por minuto y por IP, dónde se aplica y qué NO se puede afirmar con él. Es un control de laboratorio: no sustituye a un gateway ni a un WAF.

## 1. Contexto

La API se expone sin API Management ni WAF delante. El middleware de rate limiting está registrado en `Program.cs`, pero no tenía decisión escrita: `docs/api.md` lo mencionaba como límite operativo y `RateLimitTests` comprobaba el `429` a través de `WebApplicationFactory`, que valida el comportamiento del middleware y no su efecto sobre un cliente real.

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

Medido el 2026-11-05 contra el contenedor del commit `b9176b0`, con ráfaga secuencial de un cliente:

| Hecho | Observación |
|---|---|
| Cuota exacta | 120 respuestas `200`; la primera `429` en la petición 121 |
| Reinicio de la ventana | `200` de nuevo tras 62 s |
| Cuerpo del rechazo | `429` con `Content-Length: 0`, sin `Retry-After` |
| Clave de partición | Tres `X-Forwarded-For` inventadas siguieron recibiendo `429` |

Lo anterior confirma el umbral y la clave, no su robustez. Consecuencias asumidas:

- **NAT y IP compartida.** Varios usuarios detrás de la misma salida consumen la misma cuota. Es el precio de no tener identidad antes del limiter.
- **Ventana fija en el borde.** Hasta 120 peticiones al final de un minuto y 120 al principio del siguiente. Es una propiedad de `FixedWindowRateLimiter`, no un fallo; no se midió.
- **Sin `Retry-After`.** El cliente no sabe cuándo reintentar y su reintento automático puede alargar la saturación. Es una brecha de interoperabilidad, no de protección.
- **Estado local.** Cada instancia tendría su propia cuota; escalar horizontalmente multiplica el límite efectivo.
- **Sin cuotas por usuario.** Un token válido puede consumir la cuota de todos.
- **Secuencial, no concurrente.** La medición usó un cliente; no hay latencias bajo saturación ni prueba de que 120/min sea suficiente o excesivo.
- **`/health` incluido.** Un monitor que sondea demasiado rápido también puede recibir `429`.

## 5. Criterio para revisarla

Esta ADR se revisa cuando ocurra cualquiera de estas condiciones:

1. **Hay un proxy inverso o balanceador delante.** Entonces `RemoteIpAddress` deja de ser la IP del cliente: o se configura `UseForwardedHeaders` con orígenes conocidos, o el límite pasa al gateway. Sin ese paso, la decisión deja de ser válida.
2. **Hay tokens reales de Entra ID.** Con identidad disponible antes del limiter, se evalúa cuota por usuario además de por IP.
3. **Existe una medición concurrente.** Si la carga concurrente muestra un `429` antes de lo esperado o latencias que crecen, se recalibra el umbral o se cambia a ventana deslizante/token bucket.
4. **Se despliega en más de una instancia.** El estado local deja de ser suficiente; el límite se mueve a un componente compartido.
5. **Aparece tráfico legítimo rechazado.** Se revisa el valor de 120/min y se añade `Retry-After` o un mecanismo de reintento acordado con el cliente.
6. **Los costes por endpoint divergen.** Si una operación cuesta órdenes de magnitud más que otra, se pondera por ruta en lugar de aplicar un global.

## 6. Referencias

- [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs) — `AddRateLimiter` y `UseRateLimiter`.
- [`docs/api.md`](../api.md) — límites operativos y comportamiento del `429`.
- [`docs/threat-model-appsec.md`](../threat-model-appsec.md) — API4 y denegación de servicio.
- [OWASP API Security Top 10 (2023), API4](https://owasp.org/API-Security/editions/2023/en/0xa4-unrestricted-resource-consumption/)
