# --- build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/BizMetrics.Domain/*.csproj src/BizMetrics.Domain/
COPY src/BizMetrics.Infrastructure/*.csproj src/BizMetrics.Infrastructure/
COPY src/BizMetrics.Api/*.csproj src/BizMetrics.Api/
RUN dotnet restore src/BizMetrics.Api/BizMetrics.Api.csproj

COPY src/ src/
RUN dotnet publish src/BizMetrics.Api/BizMetrics.Api.csproj -c Release -o /app/publish

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BizMetrics.Api.dll"]
