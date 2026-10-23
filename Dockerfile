FROM node:24-bookworm-slim AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/ ./backend/
RUN dotnet restore backend/src/StockPilot.Api/StockPilot.Api.csproj --locked-mode
COPY --from=web /web/dist/ ./backend/src/StockPilot.Api/wwwroot/
RUN dotnet publish backend/src/StockPilot.Api/StockPilot.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir -p /var/stockpilot/keys && chown -R app:app /var/stockpilot
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=https://+:8443 \
    DataProtection__KeyPath=/var/stockpilot/keys
EXPOSE 8443
USER $APP_UID
ENTRYPOINT ["dotnet", "StockPilot.Api.dll"]
