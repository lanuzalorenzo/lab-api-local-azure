# 🧪 Pruebas de Integración con Azure AD

## 🧾 Descripción
Guía técnica para realizar pruebas de autenticación y autorización con Azure Active Directory (Azure AD) utilizando tokens JWT y la API local del laboratorio.  
Incluye obtención de tokens, pruebas con curl, pruebas en Swagger, errores comunes y un script automatizado.

---

# 🔐 1. Obtener un Token JWT desde Azure AD

## Prerrequisitos
- App Registration creada en Azure AD (EcommerceApi)  
- Aplicación cliente registrada con Client Secret  
- Permisos de API asignados  
- Variables necesarias:
  - TENANT_ID  
  - CLIENT_ID  
  - CLIENT_SECRET  
  - AUDIENCE (Application ID URI)

---

## Método 1 — Usar curl

### Definir variables
```bash
TENANT_ID="7133f9a8-4c6c-47a3-b9a7-55bad5090288"
CLIENT_ID="d6800b3e-a409-4129-ba4d-7d56bd55f1a8"
CLIENT_SECRET="tu-client-secret-aqui"
AUDIENCE="api://d6800b3e-a409-4129-ba4d-7d56bd55f1a8"
```

### Obtener token
```bash
curl -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${AUDIENCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials"
```

---

## Método 2 — Guardar token en variable
```bash
TOKEN=$(curl -s -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${AUDIENCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials" \
| jq -r '.access_token')

echo "Token: $TOKEN"
```

---

# 🧪 2. Pruebas con curl

## GET /api/products (protegido)
```bash
curl -H "Authorization: Bearer $TOKEN" \
http://localhost/api/products
```

## GET /api/products/{id}
```bash
curl -H "Authorization: Bearer $TOKEN" \
http://localhost/api/products/1
```

## GET /api/orders
```bash
curl -H "Authorization: Bearer $TOKEN" \
http://localhost/api/orders
```

## POST /api/orders
```bash
curl -X POST \
-H "Authorization: Bearer $TOKEN" \
-H "Content-Type: application/json" \
-d '{"productId":1,"quantity":2,"totalPrice":1999.98}' \
http://localhost/api/orders
```

## Sin token (debe fallar)
```bash
curl http://localhost/api/products
```

## Token inválido (debe fallar)
```bash
curl -H "Authorization: Bearer invalid-token" \
http://localhost/api/products
```

---

# 🧪 3. Pruebas en Swagger

## Paso 1 — Ejecutar API
```bash
dotnet run
```

Swagger:
```
http://localhost/swagger
```

## Paso 2 — Obtener token  
Usar métodos anteriores.

## Paso 3 — Autorizar
- Clic en **Authorize**  
- Pegar token (sin “Bearer”)  
- Confirmar

## Paso 4 — Probar endpoints
- Expandir  
- Try it out  
- Execute  

---

# ⚠️ 4. Errores comunes

| Error | Causa | Solución |
|-------|--------|----------|
| AADSTS50058 | Tenant incorrecto | Verificar TENANT_ID |
| AADSTS700016 | Client ID incorrecto | Revisar Application ID |
| AADSTS7000215 | Secret inválido | Crear nuevo secret |
| 401 Unauthorized | Audience incorrecto | Revisar `aud` en token |
| Token inválido | Formato incorrecto | Verificar estructura JWT |
| CORS | Origen distinto | Revisar configuración CORS |

---

# 🧪 5. Script automatizado

Guardar como `test-auth.sh`:

```bash
#!/bin/bash
set -e

TENANT_ID="7133f9a8-4c6c-47a3-b9a7-55bad5090288"
CLIENT_ID="d6800b3e-a409-4129-ba4d-7d56bd55f1a8"
CLIENT_SECRET="tu-client-secret-aqui"
AUDIENCE="api://d6800b3e-a409-4129-ba4d-7d56bd55f1a8"
API_URL="http://localhost"

RESPONSE=$(curl -s -X POST \
"https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
-H "Content-Type: application/x-www-form-urlencoded" \
-d "client_id=${CLIENT_ID}" \
-d "scope=${AUDIENCE}/.default" \
-d "client_secret=${CLIENT_SECRET}" \
-d "grant_type=client_credentials")

TOKEN=$(echo "$RESPONSE" | jq -r '.access_token')

curl -s -H "Authorization: Bearer $TOKEN" "$API_URL/api/products" | jq '.'
curl -s -H "Authorization: Bearer $TOKEN" "$API_URL/api/orders" | jq '.'
curl -s "$API_URL/api/products" || true
```

---

# 📋 6. Checklist

- Token válido  
- Endpoints protegidos responden con token  
- Endpoints sin token devuelven 401  
- Swagger autorizado correctamente  
- Claims correctos (`aud`, `iss`, `scp`)  
- Secret válido  
- CORS funcionando  
