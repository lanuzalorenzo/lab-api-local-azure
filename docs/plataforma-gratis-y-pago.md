# Capas gratuitas y de pago

> **Estado:** Diseño (2026-11-26). La tabla fija qué capa se usa hoy a coste cero, qué servicio de pago haría la misma función en producción, cuánto cuesta y cómo se conmuta. No se ha desplegado el camino de pago.

Elegir capa es como elegir entre la cocina de casa y la de un restaurante: la primera te deja practicar sin coste; la segunda añade extracción, capacidad y seguro, y se paga por ello. Este documento separa lo que valida la capa gratuita de lo que haría falta para producción: si mañana el laboratorio tiene que parecerse a un sistema real, ¿qué se enciende, cuánto cuesta y qué se gana? La capa gratuita sirve para validar el comportamiento; la de pago aporta garantías que la gratis no tiene: SLA, red privada, retención, detección y soporte.

## 1. La regla

- **Gratis valida.** Sirve para demostrar que el código, el contrato y los controles funcionan.
- **De pago produce.** Aporta aislamiento de red, disponibilidad, retención y detección.
- **No se mezcla el discurso.** Un recurso gratuito no se presenta como producción, y un diseño de pago no se presenta como probado. La columna «Qué añade» separa lo que se gana de lo que solo se documenta.

## 2. Mapa por capa

Los precios son minoristas, sin IVA, consultados el 2026-11-26; los que llevan «medido» salen de la tabla de costes del laboratorio y los demás quedan como «por medir» hasta que se consulten.

| Capa | Función | Opción gratuita (la que se usa) | Opción de pago (la de producción) | Coste de referencia | Qué añade |
|---|---|---|---|---|---|
| Cómputo de la API | Ejecutar el servicio | Container Apps con concesión de 180.000 vCPU-s, 360.000 GiB-s y 2 M peticiones al mes, escala a cero; o App Service F1 | App Service P1v3 con integración VNet | 129,94 USD/mes (medido) | `alwaysOn`, slots, autoscale, Private Endpoint y salida por la VNet |
| Cómputo de la Function | Procesar el outbox | Functions Consumption: 1 M ejecuciones y 400.000 GB-s al mes | Functions Premium o plan dedicado | Por medir | Sin arranque en frío, VNet y siempre listo |
| Datos | Persistir pedidos | SQLite en el contenedor o en `/home`; Azure SQL free tier para una prueba de base real | Azure SQL General Purpose serverless o Managed Instance | Por medir fuera del tramo gratis | Copias gestionadas, SLA, escalado y alta disponibilidad |
| Secretos | Guardar credenciales | Identidad gestionada y App Configuration Free (10 MB, 1.000 peticiones/día) | Key Vault Standard | 0,03 USD por 10.000 operaciones (medido) | Rotación, RBAC por secreto, HSM opcional y Private Endpoint |
| Red privada | Sacar la API de Internet | VNet, NSG y subredes | Private Endpoints y zonas DNS privadas | 0,01 USD/h por endpoint y 0,50 USD/zona/mes (medido) | Entrada privada, resolución interna y SCM fuera de Internet |
| Observabilidad | Ver qué pasa | Activity Log, métricas de plataforma y alertas de Activity Log | Log Analytics y Application Insights | 2,99 USD/GB de ingesta (medido) | KQL, trazas correlacionadas, retención y alertas finas |
| Mensajería | Entregar eventos | Emulador local de Service Bus; Event Grid Basic con 100.000 operaciones/mes | Service Bus Standard | Por medir | Colas, sesiones, *dead-letter*, duplicados y entrega garantizada |
| SIEM | Detectar y responder | Nada equivalente en la capa gratuita | Microsoft Sentinel sobre Log Analytics | Por medir | Reglas, incidentes, *playbooks* y retención de 90 días |
| Postura y cargas | Evaluar configuración y amenazas | Defender for Cloud, CSPM básico | Defender for Servers, Storage, APIs y Containers | 4,91 USD/servidor/mes P1 y 14,60 P2 (medido) | Detección de amenazas, JIT, malware y cobertura de contenedores |
| Identidad | Autenticar y autorizar | Entra ID Free: registro de aplicación, *security defaults* y registros de inicio de 7 días | Entra ID P1 y P2 | 6,10 €/usuario/mes P1 y 8,70 P2 (medido) | Acceso condicional, PIM, Identity Protection y riesgo de inicio de sesión |
| Puerta de APIs | Cuotas, validación y políticas | Ninguna equivalente | API Management | Por medir | Cuotas, JWT, transformación, versionado y Defender for APIs |
| Contenedores | Orquestar y aislar | Container Apps | AKS con ACR | AKS sin coste de plano de control; nodos y ACR aparte | Kubernetes, *admission control*, *network policies* y Defender for Containers |
| Entrada y WAF | Proteger el borde | Ninguna equivalente | Front Door con WAF | Por medir | WAF, CDN, origen privado y afinidad |
| Dominio y DNS | Nombre propio | Dominio de plataforma (`azurewebsites.net`, FQDN de Container Apps) | Azure DNS y certificado gestionado | 0,50 USD/zona/mes (medido) | Dominio propio, registros y TLS administrado |
| Administración | Entrar a una máquina | Cloud Shell y SSH con NSG | Azure Bastion | Por medir | RDP/SSH sin IP pública ni saltos |
| Copias | Recuperar datos | Copia manual del fichero SQLite | Azure Backup | Por medir | Copias programadas, retención y restauración probada |
| Máquina virtual | Cargas Windows o Linux | Ninguna | Serie B o Spot | Por medir | Control del sistema operativo y del software instalado |

