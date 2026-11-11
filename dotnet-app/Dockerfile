# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/LendIt.Api/LendIt.Api.csproj src/LendIt.Api/
RUN dotnet restore src/LendIt.Api/LendIt.Api.csproj
COPY src/LendIt.Api/ src/LendIt.Api/
RUN dotnet publish src/LendIt.Api/LendIt.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "LendIt.Api.dll"]
