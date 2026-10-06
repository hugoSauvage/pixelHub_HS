# Image de l'API PixelHub — build multi-étapes.
# Le SDK .NET 10 ne sert qu'à compiler ; l'image finale ne contient que le runtime ASP.NET.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration d'abord, seule : la couche est réutilisée tant que le .csproj ne change pas.
COPY src/PixelHub.Api/PixelHub.Api.csproj src/PixelHub.Api/
RUN dotnet restore src/PixelHub.Api/PixelHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/PixelHub.Api/PixelHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Les images .NET 8+ écoutent sur 8080 et tournent en utilisateur non-root.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PixelHub.Api.dll"]
