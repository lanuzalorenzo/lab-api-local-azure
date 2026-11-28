# Despliegue por revisiones y vuelta atrás

> **Estado:** Validado (2026-11-26) para revisiones de Container Apps; Runbook preparado para slots de App Service. El camino de App Service no se ha ejecutado: exige Standard o superior y la regla de gasto cero lo deja en Diseño. El camino de Container Apps se ejecutó con dos revisiones y vuelta atrás (ver §6). Evidencia: [2026-11-26-08-red-y-api](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/blob/main/laboratorio-final/evidencias/2026-11-26-08-red-y-api/README.md).

Publicar sin vuelta atrás es como cambiar la cerradura y tirar la llave vieja: si la nueva no gira, ya no hay forma de entrar. Este documento fija cómo se publica una versión nueva de la API y cómo se vuelve a la anterior sin improvisar. No sustituye a la evidencia: cada ejecución se registra con fecha, comando y salida.

## 1. Qué se protege

Un despliegue sin vuelta atrás convierte cualquier cambio en una apuesta. La regla es simple: **toda versión publicada tiene una versión anterior identificable y un comando que la restaura**.

| Pieza | Versión anterior | Cómo se vuelve |
|---|---|---|
| Container Apps | Revisión con nombre y fecha | Se devuelve el 100 % del tráfico a la revisión anterior |
| App Service con slots | Slot `production` anterior | Se repite el *swap* o se usa `--action reset` |

## 2. Camino A: Container Apps (coste cero, el que se usa)

### Antes de desplegar

1. La imagen está publicada y firmada en GHCR con un digest concreto. No se despliega `:main` a ciegas: se fija el digest.
2. La revisión actual está anotada con su nombre y su peso de tráfico.
3. `/health` responde 200 en la revisión actual.
4. El presupuesto y la etiqueta `expires` del grupo de recursos están puestos.

```bash
az containerapp revision list --name <app> --resource-group <rg> --output table
az containerapp ingress show --name <app> --resource-group <rg> --output json
curl -fsS https://<fqdn-revision-nueva>/health
```

### Publicar una revisión nueva

```bash
az containerapp update \
  --name <app> --resource-group <rg> \
  --image ghcr.io/<owner>/ecommerceapi@sha256:<digest> \
  --revision-suffix <aaaa-mm-dd-hhmm>
```

La revisión nueva arranca con el 0 % del tráfico. Se prueba antes de darle entrada:

```bash
az containerapp revision list --name <app> --resource-group <rg> --output table
curl -fsS https://<fqdn>/health
curl -s -o /dev/null -w '%{http_code}\n' https://<fqdn>/api/products
```

El segundo comando debe devolver `401`: la autenticación sigue exigida.

### Dar entrada y volver atrás

```bash
# 100 % a la revisión nueva
az containerapp ingress traffic set --name <app> --resource-group <rg> \
  --revision-weight <revision-nueva>=100 <revision-anterior>=0

# Vuelta atrás: 100 % a la anterior
az containerapp ingress traffic set --name <app> --resource-group <rg> \
  --revision-weight <revision-anterior>=100 <revision-nueva>=0
```

La revisión anterior no se borra hasta que la nueva lleva 24 horas estable. Si se borra antes, la vuelta atrás deja de existir.

### Criterios de aceptación

- `/health` responde 200 en la revisión nueva antes de recibir tráfico.
- `/api/products` sin token responde 401 con `WWW-Authenticate: Bearer` y cuerpo vacío; unificar ese cuerpo con `application/problem+json` sigue abierto.
- La revisión anterior sigue activa y con su imagen anotada.
- El tráfico vuelve al 100 % a la anterior con un solo comando.

## 3. Camino B: App Service con slots (Runbook preparado)

> No se ejecuta con la regla de gasto cero: los slots requieren Standard o superior. Queda como diseño para cuando el propietario autorice el gasto.

### Antes de desplegar

