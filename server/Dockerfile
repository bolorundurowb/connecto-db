FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY connecto.server.csproj connecto.server.sln ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -r linux-x64 --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5043
EXPOSE 5043

VOLUME ["/app/data"]
ENV DOTNET_EnableWriteXorExecute=0

ENTRYPOINT ["./connecto-server"]
