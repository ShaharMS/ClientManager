FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "ClientManager.Api/ClientManager.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5062
ENV ASPNETCORE_URLS=http://+:5062

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientManager.Api.dll"]
