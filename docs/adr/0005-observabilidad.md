# ADR-0005 — Observabilidad: logs de plataforma y telemetría de aplicación

> **Estado:** Aceptada · actualizada el 2026-11-01
> **Ámbito:** host HTTP `EcommerceApi` y Function `OrderProcessor`.
> **Evidencia:** [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs), [`EcommerceApi/appsettings.json`](../../EcommerceApi/appsettings.json), [`infra/main.bicep`](../../infra/main.bicep) y [`tests/EcommerceApi.Tests/ObservabilityTests.cs`](../../tests/EcommerceApi.Tests/ObservabilityTests.cs).
> **Relacionada con:** [ADR-0001](0001-monolito-modular-vs-microservicios.md).

Cuando algo falla, la pregunta útil no es qué ha pasado, sino dónde quedó el rastro
que lo cuenta. Este documento decide qué rastro guarda la API, dónde se guarda,
cuánto tiempo, a qué coste y qué se puede deducir de él cuando alguien llama
diciendo que su pedido no aparece.

## 1. Contexto

El host registra por los proveedores por defecto de ASP.NET Core (consola, Debug y
EventSource). Los niveles están definidos por el código:

- `Warning` para una `DomainException` (`EcommerceApi/ErrorHandling/DomainExceptionHandler.cs`).
- `Error` para una excepción no controlada, con la categoría
  `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`.
- `Information` para el flujo normal y el publicador de eventos local.

El contrato de errores está cerrado y probado: `400`, `404` y `500` responden
`application/problem+json`; el `500` incluye un `traceId` W3C y no expone mensaje
ni traza ([docs/api.md](../api.md)).

Antes de esta decisión, la infraestructura y el código no coincidían:
`infra/main.bicep` creaba Application Insights e inyectaba la cadena de conexión,
pero `Program.cs` no registraba el SDK y el sitio no declaraba captura de logs. El
resultado era una cadena sin consumidor y una respuesta con `traceId` que no tenía
destino garantizado.

## 2. Decisión

1. **Dos capas de persistencia.** App Service envía la consola y las peticiones
   HTTP a Log Analytics mediante las categorías de diagnóstico; `applicationLogs`
   mantiene además una copia en el sistema de ficheros para Kudu y streaming.
   Application Insights recibe peticiones, excepciones y dependencias.
2. **El SDK se registra solo si existe la cadena de conexión.** El exportador de
   Azure Monitor lanza al arrancar cuando falta, así que la ausencia de
   `APPLICATIONINSIGHTS_CONNECTION_STRING` deja la aplicación sin SDK en lugar de
   romperla.
3. **Niveles estables:** `Error` para `500`, `Warning` para violación de invariante
   de dominio, `Information` para el flujo normal.
4. **Correlación:** el `traceId` W3C de la respuesta contiene el `TraceId` de la
   operación en Application Insights. La configuración de logging añade `TraceId`
   y `SpanId` a los scopes y el logger de consola imprime scopes; el `RequestId`
   (`TraceIdentifier`) sigue disponible. La correlación se hace por el `TraceId`,
   no por el `traceId` completo de la respuesta.
5. **Sin fichero propio ni colector OTLP.** No se añade Serilog ni un *collector*
   externo; la retención la fija el *workspace* de Log Analytics.
6. **Disparador de revisión:** el primer despliegue real o la primera alerta `5xx`.

## 3. Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Serilog con sink a fichero | En App Service el sistema de ficheros es efímero; añade dependencia, rotación y un volumen que hay que montar |
| Solo consola sin captura de plataforma | El log se pierde al reciclar la instancia; el `traceId` no es accionable |
| OpenTelemetry con colector propio | Proporcionado a un laboratorio sin backend ni presupuesto |
| Confiar en la auto-instrumentación de App Service | Es implícita, no está declarada en Bicep y no se puede verificar sin desplegar |
| Registrar el SDK siempre | El exportador lanza si no hay cadena; rompería el arranque local y las pruebas |

## 4. Consecuencias

### Positivas

- La consola deja de ser un sumidero: queda retenida en Log Analytics.
- El SDK aporta peticiones, excepciones y dependencias sin código propio.
- La configuración de correlación está cubierta por prueba: el `500` lleva
  `traceId` W3C, el log de error se registra a nivel `Error` con la excepción y
  las opciones de logging incluyen `TraceId`/`SpanId` y scopes de consola.
- El arranque local y las pruebas no dependen de Azure.

### Negativas y límites

- Nueva dependencia: `Microsoft.ApplicationInsights.AspNetCore` 3.x, basado en
  OpenTelemetry.
- La exportación a Azure no se puede validar sin un recurso real: el código se
  prueba, el destino no.
- La correlación en Log Analytics no se puede validar sin un recurso real: el
  formato de consola y la consulta por `TraceId` quedan para el primer despliegue.
- La misma excepción viaja a dos destinos (log y telemetría); el volumen debe
  revisarse con el primer tráfico.
- Los logs de la Function dependen del host de Functions, fuera de este control.

**Riesgo aceptado:** la primera validación de extremo a extremo ocurre en el
primer despliegue, no antes. El contrato de respuesta y la configuración de
logging están cubiertos por pruebas locales; la correlación en Log Analytics no.

## 5. Cuándo revisar esta decisión

- Tras el primer despliegue: localizar un `500` por su `traceId` en Log Analytics
  y en `AppExceptions`, sin acceso al contenedor.
- Ante la primera alerta `5xx` o un volumen de telemetría que encarezca el
  *workspace*.
- Si se añade la Function a un plan con Application Insights propio.

**Criterio de cierre:** un `500` provocado en un entorno desplegado se localiza
por su `traceId` en Log Analytics.

## 6. Referencias

- [Referencia de la API](../api.md) — sección 6, contrato de errores.
- [`tests/EcommerceApi.Tests/ObservabilityTests.cs`](../../tests/EcommerceApi.Tests/ObservabilityTests.cs) — traza, log y configuración del SDK.
- [App Service](../app-service.md) — sección 9, observabilidad.
- [`infra/README.md`](../../infra/README.md) — estado real de la plantilla.
- [Diagnóstico de App Service](https://learn.microsoft.com/azure/app-service/troubleshoot-diagnostic-logs) — tipos de log y soporte por plataforma.
- [Categorías de Microsoft.Web/sites](https://learn.microsoft.com/azure/app-service/monitor-app-service-reference#supported-resource-logs-for-microsoftweb) — soporte Linux de `AppServiceHTTPLogs`.
- [ActivityTrackingOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.activitytrackingoptions) — `TraceId` y `SpanId` en los scopes.
- [Revisión del lote de observabilidad](../revision-observabilidad.md) — hallazgos y evidencia de la revisión.
