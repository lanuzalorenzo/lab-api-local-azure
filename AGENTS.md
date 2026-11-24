# Guía para agentes: EcommerceApi

API empresarial en .NET 10 y banco de pruebas del portfolio público. Este archivo cubre el código, la seguridad y la verificación del repositorio.

## Papel en el conjunto

Este repositorio estrena lo que después se aplica en otro sitio: una versión nueva de `LL.Security` o `LL.Hosting`, un patrón de infraestructura o un diseño asíncrono se prueban aquí antes de salir de aquí. Su CI en verde es lo que autoriza el traslado.

- Es la carga sobre la que se tomaron las evidencias de los módulos de infraestructura, red y mensajería.
- No guarda datos personales: el conjunto es sintético y un experimento se puede romper sin coste.
- Un control que solo viva aquí no se presenta como capacidad de la aplicación principal.
- No se archiva mientras la aplicación principal no tenga infraestructura y despliegue propios: hoy cubre lo que a ella le falta.

## Arquitectura

- Desacoplada: `Domain` sin dependencias, `Application` solo `Domain`, `Infrastructure` de ambas y el host compone todo. Verificado en `tests/EcommerceApi.Architecture.Tests`; no debilites esa separación.
- Toda decisión estructural se documenta como ADR en `docs/adr/`.
- `Directory.Build.props` fija la configuración común de compilación y `Directory.Packages.props` la única versión de cada paquete; los `.csproj` no repiten `Nullable`, `ImplicitUsings` ni `Version`. Ver [ADR-0008](docs/adr/0008-configuracion-de-compilacion-y-paquetes-centralizada.md).
- La versión de .NET, las actualizaciones mayores y las dependencias directas y transitivas siguen la [política de plataforma](https://github.com/lanuzalorenzo/core/blob/main/docs/versiones-y-dependencias.md): LTS, mayores de la familia bloqueados y pin de transitivas en el centro.
- `global.json` fija el SDK en un mínimo —hoy 10.0.115, `rollForward: latestFeature`—: un `10.0.x` igual o posterior compila, otra rama mayor se detiene. El protocolo de cambio de versión está en [ADR-0010](docs/adr/0010-plataforma-dotnet-y-ciclo-de-vida.md).
- `functions/` contiene el procesador de pedidos; los diseños de plataforma están en `docs/app-service.md`, `docs/functions.md` y `docs/ai-integration.md`. Si un cambio los contradice, actualiza el documento en el mismo lote.
- `infra/main.bicep` es la infraestructura; sin recursos públicos por defecto. Los nombres de recurso salen de la función `resourceName` —tipo, carga, entorno y región, con las abreviaturas del CAF—, salvo los que fija la plataforma (`logs` y las zonas DNS privadas); el CI todavía no rechaza un nombre escrito a mano ([ADR-0011](docs/adr/0011-nombres-de-recurso-caf.md)).

## Seguridad

- Security by Design y OWASP API Security Top 10.
- No elimines ni debilites la validación criptográfica JWT/JWKS: la aplica `LL.Security` (`AddHardenedJwtBearer`) y `Program.cs` solo aporta emisor y audiencia.
- Los controles de plataforma se consumen por la versión publicada de `LL.Security` y `LL.Hosting`, fijada en `Directory.Packages.props`. Una versión nueva entra por su PR y su CI; no se copia el código de otro consumidor.
- CORS con denegación por defecto y orígenes explícitos.
- Cabeceras de seguridad y política de contenido restrictiva desde `LL.Security`: la API devuelve JSON y no carga ni incrusta nada. Servir HTML o un recurso externo exige cambiar la política, no copiar el middleware.
- Dockerfile con usuario no privilegiado (`USER $APP_UID`).
- Sin secretos ni cadenas reales en el código: `appsettings.Development.json` (ignorado), user-secrets o variables de entorno.
- Pruebas de integración en `tests/EcommerceApi.Tests` para cualquier cambio de comportamiento.

## Línea base

Los identificadores y el criterio son los de [`shared-dotnet/docs/linea-base.md`](https://github.com/lanuzalorenzo/shared-dotnet/blob/main/docs/linea-base.md). Esta sección es la declaración canónica del estado: traduce a los cinco estados lo que documentan `docs/capacidades.md` y el pipeline, y no añade controles fuera de su alcance. Las etiquetas de presentación de `README.md` y `docs/capacidades.md`, en las filas de esta tabla, se leen con esta equivalencia: `Implementado` es `Validado` salvo que la fila limite el alcance (`Implementado; fuera de CI` es `Parcial`); `Validado en local` es `Validado` con alcance local. Fuera de esta tabla no se traducen, y no crean estados nuevos.

| Id | Estado | Evidencia |
|---|---|---|
| `auth` | Parcial | `AddHardenedJwtBearer` en `Program.cs`; 401 del esquema de prueba en `ProductsEndpointsTests`. La validación real de firma, `iss` y `aud` queda fuera del CI y pide tenant ([`azure-ad-tests.md`](docs/azure-ad-tests.md)). |
| `permisos` | Pendiente | sin `AddPermissionPolicies` ni `RequirePermission`; scopes y roles granulares sin implementar. El paquete lo trae con `LL.Security` 0.4.0, sin publicar. |
| `cors` | Validado | `AddDenyByDefaultCors` y `UseCors("ApiCors")`; `CorsTests` con origen permitido y ajeno en el CI del 2026-11-24 ([run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036)). |
| `cabeceras` | Validado | `UseSecurityHeaders`; `SecurityHeadersTests` para la CSP, `ServerHeaderTests` para la opción de Kestrel y la batería del paquete para el resto de cabeceras (`LL.Security.Tests/SecurityHeadersTests`), más la comprobación contra el contenedor ([run 42685834576](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42685834576), 2026-11-09). |
| `csp` | Validado | `ContentSecurityPolicy.DenyAll()`; `SecurityHeadersTests` compara la cabecera. |
| `limite` | Validado | `AddRemoteIpRateLimiting` y `UseRateLimiter()`; 429 en `RateLimitTests`; validado en local y en CI, sin SLO; [ADR-0007](docs/adr/0007-rate-limiting.md). |
| `problemdetails` | Validado | `AddApiProblemDetails` y `UseApiProblemDetails`; cuatro casos en `ErrorContractTests` con `application/problem+json`. |
| `salud` | Validado | `AddApiHealthChecks` y `MapApiHealthChecks`; `/health` anónimo en `HealthEndpointTests` y cuerpo `Healthy` sin detalle en la prueba del paquete (`LL.Hosting.Tests/HealthChecksTests`). |
| `permisos-workflow` | Validado | `contents: read` global y en los jobs que lo declaran; `security-events: write` en SAST, SCA, contenedor e IaC; [run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036) (2026-11-24). |
| `secretos` | Validado | Gitleaks en cada push a `main` y en cada PR (`.github/workflows/devsecops-ci.yml`, job `secret-scanning`); el hook local sigue `Parcial` porque es opt-in en cada clon. |
| `sast` | Validado | job `sast` con CodeQL `security-extended`; 0 alertas abiertas de Code Scanning y [run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036) en verde (2026-11-24). |
| `sca` | Validado | job `sca` con Trivy, bloqueante en `HIGH` y `CRITICAL` con corrección disponible; [run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036) (2026-11-24). |
| `contenedor` | Validado | `Dockerfile` con `USER $APP_UID`; el CI comprueba que el UID no es 0 y escanea la imagen; [run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036) (2026-11-24). |
| `dependabot` | Validado | `.github/dependabot.yml` y `dependabot-auto-merge.yml` para parche y menor; [PR #55](https://github.com/lanuzalorenzo/lab-api-local-azure/pull/55) fusionada por el flujo. |
| `main` | Validado | ruleset `main` con pull request y siete checks; sin revisor humano porque un solo mantenedor no puede aprobar su PR; [`repo-governance.md`](docs/repo-governance.md). |
| `iac` | Validado | `bicep build infra/main.bicep` y Trivy config en el job de IaC; [run 45411139036](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/45411139036) (2026-11-24). Sin desplegar. |

## Contrato editorial

El contrato completo —apertura llana, jerga, estados, evidencias y claridad que no cambia lo que se afirma— está en [portfolio-ciberseguridad-lorenzo](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/AGENTS.md). En este repo: español claro, estados explícitos (Validado · Parcial · Runbook preparado · Diseño · Pendiente) y lo ejecutado separado de lo diseñado; un hallazgo no es un PASS.

## Verificación

```bash
dotnet restore EcommerceApi.sln
dotnet build EcommerceApi.sln -c Release --no-restore
dotnet test EcommerceApi.sln -c Release --no-build --no-restore
dotnet format EcommerceApi.sln --verify-no-changes --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

Los pasos .NET, el escaneo de secretos, SAST, SCA, el escaneo de imagen y el de IaC se ejecutan con las acciones compartidas de [`core`](https://github.com/lanuzalorenzo/core) desde `.github/workflows/devsecops-ci.yml`; construir la imagen, comprobar el UID y compilar Bicep siguen en ese workflow. El hook local de secretos se instala con `scripts/install-git-hooks.sh` y se comprueba con `--check`; es un aviso temprano, no sustituye al CI. **Definición de cambio listo:** los seis comandos anteriores sin fallos y los siete checks del pipeline en verde, sin bypass. La gobernanza de `main` y los checks obligatorios están en `docs/repo-governance.md`; no presentes un bypass como flujo válido.

Un cambio que introduzca una regla nueva que el repositorio deba cumplir —un archivo raíz que pueda romper la compilación, un check obligatorio nuevo o un cambio del estado declarado de un control— actualiza este archivo y los documentos que repiten esa regla, en el mismo lote.

La composición del camino de mensajería vive fuera de la suite normal: `scripts/servicebus-emulator.sh up`, `dotnet test tests/EcommerceApi.Emulator.Tests/EcommerceApi.Emulator.Tests.csproj -c Release`, `dotnet format tests/EcommerceApi.Emulator.Tests/EcommerceApi.Emulator.Tests.csproj --verify-no-changes --no-restore` y `scripts/servicebus-emulator.sh down`. El proyecto está fuera de `EcommerceApi.sln`, lo ejecuta el workflow del emulador y no entra en los seis comandos ni en los siete checks obligatorios.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `auth`, `docker`, `ci`, `tests`, `infra`, `docs`, `repo`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

Los cambios entran por rama y pull request: `main` exige PR, los siete checks del pipeline y rama actualizada; solo el propietario fusiona, salvo las PRs de Dependabot de parche y minor, que fusiona el flujo automático si pasan los checks. No empujes directamente.
