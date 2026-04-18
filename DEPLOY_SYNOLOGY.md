# 群晖 Docker 部署说明

## 先说明一件事

原来的 `desktop/` 是 `WPF + net8.0-windows` 桌面程序，不能直接在群晖的 Linux 容器里通过浏览器打开。

现在仓库里新增了一个 `webapp/`：

- 后端：ASP.NET Core
- 前端：静态网页
- 资源读取：仍然复用 `CardDefs.xml` 和 `cardpng/`
- 访问方式：`http://群晖IP:端口`

## 仓库里新增了哪些文件

- `webapp/`：浏览器版项目
- `Dockerfile`：镜像构建文件
- `docker-compose.yml`：容器启动配置
- `.dockerignore`：避免把大资源目录打进镜像构建上下文

## 目录要求

容器运行时需要一个资源根目录，这个目录里必须同时有：

- `CardDefs.xml`
- `cardpng/`

当前仓库根目录本身就满足这个要求，所以默认的 `docker-compose.yml` 直接把仓库根目录挂载到了容器里的 `/data`。

## 本地命令行启动

如果你在支持 Docker Compose 的机器上运行，直接在仓库根目录执行：

```powershell
docker compose up -d --build
```

启动后访问：

```text
http://localhost:5888
```

## 群晖 Container Manager 部署

### 方式一：用项目方式导入 `docker-compose.yml`

这是最省事的方式。

1. 把整个仓库上传到群晖，比如：

```text
/volume1/docker/hearthstone-card-search
```

2. 确认这个目录下至少有这些内容：

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
7. 选择这个仓库目录下的 `docker-compose.yml`
8. 如果群晖提示编辑 compose，可以保留默认配置，或者把端口改成你想要的，例如：

```yaml
ports:
  - "8090:5888"
```

9. 创建并启动

启动完成后访问：

```text
http://群晖IP:5888
```

如果你改成了 `8090:5888`，那就访问：

```text
http://群晖IP:8090
```

## 如果你想把资源目录和代码目录分开

可以把 `docker-compose.yml` 里的卷映射改成你自己的群晖共享目录，例如：

```yaml
volumes:
  - /volume1/docker/hearthstone-card-search-data:/data:ro
```

这时你要确保：

```text
/volume1/docker/hearthstone-card-search-data/CardDefs.xml
/volume1/docker/hearthstone-card-search-data/cardpng/
```

这两个路径都存在。

## 如果你想改浏览器访问端口

修改 `docker-compose.yml`：

```yaml
ports:
  - "8090:5888"
```

左边是群晖暴露出去的端口，右边是容器内应用监听的端口。

也就是说：

- `5888:5888`：浏览器访问 `群晖IP:5888`
- `8090:5888`：浏览器访问 `群晖IP:8090`

## 常见问题

### 1. 容器启动后立刻退出

通常是资源目录没挂对。

检查：

- 容器里的 `/data` 是否真的包含 `CardDefs.xml`
- 容器里的 `/data/cardpng` 是否存在

### 2. 页面能打开，但图片不显示

检查：

- `cardpng/` 目录里是否真的有 `.png`
- 卡图文件名是否与 `CardID` 对应

### 3. 浏览器打不开

检查：

- 群晖防火墙是否放行了端口
- 路由器或局域网是否限制访问
- 你访问的是群晖的实际局域网 IP，而不是 `localhost`

## 推荐的最小操作路径

如果你只是想尽快跑起来，按这条路径走就行：

1. 把整个仓库上传到群晖共享目录
2. 在 `Container Manager` 里用 `docker-compose.yml` 创建项目
3. 保持默认端口 `5888`
4. 启动项目
5. 浏览器访问 `http://群晖IP:5888`