1. Plan Standard o superior; P1v3 es el SKU del diseño.
2. Slot `staging` creado y con la misma configuración no *sticky* que producción.
3. Los ajustes que no deben viajar en el *swap* marcados como *slot settings* (por ejemplo, la cadena de conexión o el origen CORS).
4. `/health` responde 200 en producción.

```bash
az webapp deployment slot create --name <app> --resource-group <rg> --slot staging
az webapp deployment slot list --name <app> --resource-group <rg> --output table
```

### Publicar y probar en el slot

```bash
az webapp deployment source config-zip \
  --name <app> --resource-group <rg> --slot staging --src dist/api.zip

curl -fsS https://<app>-staging.azurewebsites.net/health
curl -s -o /dev/null -w '%{http_code}\n' https://<app>-staging.azurewebsites.net/api/products
```

Si el slot no responde 200, no se hace el *swap*. Se revisan los logs:

```bash
az webapp log tail --name <app> --resource-group <rg> --slot staging
```

### Cambiar producción y volver atrás

```bash
# Producción pasa a la versión probada en staging
az webapp deployment slot swap --name <app> --resource-group <rg> \
  --slot staging --target-slot production

# Vuelta atrás: repetir el swap devuelve el contenido anterior a producción
az webapp deployment slot swap --name <app> --resource-group <rg> \
  --slot production --target-slot staging

# Alternativa: deshacer el último swap sin intercambiar otra vez
az webapp deployment slot swap --name <app> --resource-group <rg> \
  --slot staging --target-slot production --action reset
```

### Criterios de aceptación

- El *swap* solo ocurre con `/health` en 200 en `staging`.
- Producción responde 200 en `/health` y 401 en `/api/products` sin token.
- La vuelta atrás deja producción como estaba, con la misma imagen o el mismo zip anotado.
- Los *slot settings* no se han movido entre slots.

## 4. Qué se registra en la evidencia

| Campo | Ejemplo |
|---|---|
| Fecha y responsable | 2026-11-26, propietario |
| Revisión o slot anterior | `--revision-suffix 2026-11-26-1200` / `staging` |
| Imagen o artefacto | digest `sha256:…` |
| Comandos | los de este runbook, con marcadores sustituidos |
| Salida observada | `/health` 200, `/api/products` 401, tráfico 100/0 |
| Resultado | PASS, FAIL o parcial |
| Limpieza | revisión antigua borrada o RG eliminado |

## 5. Estado por ámbito

| Ámbito | Estado | Qué falta |
|---|---|---|
| Revisiones de Container Apps | **Validado** (2026-11-26) | Nada; los tres pasos están en §6 y en la evidencia |
| Slots de App Service | **Runbook preparado** | Presupuesto para un plan Standard o superior y ejecutarlo |

## 6. Ejecución del 2026-11-26

La aplicación de Container Apps estaba en modo de revisión única. Se cambió a múltiples, se fijó el 100 % del tráfico a la revisión `v1thzxk`, se creó la revisión `t5` con un cambio de entorno inocuo y se probó antes de darle entrada.

| Paso | Comando | Observado | Resultado |
|---|---|---|---|
| Fijar tráfico a la revisión actual | `az containerapp ingress traffic set --revision-weight v1thzxk=100` | `v1thzxk` al 100 % | PASS |
| Crear revisión nueva | `az containerapp update --revision-suffix t5 --set-env-vars LAB_REVISION=t5` | `t5` creada al 0 % | PASS |
| Probar la revisión nueva sin tráfico | `curl https://<revision-t5>/health` y `/api/products` | 200 y 401 | PASS |
| Dar el 100 % a la nueva | `az containerapp ingress traffic set --revision-weight t5=100` | 200 y 401 por el FQDN de la aplicación | PASS |
| Volver a la anterior | `az containerapp ingress traffic set --revision-weight v1thzxk=100` | 200 y 401; tráfico `v1thzxk=100`, `t5=0` | PASS |

La evidencia con los comandos y la salida está en la carpeta de la Fase 1 del índice del laboratorio. El grupo se eliminó el 2026-11-28, así que las revisiones ya no existen.
