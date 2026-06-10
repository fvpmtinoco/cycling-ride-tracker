FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Cycling.Rider.Tracking.Processor/Cycling.Rider.Tracking.Processor.csproj", "src/Cycling.Rider.Tracking.Processor/"]
COPY ["src/Cycling.Rider.Tracking.Contracts/Cycling.Rider.Tracking.Contracts.csproj", "src/Cycling.Rider.Tracking.Contracts/"]
RUN dotnet restore "./src/Cycling.Rider.Tracking.Processor/Cycling.Rider.Tracking.Processor.csproj"
COPY . .
WORKDIR "/src/src/Cycling.Rider.Tracking.Processor"
RUN dotnet build "./Cycling.Rider.Tracking.Processor.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Cycling.Rider.Tracking.Processor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cycling.Rider.Tracking.Processor.dll"]
