# ADR-0005 — Observabilidad: consola ahora, telemetría cuando haya despliegue

> **Estado:** Aceptada · 2026-10-29
> **Ámbito:** host HTTP `EcommerceApi` y Function `OrderProcessor`.
> **Evidencia:** [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs), [`infra/main.bicep`](../../infra/main.bicep) y [`tests/EcommerceApi.Tests/ErrorContractTests.cs`](../../tests/EcommerceApi.Tests/ErrorContractTests.cs).
> **Relacionada con:** [ADR-0001](0001-monolito-modular-vs-microservicios.md).

## 1. Contexto

El host registra por los proveedores por defecto de ASP.NET Core (consola, Debug y
EventSource). No hay fichero, ni SDK de Application Insights, ni exportador OTLP.
Los niveles están definidos por el código:

- `Warning` para una `DomainException` (`EcommerceApi/ErrorHandling/DomainExceptionHandler.cs:27`).
- `Error` para una excepción no controlada, con la categoría
  `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`.
- `Information` para el flujo normal y el publicador de eventos local.

El contrato de errores es uniforme desde el cierre de este lote: `400`, `404`
y `500` responden `application/problem+json` con `traceId` cuando hay cuerpo. Lo
que sigue sin resolverse es la persistencia de ese log.

La infraestructura, en cambio, sugiere algo distinto: `infra/main.bicep` crea
Application Insights, inyecta `APPLICATIONINSIGHTS_CONNECTION_STRING` y habilita
las categorías de diagnóstico de App Service. Pero `Program.cs` no llama a
`AddApplicationInsightsTelemetry()` y el `siteConfig` no declara el bloque `logs`.
El resultado es una cadena de conexión sin consumidor.

El contrato HTTP ya está cerrado y probado: un `500` devuelve
`application/problem+json` con `title`, `status` y `traceId`, sin mensaje ni traza
([docs/api.md](../api.md) y `ErrorContractTests`). Lo que no está resuelto es
dónde queda el detalle que ese `traceId` identifica.

El coste de no decidir es concreto: en un despliegue real, un `500` no se podría
investigar más allá del *log stream* del contenedor, y el `traceId` de la respuesta
no tendría un destino garantizado donde buscarlo.

## 2. Decisión

1. **Consola como único sink en esta fase.** Sin fichero, sin SDK de Application
   Insights y sin colector OTLP.
2. **Niveles estables:** `Error` para `500`, `Warning` para violación de invariante
   de dominio, `Information` para el flujo normal.
3. **`traceId` como clave de correlación** en toda respuesta `500`. Es el único
   puente entre cliente y servidor.
4. **La cadena de Application Insights se mantiene en Bicep** como preparación,
   etiquetada como no activa. No se presenta como observabilidad operativa.
5. **Disparador de revisión:** el primer despliegue real o la primera alerta `5xx`.

## 3. Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Serilog con sink a fichero | En App Service el sistema de ficheros es efímero; añade dependencia y rotación sin resolver la búsqueda |
| Registrar `AddApplicationInsightsTelemetry()` ya | Sin despliegue no hay telemetría que validar; suma configuración y una ruta de credenciales sin uso real |
| OpenTelemetry con colector propio | Proporcionado a un laboratorio sin backend ni presupuesto; el coste supera al problema |
| Confiar en la auto-instrumentación de App Service | Es implícita, no está declarada en Bicep y no se puede verificar sin desplegar |

## 4. Consecuencias

### Positivas

- Cero dependencias nuevas y un pipeline de logs que se entiende leyendo el código.
- El `500` no filtra detalles y el contrato está fijado por una prueba.
- La plantilla Bicep queda lista para activar telemetría sin rehacerla.

### Negativas y límites

- Sin retención ni búsqueda: el log vive mientras viva el proceso.
- El `traceId` no es accionable en producción: no hay dónde consultarlo.
- Bicep puede inducir a error a quien lea solo la infraestructura y deduzca
  observabilidad completa.
- Los logs de la Function dependen del host de Functions, fuera de este control.

**Riesgo aceptado:** un `500` en un entorno desplegado no sería investigable más
allá del *log stream*, y eso se declara como limitación, no se compensa con una
cadena de conexión sin usar.

## 5. Cuándo revisar esta decisión

- Cuando exista un despliegue real que haya que diagnosticar.
- Ante la primera alerta `5xx` o incidente sin traza localizable.
- Cuando se active el registro de App Service en `siteConfig`.

**Criterio de cierre:** un `500` provocado en un entorno desplegado se localiza
por su `traceId` en Log Analytics, sin acceso al contenedor.

## 6. Referencias

- [Referencia de la API](../api.md) — sección 6, contrato de errores.
- [`tests/EcommerceApi.Tests/ErrorContractTests.cs`](../../tests/EcommerceApi.Tests/ErrorContractTests.cs) — prueba del `500`.
- [App Service](../app-service.md) — sección 9, observabilidad diseñada.
- [`infra/README.md`](../../infra/README.md) — estado real de la plantilla.
