# Configuración de Microsoft Entra ID en la API

Esta guía describe la configuración **real** del código actual. Los valores
sensibles no se guardan en el repositorio: usa `appsettings.Development.json`
(ignorado por Git), user-secrets o variables de entorno.

## 1. Opciones de configuración

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

`appsettings.json` solo contiene placeholders (`YOUR_TENANT_ID`,
`YOUR_API_CLIENT_ID`). La API arranca con ellos, pero registra una advertencia y
no podrá validar tokens reales hasta que se configure un tenant.

## 2. Validación JWT en `Program.cs`

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = azureAdOptions.Authority;
        options.Audience = azureAdOptions.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = azureAdOptions.Authority,
            ValidateAudience = true,
            ValidAudience = azureAdOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
```

La firma se valida con las claves públicas (JWKS) que publica Entra ID en el
documento de metadatos de la autoridad configurada.

## 3. Orden del pipeline

```csharp
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseHsts();               // solo en Production
app.Use(async (context, next) => { /* cabeceras de seguridad */ });
app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
```

`UseAuthentication()` debe ejecutarse antes de `UseAuthorization()`. `UseCors()`
se ejecuta antes de autenticación para que las respuestas de preflight no
requieran token.

## 4. CORS

La política `ApiCors` es denegatoria por defecto:

- `Cors:AllowedOrigins` vacío → `SetIsOriginAllowed(_ => false)`.
- Con orígenes configurados → allowlist exacta, cabeceras y métodos permitidos.

No se usa `AllowAnyOrigin()`. Para un frontend local:

```json
{
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  }
}
```

## 5. Rate limiting y límites

- Límite global: 120 peticiones/minuto por IP.
- Endpoints de escritura: 16 KB de cuerpo máximo.
- Respuesta de límite excedido: `429 Too Many Requests`.

Es un control de laboratorio. En producción se complementa con API Management,
WAF y cuotas por usuario.

## 6. Autorización por usuario

`OrdersController` obtiene la identidad de `ClaimTypes.NameIdentifier`, `oid` o
`sub`, y filtra los pedidos por `UserId`. Un pedido de otro usuario devuelve
`404` para no revelar su existencia.

La autorización granular por scopes (`scp`) y roles (`roles`) **no está
implementada** todavía. Está documentada como línea de trabajo para no
sobreafirmar controles.

## 7. Registro de la aplicación

Los pasos para crear el App Registration, exponer `api://<client-id>` y asignar
permisos están en [`azure-ad-register.md`](azure-ad-register.md).

## 8. Pruebas

Las pruebas con tokens reales y con el esquema de test están en
[`azure-ad-tests.md`](azure-ad-tests.md) y
[`tests.md`](tests.md).

## 9. Errores comunes

| Síntoma | Causa probable | Comprobación |
|---|---|---|
| `401` en todos los endpoints | Authority/audience incorrectos | Revisa `AzureAd` y el claim `aud` |
| `401` con token válido | Token de otro tenant o API | Verifica `tid`, `iss` y `aud` |
| `403` | Falta de autorización (no implementada por scopes) | Revisa roles/scopes del token |
| `CORS` bloqueado | Origen no allowlisted | Añade el origen exacto en `Cors:AllowedOrigins` |
| `429` | Rate limit | Espera la ventana de un minuto |
