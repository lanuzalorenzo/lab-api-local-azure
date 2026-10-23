# Notas de preparación — Microsoft Entra ID

> **En fácil:** estas notas explican en orden qué es cada pieza del token y qué hace la API con él. La configuración práctica está en la guía de configuración.

Documento de apoyo para entender el flujo de autenticación de la API. La configuración vigente está en [`azure-ad-config.md`](azure-ad-config.md).

## Conceptos clave

- **Tenant ID**: identificador del directorio de Entra ID.
- **Client ID**: identificador público del App Registration de la API.
- **Audience**: Application ID URI, por ejemplo `api://<client-id>`.
- **Authority**: `https://login.microsoftonline.com/<tenant-id>/v2.0`.
- **Scope**: permiso granular que expone la API. La API actual no aplica autorización por scopes; solo valida el token.
- **JWT**: token firmado con claims `iss`, `aud`, `exp`, `oid`/`sub`, `scp` y `roles`.

## Flujo

1. El cliente se autentica en Entra ID.
2. Entra ID emite un JWT.
3. El cliente envía `Authorization: Bearer <token>`.
4. La API valida firma, issuer, audience y expiración.
5. La API resuelve la identidad (`NameIdentifier`/`oid`/`sub`) y autoriza el recurso.
6. Si el token no es válido, responde `401`.

## Implementación actual

- `JwtBearer` con `Authority` y `Audience` configurables.
- `[Authorize]` en los controladores.
- Pedidos filtrados por `UserId`.
- Swagger solo en `Development`.
- CORS deny-by-default.
- Rate limiting y límite de cuerpo.

No se usa `Microsoft.Identity.Web`; la integración se hace con `Microsoft.AspNetCore.Authentication.JwtBearer`.

## Trabajo pendiente

- Políticas por scopes (`scp`) y roles (`roles`).
- Auditoría persistente de accesos y operaciones.
- Rotación de secretos y uso de identidades gestionadas.
- Validación de un despliegue real con Private Endpoints.

## Checklist

- [ ] App Registration creada.
- [ ] Application ID URI configurado.
- [ ] Scopes definidos.
- [ ] Cliente de laboratorio autorizado.
- [ ] `AzureAd` configurado fuera del repositorio.
- [ ] Token válido aceptado y token inválido rechazado.
- [ ] Pedido ajeno devuelve `404`.
- [ ] Logs sin tokens ni secretos.
