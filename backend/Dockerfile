# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish backend.csproj -c Release -o /app/publish --no-restore

# Runtime stage - the SDK image above is only needed to build; the app
# itself only needs the much smaller ASP.NET Core runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.dll"]
