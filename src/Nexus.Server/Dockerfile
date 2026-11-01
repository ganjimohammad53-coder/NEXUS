FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Nexus.Server/Nexus.Server.csproj src/Nexus.Server/
RUN dotnet restore src/Nexus.Server/Nexus.Server.csproj
COPY src/Nexus.Server/ src/Nexus.Server/
RUN dotnet publish src/Nexus.Server/Nexus.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Nexus.Server.dll"]
