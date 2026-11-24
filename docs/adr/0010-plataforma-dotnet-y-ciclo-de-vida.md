# ADR-0010 — Plataforma .NET: versión vigente, fin de soporte y protocolo de migración

> **Estado:** Aceptada · 2026-11-15
> **Ámbito:** los doce `.csproj` del repositorio —diez de la solución y dos del emulador—, `global.json`, `Directory.Packages.props`, `Dockerfile`, los workflows `devsecops-ci.yml` y `servicebus-emulator.yml`, `infra/main.bicep` y `functions/OrderProcessor`.
> **Evidencia:** `global.json` resuelve el SDK 10.0.115 en esta máquina; el suelo se comprobó con tres peticiones —`10.0.100` resuelta a 10.0.115, `10.0.120` y `11.0.100` rechazadas con «A compatible .NET SDK was not found»—; la imagen `mcr.microsoft.com/dotnet/sdk:10.0` en caché trae el SDK 10.0.403 y también resuelve, porque `latestFeature` admite una banda posterior; `dotnet restore` correcto, compilación Release con cero avisos y cero errores, 91 pruebas superadas (45 de unidad, 7 de arquitectura, 10 de infraestructura y 29 de integración) y `dotnet format --verify-no-changes` sin cambios; Gitleaks sin hallazgos sobre el historial completo. `bicep build` no se ejecutó en esta máquina porque la CLI no está instalada: este cambio no toca `infra/main.bicep` y ese paso corre en el pipeline. El protocolo de §2.5 se ejecutó completo sobre .NET 11 el 23 de noviembre de 2026 en la [PR #84](https://github.com/lanuzalorenzo/lab-api-local-azure/pull/84), cerrada sin merge: 26 archivos, 95 de 95 pruebas y los siete checks del pipeline en verde.
> **Relacionada con:** [ADR-0006](0006-imagen-de-contenedor.md), que hereda las etiquetas `10.0` de la imagen base, y [ADR-0008](0008-configuracion-de-compilacion-y-paquetes-centralizada.md), que dejó `TargetFramework` en cada proyecto a propósito.

## 1. Contexto

Los doce proyectos del repositorio —diez en la solución y dos del emulador— declaran `net10.0`, pero ninguna decisión escrita explicaba la elección, su fecha de fin de soporte ni con qué criterio se cambia. La versión estaba además repartida en cinco sitios: los proyectos, la imagen del contenedor, los dos workflows, la Function en Bicep y el SDK del proyecto de Functions. Y el SDK no estaba fijado en ninguna parte: el pipeline instala `10.0.x` mientras cada máquina compila con lo que tenga instalado. `docs/dependencias.md` lo declaraba «fuera de alcance», que es declarar el hueco, no cerrarlo.

Datos del ciclo de vida según la [política de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), actualizada el 10 de noviembre de 2026:

| Versión | Tipo | Publicación | Fin de soporte |
|---|---|---|---|
| .NET 10 (vigente) | LTS, 3 años | 11 de noviembre de 2025 | 14 de noviembre de 2028 |
| .NET 11 | STS, 2 años | 10 de noviembre de 2026 | 14 de noviembre de 2028 |
| .NET 12 (prevista) | LTS, 3 años | noviembre de 2027 | noviembre de 2030 |

La fila de .NET 12 no está publicada: se deduce de la cadencia anual de noviembre y de que las versiones pares son LTS. Las otras dos son dato oficial.

Dos consecuencias que dirigen la decisión. La primera: .NET 10 y .NET 11 terminan el mismo día, así que cambiar de versión no añade ni un mes de soporte. La segunda: los últimos seis meses de cualquier versión son de mantenimiento, solo parches de seguridad; .NET 10 entra ahí hacia mayo de 2028, y conviene no estar en esa fase.

El ecosistema no bloquea, pero encarece: Azure Functions admitió .NET 11 en el modelo isolated el mismo 10 de noviembre de 2026, según su [nota de disponibilidad general](https://techcommunity.microsoft.com/blog/appsonazureblog/-net-11-is-now-generally-available-in-azure-functions/4571176). Ese soporte llega de momento solo a C# y exige un SDK de proyecto nuevo —`Azure.Functions.Sdk` 1.0.1 o superior— en lugar del que usa hoy `functions/OrderProcessor`. No se reduce a subir un número.

## 2. Decisión

1. **Se mantiene `net10.0`.** No se migra a .NET 11 en esta ventana.
2. **`global.json` fija el SDK** en `10.0.115`, con `rollForward: latestFeature` y sin pre-releases. La regla exacta: acepta cualquier SDK `10.0.x` **igual o posterior** a 10.0.115, incluida una banda de características más alta, y rechaza `11.x`. No unifica las máquinas: hoy esta tiene 10.0.115 y la imagen del contenedor, 10.0.403, y ambas valen. Fija un suelo y una rama, que es lo que faltaba; la reproducibilidad exacta no la da un `global.json`, la daría fijar el mismo parche en el pipeline y en la imagen.
3. **La migración va de LTS a LTS: destino .NET 12 en su disponibilidad general**, salvo que antes aparezca una necesidad concreta que solo cubra .NET 11. Con ese calendario el repositorio no entra en la fase de mantenimiento de la versión que usa. Es la misma línea de la [política de plataforma](https://github.com/lanuzalorenzo/core/blob/main/docs/versiones-y-dependencias.md): LTS, y los mayores de la familia bloqueados.
4. **Ninguna lista de novedades es un requisito.** De cada versión entra lo que cubre una necesidad del repositorio y se puede comprobar con una prueba. Es la misma regla que el registro de ADR aplica a los patrones: una lista sin evidencia es vocabulario.
5. **Protocolo de migración**, en este orden y en una sola pull request:

   | Paso | Archivo | Qué se cambia |
   |---|---|---|
   | 1 | `global.json` | Versión del SDK destino y, si la Function pasa a `Azure.Functions.Sdk`, el bloque `msbuild-sdks` con la clave `Azure.Functions.Sdk` y la versión de ese SDK —1.1.0 probado—, que no es la del SDK de .NET. Sin él, el proyecto intermedio `obj/azure_functions/azure_functions.g.csproj` no resuelve y el build se detiene con `MSB4236` |
   | 2 | Los doce `.csproj`: diez de la solución y los dos del emulador (`tests/EcommerceApi.Emulator.Tests`, `eng/servicebus-emulator/smoke`) | `TargetFramework` |
   | 3 | `Directory.Packages.props` y `EcommerceApi.Infrastructure/EcommerceApi.Infrastructure.csproj` | `Microsoft.Extensions.*`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.Data.Sqlite` y `Microsoft.AspNetCore.Mvc.Testing` a la major nueva. `Microsoft.Azure.Functions.Worker` 2.52.0 y `…Extensions.ServiceBus` 5.24.0 no se versionan por .NET y se quedan. Además, borrar las referencias que el framework ya aporta: en .NET 11 son `Microsoft.Extensions.Hosting.Abstractions` y `Microsoft.Extensions.Options`, con su `PackageReference` en el `.csproj` y su `PackageVersion` en el `.props`. Dan `NU1510` y rompen la compilación con los avisos como errores |
   | 4 | `Dockerfile` | Etiquetas `sdk` y `aspnet` de la imagen base, y comprobar en la etapa `sdk` con `dotnet --version` que ese SDK cumple el suelo de `global.json`. La etapa `aspnet` no trae SDK: el mismo comando ahí responde «No .NET SDKs were found» |
   | 5 | `.github/workflows/*.yml` | `dotnet-version` en los cuatro puntos. El nombre del job —hoy `Compilación, formato y pruebas (.NET 10)`— es el del check obligatorio de `main`: si se renombra, se actualiza la protección en el paso 8 o el check deja de exigirse |
   | 6 | `infra/main.bicep` | `linuxFxVersion` del site Linux de la API, el único `Microsoft.Web/sites` de la plantilla: `DOTNETCORE\|11.0`. La Function no tiene recurso propio aquí |
   | 7 | `functions/OrderProcessor` | `Sdk="Microsoft.NET.Sdk"` pasa a `Azure.Functions.Sdk` —1.1.0 probado—, desaparecen la `PackageReference` a `Microsoft.Azure.Functions.Worker.Sdk` y su `PackageVersion` en `Directory.Packages.props`, y `AzureFunctionsVersion` sigue en `v4`. Depende del `msbuild-sdks` del paso 1. El SDK genera `obj/azure_functions/azure_functions.g.csproj`, que no entra en el `restore` de la solución: con `--no-restore` el build emite `AZFW0108` y se restaura solo, así que conviene restaurar ese proyecto explícitamente |
   | 8 | `AGENTS.md`, `README.md`, `docs/app-service.md`, `docs/functions.md`, `docs/capacidades.md`, `docs/dependencias.md`, ADR-0006 y ADR-0008 | La versión que declaran y la imagen base. Si cambia el nombre del check obligatorio de `docs/repo-governance.md` y `devsecops-ci.yml`, la protección de `main` se actualiza en el mismo lote o el check deja de exigirse |
   | 9 | Verificación | Los seis comandos de `AGENTS.md`, la suite del emulador y la imagen construida con UID no privilegiado. En .NET 11 y posteriores, `ObservabilityTests` falla antes de tocar código: el `traceId` puede terminar en `-03` por el flag `random-trace-id` de W3C Trace Context nivel 2, así que la regex pasa a `0[0-3]` o se comprueba con `HasFlag`. Si los comandos .NET se ejecutan en contenedor, con la identidad del usuario o limpiando después: como `root` deja `bin/` y `obj/` que la máquina ya no puede borrar |

   El repositorio no se despliega, así que el canario de `docs/dependencias.md` es local: emulador de Service Bus y contenedor. Si algún día hay despliegue, el canario es un entorno con tráfico mínimo y este protocolo se amplía con ese paso.

   **Estado del protocolo.** Se ejecutó completo, los nueve pasos, sobre .NET 11 el 23 de noviembre de 2026 en la [PR #84](https://github.com/lanuzalorenzo/lab-api-local-azure/pull/84), cerrada sin merge: 26 archivos, 51 líneas añadidas y 54 borradas, 95 de 95 pruebas —45 unitarias, 7 de arquitectura, 10 de infraestructura, 29 de integración y las 4 del emulador—, `dotnet format` limpio y los siete checks del pipeline en verde, con la imagen publicada y el emulador de Service Bus por medio. La única prueba que falló en la primera pasada es la del paso 9, y se cierra con el ajuste que ese paso describe: el fallo es la prueba de que el paso hace falta. El paso 8 encontró 22 menciones de la versión en 10 archivos: 16 declaran el estado vigente y cambian, 6 son medidas fechadas de ADR-0006 y no se reescriben, y 1 es el nombre del check obligatorio, que no se toca mientras el job conserve el suyo. La cobertura de líneas sigue sin ser medible: `Directory.Build.props` fija `DebugType` en `none` y `DebugSymbols` en `false`. El lote no toca código de aplicación salvo la regex de la prueba del paso 9.

## 3. Alternativas descartadas

- **Migrar a .NET 11 ahora.** Mismo fin de soporte que .NET 10, cinco días transcurridos desde que Functions lo admite y ninguna de sus novedades cubre una carencia actual del repositorio. Coste alto y retorno nulo.
- **Dejarlo como estaba: sin decisión escrita y sin `global.json`.** Es lo que había: el fin de soporte sin dueño ni fecha y ninguna barrera a que una máquina compile con 11.x mientras el pipeline usa 10.0.x.
- **Multitarget `net10.0;net11.0`.** Dobla compilación y pruebas para contentar a consumidores que no existen; este repositorio produce un servicio, no una biblioteca.
- **Adoptar cada versión por costumbre, incluidos los STS.** Una migración al año en un repositorio que no despliega y sin ganancia de soporte. El ensayo de .NET 11 no rompió el emulador —4 de 4 pruebas— sino la prueba de observabilidad; el motivo para no migrar no es que algo se rompa, es que no hay nada que ganar.
- **Publicación autocontenida para no depender del runtime de la imagen.** Traslada a este repositorio la responsabilidad de parchear el runtime y engorda la imagen, a cambio de un control que hoy no se necesita.

## 4. Consecuencias

- Hasta 2027 no hay tipos unión de C# 15, validación asíncrona, OpenAPI 3.2 ni las novedades criptográficas de .NET 11. Si alguna pasa a ser necesaria —no deseable, necesaria—, se adelanta la migración con este ADR como referencia y su propio criterio.
- `global.json` convierte un fallo silencioso en un mensaje explícito: sin un SDK `10.0.115` o posterior, el build se detiene en vez de compilar con otro compilador.
- La migración hereda un cambio de comportamiento: desde .NET 11 el `traceId` lleva el flag `random-trace-id` y puede terminar en `-03`. Una prueba que lo valide, o un consumidor que lo parsee, tiene que admitir los cuatro valores y no solo `-01`.
- El protocolo está probado de extremo a extremo: ocho de los nueve pasos necesitaron corrección después de ejecutarlo, y solo el 2 —el `TargetFramework` de los proyectos— estaba bien escrito. La migración de 2027 cambia el destino y repite el recorrido con el procedimiento ya corregido.
- Cambiar de versión es un recorrido de nueve pasos en cinco clases de sitio —proyectos, paquetes, imagen, pipeline e infraestructura— con `global.json` como puerta de entrada; el ensayo lo midió en 34 líneas añadidas y 37 borradas por 18 archivos. La arqueología era antes; ahora está en la tabla.
- Dependabot no gestiona ni el SDK ni el `TargetFramework`. Es manual por diseño, y ahora con procedimiento: `docs/dependencias.md` deja de declararlo fuera de alcance.
- La fecha de fin de soporte tiene un dueño y una revisión: noviembre de 2027, no «cuando toque».

## 5. Criterio para revisarla

Se revisa en noviembre de 2027, con .NET 12 disponible, para ejecutar el protocolo o dejar escrito por qué no. Se adelanta la revisión si ocurre alguna de estas tres cosas:

- Un fallo de seguridad crítico en .NET 10 no se corrige dentro de su rama.
- Azure Functions deja de admitir el modelo isolated en la versión que usa el repositorio.
- Una capacidad que el repositorio necesita —no una que simplemente gusta— no existe en .NET 10 y sí en una versión posterior.

También se revisa si Microsoft cambia la duración del soporte LTS o STS, porque el cálculo del §1 depende de ella y es lo único que hoy hace equivalentes a .NET 10 y .NET 11.
