# 🔐 Azure AD — Notas de Preparación

## 🧾 Descripción
Documento técnico que resume los conceptos fundamentales de Azure Active Directory (Microsoft Entra ID) y cómo se integran en la API `EcommerceApi`.  
Incluye definiciones clave, flujo de autenticación, cambios necesarios en la API y fases de implementación.

---

# 🧩 1. Conceptos Fundamentales

## Azure Active Directory (Microsoft Entra ID)
Servicio de identidad empresarial que proporciona:
- Autenticación centralizada  
- Autorización basada en roles  
- Federated Identity  
- Multi-factor Authentication (MFA)

---

## App Registration
Objeto que representa la API dentro de Azure AD.

Propósito:
- Registrar la aplicación  
- Generar credenciales (Client ID, Client Secret)  
- Exponer permisos (scopes)

Pasos:
- Azure Portal → App registrations → New registration  
- Nombre: `EcommerceApi`  
- Redirect URI: no necesario para APIs

---

## Client ID
Identificador público de la aplicación.

Características:
- Formato GUID  
- No es secreto  
- Se envía en solicitudes de autenticación

Ejemplo:
```
550e8400-e29b-41d4-a716-446655440000
```

---

## Scopes
Permisos granulares que la API expone.

Ejemplos:
```
api://CLIENT-ID/.default
api://CLIENT-ID/access_as_user
```

Propósito:
- Controlar acceso  
- Implementar mínimo privilegio  
- Definir permisos específicos

---

## JWT (JSON Web Token)
Token firmado que contiene:
- Identidad del usuario  
- Roles y permisos  
- Fecha de expiración  
- Audiencia (aud)  
- Emisor (iss)

Estructura:
```
header.payload.signature
```

---

# 🔐 2. Flujo de Autenticación (OAuth 2.0)

1. Cliente solicita acceso  
2. Azure AD valida credenciales  
3. Azure AD emite un JWT  
4. Cliente envía el JWT en `Authorization: Bearer`  
5. API valida firma, expiración, audiencia y roles  
6. Si es válido → procesa la solicitud  
7. Si no → `401 Unauthorized`

---

# 🧩 3. Cambios en Program.cs

## Configuración actual (sin Azure AD)
```csharp
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

---

## Configuración futura (con Azure AD)
```csharp
builder.Services.AddMicrosoftIdentityWebApiAuthentication(
    builder.Configuration.GetSection("AzureAd")
);

app.UseHttpsRedirection();
app.UseAuthentication();   // Validación de tokens
app.UseAuthorization();    // Autorización por roles
app.MapControllers();
app.Run();
```

Cambios principales:
- Añadir `AddMicrosoftIdentityWebApiAuthentication()`  
- Añadir `UseAuthentication()`  
- Leer configuración desde `appsettings.json`  

---

# 🧩 4. Middleware de Validación de Tokens

El middleware de Microsoft.Identity.Web:
- Extrae el JWT del header  
- Valida firma digital  
- Verifica expiración  
- Comprueba audiencia  
- Extrae claims (roles, groups, oid)

---

# 🧩 5. Decoradores en Controladores

```csharp
[Authorize]
public class ProductsController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    public IActionResult GetAllProducts() { ... }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateProduct(Product product) { ... }
}
```

---

# 🧩 6. Configuración en appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "Audience": "api://YOUR_CLIENT_ID"
  }
}
```

---

# 🧩 7. Fases de Implementación

### Fase 1 — Preparación
- Estructura base  
- Modelos y controladores  
- Documentación

### Fase 2 — Configuración Azure AD
- Crear App Registration  
- Obtener Client ID y Tenant ID  
- Configurar `appsettings.json`  
- Instalar `Microsoft.Identity.Web`

### Fase 3 — Autenticación
- Modificar `Program.cs`  
- Añadir middleware  
- Decorar controladores

### Fase 4 — Testing
- Obtener token  
- Probar endpoints protegidos  
- Validar roles y permisos

### Fase 5 — Seguridad Avanzada
- Rate limiting  
- Logging de seguridad  
- CORS  
- Refresh tokens  
- Key Vault

---

# 🔐 8. Checklist de Seguridad

- HTTPS habilitado  
- Tokens validados en cada solicitud  
- MFA en Azure AD  
- Roles configurados  
- Intentos fallidos registrados  
- Rate limiting  
- Secrets en Key Vault  
- Rotación periódica de tokens  
