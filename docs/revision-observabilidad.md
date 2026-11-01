# Revisión del lote de observabilidad (2026-11-01)

> **Objeto:** commit `c09d4a2` (`feat(api): close observability and request size gaps`), 12 archivos, 289 inserciones y 65 borrados.
> **Alcance:** código de la API, pruebas, plantilla Bicep y documentación del lote.
> **Estado:** Parcial. La compilación, las pruebas y la plantilla se han ejecutado en local; no hay despliegue en Azure.

## 1. Resumen

La revisión encontró una discrepancia real entre lo que prometía el ADR y lo que hacía el código, un bloque de configuración de logs que no aplica a Linux y varias referencias desactualizadas. También descartó dos hallazgos que no se sostenían al comprobarlos.

| Hallazgo | Decisión | Estado |
|---|---|---|
| La correlación respuesta-log se declaraba probada, pero no se configuraba | Añadir `TraceId`/`SpanId` a los scopes y activar scopes en consola; probar la configuración | Corregido; validación en Azure pendiente |
| `httpLogs` en un App Service Linux | Retirar el bloque, que es de Windows; mantener `AppServiceHTTPLogs`, que sí está soportado en Linux | Corregido |
| Referencias de línea a `Program.cs` desactualizadas | Actualizar los rangos | Corregido |
| El README de la API negaba toda observabilidad persistente | Precisar que no está desplegada, pero sí declarada en plantilla y código | Corregido |
| Estados `Implementado (plantilla)/(código)` fuera de la escala de estados del repositorio | No se toca en este lote; normalizar la tabla completa es una tarea aparte | Pendiente |
| `bicep lint` no está en el workflow | Falso positivo: `bicep build` ejecuta el linter | Descartado |
| Referencia directa a `Microsoft.ApplicationInsights.AspNetCore` en pruebas | La referencia directa es correcta; la versión duplicada se resolvería con gestión centralizada | Descartado como defecto |

## 2. Correlación entre la respuesta y el log

### Qué decía la documentación

El ADR-0005 afirmaba que el `traceId` W3C de la respuesta era el identificador de la operación en Application Insights y que los logs de consola llevaban `RequestId`, que es el `TraceIdentifier` del contexto. También decía que la correlación estaba probada.

### Qué había en el código

- `Program.cs` no configuraba `ActivityTrackingOptions`.
- `appsettings.json` no activaba scopes en el logger de consola.
- El `traceId` de la respuesta lo genera `ProblemDetails` a partir de `Activity.Current.Id`, con formato W3C `00-<trace-id>-<span-id>-<flags>`.
- `RequestId` (`TraceIdentifier`) es otro identificador distinto. Sin scopes, el log de consola no lo imprimía; y aunque se imprimiera, no era el mismo valor que el `traceId` de la respuesta.

La prueba existente comprobaba el formato del `traceId` y que había un log de error, pero no que ambos compartieran el identificador de correlación.

### Corrección

- `Program.cs` configura `ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId`.
- `appsettings.json` activa `Logging:Console:FormatterOptions:IncludeScopes`.
- `ObservabilityTests` comprueba que las opciones de logging incluyen `TraceId` y `SpanId`, y que el formateador de consola incluye scopes.

El `traceId` de la respuesta contiene el `TraceId` que aparece en el log; la correlación se hace por ese valor de 32 caracteres, no por la cadena W3C completa. La prueba valida la configuración, no el formato exacto de la consola ni la consulta en Log Analytics.

### Límite

Sin un despliegue real no se puede confirmar que el formato de consola llegue a `AppServiceConsoleLogs` ni que la consulta por `TraceId` devuelva el registro. Por eso el estado es **Parcial**, no Validado.

## 3. `httpLogs` en App Service Linux

El sitio se declara con `kind: 'linux'` en `infra/main.bicep`. El bloque `httpLogs` del recurso `Microsoft.Web/sites/config` es la configuración de web server logs de Windows; no aplica a Linux.

