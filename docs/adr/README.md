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
| [0005](0005-observabilidad.md) | Observabilidad: consola ahora, telemetría cuando haya despliegue | Propuesta |

## Estados

- **Propuesta**: escrita, pendiente de aceptar.
- **Aceptada**: vigente y reflejada en el código.
- **Sustituida**: reemplazada por otra ADR, que se enlaza.
