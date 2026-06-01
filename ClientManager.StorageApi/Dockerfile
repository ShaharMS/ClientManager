ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ARG NUGET_SOURCES=""

COPY ["ClientManager.StorageApi/ClientManager.StorageApi.csproj", "ClientManager.StorageApi/"]
COPY ["ClientManager.DataAccess/ClientManager.DataAccess.csproj", "ClientManager.DataAccess/"]
COPY ["ClientManager.Shared/ClientManager.Shared.csproj", "ClientManager.Shared/"]
RUN if [ -n "$NUGET_SOURCES" ]; then \
		config=/tmp/nuget.config; \
		old_ifs="$IFS"; \
		IFS=';'; \
		set -- $NUGET_SOURCES; \
		IFS="$old_ifs"; \
		index=0; \
		for source in "$@"; do \
			index=$((index + 1)); \
			dotnet nuget add source "$source" --name "source$index" --configfile "$config"; \
		done; \
		dotnet restore "ClientManager.StorageApi/ClientManager.StorageApi.csproj" --configfile "$config"; \
	else \
		dotnet restore "ClientManager.StorageApi/ClientManager.StorageApi.csproj"; \
	fi

COPY . .
RUN dotnet publish "ClientManager.StorageApi/ClientManager.StorageApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM ${ASPNET_IMAGE} AS final
WORKDIR /app
EXPOSE 5063
ENV ASPNETCORE_URLS=http://+:5063

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientManager.StorageApi.dll"]