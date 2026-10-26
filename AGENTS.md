# Guía para agentes: EcommerceApi (lab-api-local-azure)

> Si llegas nuevo: el contrato de contenido es el de `portfolio-ciberseguridad-lorenzo/AGENTS.md`. Este archivo añade convenciones técnicas de la API.

API empresarial en .NET 10 usada como Workload de Referencia del portfolio.

## Desarrollo y seguridad

- Arquitectura desacoplada: `Domain` sin dependencias, `Application` solo `Domain`, `Infrastructure` de ambas, host compone todo. Verificado en `tests/EcommerceApi.Architecture.Tests`; no debilitar.
- Toda decisión estructural como ADR en `docs/adr/`.
- Security by Design y OWASP API Security Top 10.
- No eliminar/debilitar validación criptográfica JWT/JWKS en `Program.cs`.
- Dockerfile con usuario no privilegiado (`USER $APP_UID`).
- Sin secretos ni cadenas reales en código; usar `appsettings.Development.json` (ignorado), user-secrets o env vars.
- CORS denegación por defecto; orígenes explícitos.
- Pruebas de integración en `tests/EcommerceApi.Tests` para cambios de comportamiento.
- Infra en `infra/main.bicep`; sin recursos públicos por defecto.
- Diseños de plataforma en `docs/app-service.md`, `docs/functions.md`, `docs/ai-integration.md`; si un cambio los contradice, actualiza el doc en el mismo lote.

## Contenido compartido (portfolio)

Sin rótulos de registro; estados explícitos; estructura docs (README 240–310 palabras, `informe.md` solo si sostiene, `runbook.md` solo reejecutable, `bitacora/` solo hechos nuevos); evidencia vs hallazgo.

## Verificación

```bash
dotnet restore EcommerceApi.sln
dotnet build EcommerceApi.sln -c Release --no-restore
dotnet test EcommerceApi.sln -c Release --no-build --no-restore
dotnet format EcommerceApi.sln --verify-no-changes --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `auth`, `docker`, `ci`, `tests`, `infra`, `docs`, `repo`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Commit/push con autorización por lote.