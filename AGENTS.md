# Guía para agentes: EcommerceApi

API empresarial en .NET 10 y workload de referencia del portfolio público. Este archivo cubre el código, la seguridad y la verificación del repositorio.

## Arquitectura

- Desacoplada: `Domain` sin dependencias, `Application` solo `Domain`, `Infrastructure` de ambas y el host compone todo. Verificado en `tests/EcommerceApi.Architecture.Tests`; no debilites esa separación.
- Toda decisión estructural se documenta como ADR en `docs/adr/`.
- `functions/` contiene el procesador de pedidos; los diseños de plataforma están en `docs/app-service.md`, `docs/functions.md` y `docs/ai-integration.md`. Si un cambio los contradice, actualiza el documento en el mismo lote.
- `infra/main.bicep` es la infraestructura; sin recursos públicos por defecto.

## Seguridad

- Security by Design y OWASP API Security Top 10.
- No elimines ni debilites la validación criptográfica JWT/JWKS de `Program.cs`.
- CORS con denegación por defecto y orígenes explícitos.
- Dockerfile con usuario no privilegiado (`USER $APP_UID`).
- Sin secretos ni cadenas reales en el código: `appsettings.Development.json` (ignorado), user-secrets o variables de entorno.
- Pruebas de integración en `tests/EcommerceApi.Tests` para cualquier cambio de comportamiento.

## Contrato editorial

Español claro y directo, sin rótulos de registro. Estados explícitos (Validado · Parcial · Runbook preparado · Diseño · Pendiente). Separa lo ejecutado de lo diseñado y no presentes un hallazgo como PASS. El contrato completo está en [portfolio-ciberseguridad-lorenzo](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/AGENTS.md).

## Verificación

```bash
dotnet restore EcommerceApi.sln
dotnet build EcommerceApi.sln -c Release --no-restore
dotnet test EcommerceApi.sln -c Release --no-build --no-restore
dotnet format EcommerceApi.sln --verify-no-changes --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

Son los mismos pasos de `.github/workflows/devsecops-ci.yml`. La gobernanza de `main` y los checks obligatorios están en `docs/repo-governance.md`; no presentes un bypass como flujo válido.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `auth`, `docker`, `ci`, `tests`, `infra`, `docs`, `repo`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

Los cambios entran por rama y pull request: `main` exige PR, los siete checks del pipeline y rama actualizada. No empujes directamente; un bypass de administrador no es un flujo válido.
