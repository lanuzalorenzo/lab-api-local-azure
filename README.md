# EcommerceApi — API de referencia para AppSec y DevSecOps

**API REST en .NET 10 protegida con Microsoft Entra ID, usada como activo de laboratorio para validar controles de seguridad, pruebas de autorización y un pipeline DevSecOps.**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Microsoft Entra ID](https://img.shields.io/badge/Auth-Microsoft%20Entra%20ID-0078D4?style=flat&logo=microsoftazure)](https://learn.microsoft.com/entra/)
[![DevSecOps CI](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/workflows/devsecops-ci.yml/badge.svg)](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/workflows/devsecops-ci.yml)
[![Licencia](https://img.shields.io/badge/Licencia-MIT-green.svg)](LICENSE)

---

## Qué es y qué no es

**Es** un laboratorio reproducible para demostrar:

- Validación de tokens JWT emitidos por Microsoft Entra ID.
- Autorización a nivel de objeto (BOLA/IDOR) y separación por usuario.
- Mitigación de *mass assignment* mediante DTOs.
- CORS restrictivo, *rate limiting*, límites de tamaño y *Problem Details*.
- Pruebas de integración automatizadas.
- Pipeline con SAST, SCA, escaneo de secretos, escaneo de contenedor y escaneo de IaC.

**No es** un sistema listo para producción. El almacenamiento es en memoria, no hay
base de datos, no hay alta disponibilidad y no se han validado scopes/roles
granulares ni un despliegue real en Azure. El estado de cada control se indica en
la tabla siguiente.

---

## Arquitectura

```mermaid
flowchart LR
    C[Cliente / Swagger] -->|HTTPS + Bearer JWT| API[EcommerceApi .NET 10]
    API -->|Valida firma, issuer, audience y expiración| ENTRA[Microsoft Entra ID]
    API -->|Autoriza por usuario| STORE[(InMemoryStore)]
    API -->|Logs y métricas| OBS[Application Insights / Log Analytics]
    CI[GitHub Actions] -->|SAST · SCA · Secretos · Contenedor · IaC| API
```

La API expone dos controladores:

- `ProductsController`: catálogo de productos.
- `OrdersController`: pedidos ligados al usuario autenticado (`oid`/`sub`).

### Flujo de autorización

1. El cliente obtiene un token de Entra ID.
2. `JwtBearer` valida firma, emisor, audiencia, expiración y *clock skew*.
3. `OrdersController` resuelve la identidad del usuario y solo devuelve sus pedidos.
4. Un pedido de otro usuario responde `404` para evitar enumeración de objetos.

---

## Estado de seguridad

| Control | Estado | Evidencia |
|---|---|---|
| Validación JWT (firma, issuer, audience, lifetime) | Implementado | `EcommerceApi/Program.cs` |
| Autorización de endpoints | Implementado | `[Authorize]` en controladores |
| BOLA/IDOR en pedidos | Implementado | Filtrado por `UserId` + pruebas |
| Mass assignment en productos y pedidos | Implementado | DTOs `CreateProductRequest` / `CreateOrderRequest` |
| CORS restrictivo | Implementado | Lista vacía = denegar; allowlist por configuración |
| Rate limiting | Implementado (laboratorio) | 120 req/min por IP |
| Límite de tamaño de petición | Implementado | 16 KB en endpoints de escritura |
| `Problem Details` y cabeceras de seguridad | Implementado | `AddProblemDetails`, middleware |
| Pruebas de integración | Implementado | `tests/EcommerceApi.Tests` (10 pruebas) |
| Escaneo de secretos | Implementado | Gitleaks sobre historial completo |
| SAST | Implementado | CodeQL C# (`security-extended`) |
| SCA | Implementado | Trivy filesystem, bloqueante para CRITICAL/HIGH |
| Escaneo de contenedor | Implementado | Trivy image + verificación de usuario no-root |
| IaC seguro | Implementado (plantilla) | `infra/main.bicep` + Trivy config |
| Despliegue real en Azure | Pendiente | Sin suscripción/credenciales en este repositorio |
| Scopes/roles granulares | Pendiente | Documentado en `docs/azure-ad-config.md` |
| Persistencia y alta disponibilidad | Fuera de alcance | Almacén en memoria |
| OIDC de GitHub Actions a Azure | Documentado, no activo | `infra/README.md` |

> **Criterio de rigor:** “Implementado” significa que el código o la configuración
> existe y se ha ejecutado localmente o en CI. “Pendiente” significa que no se ha
> validado y no se presenta como terminado.

---

## Estructura

```text
lab-api-local-azure/
├── EcommerceApi/
│   ├── Configuration/          # Opciones de Entra ID
│   ├── Controllers/            # ProductsController, OrdersController
│   ├── Models/                 # Entidades y DTOs de entrada
│   ├── Services/               # InMemoryStore thread-safe
│   ├── Program.cs              # Pipeline HTTP, auth, CORS, rate limiting
│   └── appsettings.json        # Placeholders, sin credenciales
├── tests/EcommerceApi.Tests/   # Pruebas de integración xUnit
├── infra/                      # Bicep: VNet, NSG, Key Vault, App Service, PE
├── docs/                       # Autenticación, pruebas y threat model
├── .github/workflows/          # Pipeline DevSecOps
├── Dockerfile                  # Multi-stage, usuario no-root
└── README.md
```

---

## Puesta en marcha

### Requisitos

- .NET SDK 10.0
- Opcional: Docker
- Un tenant de Microsoft Entra ID para validar tokens reales

### Configuración local

```bash
cd lab-api-local-azure
cp EcommerceApi/appsettings.Development.example.json EcommerceApi/appsettings.Development.json
```

Edita `appsettings.Development.json` con tus valores:

```json
{
  "AzureAd": {
    "TenantId": "<tenant-id>",
    "ClientId": "<api-client-id>",
    "Audience": "api://<api-client-id>",
    "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0"
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  }
}
```

`appsettings.Development.json` está ignorado por Git. No guardes secretos en
`appsettings.json` ni en el repositorio.

### Ejecutar

```bash
dotnet restore EcommerceApi/EcommerceApi.csproj
dotnet run --project EcommerceApi --urls http://localhost:5080
```

- Swagger (solo Development): <http://localhost:5080/swagger>
- Health check: <http://localhost:5080/health>
- API: <http://localhost:5080/api/products>

### Pruebas

```bash
dotnet test tests/EcommerceApi.Tests/EcommerceApi.Tests.csproj
```

Las pruebas usan un esquema de autenticación de test y no necesitan un tenant
real. Cubren 401, validación de entrada, mass assignment y BOLA entre dos usuarios.

### Docker

```bash
docker build -t ecommerce-api .
docker run --rm -p 8080:8080 ecommerce-api
```

El contenedor se ejecuta con `USER $APP_UID` (no root) y escucha en `8080`.

---

## Autenticación con Entra ID

El registro de la aplicación, los scopes y las pruebas con tokens reales se
describen en:

- [`docs/azure-ad-register.md`](docs/azure-ad-register.md)
- [`docs/azure-ad-config.md`](docs/azure-ad-config.md)
- [`docs/azure-ad-tests.md`](docs/azure-ad-tests.md)

La API valida `issuer`, `audience`, `lifetime` y firma. La autorización granular
por scopes/roles está documentada como trabajo pendiente; hoy todos los endpoints
requieren un usuario autenticado y los pedidos se filtran por identidad.

### CORS

- `Cors:AllowedOrigins` vacío → se deniega cualquier origen cruzado.
- Para un frontend local, añade su origen exacto en `AllowedOrigins`.
- No se usa `AllowAnyOrigin`.

### Rate limiting

Límite global de 120 peticiones por minuto y por IP. Es un control de laboratorio;
en producción se complementaría con API Management/WAF y límites por usuario.

---

## Pipeline DevSecOps

`.github/workflows/devsecops-ci.yml` ejecuta:

| Job | Herramienta | Bloqueante |
|---|---|---|
| Compilación, formato y pruebas | .NET 10, xUnit | Sí |
| Detección de secretos | Gitleaks (historial completo) | Sí |
| SAST | CodeQL C# | Sí |
| SCA | Trivy filesystem | CRITICAL/HIGH |
| Contenedor | Docker + Trivy + verificación no-root | Sí |
| IaC | Trivy config sobre Bicep | CRITICAL/HIGH |
| Dependencias en PR | Dependency Review | High |

Los resultados SARIF se publican en la pestaña **Security → Code scanning** del
repositorio. La pipeline completa finalizó en verde el **2026-10-19** (run
`39243360276`): build, formato, 10 pruebas, Gitleaks, CodeQL, Trivy SCA,
contenedor no-root, Trivy image y Trivy config sobre Bicep.

---

## Infraestructura como código

`infra/main.bicep` define una arquitectura de referencia con:

- VNet segmentada (`snet-app`, `snet-pe`).
- NSG con denegación por defecto.
- Key Vault con RBAC, purge protection y acceso público deshabilitado.
- Private Endpoints y Private DNS para Key Vault y App Service.
- App Service Linux .NET 10 con HTTPS only, TLS 1.2 y FTPS deshabilitado.
- Identidad gestionada y rol *Key Vault Secrets User*.
- Diagnóstico a Log Analytics.

Consulta [`infra/README.md`](infra/README.md). **La plantilla no se ha desplegado**
en una suscripción real; se valida con `bicep build` y Trivy config en CI.

---

## Documentación de diseño

Estos documentos fijan las decisiones antes de escribir código o desplegar. **Ninguno está ejecutado**; su estado es *Diseño*.

- [App Service](docs/app-service.md) — plataforma de ejecución, entrada/salida de red, identidad, slots y coste.
- [Azure Functions](docs/functions.md) — cómputo asíncrono, idempotencia, dead-letter y hosting.
- [Integración de IA](docs/ai-integration.md) — gateway, controles de seguridad y evaluación.

## Relación con el portfolio

| Módulo | Relación |
|---|---|
| [07 — DevSecOps](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/07-devsecops) | Pipeline, SAST, SCA, secretos e IaC |
| [08 — Redes Azure](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/08-seguridad-red-azure) | VNet, NSG, Private Endpoints |
| [09 — Sentinel](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/09-microsoft-sentinel) | Diagnóstico y analítica KQL |
| [11 — Hacking ético de APIs](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/11-hacking-etico-api) | BOLA/BFLA, JWT y validación ofensiva |
| [12 — Forense cloud](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/12-forense-cloud-respuesta) | Evidencia y respuesta en Azure |

---

## Limitaciones conocidas

- Almacenamiento en memoria: los datos se pierden al reiniciar.
- Sin base de datos, migraciones ni transacciones.
- Sin scopes/roles aplicados endpoint a endpoint.
- Sin WAF/API Management desplegado.
- Sin pruebas de carga ni caos.
- La plantilla Bicep no se ha desplegado; su validación es estática.

Estas limitaciones son deliberadas y se declaran para no confundir un laboratorio
con un sistema productivo.

---

## Licencia

Distribuido bajo licencia [MIT](LICENSE).
