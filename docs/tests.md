# 🧪 Pruebas de la API Local — EcommerceApi

## 🧾 Descripción
Guía técnica con ejemplos de pruebas para validar los endpoints de la API local del laboratorio utilizando curl, PowerShell y Postman.  
Incluye respuestas esperadas y códigos HTTP estándar.

---

# 📦 Prerrequisitos
- API ejecutándose:
  ```bash
  dotnet run
  ```
- curl instalado  
- URL base:
  ```
  http://localhost
  ```

---

# 🛒 Productos

## 1. Obtener todos los productos
```bash
curl http://localhost/api/products
```

Respuesta esperada:
```json
[
  { "id": 1, "name": "Laptop", "price": 999.99, "stock": 5 },
  { "id": 2, "name": "Mouse", "price": 19.99, "stock": 50 }
]
```

---

## 2. Obtener un producto específico
```bash
curl http://localhost/api/products/1
```

Respuesta esperada:
```json
{ "id": 1, "name": "Laptop", "price": 999.99, "stock": 5 }
```

---

## 3. Crear un nuevo producto
```bash
curl -X POST http://localhost/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":29.99,"stock":20}'
```

Respuesta esperada:
```json
{ "id": 3, "name": "Keyboard", "price": 29.99, "stock": 20 }
```

---

# 📦 Pedidos

## 1. Obtener todos los pedidos
```bash
curl http://localhost/api/orders
```

Respuesta esperada:
```json
[
  {
    "id": 1,
    "productIds": [1, 2],
    "totalAmount": 1019.98,
    "orderDate": "2024-01-15T10:30:00"
  }
]
```

---

## 2. Obtener un pedido específico
```bash
curl http://localhost/api/orders/1
```

Respuesta esperada:
```json
{
  "id": 1,
  "productIds": [1, 2],
  "totalAmount": 1019.98,
  "orderDate": "2024-01-15T10:30:00"
}
```

---

## 3. Crear un nuevo pedido
```bash
curl -X POST http://localhost/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productIds":[1,2]}'
```

Respuesta esperada:
```json
{
  "id": 2,
  "productIds": [1, 2],
  "totalAmount": 1019.98,
  "orderDate": "2024-01-15T14:45:00"
}
```

---

# 💻 Pruebas en PowerShell (Windows)

## Obtener productos
```powershell
Invoke-WebRequest -Uri "http://localhost/api/products" -Method GET
```

## Crear producto
```powershell
$body = @{ name = "Keyboard"; price = 29.99; stock = 20 } | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost/api/products" `
  -Method POST `
  -ContentType "application/json" `
  -Body $body
```

---

# 🧪 Pruebas en Postman

### Endpoints
```
GET {{base_url}}/api/products
GET {{base_url}}/api/products/1
POST {{base_url}}/api/products

GET {{base_url}}/api/orders
GET {{base_url}}/api/orders/1
POST {{base_url}}/api/orders
```

### URL base
```
http://localhost
```

---

# 📊 Códigos HTTP esperados

| Método | Endpoint               | Código | Descripción        |
|--------|-------------------------|--------|---------------------|
| GET    | /api/products           | 200    | Éxito               |
| GET    | /api/products/{id}      | 200    | Éxito               |
| GET    | /api/products/999       | 404    | No encontrado       |
| POST   | /api/products           | 201    | Creado              |
| POST   | /api/products           | 400    | Solicitud inválida  |
| GET    | /api/orders             | 200    | Éxito               |
| GET    | /api/orders/{id}        | 200    | Éxito               |
| POST   | /api/orders             | 201    | Creado              |

---

# 🔐 Próximas pruebas (post Azure AD)
Una vez integrada la autenticación:

```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost/api/products
```

---

# ✔️ Conclusión
Este documento proporciona pruebas completas para validar la API local antes y después de integrar Azure AD, utilizando curl, PowerShell y Postman.
