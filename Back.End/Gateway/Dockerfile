FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Back.End/Gateway/Cashflow.Gateway.csproj Back.End/Gateway

RUN dotnet restore Back.End/Gateway/Cashflow.Gateway.csproj

COPY . .

RUN dotnet publish Back.End/Gateway/Cashflow.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8888

EXPOSE 8888

ENTRYPOINT ["dotnet", "Cashflow.Gateway.dll"]