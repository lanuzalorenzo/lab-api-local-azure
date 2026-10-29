# Gobernanza del repositorio — PR y checks obligatorios

> **Estado:** Validado (reglas activas y push directo rechazado) · Parcial (sin revisor humano).
> **Última revisión:** 2026-10-29.
> **Evidencia:** configuración de *branch protection*, rechazo `GH006` de un push directo y PR de prueba #PENDIENTE.

## 1. Estado actual

Reglas activas en `main`, verificadas por API el 2026-10-29:

| Regla | Valor | Estado |
|---|---|---|
| Pull request obligatorio | Activo | Validado |
| Checks obligatorios | 7 | Validado |
| Rama actualizada antes del merge | Activo (`strict`) | Validado |
| Historial lineal | Activo | Configurado |
| Force push y borrado de `main` | Bloqueados | Configurado |
| `enforce_admins` | Activo | Validado por el rechazo `GH006` |
| Aprobaciones humanas | 0 | Parcial, con límite declarado en la sección 3 |

Los siete checks exigidos son:

1. `Compilación, formato y pruebas (.NET 10)`
2. `Detección de secretos (historial completo)`
3. `SAST (CodeQL)`
4. `SCA (dependencias NuGet)`
5. `Hardening y escaneo de contenedor`
6. `Seguridad de infraestructura como código (Bicep)`
7. `Revisión de dependencias (solo PR)`

El último solo se ejecuta en pull requests; por eso el requisito se evalúa ahí y no
en los push a `main`, donde aparece como `skipped`.

## 2. Verificación

### 2.1. Push directo rechazado

Ejecutado el 2026-10-29 sobre un commit vacío en una rama temporal. No llegó nada
a `main`:

```text
remote: error: GH006: Protected branch update failed for refs/heads/main.
remote:
remote: - Changes must be made through a pull request.
remote: - 7 of 7 required status checks are expected.
To https://github.com/lanuzalorenzo/lab-api-local-azure.git
 ! [remote rejected] tmp/protection-check -> main (protected branch hook declined)
```

### 2.2. Flujo por pull request

PR de prueba #PENDIENTE: este mismo documento. Los siete checks deben estar en
verde y el merge se hace por *squash*, sin bypass.

## 3. Por qué no se exigen aprobaciones

GitHub no permite aprobar el propio pull request. Con `enforce_admins` activo y
una aprobación obligatoria, un repositorio con un solo mantenedor quedaría
bloqueado: ningún PR podría fusionarse nunca.

Se elige **PR + checks sin aprobación** antes que una regla que se salta siempre.

Límite declarado: no hay una segunda persona revisando. La barrera es automática,
no humana. Para cerrarlo, añade un colaborador y sube
`required_approving_review_count` a 1 manteniendo `enforce_admins`.

## 4. Último bypass (histórico)

Dos push directos fueron aceptados por permisos de administrador antes de activar
`enforce_admins`:

| Fecha | Commits | Qué ocurrió |
|---|---|---|
| 2026-10-22 | 7 commits | Bypass registrado en el run [39767038845](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/39767038845) |
| 2026-10-29 | `9fd1578`, `2a0a436` | Último bypass, documentación del contrato de errores |

En ambos casos el pipeline posterior terminó en verde, pero eso no sustituye al
flujo: la protección se saltó, no se cumplió.

## 5. Break-glass

Solo con motivo escrito y restauración el mismo día:

```bash
# Desactivar temporalmente la obligación para administradores
gh api -X DELETE repos/lanuzalorenzo/lab-api-local-azure/branches/main/protection/enforce_admins

# Restaurar
gh api -X PUT repos/lanuzalorenzo/lab-api-local-azure/branches/main/protection/enforce_admins
```

No se usa para evitar un check rojo. Si un check falla, se corrige el cambio.

## 6. Por qué importa

El repositorio se presenta como workload DevSecOps. Si `main` acepta push directos
que saltan PR y checks, la evidencia del pipeline es más débil: los controles
existen, pero no son obligatorios. Con las reglas activas, el pipeline pasa de
«se ejecuta» a «gobierna la integración».

## 7. Referencias

- Pipeline: [`.github/workflows/devsecops-ci.yml`](../.github/workflows/devsecops-ci.yml)
- Configuración: `gh api repos/lanuzalorenzo/lab-api-local-azure/branches/main/protection`
- Run del primer bypass: <https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/39767038845>
