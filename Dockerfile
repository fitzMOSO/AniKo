# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project files first so `restore` is cached independently of source changes.
COPY ["backend/AniKo.slnx", "backend/"]
COPY ["backend/AniKo_API/AniKo_API.csproj", "backend/AniKo_API/"]
COPY ["backend/AniKo_API.Tests/AniKo_API.Tests.csproj", "backend/AniKo_API.Tests/"]
RUN dotnet restore "backend/AniKo_API/AniKo_API.csproj"

COPY backend/ backend/
RUN dotnet publish "backend/AniKo_API/AniKo_API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Drop privileges. $APP_UID is provided by the base image.
USER $APP_UID

ENTRYPOINT ["dotnet", "AniKo_API_BROKEN.dll"]
