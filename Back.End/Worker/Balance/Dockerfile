FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Worker/Balance/Cashflow.Worker.Balance.csproj Back.End/Worker/Balance/

RUN dotnet restore Back.End/Worker/Balance/Cashflow.Worker.Balance.csproj

COPY . .

RUN dotnet publish Back.End/Worker/Balance/Cashflow.Worker.Balance.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Cashflow.Worker.Balance.dll"]
