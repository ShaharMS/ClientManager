FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ClientManager.StorageApi/ClientManager.StorageApi.csproj", "ClientManager.StorageApi/"]
COPY ["ClientManager.DataAccess/ClientManager.DataAccess.csproj", "ClientManager.DataAccess/"]
COPY ["ClientManager.Shared/ClientManager.Shared.csproj", "ClientManager.Shared/"]
RUN dotnet restore "ClientManager.StorageApi/ClientManager.StorageApi.csproj"

COPY . .
RUN dotnet publish "ClientManager.StorageApi/ClientManager.StorageApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV Persistence__DefaultJsonFile__DataDirectory=/data

COPY --from=build /app/publish .
COPY data/ /data/
ENTRYPOINT ["dotnet", "ClientManager.StorageApi.dll"]