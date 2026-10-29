# Referencia de la API

> **Estado:** Implementado · verificado el 2026-10-29 contra `main`.
> **Ámbito:** contrato HTTP de `EcommerceApi` (endpoints, autenticación, cuerpos, errores y límites).
> **Evidencia:** `EcommerceApi/Controllers`, `EcommerceApi/Models`, `EcommerceApi.Application` y `tests/EcommerceApi.Tests`.
> **Relacionada con:** [Pruebas funcionales](tests.md), [Persistencia y paginación](persistence.md), [Configuración de Entra ID](azure-ad-config.md), [ADR-0005](adr/0005-observabilidad.md).

Este documento describe lo que la API hace hoy, no lo que debería hacer. Los
comportamientos no implementados se listan al final para que no se confundan con
el contrato vigente.

## 1. Convenciones

| Aspecto | Valor |
|---|---|
| URL base local | `http://localhost:5080` |
| URL base en contenedor | `http://localhost:8080` (`ASPNETCORE_URLS`) |
| Formato | JSON UTF-8; `Content-Type: application/json` en escrituras |
| Nombres de campo | `camelCase` (serialización por defecto de ASP.NET Core) |
| Fechas | ISO 8601 en UTC, por ejemplo `2026-10-29T09:15:00Z` |
| Importes | Decimal con dos posiciones; internamente se guardan en céntimos enteros |
| Versionado de ruta | No existe; el contrato se mantiene estable mientras no se anuncie lo contrario |
| Autenticación | `Authorization: Bearer <access token>` en todo `/api/*` |
| Endpoint anónimo | Solo `GET /health` |

## 2. Autenticación y autorización

El host actúa como *resource server*: no emite tokens, los valida. La configuración
está en `EcommerceApi/Program.cs:114-129`.

| Comprobación | Detalle |
|---|---|
| Firma | Claves públicas del `Authority` (`JWKS`) |
| Emisor | `ValidateIssuer` contra `AzureAd:Authority` |
| Audiencia | `ValidateAudience` contra `AzureAd:Audience` |
| Vigencia | `ValidateLifetime` con `ClockSkew` de 2 minutos |
| Algoritmo | El aceptado por `JwtBearer` por defecto |

Identidad del llamante (`EcommerceApi/Controllers/OrdersController.cs:38-42`):

1. `ClaimTypes.NameIdentifier` (ASP.NET Core mapea `sub` aquí).
2. `oid`.
3. `sub`.

Si el token no aporta ninguno de los tres, el controlador lanza
`InvalidOperationException` y la petición termina en `500`. No es un caso esperado
con tokens de Entra ID, pero conviene saberlo si se usa un emisor distinto.

Autorización:

- Todos los endpoints de negocio exigen usuario autenticado; sin token la
  respuesta es `401` con cabecera `WWW-Authenticate`.
- No hay scopes ni roles: cualquier token válido accede a todos los endpoints.
  Los pedidos se filtran por identidad del usuario.
- Un pedido de otro usuario responde `404`, no `403`, para no revelar su
  existencia (protección frente a enumeración de objetos).

Cómo obtener un token de laboratorio: [docs/azure-ad-tests.md](azure-ad-tests.md).
El flujo interactivo con PKCE lo ejecuta el cliente; este repositorio no lo
implementa.

## 3. Endpoints

| Método | Ruta | Cuerpo | Éxito | Errores documentados |
|---|---|---|---|---|
| `GET` | `/api/products` | — | `200` `ProductView[]` | `401` |
| `GET` | `/api/products/{id}` | — | `200` `ProductView` | `401`, `404` |
| `POST` | `/api/products` | `CreateProductRequest` | `201` + `Location` | `400`, `401`, `413` |
| `GET` | `/api/orders` | — | `200` `OrderPage` | `400`, `401` |
| `GET` | `/api/orders/{id}` | — | `200` `OrderSummary` | `401`, `404` |
| `POST` | `/api/orders` | `CreateOrderRequest` | `201` + `Location` | `400`, `401`, `413` |
| `GET` | `/health` | — | `200` texto `Healthy` | — |

`{id}` es un entero positivo. El `201` incluye `Location` con la ruta del recurso
creado (`CreatedAtAction`).

## 4. Contratos

### 4.1. `ProductView` (respuesta)

| Campo | Tipo | Notas |
|---|---|---|
| `id` | entero | Asignado por la base de datos |
| `name` | cadena | Máximo 100 caracteres, sin espacios sobrantes |
| `price` | decimal | Entre `0` y `1.000.000` |
| `stock` | entero | Entre `0` y `1.000.000` |

### 4.2. `CreateProductRequest` (entrada)

