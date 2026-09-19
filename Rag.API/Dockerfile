FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Rag.API/Rag.API.csproj", "Rag.API/"]
RUN dotnet restore "Rag.API/Rag.API.csproj"

COPY Rag.API/ Rag.API/
WORKDIR /src/Rag.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rag.API.dll"]
