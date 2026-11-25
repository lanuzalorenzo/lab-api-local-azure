# ADR-0012 — Despliegue por capas de coste: la red siempre, lo demás tras un parámetro

> **Estado:** Aceptada · actualizada el 2026-11-25
> **Ámbito:** `infra/main.bicep`, `infra/main.parameters.example.json` e `infra/README.md`.
> **Evidencia:** [`infra/main.bicep`](../../infra/main.bicep) (parámetro `desplegarServiciosDePago` y las diecisiete condiciones), [`infra/main.parameters.example.json`](../../infra/main.parameters.example.json) y [`infra/README.md`](../../infra/README.md).
> **Relacionada con:** [ADR-0005](0005-observabilidad.md), [ADR-0011](0011-nombres-de-recurso-caf.md).

La plantilla de infraestructura se puede visitar sin amueblarla: la red se puede levantar
y validar, y los recursos que facturan por hora esperan a que alguien lo pida. Es como
enseñar una casa con la instalación hecha y los muebles aún en el almacén; el plano se
comprueba igual y nadie paga el alquiler de los muebles para verlo.

## 1. Contexto

Hasta el 2026-11-25, `infra/main.bicep` describía la arquitectura de referencia completa
en un solo despliegue: VNet, NSG, Key Vault, dos Private Endpoints, dos zonas de DNS
privado, App Service P1v3, Log Analytics y Application Insights. Los nombres ya salían de
una función ([ADR-0011](0011-nombres-de-recurso-caf.md)), pero no había forma de desplegar
una parte: cualquier `az deployment group create` creaba todos los recursos.

Eso choca con el propósito del repositorio. La plantilla se valida con `bicep build`, Trivy
config y la comprobación de nombres, y no se ha aplicado a una suscripción; el día que se
aplique para comprobar la red, el mismo comando crea un plan de App Service que factura por
hora y un área de Log Analytics que factura por gigabyte ingerido. El gasto empieza en el
primer recurso con medidor, sin esperar a que exista un cupo.

## 2. Decisión

Un parámetro booleano, `desplegarServiciosDePago`, con valor por defecto `false`, decide si
se despliega la capa de servicios.

- Con `false`, la plantilla despliega solo la capa de red: la VNet con sus dos subredes y el
  NSG. Son los únicos que se despliegan sin coste.
- Con `true`, despliega la arquitectura completa, igual que antes del cambio.
- Las diecisiete condiciones cubren Key Vault, los dos Private Endpoints y sus grupos de
  zona, las dos zonas de DNS privado y sus enlaces, App Service (plan y sitio), su
  configuración de logs, los dos ajustes de diagnóstico, Application Insights, Log Analytics
  y la asignación de rol de la identidad de la API.
- Las salidas dependen del mismo parámetro: con `false` devuelven cadena vacía en lugar de
  leer propiedades de un recurso que no existe.

## 3. Alternativas descartadas

- **Una segunda plantilla solo con la red.** Duplica nombres, parámetros y reglas; dos
  archivos que describen el mismo entorno se separan a la primera corrección.
- **Etiquetas y una política de Azure que impida crear recursos caros.** No evita el intento:
  el despliegue falla a medias y deja recursos creados antes del error. La política protege
  la suscripción, no el comando.
- **Dejarlo como estaba y confiar en borrar el grupo de recursos al terminar.** El gasto se
  produce entre la creación y el borrado, y una validación de red no necesita pagarlo.

## 4. Consecuencias

- La capa de red se puede desplegar y validar sin coste, y el resto sigue disponible para
  cuando haya presupuesto y autorización.
- El valor por defecto es el seguro: quien olvide el parámetro no crea recursos con medidor.
- Con `false` las salidas de nombres de Key Vault, host de la API, área de Log Analytics e
  identidad quedan vacías; cualquier consumidor debe tratar ese caso.
- El CI no cambia: `bicep build` compila la plantilla con las condiciones y Trivy config la
  escanea. La comprobación de nombres recorre el archivo completo.
- Sigue sin ejecutarse un `what-if` ni un despliegue: no hay suscripción asociada. La prueba
  de que el gate separa las dos capas es la plantilla compilada, donde la capa de red —VNet,
  NSG y sus dos subredes— no lleva condición y los diecisiete recursos restantes sí.

## 5. Criterio para revisarla

Se revisa si un recurso de la capa de servicios pasa a ser siempre gratis —entonces deja de necesitar la
condición—, si aparece una tercera capa de coste con su propio disparador (por ejemplo, la
Function con su Storage), o si el despliegue real demuestra que alguna condición rompe una
dependencia entre recursos.
