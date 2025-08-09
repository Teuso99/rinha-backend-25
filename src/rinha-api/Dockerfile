ARG LAUNCHING_FROM_VS
ARG FINAL_BASE_IMAGE=${LAUNCHING_FROM_VS:+aotdebug}

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["rinha-api/rinha-api.csproj", "rinha-api/"]
RUN dotnet restore "rinha-api/rinha-api.csproj"
COPY . .
WORKDIR "/src/rinha-api"
RUN dotnet build "./rinha-api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./rinha-api.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r linux-x64 --self-contained -p:PublishAot=true -p:PublishReadyToRun=true -p:StripSymbols=true

FROM base AS aotdebug
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gdb
USER app

FROM ${FINAL_BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:9.0} AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
RUN chmod +x rinha-api
ENTRYPOINT ["/app/rinha-api"]
