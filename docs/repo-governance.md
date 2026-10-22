# Gobernanza del repositorio — PR y checks obligatorios

> **Estado:** Pendiente. **No ejecutado.**
> **Última revisión:** 2026-10-22.
> **Evidencia:** push directo a `main` del 2026-10-22, run [39767038845](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/39767038845).

## 1. Qué pasó

El push directo de 7 commits a `main` fue aceptado, pero GitHub registró un
**bypass de las reglas de rama**:

```text
Bypassed rule violations for refs/heads/main:
- Changes must be made through a pull request.
- 7 of 7 required status checks are expected.
```

El pipeline posterior terminó en verde, pero **eso no sustituye al flujo**: la
protección se saltó por permisos de administrador, no porque el cambio cumpliera
la regla. Un resultado verde después del push no demuestra que el merge estuviera
bloqueado hasta que los checks pasaran.

## 2. Qué queda pendiente

| # | Acción | Criterio de cierre | Estado |
|---|---|---|---|
| 1 | Exigir pull request para `main` | Un push directo se rechaza | Pendiente |
| 2 | Marcar los 7 checks del pipeline como *required* | El merge se bloquea si uno falla | Pendiente |
| 3 | Revisar el bypass de administradores | Solo un *break-glass* documentado puede saltarlo | Pendiente |
| 4 | Exigir rama actualizada antes del merge | `Require branches to be up to date` activo | Pendiente |
| 5 | Documentar el flujo en `AGENTS.md` y en este documento | Enlace y pasos reproducibles | Pendiente |

## 3. Cómo se cerrará

1. Abrir un PR de prueba con un cambio inocuo.
2. Comprobar que los checks aparecen como obligatorios y que el merge está
   bloqueado mientras alguno falle.
3. Hacer merge solo con todos los checks en verde y sin bypass.
4. Guardar el enlace al PR y al run como evidencia.

## 4. Por qué importa

El repositorio se presenta como workload DevSecOps. Si `main` acepta pushes
directos que saltan PR y checks, la evidencia del pipeline es más débil: los
controles existen, pero no son **obligatorios**. Cerrar esta deuda convierte el
pipeline de "se ejecuta" en "gobierna la integración".

## 5. Referencias

- Pipeline: [`.github/workflows/devsecops-ci.yml`](../.github/workflows/devsecops-ci.yml)
- Run del push: <https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/39767038845>
- Reglas de `main`: configuración de *branch protection* del repositorio en GitHub.
