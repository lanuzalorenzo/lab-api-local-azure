# ==============================================================================
# Dockerfile: EcommerceApi (.NET 10 Web API)
# Enfoque: Multi-stage build, imagen reducida y segregación de privilegios (non-root)
# ==============================================================================

# ------------------------------------------------------------------------------
# Fase 1: Compilación y publicación del artefacto
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias primero para aprovechar la caché de capas
COPY ["EcommerceApi/EcommerceApi.csproj", "EcommerceApi/"]
COPY ["EcommerceApi.Domain/EcommerceApi.Domain.csproj", "EcommerceApi.Domain/"]
COPY ["EcommerceApi.Application/EcommerceApi.Application.csproj", "EcommerceApi.Application/"]
COPY ["EcommerceApi.Infrastructure/EcommerceApi.Infrastructure.csproj", "EcommerceApi.Infrastructure/"]
RUN dotnet restore "EcommerceApi/EcommerceApi.csproj"

# Copiar el resto del código y compilar en modo Release
COPY . .
WORKDIR "/src/EcommerceApi"
RUN dotnet build "EcommerceApi.csproj" -c Release -o /app/build
RUN dotnet publish "EcommerceApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------------------------------------------------------
# Fase 2: Runtime endurecido para producción
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Puerto por defecto no privilegiado en .NET 8+
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Directorio escribible para la base SQLite del laboratorio. Se crea antes de
# cambiar de usuario y pertenece al usuario de aplicación.
RUN mkdir -p /app/data && chown $APP_UID:$APP_UID /app/data

# Principio de mínimos privilegios: ejecutar con usuario de aplicación no-root
# En imágenes oficiales de .NET (Alpine/Debian/Chiseled), APP_UID corresponde al usuario 'app' (UID 1654)
USER $APP_UID

# Copiar exclusivamente los artefactos compilados desde la fase de build
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "EcommerceApi.dll"]
