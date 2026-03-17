FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Outbox/Worker/Cashflow.Outbox.Worker.csproj Back.End/Outbox/Worker

RUN dotnet restore Back.End/Outbox/Worker/Cashflow.Outbox.Worker.csproj

COPY . .

RUN dotnet publish Back.End/Outbox/Worker/Cashflow.Outbox.Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Cashflow.Outbox.Worker.dll"]