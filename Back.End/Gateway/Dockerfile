FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas o csproj primeiro (cache inteligente)
COPY Back.End/Gateway/Cashflow.Gateway.csproj Back.End/Gateway/

RUN dotnet restore Back.End/Gateway/Cashflow.Gateway.csproj

# Copia o resto
COPY . .

WORKDIR /src/Back.End/Gateway

RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Cashflow.Gateway.dll"]