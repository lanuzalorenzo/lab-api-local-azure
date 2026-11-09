# Credenciales para el feed privado de paquetes

> **Estado:** Parcial (2026-11-08). La separación de credenciales está validada en local y CI; la credencial de solo lectura y el registro de Dependabot están verificados con una ejecución fallida y otra correcta. Falta una PR real de actualización de `LL.*` para cerrar el ciclo completo.
> **Última revisión:** 2026-11-08.
> **Evidencia:** ejecuciones de Dependabot [fallida](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42409729475) y [correcta](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42410124439); restore local con caché vacía; prueba negativa `401`; [run de CI](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42470483409).

El restore de NuGet —la descarga y resolución de paquetes que hace el build— se parece a abrir el trastero donde están las cajas compartidas: sin la llave no hay caja, pero la llave de casa no debería servir también para el trastero. Este documento explica por qué el build necesita una credencial, por qué esa credencial debe ser de solo lectura y cómo se separa de la identidad con la que se trabaja a diario.

## 1. Por qué el restore necesita una credencial

GitHub Packages —el registro de paquetes de GitHub— exige autenticación incluso cuando los paquetes son públicos. El proyecto declara el feed (el catálogo de paquetes) en `NuGet.config` y deja la contraseña en una variable de entorno:

```xml
<packageSourceCredentials>
  <github>
    <add key="Username" value="<propietario>" />
    <add key="ClearTextPassword" value="%GH_PACKAGES_TOKEN%" />
  </github>
</packageSourceCredentials>
```

El archivo no guarda el secreto: guarda el nombre de la variable. Si la variable no existe o no vale, el restore falla con `NU1301` y un `401` del feed. El error no distingue entre «no hay credencial» y «la credencial caducó»; por eso conviene comprobar la fecha del token antes de buscar el problema en otro sitio.

La variable se llama `GH_PACKAGES_TOKEN` y no `GITHUB_TOKEN` por una razón de diseño: `gh` y Git leen `GITHUB_TOKEN` como la credencial de la sesión y le dan prioridad sobre su propio almacén. Un PAT de solo lectura ahí deja los `push` en `403`, un fallo que parece de permisos del repositorio y no de la variable. El nombre propio mantiene separadas la credencial de los paquetes y la identidad interactiva.

## 2. Identidad interactiva y credencial de automatización

Son dos identidades con trabajos distintos. La primera abre la sesión con la que una persona revisa, escribe y aprueba. La segunda solo descarga paquetes durante un build o una actualización automática.

| Capacidad | Identidad interactiva | Credencial de automatización |
|---|---|---|
| Descargar el feed privado | Sí | Sí |
| Leer código de repositorios privados | Sí | No |
| Escribir código o abrir pull requests (PRs) | Sí | No |
| Editar workflows | Sí | No |
| Cambiar ajustes o colaboradores | Sí | No |
| Caducidad | La de la sesión | Fija, corta y renovable |

La ventaja no es que la credencial de automatización sea imposible de robar. Es que su robo tiene un techo: sirve para descargar paquetes y poco más. Un build, un paquete de terceros o un target de MSBuild (el motor de compilación de .NET) pueden leer variables de entorno; si encuentran la credencial amplia, heredan todos sus permisos.

## 3. Mínimo privilegio

La credencial es un token de acceso personal (PAT) con un único permiso de paquetes: `read:packages` (en un token fine-grained, `Packages: Read`). Nada de `repo`, `workflow`, `gist` ni administración. Si la plataforma permite elegir el repositorio que publica los paquetes, se limita a ese; el permiso viaja con el feed, no con cada repositorio que consume.

| Decisión | Criterio |
|---|---|
| Tipo de token | Fine-grained (permisos por repositorio) con `Packages: Read` si la cuenta lo ofrece; si no, clásico (permisos de toda la cuenta) con solo `read:packages` |
| Caducidad | 90 días, o la más corta que permita el trabajo |
| Alcance | El repositorio que publica; ampliar solo si el feed lo rechaza |
| Uso | Restore local, lanzador del editor y secreto de Dependabot |

Un token clásico con `read:packages` sigue siendo una reducción grande frente a uno con `repo` y `workflow`, pero conserva más superficie que el fine-grained. Se acepta como último recurso, no como primera opción.

## 4. Dos almacenes, una sola credencial

La misma credencial se guarda en dos sitios porque hay dos consumidores:

