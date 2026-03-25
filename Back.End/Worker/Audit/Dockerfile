FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Worker/Audit/Cashflow.Worker.Audit.csproj Back.End/Worker/Audit/

RUN dotnet restore Back.End/Worker/Audit/Cashflow.Worker.Audit.csproj

COPY . .

RUN dotnet publish Back.End/Worker/Audit/Cashflow.Worker.Audit.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Cashflow.Worker.Audit.dll"]