#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/backend/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj", "src/backend/Ambev.DeveloperEvaluation.WebApi/"]
COPY ["src/backend/Ambev.DeveloperEvaluation.IoC/Ambev.DeveloperEvaluation.IoC.csproj", "src/backend/Ambev.DeveloperEvaluation.IoC/"]
COPY ["src/backend/Ambev.DeveloperEvaluation.Domain/Ambev.DeveloperEvaluation.Domain.csproj", "src/backend/Ambev.DeveloperEvaluation.Domain/"]
COPY ["src/backend/Ambev.DeveloperEvaluation.Common/Ambev.DeveloperEvaluation.Common.csproj", "src/backend/Ambev.DeveloperEvaluation.Common/"]
COPY ["src/backend/Ambev.DeveloperEvaluation.Application/Ambev.DeveloperEvaluation.Application.csproj", "src/backend/Ambev.DeveloperEvaluation.Application/"]
COPY ["src/backend/Ambev.DeveloperEvaluation.ORM/Ambev.DeveloperEvaluation.ORM.csproj", "src/backend/Ambev.DeveloperEvaluation.ORM/"]
RUN dotnet restore "./src/backend/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj"
COPY . .
WORKDIR "/src/src/backend/Ambev.DeveloperEvaluation.WebApi"
RUN dotnet build "./Ambev.DeveloperEvaluation.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS migrations-build
RUN export ConnectionStrings__DefaultConnection='Host=localhost;Database=build;Username=build' \
    && dotnet tool restore \
    && dotnet tool run dotnet-ef migrations bundle \
      --project /src/src/backend/Ambev.DeveloperEvaluation.ORM/Ambev.DeveloperEvaluation.ORM.csproj \
      --startup-project /src/src/backend/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj \
      --configuration Release \
      --output /app/migrate \
      --force

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Ambev.DeveloperEvaluation.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS migrations
COPY --from=migrations-build --chown=app:app /app/migrate /app/migrate
ENTRYPOINT ["/app/migrate"]

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ambev.DeveloperEvaluation.WebApi.dll"]
