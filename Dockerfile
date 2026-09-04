# syntax=docker/dockerfile:1.7

FROM node:24.19.0-bookworm-slim@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build

ARG CONFIGURATION=Release
ARG NPM_REGISTRY=https://packagefeedproxy.microsoft.io/npm/
WORKDIR /src

COPY --from=node /usr/local/ /usr/local/
COPY --from=node /opt/yarn-v1.22.22/ /opt/yarn-v1.22.22/

RUN npm config set registry "${NPM_REGISTRY}" \
    && yarn --version \
    && yarn config set registry "${NPM_REGISTRY}"

COPY . .

RUN dotnet tool install \
        --tool-path /tools \
        Volo.Abp.Cli \
        --version 10.6.0 \
        --add-source https://packagefeedproxy.microsoft.io/nuget/v3/index.json \
        --ignore-failed-sources \
    && dotnet restore SayHello.ShortLink.slnx \
        --source https://packagefeedproxy.microsoft.io/nuget/v3/index.json \
    && /tools/abp install-libs \
        --working-directory /src/host/src/SayHello.ShortLink.WebHost.Web \
    && dotnet publish \
        host/src/SayHello.ShortLink.WebHost.Web/SayHello.ShortLink.WebHost.Web.csproj \
        --configuration ${CONFIGURATION} \
        --no-restore \
        --output /out/web \
        /p:UseAppHost=false \
    && dotnet publish \
        host/src/SayHello.ShortLink.WebHost.DbMigrator/SayHello.ShortLink.WebHost.DbMigrator.csproj \
        --configuration ${CONFIGURATION} \
        --no-restore \
        --output /out/migrator \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS web

WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build --chown=app:app /out/web .
COPY --from=build --chown=app:app /out/migrator ./migrator

USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SayHello.ShortLink.WebHost.Web.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS migrator

WORKDIR /app
COPY --from=build --chown=app:app /out/migrator .

USER app
ENTRYPOINT ["dotnet", "SayHello.ShortLink.WebHost.DbMigrator.dll"]
