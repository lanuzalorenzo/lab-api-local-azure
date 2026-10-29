# Pruebas funcionales de la API

Todas las rutas de negocio requieren un token JWT válido. El endpoint `/health` es anónimo. La URL base de ejemplo es `http://localhost:5080`.

## Prerrequisitos

```bash
dotnet run --project EcommerceApi --urls http://localhost:5080
export API_URL="http://localhost:5080"
export TOKEN="<token-de-laboratorio>"
```

## Productos

### Listar productos

```bash
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/products"
```

Esperado: `200 OK` con un array de productos.

### Obtener un producto

```bash
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/products/1"
```

Esperado: `200 OK`.

### Producto inexistente

```bash
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/products/999999"
```

Esperado: `404 Not Found`.

### Crear un producto

```bash
curl -i -X POST "${API_URL}/api/products" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":49.99,"stock":20}'
```

Esperado: `201 Created`.

### Validación de entrada

```bash
curl -i -X POST "${API_URL}/api/products" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"","price":-1,"stock":-1}'
```

Esperado: `400 Bad Request`.

### Intento de mass assignment

```bash
curl -i -X POST "${API_URL}/api/products" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"id":999999,"name":"Intento","price":10,"stock":1}'
```

Esperado: `201 Created` con un `id` asignado por el servidor, distinto de `999999`. El DTO de entrada no acepta `id`.

## Pedidos

### Crear un pedido

```bash
curl -i -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[1,2]}'
```

Esperado: `201 Created` con `id`, `productIds`, `total` y `createdAt`.

### Listar los pedidos propios

```bash
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders"
```

Esperado: `200 OK` con solo los pedidos del usuario autenticado.

### Pedido vacío o producto inexistente

```bash
curl -i -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[]}'

curl -i -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[999999]}'
```

Esperado: `400 Bad Request` en ambos casos.

### BOLA: otro usuario no puede leer el pedido

Con dos tokens de usuarios distintos:

```bash
# Usuario A crea un pedido
ORDER_ID=$(curl -s -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN_A}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[1]}' | jq -r '.id')

# Usuario A: 200
curl -i -H "Authorization: Bearer ${TOKEN_A}" "${API_URL}/api/orders/${ORDER_ID}"

# Usuario B: 404
curl -i -H "Authorization: Bearer ${TOKEN_B}" "${API_URL}/api/orders/${ORDER_ID}"
```

## Pruebas automatizadas

```bash
dotnet test EcommerceApi.sln
```

| Suite | Cobertura |
|---|---|
| `tests/EcommerceApi.UnitTests` | Invariantes de `Order`, `OrderLine`, `Money` y `Product`; handlers de comando y query; idempotencia del consumidor |
| `tests/EcommerceApi.Architecture.Tests` | Regla de dependencias entre capas y agregados sin setters públicos |
| `tests/EcommerceApi.Infrastructure.Tests` | Atomicidad del outbox, rollback, reintentos, dead-letter, dispatcher (scope asíncrono), idempotencia, paginación por cursor, rango de fechas y uso del índice sobre SQLite real |
| `tests/EcommerceApi.Tests` | Comportamiento HTTP completo, incluido el contrato paginado del listado |

### Ciclo TDD: rojo → verde

Las pruebas describen el comportamiento antes de la implementación. El ejemplo reproducible que queda en el repositorio es el del dispatcher del outbox:

1. **Rojo:** se añadió `Dispatcher_ProcessesPendingMessages_WithoutDisposalErrors`, que arranca el `OutboxDispatcher` con un `IServiceScopeFactory` real y exige que no se registre ningún error. Con `CreateScope()` falla: `'SqliteSession' type only implements IAsyncDisposable. Use DisposeAsync`.
2. **Verde:** el dispatcher pasa a `CreateAsyncScope()` y la prueba deja de fallar; la suite completa queda en verde.
3. La prueba se conserva como regresión: si alguien vuelve al scope síncrono, el fallo reaparece en CI.

El mismo orden se siguió en el dominio: primero las pruebas de invariantes (`Order`, `OrderLine`, `Money`), después el agregado que las satisface.

Las pruebas de integración usan `X-Test-User` para simular identidades y cubren:

- `401` sin token.
- `400` por validación.
- *Mass assignment* ignorado.
- Aislamiento de pedidos entre usuarios (BOLA).
- `health` anónimo.
- Contrato paginado: `limit`, `afterId` y rango de fechas.

### Listado paginado

```bash
# Primera página
curl -s -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders?limit=2"

# Siguiente página con el nextCursor devuelto
curl -s -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders?limit=2&afterId=42"

# Rango de fechas: from inclusivo, to exclusivo
curl -s -H "Authorization: Bearer ${TOKEN}" \
  "${API_URL}/api/orders?from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z"
```

## Cuerpo de error

Los `400`, `404` y `500` responden `application/problem+json`. El mensaje legible
viaja en `detail` (o en `errors` si es validación de campos) y el `traceId`
permite correlacionar con el log del servidor.

```bash
# 404 con ProblemDetails
curl -s -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/orders/999999"
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Order with ID 999999 not found",
  "traceId": "00-af7d...-00"
}
```

El contrato completo está en [api.md](api.md#6-errores).

## Códigos HTTP esperados

| Método | Endpoint | Código | Descripción |
|---|---|---|---|
| GET | `/health` | 200 | Servicio sano, sin token |
| GET | `/api/products` | 200 / 401 | Lista / sin token |
| GET | `/api/products/{id}` | 200 / 404 | Producto / inexistente |
| POST | `/api/products` | 201 / 400 | Creado / validación |
| GET | `/api/orders` | 200 / 400 / 401 | Página de pedidos propios / parámetros inválidos / sin token |
| GET | `/api/orders/{id}` | 200 / 404 | Propio / ajeno o inexistente |
| POST | `/api/orders` | 201 / 400 | Creado / validación |
| Cualquiera | Cualquiera | 429 | Rate limit excedido |
