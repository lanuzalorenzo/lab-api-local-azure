# Gobernanza del repositorio — PR y checks obligatorios

Una rama principal sin reglas es una puerta sin cerradura: cualquiera con permiso de escritura entra, y el historial se puede reescribir. Este documento describe las cerraduras que GitHub aplica en `main`, cómo se comprobó que cierran y qué queda fuera de su alcance.

> **Estado:** Validado (ruleset activo, PR y siete checks obligatorios) · Parcial (sin revisor humano).
> **Última revisión:** 2026-11-04.
> **Evidencia:** ruleset `main`, rechazo de push directo y de push forzado.

## 1. Estado actual

Reglas activas en `main`:

| Regla | Valor | Estado |
|---|---|---|
| Pull request obligatorio | Activo | Validado |
| Checks obligatorios | 7 | Validado |
| Rama actualizada antes del merge | Activo | Configurado |
| Historial lineal y squash | Activo | Configurado |
| Force push | Bloqueado | Validado |
| Borrado de `main` | Bloqueado | Configurado |
| Aprobaciones humanas | 0 | Configurado |
| Actores de bypass | Ninguno: tampoco el propietario | Validado |
| Permiso de merge | Solo el propietario, salvo PRs de Dependabot de parche y minor | Configurado |

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

## 2. Flujo

Todo cambio va por rama y PR. El PR necesita los siete checks y rama actualizada.
Solo el propietario fusiona; cualquier cambio externo pasa por su merge. La excepción son las PRs de Dependabot de parche y minor, que fusiona el flujo automático cuando pasan los siete checks.

## 3. Por qué no se exigen aprobaciones

GitHub no permite aprobar el propio pull request. Con un solo mantenedor y una
aprobación obligatoria, el repositorio quedaría bloqueado. La barrera es PR, siete
checks y permiso de merge del propietario. Si se añade un segundo revisor, se sube
`required_approving_review_count` a 1.

## 4. Verificación

### 4.1. Push directo rechazado

Ejecutado el 2026-11-04 desde la cuenta propietaria, sobre un commit vacío en una
rama temporal. No llegó nada a `main`:

```text
remote: error: GH013: Repository rule violations found for refs/heads/main.
remote:
remote: - Changes must be made through a pull request.
remote:
remote: - 7 of 7 required status checks are expected.
To https://github.com/lanuzalorenzo/lab-api-local-azure.git
 ! [remote rejected] HEAD -> main (push declined due to repository rule violations)
```

El rechazo lo emite el ruleset. La versión anterior de este documento citaba
`GH006` y «protected branch hook declined», mensajes de la protección clásica;
el ruleset actual responde `GH013`.

### 4.2. Push forzado rechazado

Ese mismo día, `git push --force origin HEAD:main` desde la misma rama temporal
devolvió el mismo rechazo: la regla `non_fast_forward` impide reescribir el
historial de `main`.

### 4.3. Flujo por pull request

PR de prueba #12: este mismo documento. Los siete checks estuvieron en verde
(run [40887287486](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/40887287486))
y el merge se hizo por *squash* como `9b53be6`, sin bypass.

## 5. Por qué importa

El repositorio se presenta como workload DevSecOps. Si `main` acepta push directos
que saltan PR y checks, la evidencia del pipeline es más débil: los controles
existen, pero no son obligatorios. Con las reglas activas, el pipeline pasa de
«se ejecuta» a «gobierna la integración».

## 6. Referencias

- Pipeline: [`.github/workflows/devsecops-ci.yml`](../.github/workflows/devsecops-ci.yml)
- Ruleset: `gh api repos/lanuzalorenzo/lab-api-local-azure/rulesets`
