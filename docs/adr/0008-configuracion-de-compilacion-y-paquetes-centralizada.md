# ADR-0008 — Configuración de compilación y versiones de paquetes en dos archivos raíz

> **Estado:** Aceptada · 2026-11-06
> **Ámbito:** `Directory.Build.props`, `Directory.Packages.props` y los diez `.csproj` de la solución.
> **Evidencia:** compilación Release con cero avisos y cero errores, 87 pruebas correctas y `dotnet format --verify-no-changes` sobre el árbol de esta rama; el restore se verificó con un feed local temporal porque GitHub Packages devuelve `401` sin token.
> **Relacionada con:** [ADR-0006](0006-imagen-de-contenedor.md), que copia ambos `.props` antes del `restore` de la imagen.

## 1. Contexto

La solución tiene diez proyectos: cinco de la API, cuatro de pruebas y el procesador de Functions. Cada `.csproj` repetía `Nullable`, `ImplicitUsings` y la versión de cada paquete. Esa repetición ya había producido una divergencia real: `xunit.runner.visualstudio` estaba en 2.8.2 en `tests/EcommerceApi.Tests` y en 3.1.5 en los otros tres proyectos de prueba. Además, ningún proyecto auditaba las dependencias transitivas ni convertía un aviso de vulnerabilidad en error de compilación.

`Directory.Build.props` y `Directory.Packages.props` son mecanismos de MSBuild y NuGet, no una imposición de la plataforma. Microsoft documenta el primero para [personalizar la compilación por directorio](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory) —MSBuild lo importa solo si está en el directorio del proyecto o en uno superior— y el segundo como [gestión central de paquetes](https://learn.microsoft.com/nuget/consume-packages/central-package-management), con plantilla propia (`dotnet new packagesprops`). Son la vía soportada cuando un repositorio tiene varios proyectos que comparten configuración, y evitan que cada archivo sea una copia que se desvía.

## 2. Decisión

1. **`Directory.Build.props` en la raíz** fija la configuración común de compilación:
   - `Nullable` e `ImplicitUsings` activados.
   - `TreatWarningsAsErrors` para que un aviso no llegue a `main`.
   - `NuGetAudit` en modo `all` y `NU1901`–`NU1904` como errores, de modo que una dependencia vulnerable —directa o transitiva— rompa la compilación.
2. **`Directory.Packages.props` en la raíz** activa `ManagePackageVersionsCentrally`. Cada paquete tiene una sola `<PackageVersion>` y los `.csproj` declaran `<PackageReference>` sin `Version`.
   - El rango flotante `0.1.*` de `LL.Hosting` y `LL.Security` se conserva con `CentralPackageFloatingVersionsEnabled`: mientras esos paquetes no estabilicen su API, el laboratorio quiere recibir sus parches sin editar el repositorio.
   - `xunit.runner.visualstudio` se unifica en 3.1.5, la versión que ya usaban tres de los cuatro proyectos de prueba.
3. **`TargetFramework` sigue en cada proyecto.** Todos usan `net10.0` hoy, pero dejarlo ahí permite multi-target sin tocar la configuración común.
4. **`AnalysisLevel` no se centraliza todavía.** Al probar `latest-recommended` apareció `CA1036` en `Money` (implementa `IComparable` y no define los operadores de comparación). Es un hallazgo legítimo, pero mezclar un cambio de analizadores con la centralización habría hecho este lote más grande y difícil de revisar. Se abordará en su propio cambio.

## 3. Alternativas descartadas

- **Dejar la configuración en cada `.csproj`.** Es lo que había: versiones duplicadas y una divergencia que nadie detectó hasta revisar los archivos. La comodidad de no tener archivos raíz se paga en deriva.
- **Un `.props` compartido importado a mano.** Funciona, pero cada proyecto nuevo puede olvidar el `Import`. El archivo raíz se aplica por convención y no depende de que alguien se acuerde.
- **Un script que reescriba versiones.** Automatiza la edición, no la fuente de verdad: sigue habiendo diez sitios donde el valor puede quedar distinto.
- **Fijar `LL.*` a una versión exacta.** Quita el mecanismo de actualización deliberado y obliga a editar el repositorio por cada parche de los paquetes compartidos.

## 4. Consecuencias

- Cambiar una versión es una línea en `Directory.Packages.props` y un `restore`; no hay que recorrer proyectos.
- Una misma ID de paquete usa la misma versión en todo el repositorio. Si un proyecto necesita otra, se justifica con `VersionOverride` o se divide el archivo; no se duplica en silencio.
- El restore necesita acceso al feed que publica `LL.*`. En un clon sin `GH_PACKAGES_TOKEN` con permiso de lectura de paquetes, el restore falla; es una condición del feed privado, no de la configuración central. Con el paquete ya en la caché local se puede verificar con un feed local temporal.
- La auditoría NuGet pasa a formar parte de la compilación: una vulnerabilidad conocida bloquea igual que un error de tipos.

## 5. Criterio para revisarla

Se revisa si aparece un caso real de dos versiones simultáneas del mismo paquete, si la solución se parte en repositorios con ciclos de vida separados, o cuando se aborde la adopción de analizadores. En esos casos, `VersionOverride` o un `Directory.Packages.props` por solución son las salidas previstas, no volver a la versión por proyecto.
