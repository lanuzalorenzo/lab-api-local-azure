# Capacidades y evidencia

> **Estado:** Parcial (2026-11-07): la matriz conserva etiquetas fuera del contrato (`Implementado`, `Documentado, no activo` y variantes de alcance como `Validado en local`) y aún no está normalizada. Verificado el 2026-10-29 contra `main`; rate limit revisado el 2026-11-05 con [ADR-0007](adr/0007-rate-limiting.md); controles compartidos desde los paquetes `LL.Security` y `LL.Hosting` el 2026-11-06, con la versión fijada en `0.1.0` desde el 2026-11-09. Queda por comprobar el `413` del límite de 16 KB, la validación JWT fuera de CI, el hook de secretos opt-in, el despliegue de Service Bus y las credenciales del feed privado ([`credenciales.md`](credenciales.md), en **Parcial**).
> **Ámbito:** qué puede afirmarse sobre este repositorio, con qué se comprueba y qué queda fuera.
> **Evidencia:** `dotnet test`, `.github/workflows/devsecops-ci.yml`, `infra/main.bicep` y `docs/`.
> **Relacionada con:** [Referencia de la API](api.md), [resumen de estado](../README.md#estado-de-seguridad), [ADR](adr/README.md).

Cualquier repositorio puede decir que «hace seguridad». Este documento hace la
pregunta incómoda: de todo lo que este repositorio afirma, qué se puede comprobar
hoy, con qué prueba y qué no está demostrado. Si alguien revisa el repositorio o
pregunta en una entrevista, ¿qué afirmaciones sostiene el código y cuáles no?

Cada fila de la matriz apunta a algo ejecutable; cuando no lo hay, el estado lo
reconoce y no se presenta como validado.

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
| Contrato de error único (`400`, `404`, `500`) | `LL.Hosting` (`AddApiProblemDetails`), controladores y `DomainExceptionHandler` | `ErrorContractTests` (4 casos) | Implementado |
| CORS con denegación por defecto | `LL.Security` (`AddDenyByDefaultCors`) | `AllowedOrigin_ReceivesCorsHeader`, `UnknownOrigin_DoesNotReceiveCorsHeader` | Implementado |
| Rate limit global de 120 req/min por IP | `LL.Security` (`AddRemoteIpRateLimiting`; [ADR-0007](adr/0007-rate-limiting.md)) | `ExceedingGlobalLimit_ReturnsTooManyRequests`; carga con k6 en el portfolio | Validado en local (secuencial y concurrente); sin SLO |
| Salud anónima en `/health` | `LL.Hosting` (`MapApiHealthChecks`) | `Health_IsAnonymous_ReturnsHealthy` | Implementado |
| Controles compartidos instalados con versión fijada `0.1.0` | [`Directory.Packages.props`](../Directory.Packages.props), [`NuGet.config`](../NuGet.config) | `dotnet restore` desde GitHub Packages en CI | Implementado |
| Credencial de solo lectura para el feed privado `LL.*` | Gestor de secretos local y secreto `LL_PACKAGES_TOKEN` de Dependabot | [`credenciales.md`](credenciales.md); runs [42409729475](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42409729475) y [42410124439](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42410124439) | Parcial: falta PR real de `LL.*` |
| Límite de cuerpo de 16 KB en escrituras | `[RequestSizeLimit]` | Pendiente de comprobación manual: `TestServer` no aplica el límite de Kestrel | Implementado; `413` sin comprobar |

El contrato completo, incluido el formato de error, está en
[docs/api.md](api.md).

### 2.2. Seguridad

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Validación JWT: firma, `iss`, `aud`, `exp`, `nbf` y *clock skew* | `LL.Security` (`AddHardenedJwtBearer`) | Requiere tenant real; ver [azure-ad-tests.md](azure-ad-tests.md) | Implementado; fuera de CI |
| Autorización por propietario (BOLA/IDOR) | `OrdersController`, `GetOrderByIdHandler` | `UserCannotReadAnotherUsersOrder` y `CreateOrder_ReturnsOnlyOwnOrders` | Implementado |
| `404` en lugar de `403` para pedidos ajenos | `OrdersController.GetOrderById` | La misma prueba | Implementado |
| DTOs de entrada contra *mass assignment* | `Models/Create*Request.cs` | Revisión de código; `Id` no es asignable | Implementado |
| Sin cabecera `Server` (huella del servidor web) | `EcommerceApi/Program.cs` (`AddServerHeader = false`) | `ServerHeaderTests` fija la opción; job `container-security`: `scripts/check-server-header.sh` contra el contenedor | Parcial: rojo y verde contra el contenedor local el 2026-11-09; falta el run de CI |
| Escaneo de secretos sobre historial completo | Acción `gitleaks` de [`core`](https://github.com/lanuzalorenzo/core), en el job `secret-scanning` de `.github/workflows/devsecops-ci.yml` | Ejecución en GitHub Actions | Implementado |
| Aviso de secretos antes del commit (hook local) | `scripts/hooks/pre-commit` | `scripts/install-git-hooks.sh --check` (instalación y versión) y `gitleaks git --staged` sobre un cambio preparado | Parcial: opt-in por clon, no lo impone la plataforma |
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
| Captura persistente de logs | `Microsoft.Web/sites/config` (`logs`) + diagnóstico a Log Analytics | `bicep build` (incluye linter); ver [ADR-0005](adr/0005-observabilidad.md) | Implementado (plantilla) |
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
| Alta disponibilidad, SLA o tráfico elevado | SQLite, una instancia y solo una prueba puntual de carga, no tráfico elevado |
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

- Esta matriz es la fuente de verdad del estado; el [resumen del README](../README.md#estado-de-seguridad) no la sustituye.
- Si una capacidad cambia de estado, actualiza la matriz y el resumen del README en el mismo lote.
- No añadas una fila sin una comprobación asociada. Si no existe, escribe `Diseño` o `Pendiente` y explica qué falta.
