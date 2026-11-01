# Patrones de diseño

> **Ámbito:** qué patrones están en el código, dónde y por qué.
> **Decisión:** [ADR-0001](adr/0001-monolito-modular-vs-microservicios.md), [ADR-0002](adr/0002-capas-y-regla-de-dependencias.md), [ADR-0003](adr/0003-cqrs-ligero-en-pedidos.md) y [ADR-0004](adr/0004-persistencia-sqlite-y-outbox.md).
> **Estado:** Implementado. Cada fila cita la prueba o la ADR que la respalda.

Este documento no es un catálogo. Solo recoge los patrones que existen en el código y que se pueden señalar en un archivo concreto. Si una fila se queda sin prueba ni ADR, se retira: una lista de patrones sin evidencia es una lista de vocabulario.

Cada fila responde a tres preguntas: dónde está el patrón, por qué se aplicó ahí y qué lo comprueba. Cuando la respuesta a la tercera es una prueba, se nombra el caso.

## Patrones en uso

| Patrón | Dónde | Por qué ahí | Cómo se comprueba |
|---|---|---|---|
| Puertos y adaptadores (DIP) | `EcommerceApi.Application/Abstractions/` y los adaptadores `Sqlite*` y `ServiceBus*` de `EcommerceApi.Infrastructure/` | La regla de dependencias impide que la capa de aplicación conozca el almacén o el broker. El puerto se declara donde se usa, no donde se implementa | `LayerDependencyTests.Application_DoesNotReferenceInfrastructureOrHost` |
| Repository | `SqliteOrderRepository`, `SqliteProductRepository` | El agregado se carga solo cuando se va a mutar; las lecturas no pasan por él | `CreateOrderHandlerTests.HandleAsync_PersistsTheOrderWithIdentity`, `Commit_PersistsOrderAndOutboxTogether` |
| Unidad de trabajo | `IUnitOfWork` y `SqliteUnitOfWork`; la transacción la abre el handler | El pedido y su mensaje de outbox se confirman juntos o no se confirma ninguno | `Rollback_LeavesNeitherOrderNorOutboxMessage`, `HandleAsync_WritesTheOutboxMessageInTheSameTransaction` |
| Outbox transaccional | `SqliteOutboxStore`, `OutboxPublisher`, `OutboxDispatcher` | Un proceso que cae entre el commit y la publicación no pierde el evento: queda pendiente en la misma base de datos | `Publisher_MarksMessageAsProcessed_WhenPublishingSucceeds`, `Publisher_DeadLettersMessage_AfterMaxAttempts`, `Dispatcher_ProcessesPendingMessages_WithoutDisposalErrors` |
| CQRS ligero | Solo en pedidos: `IOrderReadModel`/`SqliteOrderReadModel` para lectura y `IOrderRepository`/`Order` para escritura | La lectura devuelve una proyección plana sin cargar el agregado; el catálogo no diverge lo suficiente y mantiene CRUD | `GetOrders_WithLimit_ReturnsCursorAndNextPage`, `OrderQueryHandlerTests` |
| Strategy | `IEventPublisher`, con `LoggingEventPublisher` y `ServiceBusEventPublisher`, elegido en `Program.cs` según `Publisher:Mode` | El adaptador local y el de Azure se intercambian en la composición; la capa de aplicación no cambia | Las pruebas `Publisher_*` ejecutan el publicador local; el adaptador de Service Bus no se ha desplegado |
| Factory Method | `Product.Create`/`Rehydrate`, `Order.Create`/`Rehydrate`, `CreateOrderResult.Success`/`Rejected`, `ProcessOrderResult.Accepted`/`AlreadyProcessed`/`Rejected` | La creación válida se hace por construcción. `Rehydrate` separa la identidad persistida de la creación nueva; los métodos de resultado evitan constructores ambiguos | `Aggregates_DoNotExposePublicSetters` y las pruebas de invariantes de `Product` y `Order`; `HandleAsync_WithUnknownProduct_ReturnsMissingProducts` |
| Fallo esperado como valor | `CreateOrderResult` y `ProcessOrderResult` | El controlador traduce el resultado a `400` sin `try`/`catch` por endpoint; el dominio sigue usando excepciones para violaciones de invariante | `CreateOrder_WithUnknownProduct_ReturnsBadRequest`, `ErrorContractTests` |
| Consumidor idempotente | `IProcessedMessageStore`, `SqliteProcessedMessageStore` y `ProcessOrderHandler` | Service Bus entrega al menos una vez: repetir un mensaje no debe repetir efectos | `ProcessedMessageStore_IsIdempotent` |

## Handlers por caso de uso, no mediator

Los controladores reciben `ICreateOrderHandler`, `IGetOrdersHandler` y el resto por constructor y los invocan directamente. No hay `IMediator`, ni MediatR, ni despacho por reflexión: el grafo de dependencias se lee en `Program.cs` y el compilador lo comprueba.

La ventaja es la que interesa en este repositorio: cada caso de uso declara sus dependencias y no hay comportamiento que se ejecute por convención. El coste es que el controlador repite inyección y que no existe un punto único donde colgar validación o telemetría de todos los casos. Se asume el coste.

Criterio para revisarlo: si un controlador acumula casos de uso hasta hacer ilegible su constructor, o si aparece un comportamiento transversal que hoy se duplica en cada handler, se abre una ADR para introducir un despachador. No antes.

## Descartado

- **Abstract Factory, Builder y Decorator.** No existen en el código. No se documentan como si estuvieran: un lector que los busque no los va a encontrar.
- **CQRS completo con base de lectura separada.** El catálogo y los pedidos comparten almacén; separarlo añade sincronización y coherencia eventual sin una necesidad medida. Ver [ADR-0003](adr/0003-cqrs-ligero-en-pedidos.md).
- **Un contenedor de servicios propio o localizador.** Las dependencias se piden por constructor; no hay acceso al contenedor desde el código de aplicación.

## Cómo se mantiene

Al añadir un patrón nuevo, la fila entra en el mismo pull request que el código, con su prueba. Al retirar un patrón, se retira la fila. Las ADR siguen siendo el sitio de las decisiones; esta página solo enlaza y sitúa.
