# 構建階段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制 csproj 文件并还原依赖
COPY ["ProductWebsite.csproj", "./"]
RUN dotnet restore

# 复制其余源代码
COPY . .
RUN dotnet build -c Release -o /app/build

# 发布
RUN dotnet publish -c Release -o /app/publish

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000
ENTRYPOINT ["dotnet", "ProductWebsite.dll"] 