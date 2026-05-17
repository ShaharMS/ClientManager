FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj", "ClientManager.DataAccess.Tests/"]
COPY ["ClientManager.DataAccess/ClientManager.DataAccess.csproj", "ClientManager.DataAccess/"]
RUN dotnet restore "ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj"

COPY . .
RUN dotnet publish "ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientManager.DataAccess.Tests.dll"]