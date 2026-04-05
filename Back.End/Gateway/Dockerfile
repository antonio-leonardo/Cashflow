FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Gateway/Cashflow.Gateway.csproj
RUN dotnet publish Gateway/Cashflow.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Cashflow.Gateway.dll"]
