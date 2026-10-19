# EcommerceApi — proyecto .NET 10

API REST de laboratorio con autenticación Microsoft Entra ID, autorización por
usuario, DTOs de entrada y pruebas de integración.

La documentación principal está en el [README raíz](../README.md). Este archivo
resume el proyecto y evita duplicar guías desactualizadas.

## Estructura

```text
EcommerceApi/
├── Configuration/
│   └── AzureAdOptions.cs
├── Controllers/
│   ├── ProductsController.cs
│   └── OrdersController.cs
├── Models/
│   ├── CreateProductRequest.cs
│   ├── Order.cs
│   └── Product.cs
├── Services/
│   └── InMemoryStore.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.example.json
├── EcommerceApi.csproj
└── EcommerceApi.http
```

## Ejecución

```bash
cp appsettings.Development.example.json appsettings.Development.json
# Edita appsettings.Development.json con tu tenant y client ID
dotnet run --urls http://localhost:5080
```

- Swagger: <http://localhost:5080/swagger>
- Health: <http://localhost:5080/health>

## Pruebas

```bash
dotnet test ../tests/EcommerceApi.Tests/EcommerceApi.Tests.csproj
```

## Documentación

- [Configuración de Entra ID](../docs/azure-ad-config.md)
- [Registro de la aplicación](../docs/azure-ad-register.md)
- [Pruebas con tokens](../docs/azure-ad-tests.md)
- [Pruebas funcionales](../docs/tests.md)
- [Threat model](../docs/threat-model-appsec.md)

## Alcance

Incluye autenticación, autorización por usuario, validación de entrada, CORS,
rate limiting y pruebas. No incluye base de datos, roles/scopes granulares,
observabilidad persistente ni despliegue real en Azure.

## Licencia

MIT. Consulta [LICENSE](../LICENSE).
