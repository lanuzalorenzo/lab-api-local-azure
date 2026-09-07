# 🧩 Laboratorio: API Local (Módulo 1)

## 🧾 Descripción
Laboratorio técnico que contiene la API local utilizada en el Módulo 1 del portfolio de ciberseguridad.  
El objetivo es disponer de una API funcional para realizar prácticas de seguridad en Azure y pruebas de integración.

---

## 📁 Estructura del repositorio
```
lab-api-local-azure/
├── EcommerceApi/   # Código fuente de la API (.NET)
├── docs/           # Documentación técnica del laboratorio
└── README.md       # Documento principal del proyecto
```

---

## 🎯 Objetivos del laboratorio
- Crear una API local funcional en .NET.  
- Integrar autenticación básica.  
- Preparar el entorno para prácticas de seguridad en Azure.  
- Proporcionar documentación técnica reproducible.

---

## 🏛️ Arquitectura general
La API se compone de:
- Proyecto `.NET` en `EcommerceApi/`.  
- Controladores y servicios básicos para pruebas.  
- Configuración mínima para autenticación.  
- Documentación técnica en `docs/`.

---

## 📦 Requisitos
- .NET SDK 8 o superior  
- Ubuntu / Windows / macOS  
- Git  
- Azure CLI (opcional para prácticas avanzadas)

---

## 🔧 Instalación y ejecución
1. Clonar el repositorio:
   ```bash
   git clone https://github.com/lanuzalorenzo/lab-api-local-azure
   cd lab-api-local-azure/EcommerceApi
   ```

2. Restaurar dependencias:
   ```bash
   dotnet restore
   ```

3. Ejecutar la API:
   ```bash
   dotnet run
   ```

4. Acceder a la API:
   ```
   http://localhost:5000
   ```

---

## 📚 Documentación
La documentación técnica completa se encuentra en la carpeta:

```
/docs
```

Incluye:
- Arquitectura  
- Endpoints  
- Pruebas  
- Seguridad  
- Troubleshooting  

---

## 🧩 Bitácoras
Las bitácoras del módulo se encuentran en el repositorio principal del portfolio.

---

## ⚖️ Aviso Legal
Este laboratorio se utiliza con fines educativos y técnicos.  
No contiene información sensible ni perteneciente a ninguna organización real.  
Los ejemplos y configuraciones son demostraciones reproducibles en entornos personales.

---

## 📜 Licencia
MIT
