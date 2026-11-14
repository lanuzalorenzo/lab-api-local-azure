<img src="assets/iconos/api.svg" width="64" alt="Icono de la API de referencia">

# EcommerceApi — API de referencia para AppSec y DevSecOps

Una tienda de juguete con las puertas de una tienda real: así es este repositorio. La API, un servicio web al que se llama por HTTP, está hecha en .NET 10 y protegida con Microsoft Entra ID; sirve de banco de pruebas para AppSec (seguridad de las aplicaciones) y DevSecOps (seguridad integrada en el ciclo de entrega). El catálogo y los pedidos son inventados; los controles de acceso, no: cada puerta rechaza a quien no debe entrar.

**In English.** A toy store with the doors of a real one: that is this repository. The API —a web service called over HTTP— is built in .NET 10 and protected with Microsoft Entra ID; it serves as a test bench for AppSec and DevSecOps. The catalogue and the orders are made up; the access controls are not: every door turns away whoever should not walk in.

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square)](https://dotnet.microsoft.com/)
[![Microsoft Entra ID](https://img.shields.io/badge/Auth-Microsoft%20Entra%20ID-0078d4?style=flat-square)](https://learn.microsoft.com/entra/)
[![DevSecOps CI](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/workflows/devsecops-ci.yml/badge.svg)](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/workflows/devsecops-ci.yml)

---

## Qué es y qué no es

**Es** un laboratorio reproducible de una API empresarial: validación de tokens, autorización por objeto, DTOs contra *mass assignment*, CORS restrictivo, *rate limiting*, arquitectura por capas verificada con pruebas, DDD táctico, CQRS ligero, SQLite con outbox transaccional, un consumidor idempotente y un pipeline DevSecOps con SAST, SCA, secretos, contenedor e IaC.

**No es** un sistema listo para producción ni una implementación de microservicios: es un monolito modular con un segundo despliegue asíncrono ([ADR-0001](docs/adr/0001-monolito-modular-vs-microservicios.md)). Service Bus y la Function están diseñados y probados en local, no desplegados. Las limitaciones concretas están al final.

El estado de cada control, con su prueba o su límite, está en [`docs/capacidades.md`](docs/capacidades.md). Es la fuente única: este README resume.

## Arquitectura

![Arquitectura de EcommerceApi: el cliente llama a la API con un JWT que valida Entra ID; Application usa Domain, guarda en SQLite y escribe en el outbox, que publica hacia una Function idempotente; la API envía telemetría a Application Insights y GitHub Actions la construye y analiza](docs/arquitectura.svg)

La API expone dos controladores: `ProductsController` (catálogo) y `OrdersController` (pedidos ligados al usuario autenticado por `oid`/`sub`). El código se organiza en cinco proyectos con una única dirección de dependencias: `Domain` ← `Application` ← `Infrastructure` ← host `EcommerceApi`, más `Contracts`, que no depende de nadie y comparten productor y consumidor. Los controladores solo conocen handlers de `Application`; los adaptadores viven en `Infrastructure`.

Las decisiones estructurales, sus alternativas y la evidencia de cada patrón están en el [registro de ADR](docs/adr/README.md).

### Eventos, outbox y consumidor

1. `POST /api/orders` abre una transacción y escribe el pedido, sus líneas y el mensaje `OrderCreatedV1` en la tabla de outbox (la bandeja de salida que se confirma junto al pedido).
2. La transacción confirma: o existen los tres, o no existe ninguno.
3. `OutboxDispatcher` publica los pendientes cada 2 segundos. Si el publicador falla, incrementa el contador de intentos; tras 5 fallos lo marca como *dead-letter*.
4. La Function `OrderProcessor` consume el mensaje, valida el contrato y consulta `ProcessedMessages` por `messageId` antes de registrar el efecto.
5. Un mensaje repetido se reconoce sin repetir el efecto; uno inválido se lanza para que Service Bus lo reintente y, agotado `maxDeliveryCount`, lo mueva a la DLQ.

`Publisher:Mode=Log` (por defecto) registra el mensaje en el log local; `Publisher:Mode=ServiceBus` usa un namespace real o el emulador local ([ADR-0009](docs/adr/0009-como-se-prueba-la-mensajeria-en-local.md)). **Service Bus y la Function no están desplegados**: el outbox, la idempotencia y la publicación contra el emulador se validan en local.

### Flujo de autorización

1. El cliente obtiene un token de Entra ID.
2. `JwtBearer` valida firma, emisor, audiencia, expiración y *clock skew* (el margen de desfase entre relojes).
3. `OrdersController` resuelve la identidad del usuario y solo devuelve sus pedidos.
4. Un pedido de otro usuario responde `404` para evitar enumeración de objetos.

## Estado de seguridad

Resumen; el detalle con pruebas y límites está en [`docs/capacidades.md`](docs/capacidades.md).

| Control | Estado |
|---|---|
| Validación JWT y autorización por usuario | Implementado; la criptografía requiere tenant real |
| BOLA/IDOR, *mass assignment*, CORS, *rate limit* y límites | Implementado |
| Aviso de secretos antes del commit (hook local) | Parcial: opt-in por clon |
| Contrato de error único (`400`, `404`, `500`) sin fugas en el `500` | Implementado |
| Arquitectura por capas verificada con pruebas | Implementado |
| Outbox transaccional y consumidor idempotente | Implementado (local) |
| Publicación en Service Bus (emulador local) | Validado en local |
| Function desplegada | Diseño |
| Scopes/roles granulares | Pendiente |
| Despliegue real en Azure | Pendiente |

> **Criterio de rigor:** «Implementado» significa que el código o la configuración existe y se ha ejecutado localmente o en CI. «Pendiente» significa que no se ha validado y no se presenta como terminado.

## Estructura

```text
lab-api-local-azure/
├── EcommerceApi.Domain/        # Agregados, value objects, eventos, invariantes
├── EcommerceApi.Application/   # Comandos, queries, handlers, puertos y consumidor
├── EcommerceApi.Infrastructure/# SQLite, outbox, dispatcher e idempotencia
├── EcommerceApi.Contracts/     # OrderCreatedV1 compartido con la Function
├── EcommerceApi/               # Host HTTP: controladores, auth, composición
├── functions/OrderProcessor/   # Function aislada: trigger de Service Bus
├── tests/                      # Unitarias, arquitectura, infraestructura e integración
├── eng/                        # Emulador de Service Bus y herramienta de humo
├── infra/                      # Bicep: VNet, NSG, Key Vault, App Service, PE
├── docs/                       # Referencia, capacidades, ADR y diseños
├── docker-compose.yml          # Arranque local con compose
└── .github/workflows/          # Pipeline DevSecOps
```

## Puesta en marcha

Requisitos: .NET SDK 10.0 y, opcionalmente, Docker. Para validar tokens reales hace falta un tenant de Entra ID. La restauración necesita una credencial de solo lectura para GitHub Packages, porque los controles compartidos vienen de un feed privado (ver [Credenciales del feed](docs/credenciales.md)).

```bash
cp EcommerceApi/appsettings.Development.example.json EcommerceApi/appsettings.Development.json
# Edita el archivo con tenant, client ID, audience, authority y orígenes CORS
# La credencial de solo lectura ya debe estar en GH_PACKAGES_TOKEN (ver docs/credenciales.md).
dotnet restore EcommerceApi.sln
dotnet run --project EcommerceApi --urls http://localhost:5080
```

- Swagger (solo Development): <http://localhost:5080/swagger>
- Health check: <http://localhost:5080/health>
- Base de datos: `data/ecommerce.db`, creada al arrancar y no versionada.
- Pedidos: `GET /api/orders?limit=20&afterId=42`; las reglas de paginación están en [`docs/persistence.md`](docs/persistence.md).

`appsettings.Development.json` está ignorado por Git. No guardes secretos en `appsettings.json` ni en el repositorio.

### Paquetes compartidos

`LL.Security` y `LL.Hosting` se publican desde [`shared-dotnet`](https://github.com/lanuzalorenzo/shared-dotnet) en el feed de GitHub Packages del propietario. El feed vive en [`NuGet.config`](NuGet.config); el usuario es el propietario y la contraseña sale de `GH_PACKAGES_TOKEN`, que debe ser una credencial de solo lectura guardada en el gestor de secretos. No se llama `GITHUB_TOKEN` porque `gh` y Git le dan prioridad y un token sin `repo` deja los push en `403`. El criterio completo, la separación entre identidad y automatización, la rotación y el caso de Dependabot están en [`docs/credenciales.md`](docs/credenciales.md).

```bash
# Con la credencial de solo lectura ya exportada por el shell:
dotnet restore EcommerceApi.sln
```

GitHub Packages exige autenticación incluso para paquetes públicos, así que un clon sin token no restaura. En CI el token lo aporta el propio workflow con `packages: read`. Para construir la imagen, el token entra como secreto de BuildKit y no queda en ninguna capa:

```bash
docker build --secret id=github_token,env=GH_PACKAGES_TOKEN -t ecommerce-api -f Dockerfile .
```

### Compilación y versiones

`Directory.Build.props` fija la configuración común de compilación —`Nullable`, `ImplicitUsings`, avisos como errores y auditoría NuGet— y `Directory.Packages.props` declara una sola vez la versión de cada paquete. Los `.csproj` solo declaran qué referencian y sus ajustes propios, sin repetir `Nullable`, `ImplicitUsings` ni `Version`. La decisión y sus límites están en [ADR-0008](docs/adr/0008-configuracion-de-compilacion-y-paquetes-centralizada.md).

### Pruebas

```bash
dotnet test EcommerceApi.sln
```

Cuatro suites (dominio y handlers, arquitectura, infraestructura sobre SQLite e integración HTTP): **91 pruebas**. Las de integración usan un esquema de autenticación de test y cada factory crea su propia base temporal. El desglose está en [`docs/capacidades.md`](docs/capacidades.md#1-verificación-en-un-bloque).

### Hook local de secretos

El pipeline escanea el historial completo en cada push a `main` y en cada pull
request, pero ese aviso llega después del commit. El repositorio incluye un hook
`pre-commit` que pasa Gitleaks sobre lo preparado y aborta el commit si Gitleaks
falla:

```bash
scripts/install-git-hooks.sh
```

Escribe `core.hooksPath=scripts/hooks` en la configuración local del clon, así
que hay que ejecutarlo una vez por clon. La ruta es relativa: el hook no se
copia, se lee del repositorio y cada worktree usa el suyo. Si la rama activa no
tiene `scripts/hooks`, Git no ejecuta ningún hook; `--check` lo detecta, y
también falla si falta Gitleaks o es anterior a 8.19. `--uninstall` borra el
ajuste local.

Es un eco rápido, no la barrera: si Gitleaks no está instalado, avisa y deja
pasar el commit, porque quien decide es el CI. Los falsos positivos se acotan en
`.gitleaks.toml`, no con `--no-verify`.

### Docker

Dos comandos, desde la raíz del repositorio. La credencial de solo lectura del
feed debe estar ya exportada (ver [Credenciales del feed](docs/credenciales.md));
los comandos construyen y comprueban:

```bash
docker compose up --build -d
curl -i http://localhost:8080/health
```

El compose construye la imagen con el `Dockerfile`, publica el `8080` y monta un volumen nombrado en `/app/data`, donde la API escribe la base SQLite; `docker compose down` lo conserva y `docker compose down -v` lo borra con la base. El contenedor corre sin root (`USER $APP_UID`, UID 1654), escucha en `8080` y no lleva SDK ni código fuente. El porqué de cada decisión está en [ADR-0006](docs/adr/0006-imagen-de-contenedor.md).

Comprobado el 2026-11-03 con Docker 29.1.3 y Compose 2.40.3: `/health` devuelve `200 OK`, el proceso del contenedor corre como UID 1654 y la base conserva tamaño y fecha tras `docker compose down` y `up -d`.

Sin compose, una alternativa equivalente por separado (usa otra etiqueta y otro
volumen, así que no comparte base con el compose):

```bash
docker build --secret id=github_token,env=GH_PACKAGES_TOKEN -t ecommerce-api .
docker run --rm -p 8080:8080 -v ecommerce-data:/app/data ecommerce-api
```

## Autenticación con Entra ID

El registro en el directorio, los scopes, la configuración que lee la API y los errores comunes están en [`docs/entra-id.md`](docs/entra-id.md). Las pruebas con tokens reales y BOLA con dos identidades, en [`docs/azure-ad-tests.md`](docs/azure-ad-tests.md).

La API valida `issuer`, `audience`, `lifetime` y firma. La autorización granular por scopes/roles está documentada como trabajo pendiente; hoy todos los endpoints requieren un usuario autenticado y los pedidos se filtran por identidad.

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

Los resultados SARIF se publican en la pestaña **Security → Code scanning**. La pipeline completa finalizó en verde el **2026-10-19** (run `39243360276`) con las entonces 10 pruebas de integración; la suite actual amplía esa cobertura.

La política de dependencias, los plazos por severidad y el estado del primer lote agrupado están en [`docs/dependencias.md`](docs/dependencias.md).

## Infraestructura como código

`infra/main.bicep` define una arquitectura de referencia con VNet segmentada, NSG con denegación por defecto, Key Vault con RBAC y *purge protection*, Private Endpoints y Private DNS, App Service Linux .NET 10 con HTTPS only y FTPS deshabilitado, identidad gestionada y diagnóstico a Log Analytics. **La plantilla no se ha desplegado**: se valida con `bicep build` y Trivy config en CI. El detalle está en [`infra/README.md`](infra/README.md).

## Documentación

**Referencia y evidencia**

| Documento | Qué aporta |
|---|---|
| [Referencia de la API](docs/api.md) | Endpoints, contratos, errores, límites y pruebas manuales. Implementado |
| [Capacidades y evidencia](docs/capacidades.md) | Qué puede afirmarse, con qué prueba y qué queda fuera. Implementado |
| [Persistencia y consultas](docs/persistence.md) | Esquema, paginación por cursor y planes de consulta. Implementado |
| [Entra ID](docs/entra-id.md) | Registro, configuración, scopes y errores comunes. Implementado |
| [Pruebas con tokens](docs/azure-ad-tests.md) | Token real, BOLA con dos identidades y checklist |
| [Threat model](docs/threat-model-appsec.md) | STRIDE, OWASP API Top 10 y riesgos residuales |
| [Dependencias](docs/dependencias.md) | Dueño, plazos por severidad y evidencia del lote |
| [Credenciales del feed](docs/credenciales.md) | Credencial de solo lectura, rotación y límite de Dependabot. Parcial |
| [Emulador de Service Bus](docs/servicebus-emulator.md) | Arranque, humo y límites del emulador local. Validado en local y en CI |
| [Gobernanza del repositorio](docs/repo-governance.md) | PR, checks obligatorios y por qué no hay bypass |

**Diseño**

| Documento | Qué aporta |
|---|---|
| [App Service](docs/app-service.md) | Plataforma de ejecución, Bicep frente al pipeline, red, identidad y coste. Diseño |
| [Azure Functions](docs/functions.md) | Cómputo asíncrono, idempotencia y hosting. Parcial: probado en local, sin despliegue |
| [Integración de IA](docs/ai-integration.md) | Gateway, controles de seguridad y evaluación. Diseño |
| [Registro de ADR](docs/adr/README.md) | Decisiones, alternativas, patrones y evidencia |

## Relación con el portfolio

| Módulo | Relación |
|---|---|
| [07 — DevSecOps](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/07-devsecops) | Pipeline, SAST, SCA, secretos e IaC |
| [08 — Redes Azure](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/08-seguridad-red-azure) | VNet, NSG, Private Endpoints |
| [09 — Sentinel](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/09-microsoft-sentinel) | Diagnóstico y analítica KQL |
| [11 — Hacking ético de APIs](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/11-hacking-etico-api) | BOLA/BFLA, JWT y validación ofensiva |
| [12 — Forense cloud](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/12-forense-cloud-respuesta) | Evidencia y respuesta en Azure |

## Limitaciones conocidas

- SQLite de laboratorio: un solo escritor, sin backups ni alta disponibilidad, y esquema con `CREATE TABLE IF NOT EXISTS`, sin migraciones versionadas.
- Service Bus y la Function no están desplegados: el publicador por defecto registra el mensaje en lugar de entregarlo a un broker.
- El outbox es *at-least-once*: la garantía efectiva depende de la idempotencia del consumidor, que sí está probada.
- Sin microservicios, sin scopes/roles aplicados endpoint a endpoint y sin WAF/API Management desplegado.
- Con una prueba puntual de carga, sin caos ni prueba de resistencia; la plantilla Bicep no se ha desplegado y su validación es estática.
- La gobernanza de `main` se apoya en PR y siete checks, sin revisor humano; el detalle está en [`docs/repo-governance.md`](docs/repo-governance.md).

Estas limitaciones son deliberadas y se declaran para no confundir un laboratorio con un sistema productivo.

## Licencia

Distribuido bajo licencia [MIT](LICENSE).
