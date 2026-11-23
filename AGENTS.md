# Guía para agentes: EcommerceApi

API empresarial en .NET 11 y banco de pruebas del portfolio público. Este archivo cubre el código, la seguridad y la verificación del repositorio.

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
- `infra/main.bicep` es la infraestructura; sin recursos públicos por defecto.

## Seguridad

- Security by Design y OWASP API Security Top 10.
- No elimines ni debilites la validación criptográfica JWT/JWKS: la aplica `LL.Security` (`AddHardenedJwtBearer`) y `Program.cs` solo aporta emisor y audiencia.
- Los controles de plataforma se consumen por la versión publicada de `LL.Security` y `LL.Hosting`, fijada en `Directory.Packages.props`. Una versión nueva entra por su PR y su CI; no se copia el código de otro consumidor.
- CORS con denegación por defecto y orígenes explícitos.
- Cabeceras de seguridad y política de contenido restrictiva desde `LL.Security`: la API devuelve JSON y no carga ni incrusta nada. Servir HTML o un recurso externo exige cambiar la política, no copiar el middleware.
- Dockerfile con usuario no privilegiado (`USER $APP_UID`).
- Sin secretos ni cadenas reales en el código: `appsettings.Development.json` (ignorado), user-secrets o variables de entorno.
- Pruebas de integración en `tests/EcommerceApi.Tests` para cualquier cambio de comportamiento.

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
