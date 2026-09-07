# 🧩 EcommerceApi — API Local con Azure AD

## 🧾 Descripción
API REST desarrollada en .NET 10 para demostrar integración real de autenticación empresarial mediante Microsoft Entra ID / Azure AD.  
La API gestiona productos y pedidos en memoria y está protegida con JWT Bearer para validar tokens emitidos por Azure AD.

---

## 🎯 Objetivos del proyecto
- Implementar autenticación empresarial con Azure AD.  
- Validar tokens JWT en una API moderna.  
- Proteger endpoints mediante `[Authorize]`.  
- Proporcionar un laboratorio técnico reproducible para prácticas de seguridad.

---

## 📦 Requisitos
- .NET 10 SDK  
- Acceso a un tenant de Azure AD configurado  
- Git  
- Opcional: Azure CLI

---

## 🚀 Ejecución de la API

### 1. Navegar al proyecto
```bash
cd EcommerceApi
```

### 2. Restaurar dependencias
```bash
dotnet restore
```

### 3. Ejecutar la API
```bash
dotnet run
```

### 4. Acceso
- Swagger UI:  
  ```
  http://localhost/swagger
  ```
- URL base:  
  ```
  http://localhost
  ```

---

## 🛒 Endpoints disponibles

### Productos
- `GET /api/products` — Obtener todos los productos  
- `GET /api/products/{id}` — Obtener producto por ID  
- `POST /api/products` — Crear producto  
  ```json
  { "name": "string", "price": number, "stock": number }
  ```

### Pedidos
- `GET /api/orders` — Obtener todos los pedidos  
- `GET /api/orders/{id}` — Obtener pedido por ID  
- `POST /api/orders` — Crear pedido  
  ```json
  { "productIds": [number, ...] }
  ```

⚠️ Todos los endpoints están protegidos y requieren un token JWT válido emitido por Azure AD.

---

## 🏛️ Estructura del proyecto
```
EcommerceApi/
├── Models/
│   ├── Product.cs
│   └── Order.cs
├── Controllers/
│   ├── ProductsController.cs
│   └── OrdersController.cs
├── Program.cs
├── appsettings.json
├── EcommerceApi.csproj
├── README.md
├── azure-ad-config.md
├── azure-ad-register.md
├── azure-ad-tests.md
├── AZURE-AD-TESTS.md
├── azure-ad-notes.md
├── tests.md
└── .gitignore
```

---

## 🔐 Seguridad actual
La API ya está protegida mediante:
- `AddAuthentication("Bearer")`  
- Validación de issuer y audience  
- `UseAuthentication()` y `UseAuthorization()`  
- Controladores protegidos con `[Authorize]`

Documentación asociada:
- `azure-ad-register.md` — registro de la aplicación  
- `azure-ad-config.md` — configuración del tenant y audiencia  
- `azure-ad-tests.md` — pruebas con tokens  
- `AZURE-AD-TESTS.md` — pruebas avanzadas  
- `tests.md` — pruebas funcionales de la API

---

## 📘 Alcance
Este laboratorio cubre:
- Autenticación con Azure AD  
- Validación de tokens JWT  
- Protección de endpoints  
- Pruebas funcionales y de seguridad

No cubre:
- Persistencia en base de datos  
- Roles avanzados  
- Observabilidad  
- Hardening completo del entorno

---

## ⚖️ Aviso Legal
Este laboratorio se utiliza con fines educativos y técnicos.  
No contiene información sensible ni perteneciente a ninguna organización real.  
Los ejemplos y configuraciones son demostraciones reproducibles en entornos personales.

---

## 📜 Licencia
MIT
