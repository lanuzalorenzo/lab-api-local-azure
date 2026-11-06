# Microsoft Entra ID: registro y configuración de la API

Para que la API sepa quién llama no basta con recibir una credencial: hay que comprobar que la emitió el directorio de la organización y que sirve para esta API y no para otra. Es la diferencia entre aceptar un carné y comprobar que es auténtico, que no ha caducado y que es de este club. Este documento reúne el registro de la aplicación en Entra ID, la configuración que la API lee y lo que queda sin implementar.

Los valores sensibles no se guardan en el repositorio: usa `appsettings.Development.json` (ignorado por Git), user-secrets o variables de entorno. Las pruebas con tokens reales están en [`azure-ad-tests.md`](azure-ad-tests.md).

## 1. Conceptos clave

| Término | Qué es |
|---|---|
| **Tenant ID** | Identificador del directorio de Entra ID, el espacio donde viven las cuentas y las aplicaciones |
| **Client ID** | Identificador público del registro de la API en ese directorio |
| **Audience** | Application ID URI, por ejemplo `api://<client-id>` |
| **Authority** | `https://login.microsoftonline.com/<tenant-id>/v2.0` |
| **Scope** | Permiso granular que expone la API; hoy no se aplica en la autorización |
| **JWT** | Token firmado; los *claims* son los datos que lleva dentro (`iss`, `aud`, `exp`, `oid`/`sub`, `scp`, `roles`) |

## 2. Registro de la aplicación

Registrar la API es darle una identidad propia en el directorio: un nombre, una dirección y una lista de permisos. Los pasos son de portal; no significa que ya se hayan ejecutado.

### 2.1. Crear el registro

1. Abre <https://portal.azure.com> y entra en **Microsoft Entra ID**.
2. Abre **App registrations** y selecciona **New registration**.
3. Nombre: `EcommerceApi`.
4. Tipo de cuenta: *Accounts in this organizational directory only*.
5. Redirect URI: vacío; una API no lo necesita.

Al terminar, **Overview** muestra el **Tenant ID** (Directory ID) y el **Client ID** (Application ID).

### 2.2. Exponer la API

1. Ve a **Expose an API**.
2. En *Application ID URI*, selecciona **Set**. Azure propone `api://<client-id>`; puedes usar algo legible como `api://ecommerce-api`.
3. En *Scopes defined by this API*, crea los permisos que necesite el cliente. Por ejemplo:

| Scope | Para qué |
|---|---|
| `order.read` | Leer pedidos |
| `order.write` | Crear y modificar pedidos |

Los scopes resultantes tienen la forma `api://ecommerce-api/order.read`. **La API no valida scopes todavía**: cualquier token válido accede a todos los endpoints. Están registrados para dejar preparada la autorización granular.

### 2.3. Aplicaciones cliente

Para una aplicación confidencial (servicio o script):

1. Registra la aplicación cliente.
2. En **API permissions**, añade **My APIs** → `EcommerceApi` y los scopes necesarios.
3. Concede **Admin consent** si procede.
4. Crea el secreto en **Certificates & secrets**. El valor solo se muestra una vez: guárdalo en Key Vault o en una variable de entorno.

Para una aplicación frontend (cliente público), configura los **Redirect URIs** y añade los permisos; el flujo interactivo con PKCE lo ejecuta el cliente y este repositorio no lo implementa.

## 3. Configuración en la API

`EcommerceApi/Configuration/AzureAdOptions.cs` enlaza la sección `AzureAd`:

```json
{
  "AzureAd": {
    "TenantId": "<tenant-id>",
    "ClientId": "<api-client-id>",
    "Audience": "api://<api-client-id>",
    "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0"
  }
}
```

`appsettings.json` solo contiene placeholders (`YOUR_TENANT_ID`, `YOUR_API_CLIENT_ID`). La API arranca con ellos, pero registra una advertencia y no valida tokens reales hasta que se configure un tenant.

