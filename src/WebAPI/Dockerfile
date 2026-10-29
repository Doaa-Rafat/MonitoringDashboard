# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so Docker caches the packages until a .csproj changes.
COPY Directory.Build.props ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/WebAPI/WebAPI.csproj src/WebAPI/
RUN dotnet restore src/WebAPI/WebAPI.csproj

COPY src/ src/
RUN dotnet publish src/WebAPI/WebAPI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is only used by the container healthcheck.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Writable locations for the non-root user: log files and generated reports.
RUN mkdir -p /app/logs /data/reports \
 && chown -R $APP_UID:$APP_UID /app/logs /data

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
  CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "WebAPI.dll"]