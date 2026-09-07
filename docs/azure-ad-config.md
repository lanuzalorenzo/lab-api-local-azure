# 🔐 Configuración de Azure AD en la API Local

## 🧾 Descripción
Guía técnica para integrar autenticación y autorización de Azure Active Directory (Azure AD) en la API local del laboratorio.  
Incluye configuración en `Program.cs`, `appsettings.json`, políticas de autorización, uso de claims y orden correcto de middlewares.

---

## ⚙️ 1. Configuración en Program.cs

### Obtener configuración desde appsettings.json
```csharp
var azureAdConfig = builder.Configuration.GetSection("AzureAd");
var tenantId = azureAdConfig["TenantId"];
var clientId = azureAdConfig["ClientId"];
var audience = azureAdConfig["Audience"];
```

---

## 🔐 2. Autenticación JWT Bearer
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.Audience = audience;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully");
                return Task.CompletedTask;
            }
        };
    });
```

---

## 🔒 3. Autorización con políticas
```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("OrderRead", policy => policy.RequireClaim("scp", "order.read"));
    options.AddPolicy("OrderWrite", policy => policy.RequireClaim("scp", "order.write"));
    options.AddPolicy("Admin", policy => policy.RequireClaim("roles", "Admin"));
});
```

---

## 📘 4. Swagger con soporte JWT
```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
```

---

## 🔄 5. Orden correcto de middlewares
```csharp
app.UseHttpsRedirection();

// Autenticación SIEMPRE antes de autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

---

# 🧩 6. Configuración en appsettings.json
```json
{
  "AzureAd": {
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientId": "11111111-1111-1111-1111-111111111111",
    "Audience": "api://ecommerce-api"
  }
}
```

### Sustituir:
- **TenantId** → Directory (tenant) ID  
- **ClientId** → Application (client) ID  
- **Audience** → Application ID URI  

---

# 🌐 7. Configuración por entorno
```json
{
  "AzureAd": {
    "TenantId": "dev-tenant-id",
    "ClientId": "dev-client-id",
    "Audience": "api://ecommerce-api-dev"
  }
}
```

---

# 🔧 8. Variables de entorno (alternativa)
```bash
export AzureAd__TenantId="00000000-0000-0000-0000-000000000000"
export AzureAd__ClientId="11111111-1111-1111-1111-111111111111"
export AzureAd__Audience="api://ecommerce-api"
```

---

# 📡 9. Autorización en controladores

### Ejemplo con políticas
```csharp
[Authorize(Policy = "OrderRead")]
public IActionResult GetOrder(int id) => Ok($"Order {id}");
```

### Endpoint público
```csharp
[AllowAnonymous]
public IActionResult PublicInfo() => Ok("This is public");
```

---

# 👤 10. Obtener usuario autenticado
```csharp
[Authorize]
[HttpGet("current-user")]
public IActionResult GetCurrentUser()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = User.FindFirst(ClaimTypes.Email)?.Value;
    var scopes = User.FindFirst("scp")?.Value;

    return Ok(new { userId, email, scopes });
}
```

---

# 🧱 11. Autorización global
```csharp
builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});
```

---

# 🔍 12. Validación de tokens JWT
Azure AD valida automáticamente:

- Firma  
- Issuer  
- Audience  
- Expiración  
- Claims  

Ejemplo de payload:
```json
{
  "iss": "https://login.microsoftonline.com/tenant/v2.0",
  "aud": "api://ecommerce-api",
  "scp": "order.read order.write",
  "email": "user@example.com"
}
```

---

# 🛠️ 13. Troubleshooting común

| Problema | Causa | Solución |
|---------|--------|----------|
| 401 en todos los endpoints | Orden incorrecto | `UseAuthentication()` antes de `UseAuthorization()` |
| Token válido pero rechazado | Audience incorrecto | Revisar `Audience` en `appsettings.json` |
| Swagger sin token | Falta configuración | Añadir `AddSecurityDefinition` |
| Claims vacíos | Scopes no asignados | Añadir API Permissions en Azure |

---

# ✔️ Conclusión
Este documento proporciona la configuración completa y estandarizada para integrar Azure AD en la API local del laboratorio, incluyendo autenticación, autorización, políticas, claims y orden correcto de middlewares.
