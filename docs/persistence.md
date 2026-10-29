# Persistencia y consultas

> **Ámbito:** almacén SQLite del laboratorio, SQL explícito sin ORM.
> **Decisión:** [ADR-0004](adr/0004-persistencia-sqlite-y-outbox.md).
> **Estado:** Implementado. Las reglas de paginación, rango y uso de índice se prueban en `tests/EcommerceApi.Infrastructure.Tests/SqliteOrderReadModelTests.cs` y en `tests/EcommerceApi.Tests/OrdersEndpointsTests.cs`.

## Esquema

| Tabla | Clave primaria | Índices | Uso |
|---|---|---|---|
| `Products` | `Id` | Clave primaria implícita | Catálogo |
| `Orders` | `Id` | `IX_Orders_UserId_Id (UserId, Id)` | Cabecera y listado por propietario |
| `OrderLines` | `(OrderId, ProductId)` | Clave primaria implícita | Líneas; el prefijo `OrderId` cubre la carga por pedido |
| `OutboxMessages` | `Id` | `IX_OutboxMessages_Pending` parcial | Despacho de pendientes |
| `ProcessedMessages` | `MessageId` | Clave primaria implícita | Idempotencia del consumidor |

`Orders.CreatedAt` se guarda como texto ISO-8601 en UTC (`DateTime.ToString("O")`), no como tipo fecha nativo. SQLite no tiene un tipo `datetime` con semántica de zona horaria; el formato elegido ordena lexicográficamente y permite comparar rangos.

El índice anterior `IX_Orders_UserId` se sustituye por `IX_Orders_UserId_Id`. El prefijo `UserId` mantiene cubiertas las consultas que solo filtran por propietario y la segunda columna resuelve el cursor.

## Reglas de consulta

### 1. Columnas explícitas

Las consultas enumeran las columnas que necesitan. No se usa `SELECT *`: evita leer y transportar datos que el modelo de lectura no expone, y permite que un índice cubra la consulta.

### 2. Rangos SARGables

`CreatedAt` se filtra con comparaciones directas sobre la columna:

```sql
WHERE UserId = $userId
  AND CreatedAt >= $from
  AND CreatedAt < $to
```

`from` es inclusivo y `to` es exclusivo. Los parámetros se formatean como UTC ISO-8601, el mismo formato que usa la escritura. No se aplican funciones como `date(CreatedAt)` o `strftime(...)` a la columna: obligan a evaluar cada fila y anulan el índice.

### 3. Paginación por cursor

`GET /api/orders` acepta `limit`, `afterId`, `from` y `to`:

```text
GET /api/orders?limit=20&afterId=42&from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z
```

- `limit` vale entre 1 y 100; por defecto, 20.
- `afterId` es el identificador del último pedido de la página anterior. Omitido o `0` empieza por el principio.
- La respuesta es un sobre con `items` y `nextCursor`. `nextCursor` es nulo en la última página.

```json
{
  "items": [
    { "id": 43, "createdAt": "2026-10-22T10:00:00Z", "productIds": [1], "total": 99.99 }
  ],
  "nextCursor": 43
}
```

La consulta pide `limit + 1` filas para saber si existe una página siguiente y devuelve como máximo `limit`. No se usa `OFFSET`: recorrer y descartar las filas anteriores encarece las páginas profundas.

### 4. Prefijo de índice compuesto

Un índice compuesto se usa por su prefijo izquierdo:

- `(UserId, Id)` cubre `WHERE UserId = ...` y `WHERE UserId = ... AND Id > ...`.
- `(OrderId, ProductId)` cubre `WHERE OrderId = ...`; no cubre `WHERE ProductId = ...` por sí solo.
- No se añade un índice por producto hasta que exista una consulta que lo necesite. Cada índice acelera lecturas y encarece escrituras.

### 5. Sin N+1

El listado ejecuta dos consultas, independientemente del número de pedidos de la página:

1. La página de `Orders`.
2. Las líneas de esos pedidos con `WHERE OrderId IN (...)`.

No se consulta la línea de cada pedido dentro de un bucle. `FindAsync` sigue el mismo patrón: una consulta para la cabecera y otra para sus líneas.

### 6. Importes en céntimos

Los importes se guardan como `INTEGER` en céntimos. SQLite no tiene `DECIMAL` y `REAL` perdería precisión en totales. La conversión a `decimal` se hace en el adaptador, no en la base de datos.

### 7. Filtro de fecha y plan

El índice `(UserId, Id)` resuelve propietario, cursor y orden. El rango de `CreatedAt` se aplica después como predicado residual sobre las filas de ese usuario.

Si el rango de fechas pasa a ser el acceso principal, el siguiente paso es un índice `(UserId, CreatedAt, Id)` y un cursor por `(CreatedAt, Id)`. Es una decisión de diseño, no está implementada: antes hay que medir con datos representativos y revisar el plan.

## Verificación

```bash
dotnet test EcommerceApi.sln --filter "FullyQualifiedName~SqliteOrderReadModelTests"
```

La prueba `KeysetQuery_UsesTheCompositeOwnerIndex` ejecuta `EXPLAIN QUERY PLAN` y exige que el plan mencione `IX_Orders_UserId_Id`. Las pruebas HTTP cubren límite fuera de rango, cursor negativo y rango de fechas invertido.

Consulta manual del plan:

```sql
EXPLAIN QUERY PLAN
SELECT Id, CreatedAt, TotalCents
FROM Orders
WHERE UserId = 'user' AND Id > 0
ORDER BY Id
LIMIT 21;
```

El plan esperado busca en `Orders` usando `IX_Orders_UserId_Id` para resolver `UserId` y el cursor `Id`.

## Límites

- SQLite es de un solo escritor y no sustituye a un motor gestionado. No hay copias de seguridad ni alta disponibilidad.
- El esquema se crea con `CREATE TABLE IF NOT EXISTS`; no hay migraciones versionadas.
- No hay el mismo modelo de estadísticas ni caché de planes que SQL Server, y no se usan Extended Events. Si el laboratorio migra a SQL Server, los índices y las consultas deben revisarse con planes reales; las reglas de esta página siguen siendo válidas, pero `CreatedAt` pasaría a `datetime2`.
- El cursor es por `Id`, no una instantánea. Un pedido insertado entre dos páginas puede aparecer en una página posterior; el listado no promete consistencia de snapshot.

## Referencias

- [ADR-0004](adr/0004-persistencia-sqlite-y-outbox.md): persistencia, outbox y límites del almacén.
- [`tests/EcommerceApi.Infrastructure.Tests`](../tests/EcommerceApi.Infrastructure.Tests): pruebas sobre SQLite real.
- [`tests/EcommerceApi.Tests`](../tests/EcommerceApi.Tests): contrato HTTP del listado.