| Campo | Tipo | Validación | Respuesta si falla |
|---|---|---|---|
| `name` | cadena | `[Required]`, longitud 1..100 | `400` `ValidationProblemDetails` |
| `price` | decimal | `[Range(0, 1000000)]` | `400` `ValidationProblemDetails` |
| `stock` | entero | `[Range(0, 1000000)]` | `400` `ValidationProblemDetails` |

Un `name` compuesto solo por espacios lo rechaza la validación del transporte
(`[Required]`), no el dominio. Las reglas del dominio repiten los mismos límites
como segunda barrera; con el contrato HTTP actual, `DataAnnotations` las cubre
antes.

### 4.3. `OrderSummary` (respuesta)

| Campo | Tipo | Notas |
|---|---|---|
| `id` | entero | Identificador del pedido |
| `createdAt` | fecha UTC | Momento de creación |
| `productIds` | entero[] | Productos incluidos, en el orden de la petición |
| `total` | decimal | Suma de líneas con el precio capturado al crear el pedido |

El precio de cada línea se captura en el momento de la compra: un cambio
posterior en el catálogo no altera pedidos ya creados.

### 4.4. `OrderPage` (respuesta)

| Campo | Tipo | Notas |
|---|---|---|
| `items` | `OrderSummary[]` | Página actual |
| `nextCursor` | entero o `null` | Identificador a pasar como `afterId`; `null` en la última página |

### 4.5. `CreateOrderRequest` (entrada)

| Campo | Tipo | Validación | Respuesta si falla |
|---|---|---|---|
| `productIds` | entero[] | `[Required]`, `[MinLength(1)]`, `[MaxLength(20)]` | `400` `ValidationProblemDetails` |

Errores de negocio adicionales:

- Producto inexistente: `400` con `{ "message": "Products not found: 7, 9" }`.
- Identificadores duplicados: se deduplican antes de crear el pedido y no
  devuelven error. `{"productIds":[1,1]}` crea un pedido con una sola línea.
- Cada línea es una unidad del producto; no hay campo de cantidad.

## 5. Pedidos: paginación y filtros

`GET /api/orders` usa paginación por cursor (*keyset*), no `offset`. El detalle del
diseño y de los planes de consulta está en [docs/persistence.md](persistence.md).

| Parámetro | Tipo | Por defecto | Regla |
|---|---|---|---|
| `limit` | entero | `20` | Entre `1` y `100` |
| `afterId` | entero | ausente | `>= 0`; devuelve pedidos con `id` mayor |
| `from` | fecha | ausente | Límite inferior **inclusivo** |
| `to` | fecha | ausente | Límite superior **exclusivo** |

Si `from` y `to` llegan juntos, `from` debe ser anterior a `to`. Cada
incumplimiento devuelve `400` con `{ "message": ... }`.

Ejemplo:

```bash
curl -s -H "Authorization: Bearer ${TOKEN}" \
  "${API_URL}/api/orders?limit=2&afterId=0&from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z"
```

```json
{
  "items": [
    { "id": 1, "createdAt": "2026-10-29T09:15:00Z", "productIds": [1, 2], "total": 79.98 }
  ],
  "nextCursor": 1
}
```

Para la página siguiente se repite la llamada con `afterId=<nextCursor>`. El
cursor es el `id` del último elemento, no un índice: insertar pedidos nuevos no
desplaza las páginas ya leídas. El orden es ascendente por `id`.

`GET /api/products` no pagina ni filtra: devuelve el catálogo completo. Es una
decisión de alcance, no un olvido; ver la sección 10.

## 6. Errores

La API no usa un único formato. Conviene saberlo antes de escribir un cliente.

| Código | Cuándo | Cuerpo | Origen |
|---|---|---|---|
| `400` | Validación de entrada (`DataAnnotations`) | `ValidationProblemDetails` con `errors` y `traceId` | `[ApiController]` + `AddProblemDetails` |
| `400` | Parámetro de consulta inválido o producto inexistente | `{ "message": "..." }` | Controlador |
| `400` | Invariante de dominio | `{ "message": "..." }` | `DomainExceptionHandler` (red de seguridad) |
| `401` | Token ausente, inválido o expirado | Vacío + `WWW-Authenticate` | `JwtBearer` |
| `403` | Autenticado sin permiso | No se produce hoy: no hay políticas ni roles | — |
| `404` | Recurso inexistente o pedido de otro usuario | `{ "message": "..." }` | Controlador |
| `413` | Cuerpo mayor de 16 KB en `POST` | Vacío | Kestrel, por `[RequestSizeLimit]` |
| `429` | Más de 120 peticiones por minuto y IP | Vacío | `RateLimiter` |
| `500` | Excepción no controlada | `application/problem+json` con `title`, `status` y `traceId` | `UseExceptionHandler` |

Los dos formatos de `400` se distinguen por `Content-Type`:
`application/problem+json` para validación y `application/json` para mensajes de
negocio.

