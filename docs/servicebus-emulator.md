# Emulador de Service Bus

El camino de mensajería de la API se podía leer en el código, pero no se ejecutaba en ningún sitio: las pruebas unitarias cambian el publicador por uno falso y el adaptador real solo se activa con un namespace de Azure. Esta carpeta levanta un Service Bus local, sin cuenta ni coste, para que ese camino se ejecute de verdad. Es como el buzón de la oficina montado en una mesa de pruebas: las cartas entran y salen igual, pero ninguna sale a la calle.

**Estado:** Validado en local el 2026-11-14 (ver §4). El workflow de CI está escrito y sin ejecutar en GitHub todavía: **Parcial**.

## 1. Qué hay aquí

```
eng/servicebus-emulator/
├── config.json          # Namespace y entidades: la cola orders
├── docker-compose.yml   # Emulador y SQL Server, que guarda las entidades
└── smoke/               # Programa que publica y consume un mensaje
scripts/servicebus-emulator.sh   # up | smoke | logs | status | down
.github/workflows/servicebus-emulator.yml  # El mismo recorrido en CI
```

El emulador es un contenedor de Microsoft; no hay imagen propia que mantener. Solo se declara lo que el laboratorio usa: una cola llamada `orders`, la misma que esperan `ServiceBusOptions` y el trigger de `functions/OrderProcessor`.

## 2. Qué emula y qué no

Sirve para probar que un mensaje sale del publicador, llega a la cola y conserva identificador, asunto, tipo de contenido y cuerpo. No sirve para probar seguridad ni comportamiento del servicio real.

| Emula | No emula |
|---|---|
| Colas, temas y suscripciones | Microsoft Entra ID: no hay autenticación que validar |
| Envío y recepción por AMQP | Políticas de IAM y de red |
| Reintentos y carta muerta según `MaxDeliveryCount` | Entidades particionadas |
| APIs de administración desde .NET | AMQP sobre WebSockets (solo TCP) |
| | Portal, métricas, alertas, geo-recuperación |
| | Persistencia: al reiniciar el contenedor se pierden los mensajes |

Límites que condicionan una prueba: un namespace y hasta 50 entidades, 256 KB por mensaje, 100 MB por entidad, un máximo de una hora de vida por mensaje y 10 conexiones concurrentes al namespace. Los cambios en `config.json` se leen al arrancar; no se recargan en caliente.

La cadena de conexión es fija y no lleva secreto real:

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

Para las operaciones de administración hay que añadir el puerto: `sb://localhost:5300`.

## 3. Arrancarlo

Requisitos: Docker en marcha y espacio para unos 5 GB entre imágenes y datos. La primera vez descarga SQL Server 2022 y el emulador.

```bash
scripts/servicebus-emulator.sh up      # arranca y espera a /health
scripts/servicebus-emulator.sh status  # estado de los dos contenedores
scripts/servicebus-emulator.sh logs    # últimas 80 líneas
scripts/servicebus-emulator.sh down    # apaga y borra los datos
```

El script genera una contraseña de SQL aleatoria la primera vez y la guarda en `eng/servicebus-emulator/.sql-password`, fuera de Git. Sin esa contraseña, `docker compose up` falla a propósito: así no queda una credencial escrita en el repositorio. Los puertos se publican solo en `127.0.0.1`.

Con las imágenes ya descargadas, el arranque tarda 38,2 s hasta que `/health` responde; el humo, 3,2 s. La primera vez hay que sumar la descarga: 313 MB del emulador y unos 1,5 GB de SQL Server.

## 4. Probarlo

```bash
scripts/servicebus-emulator.sh smoke
```

El programa usa el `ServiceBusEventPublisher` real, el mismo que se activa con `Publisher__Mode=ServiceBus`, y después lee la cola con `ServiceBusReceiver`. Compara identificador, asunto, tipo de contenido y cuerpo; si algo no coincide, sale con código 1 y escribe la diferencia.

Ejecutado en local el 2026-11-14, con el emulador recién arrancado:

```
$ scripts/servicebus-emulator.sh smoke
Cola orders; mensaje 552b39b1bdf043d4a5635440d4d278aa
Publicado por ServiceBusEventPublisher
Recibido y verificado: id=552b39b1bdf043d4a5635440d4d278aa subject=OrderCreated contentType=application/json cuerpo={"orderId":"552b39b1bdf043d4a5635440d4d278aa"}
```

El arranque tarda sobre todo la primera vez, porque descarga las imágenes. La imagen del emulador no trae `healthcheck` propio, así que `up -d --wait` solo garantiza que el contenedor está en marcha; la señal de que atiende es la espera contra `http://127.0.0.1:5300/health`, que el script hace antes de devolver el control.

## 5. Conectar la API

Con el emulador en marcha, la API publica de verdad sin tocar Azure:

```bash
Publisher__Mode=ServiceBus \
ServiceBus__ConnectionString='Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;' \
ServiceBus__QueueName=orders \
dotnet run --project EcommerceApi
```

Un pedido nuevo escribe su fila en el outbox y `OutboxDispatcher` la publica en la cola `orders`. Se puede seguir con `scripts/servicebus-emulator.sh logs` o consumiendo la cola desde el propio emulador.

Lo que sigue sin probarse ahí es el receptor: `functions/OrderProcessor` necesita el host de Functions y su almacenamiento. Queda como paso siguiente, no como parte de esta evidencia.

## 6. En CI

`.github/workflows/servicebus-emulator.yml` repite el mismo recorrido en `ubuntu-latest`: arranca, publica y consume, apaga siempre y deja los logs si falla. Corre solo cuando el PR toca la mensajería: `EcommerceApi.Infrastructure/Outbox`, `EcommerceApi.Application`, `Directory.Packages.props`, `Directory.Build.props`, `eng/servicebus-emulator`, el script o el propio workflow.

No está entre los siete checks obligatorios de `main` ([gobernanza](repo-governance.md)). La razón es la de siempre: SQL Server tarda en arrancar y no quiero una comprobación inestable bloqueando merges. Se promueve cuando lleve una semana en verde.

## 7. Límites de esta prueba

- Comprueba un mensaje, no concurrencia, ni orden, ni volúmenes.
- No valida autenticación: el emulador acepta la clave fija de desarrollo.
- No sustituye a una prueba contra un namespace real antes de desplegar.
- La imagen se actualiza con `docker compose pull`; el workflow no la fija por versión, así que un cambio de Microsoft puede aparecer sin aviso. Si eso empieza a pasar, se fija el tag.

## 8. Por qué así

Las alternativas descartadas —LocalStack, un broker embebido, Testcontainers dentro de `dotnet test`— y el motivo de cada una están en [ADR-0009](adr/0009-como-se-prueba-la-mensajeria-en-local.md), con los tiempos medidos y la regla que se sigue: Docker entra en `dotnet test` cuando el contenedor es el sujeto de la prueba, y aquí el sujeto es el adaptador, no el contenedor.
