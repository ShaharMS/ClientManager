FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ClientManager.AdminUI/ClientManager.AdminUI.csproj", "ClientManager.AdminUI/"]
COPY ["ClientManager.Shared/ClientManager.Shared.csproj", "ClientManager.Shared/"]
RUN dotnet restore "ClientManager.AdminUI/ClientManager.AdminUI.csproj"

COPY . .
RUN dotnet publish "ClientManager.AdminUI/ClientManager.AdminUI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientManager.AdminUI.dll"]