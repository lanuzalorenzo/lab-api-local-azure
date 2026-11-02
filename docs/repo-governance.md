# Gobernanza del repositorio — PR, checks y aprobación

> **Estado:** Validado (ruleset activo, PR y siete checks obligatorios) · Parcial (sin revisor humano; el bypass es del propietario).
> **Última revisión:** 2026-11-02.
> **Evidencia:** ruleset `main`, PR de prueba y rechazo de push directo de la configuración anterior.

## 1. Estado actual

Reglas activas en `main`:

| Regla | Valor | Estado |
|---|---|---|
| Pull request obligatorio | Activo | Validado |
| Checks obligatorios | 7 | Validado |
| Rama actualizada antes del merge | Activo | Validado |
| Historial lineal y squash | Activo | Validado |
| Force push y borrado de `main` | Bloqueados | Validado |
| Aprobaciones humanas | 1 | Configurado |
| Bypass | Solo `lanuzalorenzo` | Configurado |

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

Todo cambio va por rama y PR. El PR necesita los siete checks, rama actualizada y
una aprobación. El propietario puede fusionar con bypass cuando no haya un segundo
revisor; el resto de colaboradores necesita aprobación.

El bypass no es el flujo normal: existe para no bloquear al mantenedor único, no
para saltar un check rojo. Si se añade un segundo revisor, se retira el bypass y
se mantiene la aprobación.

## 3. Por qué 1 aprobación y bypass

GitHub no permite aprobar el propio pull request. Un repositorio con un solo
mantenedor y una aprobación obligatoria quedaría bloqueado. El ruleset resuelve el
caso con un bypass explícito para la cuenta del propietario; el resto de cuentas
sigue necesitando aprobación.

## 4. Verificación

### 4.1. Push directo rechazado

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

### 4.2. Flujo por pull request

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
