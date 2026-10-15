# Guía para agentes: EcommerceApi (lab-api-local-azure)

Este repositorio contiene la API empresarial desarrollada en .NET 10 y utilizada como Workload de Referencia en el portfolio de ciberseguridad. Cualquier asistente de IA (GitHub Copilot, Google Antigravity, Claude, etc.) debe respetar estas directrices:

## Convenciones de desarrollo y seguridad
- Mantener la arquitectura desacoplada en .NET (controladores REST, DTOs y modelos).
- Respetar los principios de Security by Design y directrices de AppSec (OWASP API Security Top 10).
- No eliminar ni debilitar la validación criptográfica de tokens JWT/JWKS en `Program.cs`.
- Mantener el contenedor Dockerfile con usuario no privilegiado (`USER $APP_UID`) y configuraciones seguras.
- No incluir secretos, cadenas de conexión reales ni identificadores sensibles en el código.

## Convenciones de Commits
Todos los commits de este repositorio deben seguir estrictamente el estándar **Conventional Commits**:
- Formato: `type(scope): imperative summary in English`.
- **Ámbitos (`scope`) permitidos:** `api`, `auth`, `docker`, `ci`, `tests`, `docs`, `repo`.
- **Tipos permitidos:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`.
- El asunto debe ser imperativo, conciso y redactado en inglés.
- Ejemplos:
  - `feat(api): add order validation against product catalog`
  - `feat(devsecops): add CI workflow and non-root Dockerfile`
  - `docs(auth): document Entra ID token validation process`
  - `docs(repo): update README with enterprise workload overview`
