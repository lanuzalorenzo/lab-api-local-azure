# Dependencias: dueño, plazo y evidencia

> **Estado:** Validado (política aplicada y lote cerrado el 2026-11-01) · Parcial (informe semanal manual; agrupación validada con un lote de 8 actualizaciones).
> **Última revisión:** 2026-11-01.
> **Evidencia:** 8 pull requests de Dependabot resueltas (5 fusionadas y 3 cerradas por duplicadas), una pull request agrupada con 8 actualizaciones, checks del pipeline en verde y `dotnet list package --vulnerable --include-transitive` sin paquetes vulnerables.

Una dependencia es una pieza de software que mantiene otra persona y de la que este proyecto se fía para funcionar. Un aviso de seguridad se parece a la luz de avería del coche: no arregla el motor, solo dice que hay que mirarlo. Lo que evita el problema no es el aviso, sino que alguien sea dueño del coche, lo lleve al taller y apunte la fecha.

## 1. El problema

Dependabot abre pull requests cuando una dependencia tiene un parche de seguridad o una versión nueva. El aviso, por sí solo, no cierra nada: si nadie lo revisa, la pull request envejece, el contexto se pierde y la ventana entre la alerta y el parche se mide en semanas. El coste no es el aviso; es esa ventana.

Una política de dependencias responde a tres preguntas: **quién** es el dueño, **cuánto** puede esperar cada severidad y **qué** se automatiza sin perder el control.

## 2. Política

| Severidad | Plazo desde la alerta | Quién decide | Qué se automatiza |
|---|---|---|---|
| Crítica | 7 días | Persona | Se prepara el cambio y se prioriza; la fusión la firma una persona |
| Alta | 30 días | Persona o máquina con tests verdes | Fusión automática si el cambio es un parche de bajo riesgo |
| Media | 90 días | Máquina con revisión muestral | Fusión en ventana, agrupada con otros parches |
| Baja | Siguiente ventana | Máquina | Se agrupa y se revisa por muestreo |

El plazo se cuenta desde que se publica la alerta, no desde que alguien la ve. Un aviso visto tarde no reinicia el reloj.

## 3. Configuración en este repositorio

- `.github/dependabot.yml` revisa NuGet y GitHub Actions cada lunes, con un límite de 5 pull requests abiertas y etiquetas `dependencies` y `security`.
- El pipeline exige siete checks obligatorios, rama actualizada antes de fusionar e historial lineal. La fusión es siempre con squash: una pull request, un commit.
- El límite de 5 reduce ruido, pero puede retrasar avisos: el informe semanal mira también `dotnet list package --outdated` para ver lo que Dependabot aún no ha abierto.
- **Agrupación:** dos grupos por ecosistema. Parche y minor van juntos; major va aparte y sigue necesitando revisión. El primer lote agrupado llegó el mismo 2026-11-01: una sola pull request con 8 actualizaciones.

## 4. Qué se automatiza y qué no

| Cambio | Automatizable | Quién firma |
|---|---|---|
| Parche `x.y.z`, tests verdes, sin CVE | Sí, squash automático con aviso | Máquina |
| Minor dentro del mismo major, changelog sin ruptura | Sí, en ventana y con revisión muestral | Máquina con muestreo |
| Major, SDK o cambio de framework | No | Persona, con rama y canario |
| CVE crítica o explotada | Preparar sí; fusionar no | Persona, con plazo corto |
| Ignorar o posponer | No | Persona, con fecha de caducidad |

La regla práctica: se automatiza el parche de bajo riesgo con tests verdes; se revisa el cambio que puede romper. Automatizar un major traslada el riesgo a la peor hora.

## 5. Lo que decide una persona

El aviso dice qué versión tiene el fallo; no dice si este servicio usa la función afectada. La persona aporta lo que el escaneo no ve:

- **Alcance real:** si el código vulnerable se ejecuta o solo se instala.
- **Exposición:** si el servicio es público, interno o de un solo cliente.
- **Criticidad:** qué pasa si se cae o si se compromete.
- **Controles compensatorios:** WAF, segmentación, permisos mínimos, límites de peticiones.
- **Ventana:** cuándo se puede desplegar sin romper una entrega.
- **Capacidad:** quién atiende el cambio y qué se deja de hacer para hacerlo.

## 6. Excepciones

| Dependencia | Motivo | Responsable | Caducidad |
|---|---|---|---|
| — | Sin excepciones abiertas | — | — |

Una excepción se escribe con motivo, responsable y fecha de revisión. Sin fecha no es una excepción: es deuda invisible.

## 7. Informe semanal

```bash
gh pr list --state open --json number,title,author,createdAt,mergeable,mergeStateStatus
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
gh pr checks <número> --watch
```

| Columna | Qué responde |
|---|---|
| Pull request | Qué cambio espera |
| Severidad | Cuánto puede esperar |
| Días abierta | Si ha pasado el plazo |
| Estado | Verde, en curso, bloqueada o con conflicto |
| Dueño | Quién responde de cerrarla |

## 8. Caso: lote del 2026-11-01

Entre el 26 y el 29 de octubre Dependabot abrió ocho pull requests de NuGet. El 1 de noviembre quedaron resueltas:

- **5 fusionadas con squash:** `coverlet.collector` 6.0.4 → 10.1.0 en tres proyectos de pruebas; `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Mvc.Testing` y `Microsoft.AspNetCore.OpenApi` 10.0.11 → 10.0.14; `Microsoft.Data.Sqlite` 10.0.13 → 10.0.14.
- **3 cerradas por duplicadas:** las pull requests 16, 17 y 18 subían por separado el mismo `coverlet.collector` que ya incluía la 7.
- **Verificación:** nueve checks en verde en cada pull request, rama actualizada y un conflicto resuelto por el propio bot antes de fusionar.
- **Resultado:** `dotnet list package --vulnerable --include-transitive` no encuentra paquetes vulnerables.
- **Pendiente de revisión:** `dotnet list package --outdated` deja dos actualizaciones mayor: `Microsoft.NET.Test.Sdk` 17.14.1 → 18.11.0 y `xunit.runner.visualstudio` 3.1.5 → 4.0.1. Ninguna se automatiza.

El plazo real fue de tres a seis días, dentro del límite de 30 días que fija la política para los avisos que no son críticos.

La agrupación configurada ese mismo día produjo una segunda pull request con 8 actualizaciones de parche y minor. Pasó los nueve checks y se fusionó con squash: una revisión en lugar de ocho.

## 9. Estado y límites

- **Validado:** la política se aplicó a un lote real, con checks obligatorios, rama actualizada y squash; el escaneo de dependencias queda limpio.
- **Parcial:** el informe semanal es manual; la agrupación ya produjo un lote y el primer major sigue pendiente de revisión.
- **Fuera de alcance:** el SDK de .NET se fija en el pipeline (`10.0.x`) y no tiene `global.json`; las dependencias npm del laboratorio PKCE viven en otro repositorio y siguen el mismo criterio con `npm audit`.
