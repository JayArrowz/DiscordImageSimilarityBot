# syntax=docker/dockerfile:1
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first to leverage layer caching for restore
COPY ImageSimilarityBot/ImageSimilarityBot.csproj ./ImageSimilarityBot/
RUN dotnet restore ./ImageSimilarityBot/ImageSimilarityBot.csproj

# Copy the rest of the source
COPY ImageSimilarityBot/ ./ImageSimilarityBot/

WORKDIR /src/ImageSimilarityBot
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# pgvector needs ICU; ensure culture data is available
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "ImageSimilarityBot.dll"]
