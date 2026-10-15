# 🧩 EcommerceApi | API Empresarial de Referencia (.NET & Azure)

**Carga de trabajo central y laboratorio de desarrollo seguro, DevSecOps y Cloud Security.**

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Microsoft Entra ID](https://img.shields.io/badge/Auth-Microsoft%20Entra%20ID-0078D4?style=flat&logo=microsoftazure)](https://learn.microsoft.com/entra/)
[![Portfolio](https://img.shields.io/badge/Portfolio-Ciberseguridad%20Cloud-238636?style=flat)](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo)
[![Licencia](https://img.shields.io/badge/Licencia-MIT-green.svg)](LICENSE)

---

## 🧾 Descripción del proyecto

`EcommerceApi` es una Web API empresarial desarrollada sobre el ecosistema **.NET (C#)** diseñada bajo principios de arquitectura desacoplada, extensibilidad y **Security by Design**.

Este repositorio cumple un rol estratégico dual:
1. **En Desarrollo de Software:** Muestra patrones de diseño limpios, controladores REST (`ProductsController`, `OrdersController`), tipado estricto, gestión de ciclo de vida e inyección de dependencias.
2. **En Ciberseguridad & DevSecOps:** Actúa como el **activo digital de referencia (Workload Core)** sobre el que se despliega y valida la estrategia de **Defensa en Profundidad (*Defense-in-Depth*)** documentada en el [**Portfolio de Ciberseguridad**](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo).

---

## 🏛️ Arquitectura y Seguridad en el Diseño (AppSec)

```
  [ Cliente / Swagger ] ──( 1. Petición HTTPS + Bearer JWT )──► [ EcommerceApi ]
                                                                      │
                                                ( 2. Valida JWKS )   │
                                                                      ▼
                                                       [ Microsoft Entra ID ]
                                                       Authority: /v2.0
```

### Controles de seguridad implementados a nivel de aplicación:
- **Autenticación moderna federada:** Integración con **Microsoft Entra ID** mediante middleware `JwtBearer`.
- **Validación criptográfica estricta de tokens:**
  - Validación de emisor (`ValidateIssuer = true`) contra la autoridad del tenant Entra ID.
  - Validación de audiencia (`ValidateAudience = true`) exigiendo el identificador de la API (`api://<client-id>`).
  - Validación de ciclo de vida (`ValidateLifetime = true`) para evitar el uso de tokens expirados o revocados.
  - Obtención y verificación automática de firmas mediante el conjunto de claves públicas JWKS (`/.well-known/openid-configuration`).
- **Control de acceso granular:** Decoración de endpoints críticos mediante atributos de autorización (`[Authorize]`).
- **CORS restrictivo y saneamiento:** Configuración de políticas de orígenes permitidos y preparación para mitigación de riesgos del **OWASP API Security Top 10** (BOLA, inyecciones, autenticación rota).

---

## 📁 Estructura del repositorio

```
lab-api-local-azure/
├── EcommerceApi/                    # Código fuente del proyecto .NET
│   ├── Controllers/                 # Controladores REST (Products, Orders)
│   ├── Models/                      # Modelos de dominio y DTOs de petición
│   ├── Program.cs                   # Configuración de pipeline HTTP, DI y JWT
│   ├── EcommerceApi.csproj          # Dependencias y configuración de compilación
│   └── appsettings.json             # Configuración base (sin credenciales)
├── docs/                            # Documentación técnica de integración
│   ├── azure-ad-register.md         # Registro de la aplicación en Entra ID
│   ├── azure-ad-config.md           # Configuración del middleware de autenticación
│   ├── azure-ad-tests.md            # Guía de pruebas de validación de tokens
│   └── tests.md                     # Casos de prueba de endpoints
└── README.md                        # Documento principal del proyecto
```

---

## 🚀 Requisitos e Instalación local

### Requisitos previos
- [.NET SDK 8.0 o superior](https://dotnet.microsoft.com/download)
- Git
- cURL, Postman o extensión REST Client para validación de endpoints

### Pasos para ejecución
1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/lanuzalorenzo/lab-api-local-azure.git
   cd lab-api-local-azure/EcommerceApi
   ```

2. **Restaurar dependencias del proyecto:**
   ```bash
   dotnet restore
   ```

3. **Compilar y ejecutar la API:**
   ```bash
   dotnet run
   ```

4. **Acceso interactivo:**
   La API iniciará en local (por defecto `http://localhost:5000` o `https://localhost:5001`). En modo desarrollo, la documentación OpenAPI y UI de Swagger se encuentra disponible en:
   ```
   https://localhost:5001/swagger
   ```

---

## 🛡️ Conexión con el Portfolio de Ciberseguridad

Las validaciones de seguridad de esta API se integran de forma transversal con los módulos del portfolio principal:

| Capa del Portfolio | Módulo asociado | Relación técnica con la API |
|---|---|---|
| **AppSec & IAM** | [01 \| Seguridad Azure](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/01-seguridad-azure) | Validación del flujo OAuth 2.0 / PKCE y verificación de tokens en `Program.cs`. |
| **DevSecOps** | [07 \| DevSecOps](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/07-devsecops) | Escaneo de vulnerabilidades en dependencias NuGet (SCA), SAST y escaneo de secretos. |
| **Red & Aislamiento** | [08 \| Redes Azure](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/08-seguridad-red-azure) | Arquitectura de despliegue aislada con NSG, subredes privadas y Private Link. |
| **Detección & SOC** | [09 \| Sentinel](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/09-microsoft-sentinel) & [10 \| ELK](https://github.com/lanuzalorenzo/portfolio-ciberseguridad-lorenzo/tree/main/10-elk-stack-security) | Ingesta y analítica KQL sobre intentos de acceso no autorizados (401/403). |

---

## ⚖️ Responsabilidad y Licencia

Este repositorio tiene propósitos exclusivamente técnicos, didácticos y de demostración de arquitectura. Los identificadores de tenant y client IDs utilizados corresponden a laboratorios de prueba redactados y no contienen secretos ni accesos a datos reales de producción.

Distribuido bajo licencia [MIT](LICENSE).

