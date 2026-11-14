# ADR-0009 — Cómo se prueba la mensajería en local

> **Estado:** Aceptada · 2026-11-14
> **Ámbito:** verificación local y en CI del camino outbox → Service Bus.
> **Complementa a:** [ADR-0001](0001-monolito-modular-vs-microservicios.md), [ADR-0004](0004-persistencia-sqlite-y-outbox.md).
> **Evidencia:** [`eng/servicebus-emulator`](../../eng/servicebus-emulator), [`scripts/servicebus-emulator.sh`](../../scripts/servicebus-emulator.sh), [`docs/servicebus-emulator.md`](../servicebus-emulator.md) y la ejecución del 2026-11-14 transcrita en ese runbook.

## 1. Contexto

[ADR-0004](0004-persistencia-sqlite-y-outbox.md) dejó el publicador de Service Bus en **Diseño**: el adaptador existe, pero su publicación no se ejecutaba en ningún sitio. La consecuencia es que el único tramo que habla con el servicio real —serialización del mensaje, identificador, asunto, tipo de contenido y nombre de la cola— no lo comprobaba nadie: las pruebas del outbox cambian el publicador por uno falso, que es lo correcto para probar reintentos y dead-letter, pero no dice nada del contrato con el broker.

Es como un ensayo con un actor de repuesto: las réplicas y las entradas salen bien, pero nadie ha comprobado que el actor real sepa el papel.

Ejecutar ese tramo exige un broker. Las reglas del repositorio cierran dos puertas: no se guardan credenciales y no se despliega en Azure sin autorización. Quedan los emuladores locales, y con ellos dos preguntas separadas: **qué broker** y **dónde se ejecuta la prueba**.

## 2. Decisión

1. **Emulador oficial de Microsoft** (`mcr.microsoft.com/azure-messaging/servicebus-emulator`), no un sustituto de terceros. La imagen la publica Microsoft, pero es **EULA, no código abierto**, y no tiene soporte ni SLA: es una herramienta de desarrollo y pruebas.
2. **Las entidades se declaran** en `eng/servicebus-emulator/config.json`: namespace `sbemulatorns` y cola `orders`, el mismo nombre que esperan `ServiceBusOptions` y el trigger de `functions/OrderProcessor`. El nombre de la cola es contrato, no detalle.
3. **Dos procesos separados**: el compose levanta el emulador y SQL Server 2022, que es su dependencia; un programa pequeño publica con el `ServiceBusEventPublisher` real y consume con `ServiceBusReceiver`, comparando identificador, asunto, tipo de contenido y cuerpo.
4. **Fuera de `dotnet test`.** El humo es un comando propio (`scripts/servicebus-emulator.sh smoke`) y un workflow propio. La suite de producto sigue sin Docker.
5. **La contraseña de SQL se genera** en local y se guarda en `eng/servicebus-emulator/.sql-password`, ignorado por Git. Los puertos se publican solo en `127.0.0.1`.
6. **El workflow no es obligatorio**: corre solo cuando el PR toca la mensajería y queda fuera de los siete checks de `main`.

## 3. Alternativas descartadas

### 3.1 Qué broker

| Alternativa | Por qué no |
|---|---|
| **LocalStack / broker embebido (RabbitMQ, Kafka)** | Cambia el protocolo y el contrato. Lo que quiero comprobar es que el SDK de Service Bus y mi mensaje encajan, no que un AMQP genérico transporta bytes. |
| **Emulador de comunidad** | Menos fidelidad en un contrato que quiero comparar con el servicio real, y una dependencia más sin dueño. |
| **Emulador de AWS** | Ningún consumidor de este repositorio habla con AWS. Sin consumidor no hay evidencia que conservar. |
| **Namespace real de Azure** | Necesita credenciales, cuesta dinero y no es determinista. Se reserva para la comprobación previa al despliegue. |

### 3.2 Dónde se ejecuta la prueba

Esta es la parte que más discusión tuvo, y quiero que quede escrita.

