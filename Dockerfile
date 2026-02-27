FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gov.News.WebApp/Gov.News.WebApp.csproj Gov.News.WebApp/
RUN dotnet restore Gov.News.WebApp/Gov.News.WebApp.csproj

COPY . .
RUN dotnet publish Gov.News.WebApp/Gov.News.WebApp.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gov.News.WebApp.dll"]
