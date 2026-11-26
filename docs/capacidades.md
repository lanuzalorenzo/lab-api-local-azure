# Capacidades y evidencia

> **Estado:** Parcial (2026-11-26). Matriz normalizada a los cinco estados del workspace, con el alcance entre paréntesis cuando importa (`CI`, `local`, `documento`). Queda por comprobar la validación JWT fuera de CI, el hook de secretos opt-in, el despliegue de Service Bus y la capa de pago —red privada, Key Vault y observabilidad—, que sigue en Diseño. El despliegue de la red y de la API en la capa gratuita se registra en la evidencia de la Fase 1.
> **Ámbito:** qué puede afirmarse sobre este repositorio, con qué se comprueba y qué queda fuera.
> **Evidencia:** `dotnet test`, `.github/workflows/devsecops-ci.yml`, `infra/main.bicep`, `scripts/check-infra-names.py` y `docs/`.
> **Relacionada con:** [Referencia de la API](api.md), [resumen de estado](../README.md#estado-de-seguridad), [capas gratuitas y de pago](plataforma-gratis-y-pago.md), [ADR](adr/README.md).

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
python3 scripts/check-infra-names.py infra
gitleaks detect --source . --redact --verbose --exit-code 2
```

Resultado observado el 2026-10-29, recuentos al 2026-11-12:

| Proyecto de pruebas | Casos | Cobertura de qué |
|---|---|---|
| `EcommerceApi.UnitTests` | 45 | Invariantes de dominio y handlers de aplicación |
| `EcommerceApi.Architecture.Tests` | 7 | Regla de dependencias entre capas |
| `EcommerceApi.Infrastructure.Tests` | 10 | SQLite, outbox, rollback, consultas y dispatcher |
| `EcommerceApi.Tests` | 29 | Contrato HTTP, BOLA, CORS, *rate limit*, errores y observabilidad |
| **Total** | **91** | Sin umbral de cobertura configurado |

Los recuentos cambian con el código: si actualizas la matriz, ejecuta la
verificación y ajusta la tabla en el mismo lote.

## 2. Matriz de capacidades

### 2.1. Plataforma y contrato HTTP

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| .NET 10 en API, dominio, aplicación, infraestructura y Function | `*.csproj` (`net10.0`) | `dotnet build EcommerceApi.sln -c Release` | Validado (CI) |
| ASP.NET Core Web API con controladores y *host* mínimo | `EcommerceApi/Program.cs` | Swagger en `Development` o `curl` a `/api/products` | Validado (CI) |
| Contrato REST con `201` + `Location`, `404` y validación por DTO | `Controllers/`, `Models/` | `tests/EcommerceApi.Tests` | Validado (CI) |
| Paginación por cursor (*keyset*) con límites y filtro temporal | `OrderQuery`, `SqliteOrderReadModel` | `GetOrders_WithLimit_ReturnsCursorAndNextPage` | Validado (CI) |
| Contrato de error único (`400`, `404`, `500`) | `LL.Hosting` (`AddApiProblemDetails`), controladores y `DomainExceptionHandler` | `ErrorContractTests` (4 casos) | Validado (CI) |
| CORS con denegación por defecto | `LL.Security` (`AddDenyByDefaultCors`) | `AllowedOrigin_ReceivesCorsHeader`, `UnknownOrigin_DoesNotReceiveCorsHeader` | Validado (CI) |
| Cabeceras de seguridad y política de contenido restrictiva | `LL.Security` (`UseSecurityHeaders` con `ContentSecurityPolicy.DenyAll`) | `SecurityHeadersTests` | Validado (CI) |
| Rate limit global de 120 req/min por IP | `LL.Security` (`AddRemoteIpRateLimiting`; [ADR-0007](adr/0007-rate-limiting.md)) | `ExceedingGlobalLimit_ReturnsTooManyRequests`; carga con k6 en el portfolio | Parcial (local, sin SLO) |
| Salud anónima en `/health` | `LL.Hosting` (`MapApiHealthChecks`) | `Health_IsAnonymous_ReturnsHealthy` | Validado (CI) |
| Controles compartidos con versión fijada: `LL.Hosting` `0.1.0` y `LL.Security` `0.3.0` | [`Directory.Packages.props`](../Directory.Packages.props), [`NuGet.config`](../NuGet.config) | `dotnet restore` desde GitHub Packages en CI | Validado (CI) |
| Credencial de solo lectura para el feed privado `LL.*` | Gestor de secretos local y secreto `LL_PACKAGES_TOKEN` de Dependabot | [`credenciales.md`](credenciales.md); PR #55 generada por Dependabot y fusionada por el flujo automático con los checks en verde | Validado (2026-11-09) |
| Límite de cuerpo de 16 KB en `POST /api/products` | `[RequestSizeLimit]`; `BadHttpRequestExceptionHandler` conserva el estado | Prueba local con 16 384 y 17 408 bytes contra Kestrel; [evidencia](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-12-11-api-body-limit/README.md) | Parcial (local: `POST /api/products` medido; `POST /api/orders` sin medir) |

El contrato completo, incluido el formato de error, está en
[docs/api.md](api.md).

### 2.2. Seguridad

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Validación JWT: firma, `iss`, `aud`, `exp`, `nbf` y *clock skew* | `LL.Security` (`AddHardenedJwtBearer`) | Requiere tenant real; ver [azure-ad-tests.md](azure-ad-tests.md) | Parcial (pruebas con doble, sin criptografía real) |
| Autorización por propietario (BOLA/IDOR) | `OrdersController`, `GetOrderByIdHandler` | `UserCannotReadAnotherUsersOrder` y `CreateOrder_ReturnsOnlyOwnOrders` | Validado (CI) |
| `404` en lugar de `403` para pedidos ajenos | `OrdersController.GetOrderById` | La misma prueba | Validado (CI) |
| DTOs de entrada contra *mass assignment* | `Models/Create*Request.cs` | Revisión de código; `Id` no es asignable | Validado (CI) |
| Sin cabecera `Server` (huella del servidor web) | `LL.Security` `0.3.0`, dentro de `UseSecurityHeaders` | `ServerHeaderTests` fija la opción; job `container-security`: `scripts/check-server-header.sh` contra el contenedor | Validado (CI, 2026-11-09) |
| Escaneo de secretos sobre historial completo | Acción `gitleaks` de [`core`](https://github.com/lanuzalorenzo/core), en el job `secret-scanning` de `.github/workflows/devsecops-ci.yml` | Ejecución en GitHub Actions | Validado (CI) |
| Aviso de secretos antes del commit (hook local) | `scripts/hooks/pre-commit` | `scripts/install-git-hooks.sh --check` (instalación y versión) y `gitleaks git --staged` sobre un cambio preparado | Parcial: opt-in por clon, no lo impone la plataforma |
| SAST con CodeQL (`security-extended`) | Workflow, job `sast` | Ejecución en GitHub Actions | Validado (CI) |
| SCA con Trivy, bloqueante en `CRITICAL`/`HIGH` | Workflow, job `sca` | Ejecución en GitHub Actions | Validado (CI) |
| Escaneo de imagen y verificación de usuario no-root | Workflow, job `container-security` | `docker run --entrypoint id ... -u` distinto de `0` | Validado (CI) |
| Threat model STRIDE y OWASP API Top 10 | [threat-model-appsec.md](threat-model-appsec.md) | Revisión del documento | Validado (documento) |

La validación JWT no se ejecuta en CI: `tests/EcommerceApi.Tests/ApiFactory.cs`
sustituye el esquema por `TestAuthHandler`. Lo que las pruebas demuestran es la
autorización por usuario, no la criptografía de `JwtBearer`. Ese matiz es
importante y está en el threat model como `Parcial` en *Information Disclosure*.

### 2.3. Arquitectura

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Arquitectura por capas con dependencias en una sola dirección | `Domain` ← `Application` ← `Infrastructure` ← host | `LayerDependencyTests` (7 casos) | Validado (CI) |
| Inversión de dependencias con puertos y adaptadores | Interfaces en `Application/Abstractions`, adaptadores en `Infrastructure` | Pruebas de arquitectura y de infraestructura | Validado (CI) |
| DDD táctico: agregados, *value objects*, invariantes y evento de dominio | `Domain/Orders`, `Domain/Products`, `Money` | `OrderTests`, `ProductTests`, `MoneyTests` | Validado (CI) |
| CQRS ligero solo en pedidos | `Application/Orders`, [ADR-0003](adr/0003-cqrs-ligero-en-pedidos.md) | `CreateOrderHandlerTests`, `OrderQueryHandlerTests` | Validado (CI) |
| Monolito modular con criterios de extracción escritos | [ADR-0001](adr/0001-monolito-modular-vs-microservicios.md) | Revisión del documento | Validado (decisión) |

### 2.4. Datos y mensajería

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Persistencia transaccional sobre SQLite | `SqliteUnitOfWork`, `SqliteSession` | `EcommerceApi.Infrastructure.Tests` | Validado (local, laboratorio) |
| Importes en céntimos enteros | `Money`, `MoneyCents` | `MoneyTests`, pruebas de infraestructura | Validado (CI) |
| Outbox transaccional: pedido y mensaje en la misma transacción | `SqliteOrderRepository`, `SqliteOutboxStore` | `SqliteOutboxTests` (rollback incluido) | Validado (CI) |
| Reintentos y *dead-letter* tras 5 fallos | `OutboxPublisher` | `SqliteOutboxTests` | Validado (CI) |
| Contrato versionado `OrderCreatedV1` | `EcommerceApi.Contracts` | Compilación compartida por API y Function | Validado (CI) |
| Consumidor idempotente por `messageId` | `ProcessOrderHandler`, `ProcessedMessages` | `ProcessOrderHandlerTests` | Validado (local) |
| Publicación en Service Bus | `ServiceBusEventPublisher` | Humo contra el emulador local, 2026-11-14 ([ADR-0009](adr/0009-como-se-prueba-la-mensajeria-en-local.md)) | Parcial (emulador, sin namespace real) |
| Function de Service Bus | `functions/OrderProcessor` | `dotnet build`; sin despliegue | Diseño |

### 2.5. Entrega y operación

| Capacidad | Dónde | Cómo se comprueba | Estado |
|---|---|---|---|
| Artefacto de publicación sin PDB | `Directory.Build.props` (`DebugType=none` y `DebugSymbols=false` en `Release`) | `dotnet publish` + `find -name '*.pdb'`; [evidencia](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-12-05-api-binary/README.md) | Validado (local, 2026-11-12) |
| Suite xUnit en cuatro proyectos | `tests/` | `dotnet test EcommerceApi.sln` | Validado (CI) |
| Fakes propios en lugar de framework de *mocking* | `tests/EcommerceApi.UnitTests/Fakes/InMemoryFakes.cs` | Revisión de código | Validado (CI) |
| Verificación de formato en CI | Workflow, job `build-and-test` | `dotnet format --verify-no-changes` | Validado (CI) |
| Pipeline con 7 jobs: build, secretos, SAST, SCA, contenedor, IaC y revisión de dependencias | `.github/workflows/devsecops-ci.yml` | Ejecución en Actions | Validado (CI) |
| Imagen firmada con cosign keyless | `.github/workflows/container-publish.yml`, job `sign` | `cosign sign` y `cosign verify` en el mismo job | Validado (CI, 2026-11-12) |
| SBOM atestado y ligado al digest | `.github/workflows/container-publish.yml`, job `sign` | `cosign attest --type cyclonedx` y `cosign verify-attestation` | Validado (CI, 2026-11-12) |
| Imagen multi-stage con usuario no privilegiado | `Dockerfile` (`USER $APP_UID`) | Job `container-security` | Validado (CI) |
| IaC Bicep con VNet, NSG, Key Vault, Private Endpoints y diagnóstico | `infra/main.bicep` | `bicep build` + Trivy config + `scripts/check-infra-names.py` en el [run 45468481809](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45468481809) | Diseño (validación estática) |
| Despliegue en Azure | — | — | Pendiente |
| OIDC de GitHub Actions a Azure | [infra/README.md](../infra/README.md) | — | Diseño |
| Captura persistente de logs | `Microsoft.Web/sites/config` (`logs`) + diagnóstico a Log Analytics | `bicep build` (incluye linter); ver [ADR-0005](adr/0005-observabilidad.md) | Diseño (validación estática) |
| Telemetría de aplicación | `AddApplicationInsightsTelemetry()` condicionado a la cadena | `ObservabilityTests` | Parcial (código, sin despliegue) |

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
| Azure desplegado | La capa sin coste (VNet, NSG y subredes) se desplegó el 2026-11-26; la capa de servicios se compila y se escanea, pero no se ha aplicado |
| OIDC de CI a Azure activo | Hay un workflow de CI, no de despliegue |
| Alta disponibilidad, SLA o tráfico elevado | SQLite, una instancia y solo una prueba puntual de carga, no tráfico elevado |
| Cobertura de pruebas medida | `coverlet` está disponible, pero no hay umbral ni informe en CI |
| Validación de dominio ejercitada por HTTP | `DataAnnotations` y la deduplicación del caso de uso la cubren antes; `DomainExceptionHandler` actúa como red de seguridad y no tiene prueba de integración |

## 4. Cómo formular una afirmación defendible

| En lugar de | Escribe |
|---|---|
| «Implementé microservicios con Service Bus» | «Implementé un monolito modular con outbox transaccional sobre SQLite, reintentos y *dead-letter*, y un consumidor idempotente; el adaptador de Service Bus está escrito y no desplegado» |
| «Arquitectura en Azure» | «Desplegué la red de la plantilla Bicep y ejecuté la API en Container Apps; la capa privada —Key Vault, Private Endpoints, App Service— sigue en Diseño» |
| «Clean Architecture» | «Arquitectura por capas con inversión de dependencias, verificada con pruebas que fallan si se rompe la dirección» |
| «Alta cobertura de tests» | «91 pruebas en cuatro proyectos: dominio, aplicación, arquitectura, infraestructura e integración HTTP; sin umbral de cobertura» |
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

- Esta matriz guarda el detalle y la evidencia de cada control; el estado se declara en [`AGENTS.md` §Línea base](../AGENTS.md#línea-base) y el [resumen del README](../README.md#estado-de-seguridad) no lo sustituye.
- Si una capacidad cambia de estado, actualiza la matriz y el resumen del README en el mismo lote.
- No añadas una fila sin una comprobación asociada. Si no existe, escribe `Diseño` o `Pendiente` y explica qué falta.
