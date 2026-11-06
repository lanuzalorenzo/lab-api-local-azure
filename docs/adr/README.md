# Arquitectura: registro de decisiones (ADR)

Cada decisión estructural del repositorio se documenta antes o junto al código que la implementa. La regla es la misma que en el resto del laboratorio: **no se introduce una pieza sin una decisión escrita** y no se presenta como ejecutado lo que solo está diseñado.

## Formato

Cada ADR contiene: contexto, decisión, alternativas descartadas, consecuencias y criterio para revisarla. Una decisión sin criterio de revisión es un dogma.

## Índice

| ADR | Título | Estado |
|---|---|---|
| [0001](0001-monolito-modular-vs-microservicios.md) | Monolito modular con extracción asíncrona, no microservicios por defecto | Aceptada |
| [0002](0002-capas-y-regla-de-dependencias.md) | Capas y regla de dependencias verificada con pruebas | Aceptada |
| [0003](0003-cqrs-ligero-en-pedidos.md) | CQRS ligero aplicado solo a pedidos | Aceptada |
| [0004](0004-persistencia-sqlite-y-outbox.md) | Persistencia SQLite y outbox transaccional | Aceptada |
| [0005](0005-observabilidad.md) | Observabilidad: logs de plataforma y telemetría de aplicación | Aceptada |
| [0006](0006-imagen-de-contenedor.md) | Imagen de contenedor: dos fases, sin root y sin SDK en ejecución | Aceptada |
| [0007](0007-rate-limiting.md) | Rate limiting global por IP como umbral de laboratorio | Aceptada |
| [0008](0008-configuracion-de-compilacion-y-paquetes-centralizada.md) | Configuración de compilación y versiones de paquetes en dos archivos raíz | Aceptada |

## Patrones en el código

Cada patrón que existe se puede señalar en un archivo y comprobar con una prueba. Si una fila se queda sin prueba ni ADR, se retira: una lista de patrones sin evidencia es una lista de vocabulario.

| Patrón | Dónde | Cómo se comprueba |
|---|---|---|
| Puertos y adaptadores (DIP) | `Application/Abstractions/` y adaptadores `Sqlite*`/`ServiceBus*` | `LayerDependencyTests.Application_DoesNotReferenceInfrastructureOrHost` |
| Repository | `SqliteOrderRepository`, `SqliteProductRepository` | `HandleAsync_PersistsTheOrderWithIdentity`, `Commit_PersistsOrderAndOutboxTogether` |
| Unidad de trabajo | `IUnitOfWork`, `SqliteUnitOfWork`; la transacción la abre el handler | `Rollback_LeavesNeitherOrderNorOutboxMessage` |
| Outbox transaccional | `SqliteOutboxStore`, `OutboxPublisher`, `OutboxDispatcher` | `Publisher_DeadLettersMessage_AfterMaxAttempts`, `Dispatcher_ProcessesPendingMessages_WithoutDisposalErrors` |
| CQRS ligero | Solo pedidos: `IOrderReadModel`/`SqliteOrderReadModel` y `IOrderRepository`/`Order` | `GetOrders_WithLimit_ReturnsCursorAndNextPage` |
| Strategy | `IEventPublisher`, con publicador local y de Service Bus según `Publisher:Mode` | Pruebas `Publisher_*`; el adaptador de Service Bus no se ha desplegado |
| Factory Method | `Product.Create`/`Rehydrate`, `Order.Create`/`Rehydrate`, resultados de caso de uso | `Aggregates_DoNotExposePublicSetters` y pruebas de invariantes |
| Fallo esperado como valor | `CreateOrderResult`, `ProcessOrderResult` | `CreateOrder_WithUnknownProduct_ReturnsBadRequest`, `ErrorContractTests` |
| Consumidor idempotente | `IProcessedMessageStore`, `ProcessOrderHandler` | `ProcessedMessageStore_IsIdempotent` |

Los controladores reciben los handlers por constructor y los invocan directamente. No hay `IMediator` ni despacho por reflexión: el grafo se lee en `Program.cs` y lo comprueba el compilador. El coste asumido es que cada controlador repite inyección y no hay un punto único para telemetría transversal.

**Descartado y por qué:** Abstract Factory, Builder y Decorator no existen en el código y no se documentan como si estuvieran; CQRS completo con base de lectura separada añade sincronización sin una necesidad medida ([ADR-0003](0003-cqrs-ligero-en-pedidos.md)); no hay contenedor de servicios propio ni localizador.

## Estados

- **Propuesta**: escrita, pendiente de aceptar.
- **Aceptada**: vigente y reflejada en el código.
- **Sustituida**: reemplazada por otra ADR, que se enlaza.
