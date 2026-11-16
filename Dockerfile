# CREO FlowTwin: API y aplicación web en una sola imagen (mismo dominio: cookie SameSite=Strict y SignalR sin CORS).
# Guía de despliegue: docs/despliegue.md. Las versiones están fijas: una actualización se decide, no llega sola.
#
#   docker build -t flowtwin .
#   docker run -p 8080:8080 --env-file flowtwin.env flowtwin

# ---- 1. Aplicación web (Angular)
FROM node:24.15.0-alpine AS web
WORKDIR /src
COPY frontend/flowtwin-web/package.json frontend/flowtwin-web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/flowtwin-web/ ./
RUN npx ng build --configuration production

# ---- 2. API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/ ./
RUN dotnet publish src/FlowTwin.Api/FlowTwin.Api.csproj -c Release -o /publicado --nologo

# ---- 3. Imagen final: solo lo que corre, sin compiladores ni código fuente
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /publicado ./
COPY --from=web /src/dist/flowtwin-web/browser ./web
COPY maquetas/piso-1 ./maquetas/piso-1

# Producción por defecto. Los secretos NO van aquí: se pasan como variables de entorno al ejecutar.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
# Detrás del proxy de la plataforma (Render): la API usa la IP y el https reales del visitante
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV Web__Ruta=/app/web
ENV Maquetas__Ruta=/app/maquetas
# Los logs van a la consola (la plataforma los guarda): el disco del contenedor se pierde al reiniciar
ENV Registro__Archivo=""

EXPOSE 8080
# Usuario sin privilegios que trae la imagen de .NET: si alguien lograra ejecutar algo, no sería root.
# La salud la revisa la plataforma con GET /api/v1/salud.
USER $APP_UID
ENTRYPOINT ["dotnet", "FlowTwin.Api.dll"]