| Opción | Qué aporta | Qué cuesta | Veredicto |
|---|---|---|---|
| **A. Testcontainers en el proyecto de pruebas actual** | Ciclo de vida en código, sin scripts; el módulo `Testcontainers.ServiceBus` existe y está verificado | `dotnet test` pasa a necesitar Docker; la suite de 91 pruebas y ~10 s pasa a ~50 s y 1,8 GB de imágenes | **Descartada** |
| **B. Proyecto de pruebas aparte con Testcontainers** | Lo anterior sin tocar la suite de producto; permite probar la composición con `WebApplicationFactory` | Un proyecto más y otro comando que mantener | **Aplazada** (§5) |
| **C. Programa de humo y compose arrancado por script** | Cero acoplamiento al framework de pruebas; se ejecuta a mano y sirve de runbook | Prueba la clase, no el cableado de `Program.cs` | **Elegida** |
| **D. Híbrido: C como herramienta y B como prueba de composición** | Cubre las dos cosas | Más piezas en el mismo asunto | **Destino natural** |

El motivo de descartar A, con las cifras medidas en este repositorio:

1. **Coste.** `dotnet build` y `dotnet test` tardan segundos (91 pruebas, ~10 s). El emulador tarda 38,2 s en estar sano con las imágenes ya descargadas, y un runner efímero se baja 313 MB de emulador más ~1,5 GB de SQL Server en cada ejecución.
2. **Semántica del fallo con xUnit 2.9.3.** No hay *skip* dinámico. Un test que necesita el emulador solo puede fallar cuando no hay Docker —y entonces el build de producto depende de un daemon— o saltarse solo, que produce un verde que no prueba nada. Las dos salidas son peores que separar el comando.
3. **Invierte el «funciona en mi máquina».** Con Docker dentro, al revisor sin daemon le falla lo que a mí me pasa; con el comando aparte, cada uno ejecuta lo que su entorno permite y el hueco se ve, en vez de esconderse en un verde.

La regla que queda: **Docker entra en `dotnet test` cuando el contenedor es el sujeto de la prueba.** Aquí el sujeto es el adaptador, no el contenedor.

## 4. Consecuencias

**A favor**

- El adaptador se ejecuta de verdad: evidencia del 2026-11-14, con salida transcrita en el runbook.
- Cero credenciales de Azure y cero coste.
- El mismo comando sirve en local y en CI, así que no hay dos procedimientos que se separen con el tiempo.
- La suite de producto sigue siendo rápida y ejecutable sin Docker, que es lo que permite que los siete checks no dependan de un contenedor.

**Coste asumido**

- 1,8 GB de descarga por ejecución de CI, porque el runner es efímero.
- La imagen no está fijada por versión: un cambio de Microsoft puede aparecer sin aviso. Si empieza a romper, se fija el tag.
- El emulador no evalúa Entra ID ni políticas, admite un namespace, 256 KB por mensaje y una hora de vida, y pierde el estado al reiniciar. **No sirve para probar seguridad.**
- Workflow nuevo que hay que mantener y mirar; si nadie lo mira, se pudre como cualquier comprobación no obligatoria.

**Hueco declarado**

El humo construye `ServiceBusOptions` a mano, así que no cubre el cableado de `Program.cs`: si alguien rompe la clave `Publisher:Mode` o el binding de `ServiceBus`, el humo sigue en verde. Es el motivo de la opción B.

## 5. Criterio para revisarla

- **Composición.** Cuando haga falta cubrir el cableado, se añade `tests/EcommerceApi.Emulator.Tests` fuera de la solución, con `WebApplicationFactory` y `Testcontainers.ServiceBus`, ejecutado por el mismo workflow. `Microsoft.AspNetCore.Mvc.Testing` ya está en `Directory.Packages.props`.
- **Segundo consumidor.** Si otro repositorio usa Service Bus, el compose y el humo suben a `core` como acción parametrizada.
- **Obligatoriedad.** Si el workflow lleva una semana en verde, se propone como check obligatorio y se actualiza la gobernanza.
- **Mantenimiento del emulador.** Si Microsoft lo abandona o cambia la imagen, se fija el tag; si deja de servir, se reevalúa LocalStack con la misma tabla.
