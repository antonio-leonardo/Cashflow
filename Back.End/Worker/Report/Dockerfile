FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Worker/Report/Cashflow.Worker.Report.csproj Back.End/Worker/Report/

RUN dotnet restore Back.End/Worker/Report/Cashflow.Worker.Report.csproj

COPY . .

RUN dotnet publish Back.End/Worker/Report/Cashflow.Worker.Report.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Cashflow.Worker.Report.dll"]