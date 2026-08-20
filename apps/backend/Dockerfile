# Imagen de produccion de la app del tenant de TRONOX SGDEA (Tronox.Web, Blazor Server, .NET 10).
# Contexto de build esperado: apps/backend (la carpeta de la solucion).
#
# A diferencia del Dockerfile heredado del backbone, esta imagen:
#  - publica Tronox.Web (no Ecorex.SuperAdmin),
#  - NO copia apps/agent (podado),
#  - instala Chromium para el editor de texto interno (RF08): PuppeteerSharp genera el PDF con
#    Chromium headless, apuntando a /usr/bin/chromium (PUPPETEER_EXECUTABLE_PATH), sin descargar
#    el binario en runtime,
#  - escucha en 8080 detras del Caddy externo (mismo patron que Visal/ECOREX/DokTrino).

# --- build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero solo los csproj para cachear la capa de dependencias.
COPY *.sln ./
COPY src/ ./src/
RUN dotnet restore src/Tronox.Web/Tronox.Web.csproj

RUN dotnet publish src/Tronox.Web/Tronox.Web.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Chromium + fuentes para el editor de texto interno (RF08). PuppeteerSharp usa este binario
# (PUPPETEER_EXECUTABLE_PATH) para convertir HTML a PDF, sin descargar Chromium en runtime.
RUN apt-get update && apt-get install -y --no-install-recommends \
        chromium \
        fonts-liberation \
        fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

COPY --from=build /app/publish ./

# El Caddy externo termina TLS y reenvia HTTP a este puerto (igual que los stacks hermanos).
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Tronox.Web.dll"]
