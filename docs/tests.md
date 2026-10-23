# Pruebas funcionales de la API

> **En fácil:** estas pruebas comprueban que cada endpoint responde lo que debe cuando el token es válido, falta o pertenece a otro usuario. Los comandos de `curl` se pueden copiar y ejecutar contra la API local.

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
| `tests/EcommerceApi.Infrastructure.Tests` | Atomicidad del outbox, rollback, reintentos, dead-letter, dispatcher (scope asíncrono) e idempotencia sobre SQLite real |
| `tests/EcommerceApi.Tests` | Comportamiento HTTP completo |

### Ciclo TDD: rojo → verde

Las pruebas describen el comportamiento antes de la implementación. El ejemplo reproducible que queda en el repositorio es el del dispatcher del outbox:

1. **Rojo:** se añadió `Dispatcher_ProcessesPendingMessages_WithoutDisposalErrors`, que arranca el `OutboxDispatcher` con un `IServiceScopeFactory` real y exige que no se registre ningún error. Con `CreateScope()` falla: `'SqliteSession' type only implements IAsyncDisposable. Use DisposeAsync`.
2. **Verde:** el dispatcher pasa a `CreateAsyncScope()` y la prueba deja de fallar; la suite completa queda en **67 pruebas**.
3. La prueba se conserva como regresión: si alguien vuelve al scope síncrono, el fallo reaparece en CI.

El mismo orden se siguió en el dominio: primero las pruebas de invariantes (`Order`, `OrderLine`, `Money`), después el agregado que las satisface.

Las pruebas de integración usan `X-Test-User` para simular identidades y cubren:

- `401` sin token.
- `400` por validación.
- *Mass assignment* ignorado.
- Aislamiento de pedidos entre usuarios (BOLA).
- `health` anónimo.

## Códigos HTTP esperados

| Método | Endpoint | Código | Descripción |
|---|---|---|---|
| GET | `/health` | 200 | Servicio sano, sin token |
| GET | `/api/products` | 200 / 401 | Lista / sin token |
| GET | `/api/products/{id}` | 200 / 404 | Producto / inexistente |
| POST | `/api/products` | 201 / 400 | Creado / validación |
| GET | `/api/orders` | 200 / 401 | Pedidos propios / sin token |
| GET | `/api/orders/{id}` | 200 / 404 | Propio / ajeno o inexistente |
| POST | `/api/orders` | 201 / 400 | Creado / validación |
| Cualquiera | Cualquiera | 429 | Rate limit excedido |
