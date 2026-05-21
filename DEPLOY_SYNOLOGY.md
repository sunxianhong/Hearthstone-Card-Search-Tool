# 群晖 Docker 部署说明

当前仓库只保留 Web / Docker 版本，可直接通过浏览器访问，不再包含 Windows 桌面端。

## 目录要求

容器运行时需要一个资源根目录，目录里必须同时包含：

- `CardDefs.xml`
- `cardpng/`

如果你直接把整个仓库上传到群晖，那么仓库根目录本身就满足这个要求。

## 方式一：用 `docker-compose.yml` 部署

这是最省事的方式。

1. 把整个仓库上传到群晖，例如：

```text
/volume1/docker/hearthstone-card-search
```

2. 确认目录里至少有这些内容：

```text
/volume1/docker/hearthstone-card-search/CardDefs.xml
/volume1/docker/hearthstone-card-search/cardpng/
/volume1/docker/hearthstone-card-search/Dockerfile
/volume1/docker/hearthstone-card-search/docker-compose.yml
```

3. 打开群晖 `Container Manager`
4. 进入“项目”
5. 选择“新增”或“创建项目”
6. 选择从现有 `docker-compose.yml` 创建
7. 选中仓库目录下的 `docker-compose.yml`
8. 如需改端口，编辑：

```yaml
ports:
  - "8090:5888"
```

9. 创建并启动

启动后访问：

```text
http://群晖IP:5888
```

如果你改成了 `8090:5888`，则访问：

```text
http://群晖IP:8090
```

## 方式二：命令行启动

在仓库目录执行：

```powershell
docker compose up -d --build
```

## 如果想把资源目录和代码目录分开

可以把 `docker-compose.yml` 中的数据卷改成你自己的共享目录，例如：

```yaml
volumes:
  - /volume1/docker/hearthstone-card-search-data:/data:ro
  - /volume1/docker/hearthstone-card-search-config:/config
```

这时要保证：

```text
/volume1/docker/hearthstone-card-search-data/CardDefs.xml
/volume1/docker/hearthstone-card-search-data/cardpng/
```

都真实存在。

## 常见问题

### 容器启动后立刻退出

通常是 `/data` 挂载错了。检查：

- `/data/CardDefs.xml` 是否存在
- `/data/cardpng/` 是否存在

### 页面能打开，但图片不显示

检查：

- `cardpng/` 目录里是否真的有 `.png` 或 `.webp`
- 图片文件名是否与 `CardID` 对应

### 浏览器打不开页面

检查：

- 群晖防火墙是否放行端口
- 路由器或局域网是否限制访问
- 访问的是群晖实际局域网 IP，而不是 `localhost`