El `400` de dominio es una red de seguridad, no un camino principal: el agregado
repite los límites que ya valida el transporte y el caso de uso deduplica los
identificadores, así que con los endpoints actuales no se alcanza. Existe para
que una ruta futura que rompa una invariante responda `400` en lugar de `500`.

### 6.1. Validación de entrada

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["The Name field is required."]
  },
  "traceId": "00-3f2a...-01"
}
```

El diccionario `errors` usa el nombre de la propiedad en PascalCase y puede traer
varios mensajes por campo.

### 6.2. Error de negocio

```json
{ "message": "Products not found: 7, 9" }
```

Es un objeto anónimo, no `ProblemDetails`. El mensaje está pensado para el
cliente: describe qué regla se incumplió sin exponer detalles internos. Hoy se
emite para parámetros de consulta inválidos, productos inexistentes y, como red
de seguridad, para una `DomainException`.

### 6.3. Error no controlado

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-8c1d...-01"
}
```

No incluye mensaje de la excepción ni traza. El detalle queda en el log del
servidor, a nivel `Error`, con la categoría
`Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`. El `traceId` es el
único puente entre la respuesta y el log. En esta fase no hay sink persistente:
ver [ADR-0005](adr/0005-observabilidad.md).

El contrato está fijado por `tests/EcommerceApi.Tests/ErrorContractTests.cs`, que
comprueba el código, el `Content-Type`, la presencia de `traceId` y la ausencia
de mensaje y tipo de excepción.

Regla para el cliente: mirar `Content-Type`. Si es `application/problem+json`,
leer `title` y `traceId`; si es `application/json`, leer `message`.

## 7. Límites operativos

| Límite | Valor | Dónde |
|---|---|---|
| Peticiones por IP | 120 por minuto, ventana fija, sin cola | `Program.cs:100-112` |
| Cuerpo en `POST` | 16 KB | `OrdersController`, `ProductsController` |
| Líneas por pedido | 20 | `Order.MaxLines` |
| Tamaño de página | 100 | `OrderQuery.MaxLimit` |
| Longitud de nombre de producto | 100 | `Product.NameMaxLength` |
| Precio y stock | 0 a 1.000.000 | `Product.MaxPrice`, `Product.MaxStock` |

El rate limit es un control de laboratorio por IP; detrás de un proxy inverso
todas las peticiones pueden compartir la misma IP de origen.

## 8. Swagger, health y diagnóstico

- Swagger UI y el documento OpenAPI solo se exponen en `Development`
  (`Program.cs:141-145`); en `Production` no hay endpoint de documentación.
- `GET /health` es anónimo y devuelve `200` con el texto `Healthy`.
- Cabeceras de seguridad en todas las respuestas: `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` (`Program.cs:159-166`).
- CORS: lista vacía deniega cualquier origen; solo se permite lo declarado en
  `Cors:AllowedOrigins`.

## 9. Flujo completo

```bash
export API_URL="http://localhost:5080"
export TOKEN="<token-de-laboratorio>"

# 1. Crear producto
curl -i -X POST "${API_URL}/api/products" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":49.99,"stock":20}'

# 2. Crear pedido
curl -i -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[1]}'

# 3. Listar los pedidos propios
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders"

# 4. Leer un pedido propio
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders/1"
```

Resultados esperados: `201`, `201`, `200` con `items`, `200`. Con un token de otro
usuario, el paso 4 devuelve `404`.

## 10. Lo que la API no ofrece

Listado para evitar suposiciones al integrar:

- Versionado de API ni negociación de contenido.
- `PUT`, `PATCH` o `DELETE`: el catálogo solo crea y lee; los pedidos no se modifican.
- Clave de idempotencia en `POST`: repetir la petición crea otro recurso.
- Error por identificadores repetidos: `productIds` se deduplica en silencio.
- Paginación o búsqueda en `/api/products`.
- `ETag`, `If-Match` ni control de concurrencia optimista en HTTP.
- Idempotencia del consumidor expuesta por HTTP: vive en la Function
  (`ProcessedMessages`), no en la API.
- Scopes, roles o políticas de autorización granular.
- Un formato de error único en los `400`.

## 11. Mapa del código

| Elemento | Fichero |
|---|---|
| Pipeline, auth, CORS, rate limit | `EcommerceApi/Program.cs` |
| Endpoints de catálogo | `EcommerceApi/Controllers/ProductsController.cs` |
| Endpoints de pedidos | `EcommerceApi/Controllers/OrdersController.cs` |
| DTOs de entrada | `EcommerceApi/Models` |
| Contratos de lectura y paginación | `EcommerceApi.Application/Orders/OrderContracts.cs` |
| 400 de dominio | `EcommerceApi/ErrorHandling/DomainExceptionHandler.cs` |
| Invariantes | `EcommerceApi.Domain/Orders`, `EcommerceApi.Domain/Products` |
| Pruebas de contrato HTTP | `tests/EcommerceApi.Tests` |
