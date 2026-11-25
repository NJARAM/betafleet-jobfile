# Adds an EF Core migration using the .NET 11 SDK container. Usage: see README.
set -e
export Jwt__Key=design-time-only-key-not-used-at-runtime-000
cd /src/BetaFleet.Api
dotnet build 2>&1 | grep -E "error|warning CS|Build succeeded" | sort -u | head -30
dotnet tool install --global dotnet-ef --version 11.0.0 >/dev/null 2>&1 || true
/root/.dotnet/tools/dotnet-ef migrations add "$1" -o Data/Migrations --no-build 2>&1 | tail -2
rm -rf bin obj