1. **El gestor de secretos del sistema** (el llavero o *keyring*), para el shell, el editor y los builds locales. El archivo de configuración del shell no contiene el valor: lo pide al gestor y lo exporta solo durante esa sesión.
2. **El secreto de Dependabot del repositorio** (el servicio de actualizaciones automáticas de GitHub), para la actualización automática. Se guarda por nombre y Dependabot lo resuelve al ejecutar.

No se duplica el token por repositorio ni se pega en `NuGet.config`, en un workflow (flujo de trabajo de GitHub Actions) o en un archivo de configuración. Cuando se rota, se actualizan los dos almacenes y se revoca el anterior. Si solo se actualiza uno, el restore local sigue funcionando y la actualización automática empieza a fallar en silencio.

## 5. El límite de Dependabot

Dependabot no resuelve el `GITHUB_TOKEN` de Actions (el token efímero de los workflows) dentro de `registries` (la lista de feeds que consulta). Si se configura así, la ejecución falla antes de consultar el feed:

```yaml
registries:
  github-packages:
    type: nuget-feed
    url: https://nuget.pkg.github.com/<propietario>/index.json
    username: <propietario>
    password: ${{secrets.GITHUB_TOKEN}}   # no funciona en Dependabot
```

El error es explícito: `422 Secret Not Found: GITHUB_TOKEN`. La solución es guardar la credencial como secreto de Dependabot y referenciarla por su nombre:

```yaml
registries:
  github-packages:
    type: nuget-feed
    url: https://nuget.pkg.github.com/<propietario>/index.json
    username: <propietario>
    password: ${{secrets.LL_PACKAGES_TOKEN}}
```

El nombre del secreto no es especial; lo importante es que exista en el repositorio que ejecuta Dependabot y que su valor sea la credencial de solo lectura, no el token de la sesión interactiva. Cambiar `dependabot.yml` en la rama principal provoca una ejecución nueva, que es la forma de comprobar el arreglo.

## 6. Rotación y revocación

La rotación es una tarea de calendario, no una reacción a un incidente. Cada 90 días, o antes si hay sospecha de exposición:

1. Crear una credencial nueva con los mismos permisos.
2. Sustituirla en el gestor de secretos local.
3. Sustituir el secreto de Dependabot por el valor nuevo.
4. Repetir la verificación de §7.
5. Revocar la credencial anterior.
6. Anotar la fecha y comprobar que la nueva caduca cuando toca.

En la rotación normal se crea y se sustituye antes de revocar, para no quedarse sin credencial. Si hay una fuga, se revoca de inmediato y se rota después. Esperar a la fecha de caducidad convierte una ventana de 90 días en una ventana de 90 días con la credencial en manos de otro.

| Síntoma | Causa probable | Qué mirar |
|---|---|---|
| `401` en el restore local | Token caducado, revocado o sin `read:packages` | Fecha y permisos en GitHub |
| `403` en Dependabot | Secreto caducado o sin acceso al feed | Nombre del secreto y fecha |
| `422 Secret Not Found` | Se referenció `GITHUB_TOKEN` dentro de `registries` | Cambiar a un secreto de Dependabot |

## 7. Cómo se verifica

La comprobación tiene cuatro partes y ninguna imprime el valor de la credencial.

**a) El shell usa la credencial correcta.** En una terminal nueva, compara el valor con el del gestor de secretos y comprueba que no coincide con el token de la sesión interactiva. No imprimas ninguno de los dos. Sustituye `leer-del-gestor` por el comando de tu gestor:

```bash
[ "$GH_PACKAGES_TOKEN" = "$(leer-del-gestor)" ] && echo 'coincide con el gestor'
[ -z "${GITHUB_TOKEN:-}" ] && echo 'GITHUB_TOKEN libre: gh y git no lo heredan'
[ "$GH_PACKAGES_TOKEN" = "$(env -u GITHUB_TOKEN -u GH_TOKEN gh auth token)" ] \
  && echo 'es el token de gh' || echo 'no es el token de gh'
```

**b) El restore funciona con la caché vacía.** Se fuerza una descarga real en un directorio temporal, para que no valga un paquete ya guardado en la caché local:

```bash
NUGET_PACKAGES="$(mktemp -d)" GH_PACKAGES_TOKEN="$(leer-del-gestor)" \
  dotnet restore EcommerceApi.sln --no-cache
```

