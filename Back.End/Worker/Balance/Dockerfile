FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Worker/Balance/Cashflow.Worker.Balance.csproj
RUN dotnet publish Worker/Balance/Cashflow.Worker.Balance.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Cashflow.Worker.Balance.dll"]
