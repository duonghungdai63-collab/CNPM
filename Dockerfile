FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DaLat.DAL/DaLat.DAL.csproj DaLat.DAL/
COPY DaLat.BLL/DaLat.BLL.csproj DaLat.BLL/
COPY DaLat.API/DaLat.API.csproj DaLat.API/
RUN dotnet restore DaLat.API/DaLat.API.csproj
COPY DaLat.DAL/ DaLat.DAL/
COPY DaLat.BLL/ DaLat.BLL/
COPY DaLat.API/ DaLat.API/
RUN dotnet publish DaLat.API/DaLat.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DaLat.API.dll"]
