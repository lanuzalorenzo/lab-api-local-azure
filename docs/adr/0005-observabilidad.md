# ADR-0005 — Observabilidad: logs de plataforma y telemetría de aplicación

> **Estado:** Aceptada · actualizada el 2026-10-29
> **Ámbito:** host HTTP `EcommerceApi` y Function `OrderProcessor`.
> **Evidencia:** [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs), [`infra/main.bicep`](../../infra/main.bicep) y [`tests/EcommerceApi.Tests/ObservabilityTests.cs`](../../tests/EcommerceApi.Tests/ObservabilityTests.cs).
> **Relacionada con:** [ADR-0001](0001-monolito-modular-vs-microservicios.md).

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

1. **Dos capas de persistencia.** App Service escribe la consola y las peticiones
   HTTP en el sistema de ficheros de la instancia y el diagnóstico las envía a Log
   Analytics; Application Insights recibe además peticiones, excepciones y
   dependencias.
2. **El SDK se registra solo si existe la cadena de conexión.** El exportador de
   Azure Monitor lanza al arrancar cuando falta, así que la ausencia de
   `APPLICATIONINSIGHTS_CONNECTION_STRING` deja la aplicación sin SDK en lugar de
   romperla.
3. **Niveles estables:** `Error` para `500`, `Warning` para violación de invariante
   de dominio, `Information` para el flujo normal.
4. **Correlación:** el `traceId` W3C de la respuesta es el identificador de la
   operación en Application Insights. Los logs de consola llevan `RequestId`, que
   es el `TraceIdentifier` del contexto.
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
- La correlación está probada: el `500` lleva `traceId` W3C y se registra a nivel
  `Error` con la excepción.
- El arranque local y las pruebas no dependen de Azure.

### Negativas y límites

- Nueva dependencia: `Microsoft.ApplicationInsights.AspNetCore` 3.x, basado en
  OpenTelemetry.
- La exportación a Azure no se puede validar sin un recurso real: el código se
  prueba, el destino no.
- La misma excepción viaja a dos destinos (log y telemetría); el volumen debe
  revisarse con el primer tráfico.
- Los logs de la Function dependen del host de Functions, fuera de este control.

**Riesgo aceptado:** la primera validación de extremo a extremo ocurre en el
primer despliegue, no antes. El contrato de correlación y el registro ya están
cubiertos por pruebas locales.

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
