# 🔐 Registro de la API en Azure AD

## 🧾 Descripción
Guía técnica para registrar la API del laboratorio en Azure Active Directory (Azure AD).  
Incluye la creación del App Registration, configuración del Application ID URI, definición de scopes y asignación de permisos a aplicaciones cliente.

---

## 🏛️ 1. ¿Qué es Azure AD y App Registration?

### Azure AD
Servicio de identidad de Microsoft que proporciona:
- Autenticación centralizada  
- Gestión de identidades  
- Control de acceso basado en roles (RBAC)  
- Emisión y validación de tokens JWT  

### App Registration
Proceso mediante el cual una API o aplicación:
- Se identifica en Azure AD  
- Puede autenticar usuarios  
- Puede autorizar aplicaciones cliente  
- Expone permisos (scopes) para acceso seguro  

---

## 🧩 2. Crear el registro de la API

### Acceder al portal
1. Ir a: https://portal.azure.com  
2. Iniciar sesión  
3. Buscar: **Azure Active Directory**  
4. Abrir: **App registrations**

### Crear nuevo registro
- **Name:** EcommerceApi  
- **Supported account types:**  
  - *Accounts in this organizational directory only* (recomendado)  
- **Redirect URI:** vacío (las APIs no lo necesitan)

Hacer clic en **Register**.

---

## 🧩 3. Obtener Tenant ID, Client ID y Application ID URI

Tras registrar la API, en **Overview** aparecen:

### Tenant ID (Directory ID)
Ejemplo:
```
00000000-0000-0000-0000-000000000000
```

### Client ID (Application ID)
Ejemplo:
```
11111111-1111-1111-1111-111111111111
```

### Application ID URI
1. Ir a **Expose an API**  
2. En *Application ID URI*, hacer clic en **Set**  
3. Azure sugiere:
```
api://[CLIENT-ID]
```
4. Se puede personalizar:
```
api://ecommerce-api
```

Guardar cambios.

---

## 🔧 4. Crear scopes (API permissions)

### ¿Qué es un scope?
Un permiso granular que una aplicación cliente puede solicitar.

Ejemplos:
- `api://ecommerce-api/order.read`  
- `api://ecommerce-api/order.write`  
- `api://ecommerce-api/admin`  

### Crear scopes
1. Ir a **Expose an API**  
2. En *Scopes defined by this API*, clic en **Add a scope**  
3. Crear:

#### Scope: order.read
- **Scope name:** order.read  
- **Admin consent display name:** Read orders  
- **Admin consent description:** Allows reading orders  
- **User consent display name:** Read orders  
- **User consent description:** Allows you to read orders  
- **State:** Enabled  

#### Scope: order.write
- **Scope name:** order.write  
- **Admin consent display name:** Manage orders  
- **Admin consent description:** Allows creating and updating orders  
- **User consent display name:** Manage orders  
- **User consent description:** Allows you to create and update orders  
- **State:** Enabled  

Scopes resultantes:
```
api://ecommerce-api/order.read
api://ecommerce-api/order.write
```

---

## 🧩 5. Asignar permisos a aplicaciones cliente

### Aplicaciones cliente (Confidential Clients)
1. Registrar la aplicación cliente  
2. Ir a **API permissions**  
3. Clic en **Add a permission**  
4. Seleccionar **My APIs**  
5. Elegir **EcommerceApi**  
6. Añadir scopes necesarios  
7. Conceder **Admin consent** si aplica  
8. Crear un **Client Secret** en *Certificates & secrets*

⚠️ Importante:  
El valor del secret solo se muestra una vez.  
Debe almacenarse en Key Vault o variables de entorno.

---

## 🧩 6. Aplicaciones frontend (Public Clients)
- Registrar la aplicación  
- Añadir permisos en **API permissions**  
- Configurar correctamente los **Redirect URIs**

---

## 🔒 7. Mejores prácticas de seguridad

### Rotación de secrets
- Cambiar cada 6–12 meses  
- Evitar expiración “Never”

### Managed Identity
- Usar en Azure App Service, Containers o VMs  
- Evita almacenar secrets

### Almacenamiento seguro
- Nunca guardar secrets en código  
- Usar Key Vault o variables de entorno

### Principio de mínimo privilegio
- Asignar solo los scopes necesarios

### Validación de tokens
- Validar firma  
- Validar issuer  
- Validar audiencia  
- Validar expiración  

### HTTPS obligatorio
- Todos los endpoints deben usar HTTPS

---

## ⚠️ 8. Errores comunes

| Error | Causa | Solución |
|-------|--------|----------|
| invalid_client | Client ID incorrecto | Verificar Application ID |
| invalid_scope | Scope no registrado | Revisar *Expose an API* |
| unauthorized_client | Cliente sin permisos | Añadir API permissions |
| AADSTS50001 | Tenant no encontrado | Revisar Tenant ID |
| AADSTS70001 | Application no encontrada | Revisar Application ID URI |

---

## 🔐 9. Checklist final
- Application ID URI configurado  
- Scopes creados  
- Aplicación cliente registrada  
- Permisos asignados  
- Admin consent concedido  
- Client secret almacenado correctamente  
- HTTPS habilitado  
- Validación completa de tokens JWT  
- Logs de autenticación monitorizados  
- Plan de rotación de secrets activo  