Resultado esperado: `exit 0`. La salida puede citarse sin el valor de la credencial; las rutas locales se sustituyen por `<repo>`.

**c) La prueba negativa confirma que la credencial es lo que autentica.** Es la prueba de control: con la variable sin definir y la caché vacía, el restore debe fallar:

```bash
env -u GITHUB_TOKEN -u GH_PACKAGES_TOKEN NUGET_PACKAGES="$(mktemp -d)" \
  dotnet restore EcommerceApi.sln --no-cache
```

Resultado esperado: `NU1301` con `401` en los paquetes `LL.*`. Si el restore pasa, la caché no estaba vacía o el feed no estaba aplicando autenticación.

**d) Dependabot ejecuta sin error de credenciales.** La ejecución de `nuget` termina sin `Secret Not Found`, `401` ni `403`. Si no hay versión nueva, termina sin abrir PR; eso no es un fallo de la credencial.

## 8. Evidencia y estado

| Fecha | Comprobación | Resultado |
|---|---|---|
| 2026-11-07 | Dependabot con `${{secrets.GITHUB_TOKEN}}` en `registries` | [Ejecución fallida](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42409729475): `422 Secret Not Found` |
| 2026-11-07 | Cambio a `${{secrets.LL_PACKAGES_TOKEN}}` | [PR y configuración](https://github.com/lanuzalorenzo/lab-api-local-azure/pull/46) |
| 2026-11-07 | Dependabot después del cambio | [Ejecución correcta](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42410124439): sin error de credenciales |
| 2026-11-07 | Restore local con caché vacía | `exit 0`, sin credencial en la salida |
| 2026-11-07 | Restore sin credencial y caché vacía | `NU1301` `401` |
| 2026-11-08 | Restore con caché vacía y `GH_PACKAGES_TOKEN` | `exit 0`, sin credencial en la salida |
| 2026-11-08 | Restore sin `GH_PACKAGES_TOKEN` y caché vacía | `NU1301` `401` |
| 2026-11-08 | Shell nuevo: `GITHUB_TOKEN` libre y `gh` con la sesión del llavero | `GH_PACKAGES_TOKEN` definido; `gh auth status` con `repo` |
| 2026-11-08 | CI con `secrets.GITHUB_TOKEN` mapeado a `GH_PACKAGES_TOKEN` | [Run 42470483409](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/42470483409) en verde |

Salida resumida del restore con caché vacía (rutas locales sustituidas por `<repo>`):

```text
Determinando los proyectos que se van a restaurar...
Se ha restaurado <repo>/EcommerceApi.Domain/EcommerceApi.Domain.csproj (en 178 ms).
Se ha restaurado <repo>/EcommerceApi.Contracts/EcommerceApi.Contracts.csproj (en 178 ms).
Se ha restaurado <repo>/EcommerceApi.Application/EcommerceApi.Application.csproj (en 194 ms).
```

Prueba de control sin credencial:

```text
error NU1301: No se pudo recuperar información sobre "LL.Hosting" del origen remoto
"https://nuget.pkg.github.com/<propietario>/download/ll.hosting/index.json".
Response status code does not indicate success: 401 (Unauthorized).
```

La ejecución local terminó con `exit 0`; la prueba de control, con `exit 1`. El extracto anterior no incluye el valor de la credencial ni las rutas completas del equipo.

**Estado real: Parcial.** El ciclo de la credencial está verificado, pero falta la prueba que cierra el propósito: una pull request (PR) real de actualización de `LL.*` generada por Dependabot y con su CI (integración continua) en verde. La versión publicada `0.1.0` es la última, así que Dependabot no tiene una actualización que proponer. Pasará a **Validado** cuando Dependabot genere esa PR y su CI esté en verde. Con la versión fijada, se espera que un parche o una minor nueva abran la PR; las major siguen ignoradas en `dependabot.yml` y se revisan a mano.

## 9. Límites

- La credencial no protege la cuenta: eso depende de passkey (llave de acceso), 2FA (doble factor) y códigos de recuperación.
- No impide que un proceso lea la variable mientras está en el entorno; reduce lo que puede hacer con ella.
- No audita la cadena de suministro: limita el daño de una credencial robada, no revisa el contenido de los paquetes.
- Si los paquetes pasan a un feed público sin autenticación, la credencial desaparece del restore y este documento deja de aplicar.
