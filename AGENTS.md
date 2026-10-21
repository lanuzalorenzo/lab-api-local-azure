# Guía para agentes: EcommerceApi (lab-api-local-azure)

Este repositorio contiene la API empresarial desarrollada en .NET 10 y utilizada como Workload de Referencia en el portfolio de ciberseguridad. Cualquier asistente de IA (GitHub Copilot, Google Antigravity, Claude, etc.) debe respetar estas directrices:

- Este repositorio es el workload del laboratorio final. La arquitectura del laboratorio (identidad, red, detección, respuesta y forense) se documenta en `portfolio-ciberseguridad-lorenzo/docs/laboratorio-final/`; no dupliques aquí IaC de plataforma.
- Los runbooks y las evidencias se enlazan desde el portfolio. No publiques datos reales, secretos ni salidas sin redactar.

## Convenciones de desarrollo y seguridad
- Mantener la arquitectura desacoplada en .NET (controladores REST, DTOs y modelos).
- Respetar los principios de Security by Design y directrices de AppSec (OWASP API Security Top 10).
- No eliminar ni debilitar la validación criptográfica de tokens JWT/JWKS en `Program.cs`.
- Mantener el contenedor Dockerfile con usuario no privilegiado (`USER $APP_UID`) y configuraciones seguras.
- No incluir secretos, cadenas de conexión reales ni identificadores sensibles en el código.
- Toda configuración sensible debe ir en `appsettings.Development.json` (ignorado), user-secrets o variables de entorno.
- Mantener CORS en modo denegación por defecto; los orígenes permitidos se configuran explícitamente.
- Añadir o actualizar pruebas de integración en `tests/EcommerceApi.Tests` para cambios de comportamiento.
- La infraestructura se define en `infra/main.bicep`; no introducir recursos públicos por defecto.
- Los diseños de plataforma viven en `docs/app-service.md`, `docs/functions.md` y `docs/ai-integration.md`. Si un cambio contradice uno de ellos, actualiza el documento en el mismo lote.

## Comandos de verificación
```bash
dotnet restore tests/EcommerceApi.Tests/EcommerceApi.Tests.csproj
dotnet build EcommerceApi/EcommerceApi.csproj -c Release --no-restore
dotnet test tests/EcommerceApi.Tests/EcommerceApi.Tests.csproj -c Release --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

## Convenciones de Commits
Todos los commits de este repositorio deben seguir estrictamente el estándar **Conventional Commits**:
- Formato: `type(scope): imperative summary in English`.
- **Ámbitos (`scope`) permitidos:** `api`, `auth`, `docker`, `ci`, `tests`, `infra`, `docs`, `repo`.
- **Tipos permitidos:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`.
- El asunto debe ser imperativo, conciso y redactado en inglés.
- Ejemplos:
  - `feat(api): add order validation against product catalog`
  - `feat(ci): add CodeQL, Trivy and container hardening checks`
  - `docs(auth): document Entra ID token validation process`
  - `docs(repo): update README with enterprise workload overview`
