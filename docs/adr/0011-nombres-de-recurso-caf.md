# ADR-0011 — Nombres de recurso por función CAF

> **Estado:** Aceptada · actualizada el 2026-11-24
> **Ámbito:** `infra/main.bicep` y `infra/main.parameters.example.json`.
> **Evidencia:** [`infra/main.bicep`](../../infra/main.bicep) (`resourceName`, `names`, `regionCodes`), [`scripts/check-infra-names.py`](../../scripts/check-infra-names.py) y [`infra/main.parameters.example.json`](../../infra/main.parameters.example.json).
> **Relacionada con:** [`infra/README.md`](../../infra/README.md).

Cada recurso de Azure necesita un nombre único, y ese nombre acaba en el portal, en los
registros y en las alertas. Este documento fija de dónde sale el nombre: de una sola
función, no de una variable por recurso. Es como el rótulo de las calles de una ciudad:
si cada calle se rotula a mano, dos mapas de la misma ciudad no coinciden.

## 1. Contexto

Hasta el 2026-11-24, `infra/main.bicep` declaraba nueve variables de nombre
(`vnetName`, `appSubnetName`, `privateEndpointSubnetName`, `nsgName`,
`logAnalyticsName`, `keyVaultName`, `appServicePlanName`, `appName`,
`appInsightsName`) y el resto de los nombres se componía dentro de cada recurso,
sobre `namePrefix` y `uniqueString`. Cada recurso nuevo añadía otra variable o otra
cadena, y nada impedía que dos de ellas usaran convenciones distintas ni que un
nombre se escribiera directamente en el recurso.

La plantilla nunca se ha desplegado en una suscripción real; solo se valida con
`bicep build` y Trivy config en el pipeline (`infra/README.md`, párrafo de apertura). Por
eso el cambio de nombres no exige migración de recursos existentes y se puede hacer
antes de la primera ejecución real.

El resto del laboratorio ya exige que el nombre salga de una función que reciba tipo,
carga, entorno y región y aplique las abreviaturas del CAF. La prueba que rechaza un
nombre escrito a mano en el CI vive en `scripts/check-infra-names.py`, dentro del job de
IaC; este ADR fija la convención que esa prueba exige.

## 2. Decisión

1. **Una función, cinco segmentos.** `resourceName(resourceType, workloadName,
   environmentName, regionShort, instanceSuffix)` devuelve
   `<tipo>-<carga>-<entorno>-<región>-<instancia>`.
2. **Los segmentos se declaran una vez.** `workload` (3–7 caracteres, minúsculas y
   dígitos, sin separadores), `environmentName` (`dev`, `test`, `prod`) y `location`
   (`westeurope`, `northeurope`, `spaincentral`, `eastus`) son parámetros; `regionCodes`
   traduce la región a su código CAF y `instanceSuffix` toma cuatro caracteres de
   `uniqueString(resourceGroup().id)`.
3. **Todos los nombres se calculan en un solo objeto**, `names`, y ningún recurso
   compone su nombre con una cadena. Las subredes y los puntos de enlace privados usan
   un papel (`app`, `pe`, `kv`) como segmento de instancia, en lugar del sufijo único.
4. **Los nombres fijados por Azure no pasan por la función.** Las zonas DNS privadas
   (`privatelink.vaultcore.azure.net`, `privatelink.azurewebsites.net`) y el nombre
   `logs` de la configuración del App Service son los que impone la plataforma.
5. **Abreviaturas CAF usadas:** `vnet`, `snet`, `nsg`, `log`, `kv`, `plan`, `app`,
   `appi`, `diag`, `pe`, `pec`, `pdnszg` y `vnetlink`.
6. **La prueba del CI recorre la plantilla.** `scripts/check-infra-names.py` lee cada
   declaración `resource` —incluidas las de `existing`, `if` o bucle— y exige que el
   `name` de primer nivel sea `names.<clave>`; comprueba además que cada entrada de
   `names` llama a `resourceName`. Admite las excepciones de la plataforma y el
   `guid(...)` de una asignación de rol, que no es un nombre de recurso. Cualquier otro
   valor hace fallar el job de IaC. La comprobación recorre todos los `.bicep` de
   `infra/`: un módulo debe calcular sus nombres con `resourceName` igual que la
   plantilla principal.

El límite de siete caracteres en `workload` y las regiones de tres letras no son
arbitrarios: con un entorno de cuatro letras (`test`/`prod`) y el sufijo de cuatro, el
nombre del Key Vault mide exactamente 24 caracteres, su máximo. Un `workload` con
mayúsculas, un separador o un espacio detiene el despliegue con un mensaje propio antes
de crear nada; los demás caracteres inválidos los rechaza Azure al crear el recurso.
Bicep no admite una expresión regular en los parámetros.

## 3. Alternativas descartadas

- **Mantener una variable por recurso.** Es lo que había: funciona, pero la convención
  vive en cada variable y en cada recurso, y la deriva no se detecta.
- **Un prefijo común en una sola variable.** Reduce la repetición, pero no distingue
  tipo ni aplica abreviaturas; el nombre del tipo vuelve a escribirse en el recurso.
- **Truncar el nombre por tipo dentro de la función.** Evita el error de longitud, pero
  puede cortar el sufijo único o dejar un guion final, y el nombre deja de ser legible.
  Se prefiere acotar la entrada con `@maxLength` y documentar el motivo.
- **Un módulo de nombres publicado aparte.** No hay artefacto compartido para ello y un
  módulo de una sola plantilla añade una indirección sin reutilización real.

## 4. Consecuencias

- Cambian los nombres de todos los recursos de la plantilla. Como no hay despliegue, no
  hay recursos que renombrar ni estados que migrar.
- Un `workload` de más de siete caracteres, una región fuera de la tabla o un entorno
  fuera de la lista fallan al desplegar con un mensaje claro de parámetro.
- Añadir una región o un tipo de recurso es una línea en `regionCodes` o en `names`;
  el resto de la plantilla no cambia.
- La comprobación de CI que rechaza nombres escritos a mano
  (`scripts/check-infra-names.py`) se apoya en esta convención: recorre las propiedades
  `name` de primer nivel de cada recurso y rechaza todo lo que no sea `names.<clave>`,
  salvo los nombres que fija la plataforma (`logs` y las zonas DNS privadas) y el
  `guid(...)` de la asignación de rol.
- El sufijo de cuatro caracteres sale de `uniqueString(resourceGroup().id)`: dos grupos
  de recursos distintos con la misma carga, entorno y región pueden generar el mismo
  nombre para el Key Vault y el App Service, que son únicos a nivel global. Se acepta en
  un laboratorio sin despliegue; un despliegue real con varios entornos compartidos
  necesita un sufijo más largo o un nombre global explícito.
- Trivy config y `bicep lint` siguen siendo la validación estática; el cambio no altera
  ninguna propiedad de seguridad de los recursos.

## 5. Criterio para revisarla

Se revisa si aparece un segundo workload en la misma suscripción (el segmento de carga
dejaría de ser un parámetro único), si un tipo de recurso necesita una restricción de
longitud o de caracteres que la función no pueda cumplir sin truncar, o si se adopta
una política de Azure que imponga otro patrón de nombres.
