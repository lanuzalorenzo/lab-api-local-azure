# ADR-0006 — Imagen de contenedor: dos fases, sin root y sin SDK en ejecución

> **Estado:** Aceptada · actualizada el 2026-11-12
> **Ámbito:** `Dockerfile`, composición local y trabajo de imagen en CI.
> **Evidencia:** [`Dockerfile`](../../Dockerfile), [`NuGet.config`](../../NuGet.config), [`Directory.Build.props`](../../Directory.Build.props), [`Directory.Packages.props`](../../Directory.Packages.props), [`.github/workflows/devsecops-ci.yml`](../../.github/workflows/devsecops-ci.yml) (construcción y comprobación de UID), [`.github/workflows/container-publish.yml`](../../.github/workflows/container-publish.yml) (publicación, firma y atestación), [`EcommerceApi/Program.cs`](../../EcommerceApi/Program.cs) (salud vía `LL.Hosting`) y [`EcommerceApi/appsettings.json`](../../EcommerceApi/appsettings.json) (`Storage`).
> **Relacionada con:** [ADR-0005](0005-observabilidad.md) y [ADR-0004](0004-persistencia-sqlite-y-outbox.md).

La API se entrega como una imagen de contenedor, y esa imagen es lo que se escanea,
se firma y se despliega más adelante. Este documento fija qué lleva dentro, con qué
usuario arranca y qué se deja fuera, para que la imagen no cambie por costumbre.
Es como una mudanza: se empaqueta en una habitación de trabajo y se entrega solo lo
que hace falta para vivir, no las cajas, las herramientas ni el andamio.

## 1. Contexto

El `Dockerfile` existe desde el 2026-10-22 (`5b480d1` y `dc36437`) y el pipeline lo
construye en cada cambio (`devsecops-ci.yml`, trabajo de imagen). Comprueba además
que el proceso no corre como root. Sin embargo, la decisión no estaba escrita y el
arranque local no tenía un camino corto: se documentaba `docker run` con parámetros
sueltos, difíciles de reproducir.

La imagen es la unidad que consumen el análisis dinámico de la API, el inventario
de componentes y la firma. En el momento de la decisión, el inventario y la firma
estaban pendientes; cambiar la forma de la imagen después obliga a rehacer esos
artefactos.

Datos medidos el 2026-11-03 en el host de trabajo:

- `mcr.microsoft.com/dotnet/aspnet:10.0` ocupa 95 934 226 bytes (unos 92 MB) y **no
  incluye `curl`**: `docker run --rm --entrypoint curl … --version` falla con
  «executable file not found in $PATH».

## 2. Decisión

1. **Dos fases.** `sdk:10.0` compila y publica; `aspnet:10.0` ejecuta. La imagen
   final no lleva SDK, `obj/`, `.csproj` ni código fuente.
2. **Se copia `publish`, no `build`.** `publish` deja una carpeta autocontenida con
   sus `runtimeconfig.json` y `deps.json` coherentes, sin activos de compilación.
   Con `-c Release` y `/p:UseAppHost=false`, porque el `ENTRYPOINT` arranca con
   `dotnet EcommerceApi.dll` y el lanzador nativo no se usaría.
3. **Caché de capas ordenada.** Primero `NuGet.config`, `Directory.Build.props`,
   `Directory.Packages.props` y los cinco `.csproj`, después `restore`, y solo al
   final `COPY . .`. Cambiar un controlador no invalida
   la restauración. El token del feed entra como secreto de BuildKit y no queda en
   ninguna capa.
4. **Usuario no root.** `USER $APP_UID` (UID 1654 en las imágenes oficiales). CI lo
   verifica con `id -u` distinto de `0`.
5. **Puerto no privilegiado.** `ASPNETCORE_URLS=http://+:8080` y `EXPOSE 8080`.
6. **Datos fuera de la imagen.** `/app/data` se crea en la construcción y pertenece
   al usuario de aplicación. En local se monta un volumen nombrado; la imagen no
   guarda estado.
7. **Composición local explícita.** Un `docker-compose.yml` con `build: .`, el
   secreto `github_token` leído de `GH_PACKAGES_TOKEN`, puerto `8080:8080`, volumen
   sobre `/app/data` y `restart: unless-stopped`. **Sin
   `healthcheck` en el contenedor**: la imagen no trae `curl` y añadir un cliente
   HTTP solo para sondear aumenta la superficie; la comprobación se hace desde el
   host contra `/health`, que es anónimo.
8. **Parches del sistema operativo en la construcción.** La fase final ejecuta
   `apt-get update && apt-get upgrade` y borra las listas. La etiqueta `:10.0`
   aporta el runtime de .NET parcheado; los paquetes de Ubuntu se actualizan en
   cada construcción para no publicar CVE corregibles que el escaneo de CI
   convertiría en fallo.

