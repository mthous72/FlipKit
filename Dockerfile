# FlipKit Docker Image
# Runs Web server (port 5000) and API server (port 5001)

# Build stage for Web (net8.0)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-web
WORKDIR /src
COPY FlipKit.Core/ FlipKit.Core/
COPY FlipKit.Web/ FlipKit.Web/
RUN dotnet publish FlipKit.Web/FlipKit.Web.csproj -c Release -r linux-x64 --self-contained -o /app/web

# Build stage for API (net9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-api
WORKDIR /src
COPY FlipKit.Core/ FlipKit.Core/
COPY FlipKit.Api/ FlipKit.Api/
RUN dotnet publish FlipKit.Api/FlipKit.Api.csproj -c Release -r linux-x64 --self-contained -o /app/api

# Runtime stage - minimal base image (no runtime needed since apps are self-contained)
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published apps (self-contained, no runtime needed)
COPY --from=build-web /app/web ./web
COPY --from=build-api /app/api ./api

# Create data directory for SQLite
RUN mkdir -p /data

# Create entrypoint script using printf (avoids Windows line ending issues)
RUN printf '#!/bin/bash\n\
set -e\n\
echo "========================================="\n\
echo "FlipKit Docker Container Starting"\n\
echo "========================================="\n\
mkdir -p /data\n\
echo "Database: ${FLIPKIT_DB_PATH:-/data/cards.db}"\n\
echo "Settings: ${FLIPKIT_SETTINGS_PATH:-/data/settings.json}"\n\
echo "Starting API server on port 5001..."\n\
cd /app/api\n\
ASPNETCORE_URLS="http://0.0.0.0:5001" ./FlipKit.Api &\n\
API_PID=$!\n\
sleep 2\n\
echo "Starting Web server on port 5000..."\n\
cd /app/web\n\
ASPNETCORE_URLS="http://0.0.0.0:5000" ./FlipKit.Web &\n\
WEB_PID=$!\n\
echo "========================================="\n\
echo "FlipKit is running!"\n\
echo "  Web:  http://localhost:5000"\n\
echo "  API:  http://localhost:5001"\n\
echo "  Settings: http://localhost:5000/Settings"\n\
echo "========================================="\n\
trap "kill $API_PID $WEB_PID 2>/dev/null; exit 0" SIGTERM SIGINT\n\
wait -n $API_PID $WEB_PID\n\
kill $API_PID $WEB_PID 2>/dev/null\n\
exit 1\n' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV FLIPKIT_DB_PATH=/data/cards.db
ENV FLIPKIT_SETTINGS_PATH=/data/settings.json

# Expose ports
EXPOSE 5000 5001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
