FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/BeatDash.API/Shiron.BeatDash.API.csproj backend/src/BeatDash.API/

RUN dotnet restore backend/src/BeatDash.API/Shiron.BeatDash.API.csproj

COPY backend/src/ backend/src/

RUN dotnet publish backend/src/BeatDash.API/Shiron.BeatDash.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Shiron.BeatDash.API.dll"]
