# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RaceDay.Api.csproj ./
RUN dotnet restore RaceDay.Api.csproj
COPY . ./
RUN dotnet publish RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