La validación la registra el paquete `LL.Security`; el host solo aporta emisor y audiencia:

```csharp
builder.Services.AddHardenedJwtBearer(options =>
{
    options.Authority = azureAdOptions.Authority;
    options.Audience = azureAdOptions.Audience;
});
```

El paquete activa firma, emisor, audiencia y vigencia, y falla al arrancar si el emisor no es una URL absoluta o la audiencia está vacía.

La firma se comprueba con las claves públicas (JWKS) del documento de metadatos de la autoridad configurada.

Orden del pipeline:

```csharp
app.UseSecurityHeaders();
app.UseApiProblemDetails();
app.UseApiTransportSecurity(app.Environment);  // HTTPS y HSTS; se salta en Testing
app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapApiHealthChecks();
```

`UseAuthentication()` va antes de `UseAuthorization()`. `UseCors()` va antes de autenticación para que las respuestas de preflight no exijan token.

## 4. CORS, límites y autorización por usuario

La política `ApiCors` es denegatoria por defecto: con `Cors:AllowedOrigins` vacío se rechaza cualquier origen cruzado y no se usa `AllowAnyOrigin()`. Para un frontend local:

```json
{
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  }
}
```

El límite global es de 120 peticiones por minuto y por IP; las escrituras admiten 16 KB de cuerpo y el exceso responde `429` o `413`. El contrato completo y la tabla de límites están en [`api.md`](api.md#7-límites-operativos).

`OrdersController` obtiene la identidad de `ClaimTypes.NameIdentifier`, `oid` o `sub`, y filtra los pedidos por `UserId`. Un pedido de otro usuario devuelve `404` para no revelar su existencia. La autorización granular por scopes (`scp`) y roles (`roles`) **no está implementada**.

## 5. Buenas prácticas de seguridad

- Rota los secretos cada 6–12 meses y evita la expiración «Never».
- Usa identidad gestionada en App Service, contenedores o máquinas virtuales.
- Nunca guardes secretos en el código; usa Key Vault o variables de entorno.
- Asigna solo los scopes necesarios.
- Exige HTTPS en todos los endpoints.
- Valida firma, emisor, audiencia y expiración en el servidor, no en el cliente.

## 6. Errores comunes

| Síntoma | Causa probable | Comprobación |
|---|---|---|
| `401` en todos los endpoints | Authority o audience incorrectos | Revisa `AzureAd` y el claim `aud` |
| `401` con token válido | Token de otro tenant o de otra API | Verifica `tid`, `iss` y `aud` |
| `403` | Falta de autorización por scopes | Revisa `scp` y `roles`; hoy no se aplican |
| CORS bloqueado | Origen no incluido en la allowlist | Añade el origen exacto en `Cors:AllowedOrigins` |
| `429` | Límite de peticiones | Espera la ventana de un minuto |
| `invalid_client` | Client ID incorrecto | Verifica el Application ID |
| `invalid_scope` | Scope no registrado | Revisa *Expose an API* |
| `unauthorized_client` | Cliente sin permisos | Añade API permissions |
| `AADSTS50001` | Tenant no encontrado | Revisa el Tenant ID |
| `AADSTS70001` | Aplicación no encontrada | Revisa el Application ID URI |

## 7. Checklist

- [ ] Application ID URI configurado.
- [ ] Scopes creados (aunque la API aún no los exija).
- [ ] Aplicación cliente registrada y con permisos.
- [ ] Admin consent concedido si procede.
- [ ] Client secret guardado fuera del repositorio.
- [ ] HTTPS habilitado.
- [ ] Validación completa de tokens en `Program.cs`.
- [ ] Plan de rotación de secretos activo.

## 8. Verificación con tokens

Las pruebas con tokens reales, la comprobación de BOLA con dos identidades y los errores de Entra ID están en [`azure-ad-tests.md`](azure-ad-tests.md).
