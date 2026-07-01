FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PurcHistoricTariffReckoner.CSharp.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render injects $PORT at container start; default to 10000 (Render's own default) for other hosts.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet PurcHistoricTariffReckoner.CSharp.dll"]