## 3. Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Una sola fase con `sdk` | Duplica el tamaño, deja compilador y MSBuild en ejecución y amplía la superficie sin necesidad |
| Copiar la salida de `build` | Arrastra artefactos de compilación y no está pensada para copiarse como despliegue |
| Reutilizar el artefacto de CI en la imagen | Ataría la imagen a la salida de un trabajo concreto y rompería la construcción reproducible desde el repositorio |
| Ejecutar como root | Un compromiso de la aplicación sería un compromiso del contenedor entero; CI ya lo prohíbe |
| Servir en el puerto 80 | Requiere privilegios o redirección; 8080 evita ambas |
| Imagen Alpine | Cambia glibc por musl y obliga a revisar cada dependencia nativa; se valora si el escaneo lo justifica |
| Imagen *chiseled* | Sin shell ni gestor de paquetes: menos superficie, pero no se puede depurar dentro del contenedor |
| `docker run` como camino principal | Muchos parámetros que se desincronizan; queda como alternativa documentada, no como entrada |

## 4. Consecuencias

### Positivas

- La imagen final no contiene herramientas de compilación ni código fuente: menos
  componentes que puedan tener una vulnerabilidad explotable.
- La imagen no hereda CVE de Ubuntu ya corregidos: los parches del sistema se
  aplican en la construcción y el escaneo del pipeline sigue siendo bloqueante.
- El arranque local es reproducible y no depende de recordar parámetros.
- La compilación en la imagen es independiente de la de CI: cualquiera con acceso
  al feed puede reconstruirla desde el repositorio.

### Negativas y límites

- El código se compila dos veces en cada cambio: una para verificar y otra para la
  imagen. Es el precio de que la imagen se construya sola.
- La caché depende de que el `.dockerignore` excluya lo que no debe entrar; un
  cambio en un fichero no ignorado rehace capas.
- Sin `healthcheck` en el contenedor, el orquestador no sabe si está sano. En local
  se acepta porque la comprobación es manual; antes de desplegar hay que decidirlo.
- El inventario de componentes ya se genera; la firma y la atestación se validaron
  en el run [43314295991](https://github.com/lanuzalorenzo/lab-api-local-azure/actions/runs/43314295991) (2026-11-12). El workflow genera el SBOM CycloneDX del
  digest publicado, firma la imagen con cosign keyless y atesta ese SBOM. El
  escaneo de vulnerabilidades de la imagen ya lo hacía el pipeline.
- La capa de `apt` añade tamaño y unos segundos de construcción, y dos
  construcciones del mismo commit pueden diferir si el repositorio de Ubuntu
  cambia. Se acepta porque el escaneo es bloqueante y la alternativa es publicar
  CVE corregidos.
- Desde el 2026-11-06 la construcción exige un `GH_PACKAGES_TOKEN` con `read:packages`,
  porque `LL.Security` y `LL.Hosting` vienen de GitHub Packages. Sin el secreto de
  BuildKit, el `restore` falla; es el precio de no duplicar los controles.

**Comprobado en ejecución:** el 2026-11-03, con Docker 29.1.3 y Compose 2.40.3,
`docker compose up --build -d` levanta la API desde una imagen borrada y
`curl -i localhost:8080/health` devuelve `200 OK`; el proceso corre como UID 1654.
La base (`/app/data/ecommerce.db`, 49 152 bytes) conserva tamaño y fecha tras
`docker compose down` y `up -d`. Lo que sigue sin probarse es el despliegue en un
orquestador, que decide el `healthcheck`.

**Comprobado en CI:** el 2026-11-06 el job `container-security` construyó la
imagen con `--secret id=github_token,env=GH_PACKAGES_TOKEN` contra el feed de GitHub
Packages; la comprobación de UID y el escaneo de Trivy pasaron en la misma
ejecución.

**Comprobado en ejecución:** el 2026-11-05, con Docker 29.1.3, una construcción con
`--pull` produce una imagen con .NET 10.0.14, `apt` 2.8.4ubuntu0.1, `/health` en
`200 OK` y UID 1654. El escaneo de Trivy (`CRITICAL,HIGH`, `ignore-unfixed`) no
devuelve hallazgos; antes del cambio marcaba CVE-2026-109518 en `apt` y
CVE-2026-111497 en el runtime 10.0.13. El escaneo se hizo con `aquasec/trivy`
sobre la imagen exportada, no con el pipeline.

## 5. Cuándo revisar esta decisión

- Al primer despliegue en un orquestador: si el contenedor necesita sonda, la
  decisión cambia con la causa.
- Si cambia la identidad de firma o el formato del SBOM: la verificación puede pedir otra base.
- Antes del primer despliegue: decidir `healthcheck` y usuario en la plataforma.
- Si el tamaño de la imagen o sus CVE obligan a Alpine o *chiseled*.
- Si la etiqueta `:10.0` deja de traer el runtime parcheado o el `apt-get upgrade`
  rompe el arranque: se fija el digest y se mueve el parcheo al pipeline.

**Criterio de cierre:** un orquestador decide el `healthcheck` y la base sigue
disponible en ese despliegue.

## 6. Referencias

- [`Dockerfile`](../../Dockerfile) — construcción y ejecución.
- [`devsecops-ci.yml`](../../.github/workflows/devsecops-ci.yml) — construcción de la imagen y comprobación de UID.
- [Persistencia SQLite y outbox](0004-persistencia-sqlite-y-outbox.md) — por qué hay una base y por qué es local.
- [Referencia de la API](../api.md) — contrato y `/health`.
- [Multi-stage builds](https://docs.docker.com/build/building/multi-stage/) — fases y copia entre imágenes.
- [Imágenes oficiales de .NET](https://learn.microsoft.com/dotnet/core/docker/container-images) — contenido de `aspnet` frente a `sdk`.
