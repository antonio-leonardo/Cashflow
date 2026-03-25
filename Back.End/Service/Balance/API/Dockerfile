FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj Back.End/Service/Balance/API/
COPY Back.End/Shared/Contracts/Cashflow.Shared.Contracts.csproj Back.End/Shared/Contracts/
COPY Back.End/Shared/NoSql/Abstractions/Cashflow.Shared.NoSql.Abstractions.csproj Back.End/Shared/NoSql/Abstractions/
COPY Back.End/Shared/NoSql/Providers/Redis/Cashflow.Shared.NoSql.Redis.csproj Back.End/Shared/NoSql/Providers/Redis/
COPY Back.End/Shared/Resilience/Cashflow.Shared.Resilience.csproj Back.End/Shared/Resilience/

RUN dotnet restore Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj

COPY Back.End/Service/Balance/API/ Back.End/Service/Balance/API/
COPY Back.End/Shared/Contracts/ Back.End/Shared/Contracts/
COPY Back.End/Shared/NoSql/Abstractions/ Back.End/Shared/NoSql/Abstractions/
COPY Back.End/Shared/NoSql/Providers/Redis/ Back.End/Shared/NoSql/Providers/Redis/
COPY Back.End/Shared/Resilience/ Back.End/Shared/Resilience/

RUN dotnet publish Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "Cashflow.Service.Balance.API.dll"]
