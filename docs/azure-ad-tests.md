# Pruebas de autenticación con Microsoft Entra ID

Guía para validar la API con **tokens reales de Entra ID**. Usa un tenant de laboratorio y tokens desechables. No pegues tokens de producción en sitios externos como jwt.io.

Las pruebas funcionales de la API con el esquema de test (`X-Test-User`) están en [`tests.md`](tests.md).

## 1. Variables de entorno

```bash
export TENANT_ID="00000000-0000-0000-0000-000000000000"
export CLIENT_ID="11111111-1111-1111-1111-111111111111"
export CLIENT_SECRET="<client-secret-de-laboratorio>"
export AUDIENCE="api://11111111-1111-1111-1111-111111111111"
export API_URL="http://localhost:5080"
```

El `CLIENT_SECRET` debe obtenerse de un almacén seguro (Key Vault, user-secrets o variable de entorno). Nunca se commitea.

## 2. Obtener un token (client credentials)

```bash
TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=${CLIENT_ID}" \
  -d "scope=${AUDIENCE}/.default" \
  -d "client_secret=${CLIENT_SECRET}" \
  -d "grant_type=client_credentials" \
  | jq -r '.access_token')

test -n "${TOKEN}" && test "${TOKEN}" != "null" || {
  echo "No se obtuvo token. Revisa tenant, client ID, secret y permisos."
  exit 1
}
```

## 3. Validar que el token funciona

### Health check (anónimo)

```bash
curl -i "${API_URL}/health"
```

Esperado: `200 OK`.

### Sin token (debe fallar)

```bash
curl -i "${API_URL}/api/products"
```

Esperado: `401 Unauthorized`.

### Token inválido (debe fallar)

```bash
curl -i -H "Authorization: Bearer invalid-token" "${API_URL}/api/products"
```

Esperado: `401 Unauthorized`.

### Token válido

```bash
curl -i -H "Authorization: Bearer ${TOKEN}" "${API_URL}/api/products"
```

Esperado: `200 OK`.

## 4. Prueba de BOLA con dos identidades (client credentials)

Para demostrar la separación por identidad de cliente necesitas dos App Registrations con credenciales distintas. Con dos client credentials:

```bash
# Token del cliente A
TOKEN_A=$(curl -s -X POST \
  "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=${CLIENT_A_ID}" \
  -d "scope=${AUDIENCE}/.default" \
  -d "client_secret=${CLIENT_A_SECRET}" \
  -d "grant_type=client_credentials" | jq -r '.access_token')

# Token del cliente B
TOKEN_B=$(curl -s -X POST \
  "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=${CLIENT_B_ID}" \
  -d "scope=${AUDIENCE}/.default" \
  -d "client_secret=${CLIENT_B_SECRET}" \
  -d "grant_type=client_credentials" | jq -r '.access_token')

# A crea un pedido
ORDER_ID=$(curl -s -X POST "${API_URL}/api/orders" \
  -H "Authorization: Bearer ${TOKEN_A}" \
  -H "Content-Type: application/json" \
  -d '{"productIds":[1]}' | jq -r '.id')

# A puede leerlo
curl -i -H "Authorization: Bearer ${TOKEN_A}" "${API_URL}/api/orders/${ORDER_ID}"

# B recibe 404 (no 403) para evitar enumeración
curl -i -H "Authorization: Bearer ${TOKEN_B}" "${API_URL}/api/orders/${ORDER_ID}"
```

Resultado esperado: `200` para A y `404` para B.

> Con client credentials, el claim `sub` identifica a la aplicación cliente, no a una persona. Para pruebas de usuario final usa flujos delegados (authorization code + PKCE) y dos usuarios de laboratorio.

## 5. Swagger con token real

```bash
dotnet run --project EcommerceApi --urls http://localhost:5080
```

Abre <http://localhost:5080/swagger>, pulsa **Authorize** y pega el token sin el prefijo `Bearer`. Swagger solo está activo en `Development`.

## 6. Errores comunes de Entra ID

| Error | Causa | Solución |
|---|---|---|
| `AADSTS50058` | Tenant incorrecto | Revisa `TENANT_ID` |
| `AADSTS700016` | Client ID incorrecto | Revisa el App Registration |
| `AADSTS7000215` | Secret inválido | Genera uno nuevo y guárdalo de forma segura |
| `401` con token válido | Audience/issuer incorrectos | Revisa `AzureAd` y `aud`/`iss` |
| `429` | Rate limit | Espera un minuto |
| CORS bloqueado | Origen no allowlisted | Añade el origen exacto en `Cors:AllowedOrigins` |

## 7. Checklist

- [ ] Health check responde `200` sin token.
- [ ] Endpoint protegido responde `401` sin token.
- [ ] Token inválido responde `401`.
- [ ] Token válido responde `200`.
- [ ] `POST` inválido responde `400`.
- [ ] Cliente B no puede leer el pedido de A (`404`).
- [ ] Los logs no incluyen tokens ni secretos.