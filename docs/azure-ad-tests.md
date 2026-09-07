# 🧪 Pruebas de Azure AD — Guía Técnica

## 🧾 Descripción
Guía completa para realizar pruebas de autenticación y autorización con Azure Active Directory (Azure AD) utilizando tokens JWT y la API local del laboratorio.  
Incluye obtención de tokens, validación, pruebas con curl, pruebas en Swagger, errores comunes y un script automatizado.

---

# 🔐 1. Obtener un Token JWT desde Azure AD

## Opción 1 — Usando curl

### Paso 1: Variables necesarias
```bash
TENANT_ID="00000000-0000-0000-0000-000000000000"
CLIENT_ID="11111111-1111-1111-1111-111111111111"
CLIENT_SECRET="your-client-secret-here"
RESOURCE="api://ecommerce-api"
```

### Paso 2: Solicitar token
```bash
curl -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${RESOURCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials"
```

### Paso 3: Guardar token en variable (bash)
```bash
TOKEN=$(curl -s -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${RESOURCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials" \
| jq -r '.access_token')

echo "Token obtenido: $TOKEN"
```

---

## Opción 2 — Usando jwt.io
1. Ir a [https://jwt.io](https://jwt.io)  
2. Pegar el token en “Encoded”  
3. Ver claims como `iss`, `aud`, `scp`, `exp`

Ejemplo de payload:
```json
{
  "iss": "https://login.microsoftonline.com/tenant/v2.0",
  "aud": "api://ecommerce-api",
  "scp": "order.read order.write"
}
```

---

# 🧪 2. Pruebas con curl

## Prueba 1 — Endpoint público
```bash
curl http://localhost/api/products/public/info
```

Respuesta esperada:
```json
{ "message": "This is public" }
```

---

## Prueba 2 — Endpoint protegido con token
```bash
curl -H "Authorization: Bearer $TOKEN" \
http://localhost/api/orders
```

---

## Prueba 3 — Endpoint protegido sin token
```bash
curl http://localhost/api/orders
```

Esperado: `401 Unauthorized`

---

## Prueba 4 — Token inválido
```bash
curl -H "Authorization: Bearer invalid-token" \
http://localhost/api/orders
```

---

## Prueba 5 — Verificar claims
```bash
curl -H "Authorization: Bearer $TOKEN" \
http://localhost/api/orders/current-user
```

---

## Prueba 6 — Token expirado
```bash
curl -H "Authorization: Bearer expired-token" \
http://localhost/api/orders
```

---

## Prueba 7 — Scope insuficiente
```bash
curl -H "Authorization: Bearer token-with-order-read-only" \
-X POST \
-H "Content-Type: application/json" \
-d '{"name": "New Product"}' \
http://localhost/api/products
```

Esperado: `403 Forbidden`

---

# 🧪 3. Pruebas en Swagger

## Paso 1 — Abrir Swagger
```
http://localhost/swagger
```

## Paso 2 — Obtener token  
Usar método anterior.

## Paso 3 — Autorizar
- Clic en **Authorize**
- Pegar token (sin “Bearer”)
- Clic en **Authorize**

## Paso 4 — Ejecutar pruebas
- Expandir endpoint  
- “Try it out”  
- “Execute”

---

# ⚠️ 4. Errores comunes

| Error | Causa | Solución |
|------|--------|----------|
| AADSTS50058 | Tenant incorrecto | Verificar `TENANT_ID` |
| AADSTS700016 | Client ID incorrecto | Revisar Application ID |
| AADSTS7000215 | Secret inválido | Crear uno nuevo |
| 401 Unauthorized | Audience incorrecto | Revisar `Audience` en `appsettings.json` |
| 403 Forbidden | Scope insuficiente | Añadir permisos en Azure AD |
| Token inválido | Formato incorrecto | Verificar estructura `header.payload.signature` |

---

# 🧪 5. Script automatizado de pruebas

Guardar como `test-api.sh`:

```bash
#!/bin/bash
set -e

TENANT_ID="00000000-0000-0000-0000-000000000000"
CLIENT_ID="11111111-1111-1111-1111-111111111111"
CLIENT_SECRET="your-client-secret"
RESOURCE="api://ecommerce-api"
API_URL="http://localhost"

echo "Iniciando pruebas..."

# Obtener token
RESPONSE=$(curl -s -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${RESOURCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials")

TOKEN=$(echo "$RESPONSE" | jq -r '.access_token')

# Pruebas
curl -s "$API_URL/api/products/public/info" | jq '.'
curl -s -H "Authorization: Bearer $TOKEN" "$API_URL/api/orders" | jq '.'
curl -s "$API_URL/api/orders" || echo "Error esperado"
curl -s -H "Authorization: Bearer $TOKEN" "$API_URL/api/orders/current-user" | jq '.'

echo "Pruebas completadas"
```

---

# 📋 6. Checklist antes de producción

- Token se obtiene correctamente  
- Endpoint público funciona  
- Endpoint protegido rechaza sin token  
- Endpoint protegido acepta token válido  
- Scopes se validan correctamente  
- Swagger configurado  
- Logs correctos  
- HTTPS habilitado  
- Token expira correctamente  
