FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Kargo.Domain/Kargo.Domain.csproj Kargo.Domain/
COPY Kargo.Application/Kargo.Application.csproj Kargo.Application/
COPY Kargo.Infrastructure/Kargo.Infrastructure.csproj Kargo.Infrastructure/
COPY KargoApi/KargoApi.csproj KargoApi/
RUN dotnet restore KargoApi/KargoApi.csproj

COPY . .
RUN dotnet publish KargoApi/KargoApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "KargoApi.dll"]