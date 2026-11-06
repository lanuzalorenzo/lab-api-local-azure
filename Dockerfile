# syntax=docker/dockerfile:1
# ==============================================================================
# Dockerfile: EcommerceApi (.NET 10 Web API)
# Enfoque: Multi-stage build, imagen reducida y segregación de privilegios (non-root)
# ==============================================================================

# ------------------------------------------------------------------------------
# Fase 1: Compilación y publicación del artefacto
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar la configuración raíz, los csproj y restaurar dependencias primero para aprovechar la caché de capas.
# El feed de GitHub Packages exige token incluso para paquetes públicos: entra
# como secreto de BuildKit y no queda en ninguna capa de la imagen.
COPY ["NuGet.config", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["EcommerceApi/EcommerceApi.csproj", "EcommerceApi/"]
COPY ["EcommerceApi.Domain/EcommerceApi.Domain.csproj", "EcommerceApi.Domain/"]
COPY ["EcommerceApi.Application/EcommerceApi.Application.csproj", "EcommerceApi.Application/"]
COPY ["EcommerceApi.Contracts/EcommerceApi.Contracts.csproj", "EcommerceApi.Contracts/"]
COPY ["EcommerceApi.Infrastructure/EcommerceApi.Infrastructure.csproj", "EcommerceApi.Infrastructure/"]
RUN --mount=type=secret,id=github_token \
    GITHUB_TOKEN="$(cat /run/secrets/github_token)" \
    dotnet restore "EcommerceApi/EcommerceApi.csproj"

# Copiar el resto del código y compilar en modo Release
COPY . .
WORKDIR "/src/EcommerceApi"
RUN dotnet build "EcommerceApi.csproj" -c Release --no-restore -o /app/build
RUN dotnet publish "EcommerceApi.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ------------------------------------------------------------------------------
# Fase 2: Runtime endurecido para producción
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Parches del sistema operativo base en la construcción. La etiqueta :10.0 trae
# el runtime al día; los paquetes de Ubuntu se actualizan aquí para no heredar
# CVE corregibles en la imagen publicada. Sin listas de APT en la capa final.
RUN apt-get update \
    && apt-get -y upgrade \
    && rm -rf /var/lib/apt/lists/*

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
