FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["osu.Server.ReplayStore/osu.Server.ReplayStore.csproj", "osu.Server.ReplayStore/"]
RUN dotnet restore "osu.Server.ReplayStore/osu.Server.ReplayStore.csproj"
COPY . .
WORKDIR "/src/osu.Server.ReplayStore"
RUN dotnet build "osu.Server.ReplayStore.csproj" -c $BUILD_CONFIGURATION -o /app/build
# get rid of bloat
RUN rm -rf /app/build/runtimes /app/build/osu.Game.Resources.dll

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "osu.Server.ReplayStore.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "osu.Server.ReplayStore.dll"]