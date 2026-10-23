# Registro de la API en Microsoft Entra ID

> **En fácil:** registrar la API es darle una identidad propia en el directorio de Microsoft: un nombre, una dirección y una lista de permisos. Esta guía describe los pasos en el portal; no significa que ya se hayan ejecutado.

## 1. Qué es Entra ID y el registro de una aplicación

### Entra ID

Microsoft Entra ID (antes Azure AD) es el servicio de identidad de Microsoft. Proporciona:

- Autenticación centralizada.
- Gestión de identidades.
- Control de acceso basado en roles (RBAC).
- Emisión y validación de tokens JWT.

### Registro de la aplicación

El registro permite que una API o aplicación:

- Se identifique en Entra ID.
- Autentique usuarios.
- Autorice aplicaciones cliente.
- Exponga permisos (*scopes*) para un acceso seguro.

## 2. Crear el registro de la API

### Acceder al portal

1. Abre <https://portal.azure.com>.
2. Inicia sesión.
3. Busca **Microsoft Entra ID**.
4. Abre **App registrations**.

### Crear el registro

- **Name:** EcommerceApi.
- **Supported account types:** *Accounts in this organizational directory only* (recomendado).
- **Redirect URI:** vacío (las APIs no lo necesitan).

Selecciona **Register**.

## 3. Obtener Tenant ID, Client ID y Application ID URI

Tras registrar la API, estos valores aparecen en **Overview**.

### Tenant ID (Directory ID)

Ejemplo:

```text
00000000-0000-0000-0000-000000000000
```

### Client ID (Application ID)

Ejemplo:

```text
11111111-1111-1111-1111-111111111111
```

### Application ID URI

1. Ve a **Expose an API**.
2. En *Application ID URI*, selecciona **Set**.
3. Azure propone:

```text
api://[CLIENT-ID]
```

4. Puedes personalizarla:

```text
api://ecommerce-api
```

Guarda los cambios.

## 4. Crear scopes (API permissions)

### Qué es un scope

Un permiso granular que una aplicación cliente puede solicitar. Ejemplos:

- `api://ecommerce-api/order.read`
- `api://ecommerce-api/order.write`
- `api://ecommerce-api/admin`

### Crear los scopes

1. Ve a **Expose an API**.
2. En *Scopes defined by this API*, selecciona **Add a scope**.
3. Crea los dos scopes siguientes.

#### Scope: order.read

- **Scope name:** order.read.
- **Admin consent display name:** Read orders.
- **Admin consent description:** Allows reading orders.
- **User consent display name:** Read orders.
- **User consent description:** Allows you to read orders.
- **State:** Enabled.

#### Scope: order.write

- **Scope name:** order.write.
- **Admin consent display name:** Manage orders.
- **Admin consent description:** Allows creating and updating orders.
- **User consent display name:** Manage orders.
- **User consent description:** Allows you to create and update orders.
- **State:** Enabled.

Scopes resultantes:

```text
api://ecommerce-api/order.read
api://ecommerce-api/order.write
```

## 5. Asignar permisos a aplicaciones cliente

### Aplicaciones cliente (confidential clients)

1. Registra la aplicación cliente.
2. Ve a **API permissions**.
3. Selecciona **Add a permission**.
4. Elige **My APIs**.
5. Selecciona **EcommerceApi**.
6. Añade los scopes necesarios.
7. Concede **Admin consent** si procede.
8. Crea un **Client Secret** en *Certificates & secrets*.

> **Aviso:** el valor del secret solo se muestra una vez. Guárdalo en Key Vault o en una variable de entorno.

## 6. Aplicaciones frontend (public clients)

- Registra la aplicación.
- Añade los permisos en **API permissions**.
- Configura correctamente los **Redirect URIs**.

## 7. Buenas prácticas de seguridad

### Rotación de secrets

- Cambia el secret cada 6–12 meses.
- Evita la expiración «Never».

### Identidad gestionada

- Úsala en Azure App Service, contenedores o máquinas virtuales.
- Evita almacenar secrets.

### Almacenamiento seguro

- Nunca guardes secrets en el código.
- Usa Key Vault o variables de entorno.

### Mínimo privilegio

- Asigna solo los scopes necesarios.

### Validación de tokens

- Valida la firma.
- Valida el emisor.
- Valida la audiencia.
- Valida la expiración.

### HTTPS obligatorio

- Todos los endpoints deben usar HTTPS.

## 8. Errores comunes

| Error | Causa | Solución |
|---|---|---|
| `invalid_client` | Client ID incorrecto | Verifica el Application ID |
| `invalid_scope` | Scope no registrado | Revisa *Expose an API* |
| `unauthorized_client` | Cliente sin permisos | Añade API permissions |
| `AADSTS50001` | Tenant no encontrado | Revisa el Tenant ID |
| `AADSTS70001` | Aplicación no encontrada | Revisa el Application ID URI |

## 9. Checklist final

- [ ] Application ID URI configurado.
- [ ] Scopes creados.
- [ ] Aplicación cliente registrada.
- [ ] Permisos asignados.
- [ ] Admin consent concedido.
- [ ] Client secret almacenado correctamente.
- [ ] HTTPS habilitado.
- [ ] Validación completa de tokens JWT.
- [ ] Logs de autenticación monitorizados.
- [ ] Plan de rotación de secrets activo.
