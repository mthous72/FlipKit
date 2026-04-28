#!/bin/bash
set -e

echo "========================================="
echo "FlipKit Docker Container Starting"
echo "========================================="
echo ""

# Ensure data directory exists
mkdir -p /data

# Show configuration
echo "Database path: ${FLIPKIT_DB_PATH:-/data/cards.db}"
echo "Settings path: ${FLIPKIT_SETTINGS_PATH:-/data/settings.json}"
echo ""

# Start API server in background
echo "Starting API server on port 5001..."
cd /app/api
ASPNETCORE_URLS="http://0.0.0.0:5001" \
FLIPKIT_DB_PATH="${FLIPKIT_DB_PATH:-/data/cards.db}" \
./FlipKit.Api &
API_PID=$!

# Give API a moment to start
sleep 2

# Start Web server
echo "Starting Web server on port 5000..."
cd /app/web
ASPNETCORE_URLS="http://0.0.0.0:5000" \
FLIPKIT_DB_PATH="${FLIPKIT_DB_PATH:-/data/cards.db}" \
FLIPKIT_SETTINGS_PATH="${FLIPKIT_SETTINGS_PATH:-/data/settings.json}" \
./FlipKit.Web &
WEB_PID=$!

echo ""
echo "========================================="
echo "FlipKit is running!"
echo "  Web:  http://localhost:5000"
echo "  API:  http://localhost:5001"
echo "  Settings: http://localhost:5000/Settings"
echo "========================================="
echo ""

# Handle shutdown gracefully
trap "kill $API_PID $WEB_PID 2>/dev/null; exit 0" SIGTERM SIGINT

# Wait for either process to exit
wait -n $API_PID $WEB_PID

# If one exits, kill the other
kill $API_PID $WEB_PID 2>/dev/null
exit 1
