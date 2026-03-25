FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj Back.End/Service/Balance/API/

RUN dotnet restore Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj

COPY . .

RUN dotnet publish Back.End/Service/Balance/API/Cashflow.Service.Balance.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "Cashflow.Service.Balance.API.dll"]