La categoría de diagnóstico `AppServiceHTTPLogs` es otra cosa: la tabla de soporte de App Service la marca disponible también para Linux. Por eso se retira el bloque `httpLogs`, pero se mantiene la categoría `AppServiceHTTPLogs`. Los logs de la aplicación .NET en Linux llegan a `AppServiceConsoleLogs`; `AppServiceAppLogs` cubre Java/Tomcat. `applicationLogs` a sistema de ficheros se mantiene para Kudu y streaming.

## 4. Referencias de línea

`docs/api.md` y `docs/capacidades.md` citaban rangos de `Program.cs` anteriores al lote. Se actualizaron a:

- JWT: `Program.cs:133-148`.
- Rate limit: `Program.cs:119-131`.
- Swagger: `Program.cs:160-164`.
- Cabeceras: `Program.cs:178-185`.

El desfase real era de 9 a 11 líneas, no de 8 como decía una primera anotación de la revisión.

## 5. Alcance del README de la API

`EcommerceApi/README.md` decía que el proyecto no incluía «observabilidad persistente», mientras el README principal y `docs/capacidades.md` declaraban captura de logs y telemetría. La frase se precisa: no hay observabilidad persistente **desplegada**, pero la captura y la telemetría están declaradas en la plantilla y el código.

## 6. Estados

La revisión señaló que `Implementado (plantilla)` y `Implementado (código)` no pertenecen a la escala `Validado · Parcial · Runbook preparado · Diseño · Pendiente`. No se cambia en este lote porque el README define «Implementado» como código o configuración ejecutada en local o CI, y normalizar toda la tabla requiere una revisión aparte. Queda anotado como pendiente para no dejar una corrección a medias.

## 7. Falsos positivos descartados

- **`bicep lint`:** el workflow solo ejecuta `bicep build`, pero `bicep build` ejecuta el linter. La columna se aclara como `bicep build (incluye linter)`.
- **Referencia a `Microsoft.ApplicationInsights.AspNetCore` en el proyecto de pruebas:** la referencia directa es correcta porque el test usa tipos de ese paquete; el problema real, si se quiere evitar duplicar la versión, es la gestión centralizada de paquetes, no eliminar la referencia.
- **Reflexión en `RequestLimitTests`:** es una fragilidad conocida y deliberada; se acepta como límite de la prueba, no como defecto del código.

## 8. Verificación ejecutada

```bash
dotnet build EcommerceApi.sln -c Release
dotnet test EcommerceApi.sln -c Release
dotnet format EcommerceApi.sln --verify-no-changes
bicep build infra/main.bicep --outfile /tmp/opencode/main.json
git diff --check
```

Resultado: compilación correcta, 87 pruebas superadas (45 + 7 + 10 + 25), formato sin cambios, plantilla compilada con Bicep 0.48.1 y diff sin errores de espacio. `gitleaks` no se ejecutó en local; lo ejecuta el pipeline.

## 9. Lo que no se ha verificado

- El despliegue en Azure y la consulta real de un `500` por su `TraceId` en Log Analytics.
- La exportación a Application Insights y el formato exacto de la consola en App Service.
- La retención y el coste reales del workspace de Log Analytics.
- La normalización completa de los estados del repositorio.

## 10. Referencias

- [Diagnóstico de App Service](https://learn.microsoft.com/azure/app-service/troubleshoot-diagnostic-logs) — tipos de log y soporte por plataforma.
- [Categorías de Microsoft.Web/sites](https://learn.microsoft.com/azure/app-service/monitor-app-service-reference#supported-resource-logs-for-microsoftweb) — soporte Linux de `AppServiceHTTPLogs`.
- [ActivityTrackingOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.activitytrackingoptions) — `TraceId` y `SpanId` en los scopes.
- [ADR-0005](adr/0005-observabilidad.md) — decisión de observabilidad.
- [App Service](app-service.md) — sección 9, consultas de referencia.
