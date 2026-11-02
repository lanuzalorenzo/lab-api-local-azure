# EcommerceApi — proyecto .NET 10

Es la tienda del laboratorio: catálogo y pedidos de mentira, con los mismos controles de acceso que se pondrían en una tienda real. Este archivo es la guía rápida de quien va a ejecutar y tocar el proyecto; el contrato de la API y las decisiones están en la documentación principal.

API REST de laboratorio con autenticación Microsoft Entra ID, autorización por usuario, DTOs de entrada y arquitectura por capas. La documentación principal está en el [README raíz](../README.md); este archivo resume el proyecto y evita duplicar guías desactualizadas.

## Estructura

```text
EcommerceApi/
├── Configuration/
│   └── AzureAdOptions.cs
├── Controllers/
│   ├── ProductsController.cs
│   └── OrdersController.cs
├── ErrorHandling/
│   └── DomainExceptionHandler.cs   # DomainException -> 400 ProblemDetails
├── Models/
│   ├── CreateProductRequest.cs
│   └── CreateOrderRequest.cs
├── Program.cs                  # Pipeline HTTP y composition root
├── appsettings.json
├── appsettings.Development.example.json
├── EcommerceApi.csproj
└── EcommerceApi.http
```

El dominio, los casos de uso y los adaptadores viven en proyectos propios:

```text
EcommerceApi.Domain/          # Order, Product, Money, eventos, invariantes
EcommerceApi.Application/     # Comandos, queries, handlers, puertos y consumidor
EcommerceApi.Infrastructure/  # SQLite, outbox, dispatcher e idempotencia
EcommerceApi.Contracts/       # OrderCreatedV1 compartido con la Function
```

Dependencias: `Domain` ← `Application` ← `Infrastructure` ← host. La regla se verifica en `tests/EcommerceApi.Architecture.Tests`.

## Ejecución

```bash
cp appsettings.Development.example.json appsettings.Development.json
# Edita appsettings.Development.json con tu tenant y client ID
dotnet run --urls http://localhost:5080
```

- Swagger: <http://localhost:5080/swagger>
- Health: <http://localhost:5080/health>
- Base de datos: `data/ecommerce.db` (ignorada por Git)

## Pruebas

```bash
dotnet test ../EcommerceApi.sln
```

## Documentación

- [Referencia de la API](../docs/api.md)
- [Decisiones de arquitectura](../docs/adr/README.md)
- [Entra ID: registro y configuración](../docs/entra-id.md)
- [Pruebas con tokens](../docs/azure-ad-tests.md)
- [Threat model](../docs/threat-model-appsec.md)

## Alcance

Incluye autenticación, autorización por usuario, validación de entrada, CORS, rate limiting, DDD táctico, CQRS ligero en pedidos, persistencia SQLite, outbox transaccional y consumidor idempotente. No incluye Service Bus ni Function desplegados, roles/scopes granulares, observabilidad persistente desplegada, microservicios ni despliegue real en Azure. La captura de logs y la telemetría están declaradas en la plantilla y el código, sin despliegue.

## Licencia

MIT. Consulta [LICENSE](../LICENSE).
