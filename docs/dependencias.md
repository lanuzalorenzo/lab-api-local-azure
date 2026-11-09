# Dependencias: dueño, plazo y evidencia

> **Estado:** Validado (política aplicada y lote cerrado; la fusión automática se estrenó con la PR #55 el 2026-11-09) · Parcial (informe semanal manual; agrupación validada con un lote de 8 actualizaciones).
> **Última revisión:** 2026-11-07.
> **Evidencia:** lote de 8 actualizaciones ([PR #27](https://github.com/lanuzalorenzo/lab-api-local-azure/pull/27), checks en verde) resuelto con squash; `dotnet list package --vulnerable --include-transitive`: todos los proyectos sin paquetes vulnerables (salida local, 2026-11-07).

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
- El feed de GitHub Packages de `LL.*` se declara en `NuGet.config` y en el registro de Dependabot. Dependabot no acepta `GITHUB_TOKEN` dentro de `registries`: usa el secreto `LL_PACKAGES_TOKEN`. El criterio, la rotación y la verificación están en [`credenciales.md`](credenciales.md).
- El pipeline exige siete checks obligatorios, rama actualizada antes de fusionar e historial lineal. La fusión es siempre con squash: una pull request, un commit.
- `.github/workflows/dependabot-auto-merge.yml` fusiona solo las PRs de Dependabot de parche y minor cuando pasan los siete checks; las major siguen necesitando una persona.
- El límite de 5 reduce ruido, pero puede retrasar avisos: el informe semanal mira también `dotnet list package --outdated` para ver lo que Dependabot aún no ha abierto.
- **Agrupación:** dos grupos por ecosistema. Parche y minor van juntos; major va aparte y sigue necesitando revisión. La agrupación está validada con un lote real de 8 actualizaciones en una sola pull request.

## 4. Qué se automatiza y qué no

| Cambio | Automatizable | Quién firma |
|---|---|---|
| Parche `x.y.z`, tests verdes, sin CVE | Sí, squash automático con aviso | Máquina |
| Minor dentro del mismo major, changelog sin ruptura | Sí, squash automático con aviso | Máquina |
| Major, SDK o cambio de framework | No | Persona, con rama y canario |
| CVE crítica o explotada | Preparar sí; la automatización no filtra por severidad | Persona en la priorización; máquina si llega como parche o minor con los checks en verde |
| Ignorar o posponer | No | Persona, con fecha de caducidad |

La automatización filtra por tipo de versión, no por severidad: un aviso crítico que llegue como parche o minor también se fusiona con los checks en verde; la persona mantiene la priorización y el seguimiento.

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

## 8. Estado y límites

- **Validado:** la política se aplicó a un lote real, con checks obligatorios, rama actualizada y squash; el escaneo de dependencias queda limpio. La fusión automática se estrenó con la PR #55, de `LL.Security` 0.1.0 a 0.2.0, con los checks en verde (9 de noviembre de 2026).
- **Parcial:** el informe semanal es manual; la agrupación ya produjo un lote y el primer major sigue pendiente de revisión.
- **Fuera de alcance:** el SDK de .NET se fija en el pipeline (`10.0.x`) y no tiene `global.json`; las dependencias npm del laboratorio PKCE viven en otro repositorio y siguen el mismo criterio con `npm audit` y Dependabot.
