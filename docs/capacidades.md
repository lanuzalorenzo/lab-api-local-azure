# Capacidades y evidencia

> **Estado:** Implementado · verificado el 2026-10-29 contra `main`.
> **Ámbito:** qué puede afirmarse sobre este repositorio, con qué se comprueba y qué queda fuera.
> **Evidencia:** `dotnet test`, `.github/workflows/devsecops-ci.yml`, `infra/main.bicep` y `docs/`.
> **Relacionada con:** [Referencia de la API](api.md), [Estado de seguridad](../README.md#estado-de-seguridad), [ADR](adr/README.md).

Este documento responde a una pregunta concreta: si alguien revisa el repositorio
o pregunta en una entrevista, ¿qué afirmaciones sostiene el código y cuáles no?
Cada fila de la matriz tiene una comprobación ejecutable; cuando no la tiene, se
indica en el estado y no se presenta como validado.

## 1. Verificación en un bloque

```bash
dotnet restore EcommerceApi.sln
dotnet build EcommerceApi.sln -c Release --no-restore
dotnet test EcommerceApi.sln -c Release --no-build --no-restore
dotnet format EcommerceApi.sln --verify-no-changes --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

Resultado observado el 2026-10-29:

| Proyecto de pruebas | Casos | Cobertura de qué |
|---|---|---|
| `EcommerceApi.UnitTests` | 45 | Invariantes de dominio y handlers de aplicación |
| `EcommerceApi.Architecture.Tests` | 7 | Regla de dependencias entre capas |
| `EcommerceApi.Infrastructure.Tests` | 10 | SQLite, outbox, rollback, consultas y dispatcher |
| `EcommerceApi.Tests` | 25 | Contrato HTTP, BOLA, CORS, *rate limit*, errores y observabilidad |
| **Total** | **87** | Sin umbral de cobertura configurado |

Los recuentos cambian con el código: si actualizas la matriz, ejecuta la
verificación y ajusta la tabla en el mismo lote.

## 2. Matriz de capacidades

### 2.1. Plataforma y contrato HTTP

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| .NET 10 en API, dominio, aplicación, infraestructura y Function | `*.csproj` (`net10.0`) | `dotnet build EcommerceApi.sln -c Release` | Implementado |
| ASP.NET Core Web API con controladores y *host* mínimo | `EcommerceApi/Program.cs` | Swagger en `Development` o `curl` a `/api/products` | Implementado |
| Contrato REST con `201` + `Location`, `404` y validación por DTO | `Controllers/`, `Models/` | `tests/EcommerceApi.Tests` | Implementado |
| Paginación por cursor (*keyset*) con límites y filtro temporal | `OrderQuery`, `SqliteOrderReadModel` | `GetOrders_WithLimit_ReturnsCursorAndNextPage` | Implementado |
| Contrato de error único (`400`, `404`, `500`) | `ProblemDetails`, controladores y `DomainExceptionHandler` | `ErrorContractTests` (4 casos) | Implementado |
| CORS con denegación por defecto | `Program.cs` | `AllowedOrigin_ReceivesCorsHeader`, `UnknownOrigin_DoesNotReceiveCorsHeader` | Implementado |
| Rate limit global de 120 req/min por IP | `Program.cs` | `ExceedingGlobalLimit_ReturnsTooManyRequests` | Implementado (laboratorio) |
| Límite de cuerpo de 16 KB en escrituras | `[RequestSizeLimit]` | Manual: `TestServer` no aplica el límite de Kestrel | Implementado |

El contrato completo, incluido el formato de error, está en
[docs/api.md](api.md).

### 2.2. Seguridad

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Validación JWT: firma, `iss`, `aud`, `exp`, `nbf` y *clock skew* | `Program.cs:114-129` | Requiere tenant real; ver [azure-ad-tests.md](azure-ad-tests.md) | Implementado; fuera de CI |
| Autorización por propietario (BOLA/IDOR) | `OrdersController`, `GetOrderByIdHandler` | `UserCannotReadAnotherUsersOrder` y `CreateOrder_ReturnsOnlyOwnOrders` | Implementado |
| `404` en lugar de `403` para pedidos ajenos | `OrdersController.GetOrderById` | La misma prueba | Implementado |
| DTOs de entrada contra *mass assignment* | `Models/Create*Request.cs` | Revisión de código; `Id` no es asignable | Implementado |
| Escaneo de secretos sobre historial completo | `.github/workflows/devsecops-ci.yml` (`gitleaks`) | Ejecutar el comando de la sección 1 | Implementado |
| SAST con CodeQL (`security-extended`) | Workflow, job `sast` | Ejecución en GitHub Actions | Implementado |
| SCA con Trivy, bloqueante en `CRITICAL`/`HIGH` | Workflow, job `sca` | Ejecución en GitHub Actions | Implementado |
| Escaneo de imagen y verificación de usuario no-root | Workflow, job `container-security` | `docker run --entrypoint id ... -u` distinto de `0` | Implementado |
| Threat model STRIDE y OWASP API Top 10 | [threat-model-appsec.md](threat-model-appsec.md) | Revisión del documento | Implementado (documento) |

La validación JWT no se ejecuta en CI: `tests/EcommerceApi.Tests/ApiFactory.cs`
sustituye el esquema por `TestAuthHandler`. Lo que las pruebas demuestran es la
autorización por usuario, no la criptografía de `JwtBearer`. Ese matiz es
importante y está en el threat model como `Parcial` en *Information Disclosure*.

### 2.3. Arquitectura

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Arquitectura por capas con dependencias en una sola dirección | `Domain` ← `Application` ← `Infrastructure` ← host | `LayerDependencyTests` (7 casos) | Implementado |
| Inversión de dependencias con puertos y adaptadores | Interfaces en `Application/Abstractions`, adaptadores en `Infrastructure` | Pruebas de arquitectura y de infraestructura | Implementado |
| DDD táctico: agregados, *value objects*, invariantes y evento de dominio | `Domain/Orders`, `Domain/Products`, `Money` | `OrderTests`, `ProductTests`, `MoneyTests` | Implementado |
| CQRS ligero solo en pedidos | `Application/Orders`, [ADR-0003](adr/0003-cqrs-ligero-en-pedidos.md) | `CreateOrderHandlerTests`, `OrderQueryHandlerTests` | Implementado |
| Monolito modular con criterios de extracción escritos | [ADR-0001](adr/0001-monolito-modular-vs-microservicios.md) | Revisión del documento | Implementado (decisión) |

### 2.4. Datos y mensajería

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Persistencia transaccional sobre SQLite | `SqliteUnitOfWork`, `SqliteSession` | `EcommerceApi.Infrastructure.Tests` | Implementado (laboratorio) |
| Importes en céntimos enteros | `Money`, `MoneyCents` | `MoneyTests`, pruebas de infraestructura | Implementado |
| Outbox transaccional: pedido y mensaje en la misma transacción | `SqliteOrderRepository`, `SqliteOutboxStore` | `SqliteOutboxTests` (rollback incluido) | Implementado |
| Reintentos y *dead-letter* tras 5 fallos | `OutboxPublisher` | `SqliteOutboxTests` | Implementado |
| Contrato versionado `OrderCreatedV1` | `EcommerceApi.Contracts` | Compilación compartida por API y Function | Implementado |
| Consumidor idempotente por `messageId` | `ProcessOrderHandler`, `ProcessedMessages` | `ProcessOrderHandlerTests` | Implementado (local) |
| Publicación en Service Bus | `ServiceBusEventPublisher` | Compila; requiere namespace real | Diseño, no desplegado |
| Function de Service Bus | `functions/OrderProcessor` | `dotnet build`; sin despliegue | Diseño, no desplegado |

### 2.5. Entrega y operación

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Suite xUnit en cuatro proyectos | `tests/` | `dotnet test EcommerceApi.sln` | Implementado |
| Fakes propios en lugar de framework de *mocking* | `tests/EcommerceApi.UnitTests/Fakes/InMemoryFakes.cs` | Revisión de código | Implementado |
| Verificación de formato en CI | Workflow, job `build-and-test` | `dotnet format --verify-no-changes` | Implementado |
| Pipeline con 7 jobs: build, secretos, SAST, SCA, contenedor, IaC y revisión de dependencias | `.github/workflows/devsecops-ci.yml` | Ejecución en Actions | Implementado |
| Imagen multi-stage con usuario no privilegiado | `Dockerfile` (`USER $APP_UID`) | Job `container-security` | Implementado |
| IaC Bicep con VNet, NSG, Key Vault, Private Endpoints y diagnóstico | `infra/main.bicep` | `bicep build` + Trivy config | Implementado (plantilla) |
| Despliegue en Azure | — | — | Pendiente |
| OIDC de GitHub Actions a Azure | [infra/README.md](../infra/README.md) | — | Documentado, no activo |
| Captura persistente de logs | `Microsoft.Web/sites/config` (`logs`) + diagnóstico a Log Analytics | `bicep build` + `bicep lint`; ver [ADR-0005](adr/0005-observabilidad.md) | Implementado (plantilla) |
| Telemetría de aplicación | `AddApplicationInsightsTelemetry()` condicionado a la cadena | `ObservabilityTests` | Implementado (código) |

## 3. Lo que este repositorio no demuestra

Lista deliberada. Sirve para no arrastrar afirmaciones que el código no sostiene.

| Afirmación | Por qué no |
|---|---|
| Microservicios | Es un monolito modular con un segundo desplegable asíncrono; [ADR-0001](adr/0001-monolito-modular-vs-microservicios.md) rechaza partir por defecto |
| Entity Framework Core | La persistencia usa `Microsoft.Data.Sqlite` con SQL explícito |
| Moq | No hay referencias a Moq; se usan fakes escritos a mano |
| SonarQube | El análisis estático es CodeQL, no SonarQube |
| Feature Flags | No existe infraestructura de *feature flags* en el código |
| Event Hubs | El único servicio de mensajería previsto es Service Bus |
| PKCE implementado | El flujo lo ejecuta el cliente; aquí solo se documenta y se valida el token resultante |
| Azure desplegado | `infra/main.bicep` se compila y se escanea, pero no se ha aplicado a una suscripción |
| OIDC de CI a Azure activo | Hay un workflow de CI, no de despliegue |
| Alta disponibilidad, SLA o tráfico elevado | SQLite, una instancia y ninguna prueba de carga |
| Cobertura de pruebas medida | `coverlet` está disponible, pero no hay umbral ni informe en CI |
| Validación de dominio ejercitada por HTTP | `DataAnnotations` y la deduplicación del caso de uso la cubren antes; `DomainExceptionHandler` actúa como red de seguridad y no tiene prueba de integración |

## 4. Cómo formular una afirmación defendible

| En lugar de | Escribe |
|---|---|
| «Implementé microservicios con Service Bus» | «Implementé un monolito modular con outbox transaccional sobre SQLite, reintentos y *dead-letter*, y un consumidor idempotente; el adaptador de Service Bus está escrito y no desplegado» |
| «Arquitectura en Azure» | «Diseñé y validé estáticamente una plantilla Bicep con red privada, Private Endpoints, Key Vault y diagnóstico; no la he desplegado» |
| «Clean Architecture» | «Arquitectura por capas con inversión de dependencias, verificada con pruebas que fallan si se rompe la dirección» |
| «Alta cobertura de tests» | «87 pruebas en cuatro proyectos: dominio, aplicación, arquitectura, infraestructura e integración HTTP; sin umbral de cobertura» |
| «API segura» | «Validación JWT con `JwtBearer`, autorización por propietario con `404` anti-enumeración, CORS denegado por defecto y *rate limiting* de laboratorio» |

La forma general: **verbo + artefacto + límite**. Si falta el límite, la frase
suena a folleto y no se sostiene en una pregunta de seguimiento.

## 5. Preguntas que este repositorio permite responder

1. **¿Por qué paginación por cursor y no `offset`?** Porque el cursor es el `id`
   del último elemento: insertar pedidos no desplaza páginas ya leídas y la
   consulta usa el índice. Ver [persistence.md](persistence.md).
2. **¿Por qué `404` y no `403` cuando el pedido es de otro usuario?** Para no
   confirmar que el recurso existe; `403` facilitaría la enumeración de objetos.
3. **¿Por qué outbox y no publicar dentro del handler?** Porque el pedido y el
   mensaje deben confirmarse en la misma transacción; si el *broker* falla, el
   mensaje queda pendiente en lugar de perderse.
4. **¿Por qué importes en céntimos?** Para evitar deriva de coma flotante en
   totales y comparaciones; `Money` redondea una sola vez al construir.
5. **¿Por qué CQRS solo en pedidos?** Porque el catálogo es CRUD y separar
   modelos ahí solo añade mapeo. Ver [ADR-0003](adr/0003-cqrs-ligero-en-pedidos.md).
6. **¿Cómo evitas que las capas se mezclen?** Con pruebas de arquitectura que
   inspeccionan referencias entre ensamblados, no con revisión manual.
7. **¿Qué no llevarías a producción tal cual?** SQLite sin *backups*, *rate
   limit* por IP detrás de un proxy, telemetría sin validar en un despliegue real
   y una sola instancia.

## 6. Mantenimiento

- La fuente de verdad del estado es la tabla [Estado de seguridad](../README.md#estado-de-seguridad);
  esta matriz la desarrolla, no la sustituye.
- Si una capacidad cambia de estado, actualiza las dos tablas y la fecha de este
  documento en el mismo lote.
- No añadas una fila sin una comprobación asociada. Si no existe, escribe
  `Diseño` o `Pendiente` y explica qué falta.