## 3. Cómo se enciende el camino de pago

La plantilla cubre la capa de pago y se controla con un parámetro. La capa gratuita que se ejecutó —Container Apps— se creó por CLI y todavía no está en la IaC:

```bash
# Solo red: lo que se despliega hoy, coste cero
az deployment group what-if \
  --resource-group <rg> --template-file infra/main.bicep \
  --parameters desplegarServiciosDePago=false \
               tenantId=<tenant-id> apiClientId=<app-id> apiAudience=api://<app-id>

# Capa de servicios: Key Vault, Private Endpoints, DNS, App Service, Log Analytics
az deployment group what-if \
  --resource-group <rg> --template-file infra/main.bicep \
  --parameters desplegarServiciosDePago=true \
               tenantId=<tenant-id> apiClientId=<app-id> apiAudience=api://<app-id>
```

Antes de cambiar el parámetro a `true`:

1. Presupuesto y alerta puestos en la suscripción.
2. Grupo de recursos temporal con etiquetas `lab`, `owner` y `expires`.
3. Ventana de prueba y día de destrucción escritos.
4. Autorización explícita del propietario de la suscripción.

El coste de encender todo lo que la plantilla declara está medido en 163,76 USD/mes; la ventana de siete días, en 37,69 USD. Lo que no está en la plantilla —Sentinel, APIM, AKS, Front Door, Bastion— se suma aparte y hay que medirlo antes de aprobarlo.

## 4. Qué no se puede validar gratis

Estas afirmaciones no se sostienen con la capa gratuita y no deben presentarse como probadas:

- Que la API no tiene entrada pública y que el SCM está cerrado.
- Que el DNS privado resuelve dentro de la VNet.
- Que los secretos viven en Key Vault y se leen con identidad gestionada.
- Que hay detección de amenazas sobre la carga.
- Que existe una puerta de APIs con cuotas y validación.
- Que hay alta disponibilidad o SLA.

Cada una se convierte en evidencia solo cuando se enciende su servicio de pago y se ejecuta la prueba positiva y la negativa.

## 5. Cuándo se justifica el gasto

No se enciende por completar una tabla. Se enciende cuando aparece una de estas razones:

- El laboratorio se enseña a un tercero y necesita un dominio y TLS propios.
- Se quieren datos que sobrevivan a la destrucción del recurso.
- Se va a simular tráfico o un ataque y hace falta aislamiento.
- Se prepara una entrevista o una entrega que pide red privada, SIEM o WAF.
- Un proyecto real reutiliza esta base y necesita SLA.

Mientras no aparezca una, la capa gratuita es suficiente y el diseño de pago queda escrito, medido y listo para encenderse.
