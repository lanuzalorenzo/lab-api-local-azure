# Guía para agentes: EcommerceApi (lab-api-local-azure)

> **Si llegas nuevo:** el contrato de contenido es el de `portfolio-ciberseguridad-lorenzo/AGENTS.md`. Este archivo añade convenciones técnicas propias de la API.

Este repositorio contiene la API empresarial desarrollada en .NET 10 y utilizada como Workload de Referencia en el portfolio de ciberseguridad.

## Convenciones de desarrollo y seguridad
- Mantener la arquitectura desacoplada en .NET: `Domain` no depende de nadie, `Application` solo de `Domain`, `Infrastructure` de ambas y el host compone todo. La regla se verifica en `tests/EcommerceApi.Architecture.Tests`; no la debilites.
- Documentar toda decisión estructural como ADR en `docs/adr/` antes o junto al código que la implementa.
- Respetar los principios de Security by Design y directrices de AppSec (OWASP API Security Top 10).
- No eliminar ni debilitar la validación criptográfica de tokens JWT/JWKS en `Program.cs`.
- Mantener el contenedor Dockerfile con usuario no privilegiado (`USER $APP_UID`) y configuraciones seguras.
- No incluir secretos, cadenas de conexión reales ni identificadores sensibles en el código.
- Toda configuración sensible debe ir en `appsettings.Development.json` (ignorado), user-secrets o variables de entorno.
- Mantener CORS en modo denegación por defecto; los orígenes permitidos se configuran explícitamente.
- Añadir o actualizar pruebas de integración en `tests/EcommerceApi.Tests` para cambios de comportamiento.
- La infraestructura se define en `infra/main.bicep`; no introducir recursos públicos por defecto.
- Los diseños de plataforma viven en `docs/app-service.md`, `docs/functions.md` y `docs/ai-integration.md`. Si un cambio contradice uno de ellos, actualiza el documento en el mismo lote.

## Convenciones de contenido (compartidas con el portfolio)
- **Prohibido:** bloques «En fácil», «TL;DR», «En resumen» u otros rótulos de registro. Se escribe en español claro y directo.
- **Estados:** usa solo **Validado**, **Parcial**, **Runbook preparado**, **Diseño** o **Pendiente**. Un diseño o runbook sin ejecutar no es una validación.
- **Estructura de docs:**
  - `README.md` de práctica: 240–310 palabras, problema en prosa, tabla con lo hecho/encontrado, 2–3 lecciones, enlaces.
  - `informe.md` solo si sostiene lo que la página no puede (catálogo, valores, matriz, verificación). No duplica la página.
  - `runbook.md` solo para prácticas reejecutables.
  - `bitacora/` solo si guarda un hecho que no está en otro sitio.
- **Evidencia vs hallazgo:** una auditoría que encuentra el problema ha funcionado, pero el control no está aprobado. La tabla lleva resultado observado y gravedad; aparte, qué se corrigió y qué solo se recomendó.

## Comandos de verificación
```bash
dotnet restore EcommerceApi.sln
dotnet build EcommerceApi.sln -c Release --no-restore
dotnet test EcommerceApi.sln -c Release --no-build --no-restore
dotnet format EcommerceApi.sln --verify-no-changes --no-restore
bicep build infra/main.bicep --outfile /tmp/main.json
gitleaks detect --source . --redact --verbose --exit-code 2
```

## Convenciones de Commits
Formato: `type(scope): imperative summary in English`.
Ámbitos permitidos: `api`, `auth`, `docker`, `ci`, `tests`, `infra`, `docs`, `repo`.
Tipos permitidos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`.