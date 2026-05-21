FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY core/HearthstoneCardSearchTool.Core.csproj core/
COPY webapp/HearthstoneCardSearchTool.Web.csproj webapp/
COPY enchantment.png .
COPY config/card-data-map-defaults.json config/
RUN dotnet restore webapp/HearthstoneCardSearchTool.Web.csproj

COPY core/ core/
COPY webapp/ webapp/
COPY config/card-data-map-defaults.json config/
RUN dotnet publish webapp/HearthstoneCardSearchTool.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=5888
ENV CARD_RESOURCE_ROOT=/data
ENV FILTER_BAR_CONFIG_ROOT=/config

COPY --from=build /app/publish .

EXPOSE 5888
VOLUME ["/data", "/config"]

ENTRYPOINT ["dotnet", "HearthstoneCardSearchTool.Web.dll"]
