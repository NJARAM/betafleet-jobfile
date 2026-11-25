FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY BetaFleet.Api/BetaFleet.Api.csproj BetaFleet.Api/
RUN dotnet restore BetaFleet.Api/BetaFleet.Api.csproj
COPY BetaFleet.Api/ BetaFleet.Api/
RUN dotnet publish BetaFleet.Api/BetaFleet.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Africa/Dar_es_Salaam
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "BetaFleet.Api.dll"]
