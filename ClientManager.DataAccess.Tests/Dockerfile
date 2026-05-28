ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ARG NUGET_SOURCES=""

COPY ["ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj", "ClientManager.DataAccess.Tests/"]
COPY ["ClientManager.DataAccess/ClientManager.DataAccess.csproj", "ClientManager.DataAccess/"]
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
		dotnet restore "ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj" --configfile "$config"; \
	else \
		dotnet restore "ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj"; \
	fi

COPY . .
RUN dotnet publish "ClientManager.DataAccess.Tests/ClientManager.DataAccess.Tests.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientManager.DataAccess.Tests.dll"]