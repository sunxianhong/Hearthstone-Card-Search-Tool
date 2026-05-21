# 炉石卡牌检索器

一个基于 ASP.NET Core 和 Docker 的炉石卡牌本地检索工具。

项目会读取仓库根目录下的 `CardDefs.xml` 与 `cardpng/` 资源，提供中文界面的卡牌搜索、筛选、详情查看，以及标签浏览能力。当前仓库只保留 Web / Docker 版本，Windows WPF 桌面端已经移除。

## 当前功能

- 支持按中文名、英文名、`CardID`、`DbfId`、卡牌描述进行普通搜索
- 支持按标签搜索，例如 `HEALTH:5`、`RARITY:5`、`SPELL_SCHOOL:2`
- 支持按 `EnumID:值` 搜索，例如 `45:5`
- 支持标准 / 狂野模式切换
- 支持法力值、职业、扩展包、稀有度、卡牌类型、种族、法术派系、是否可收藏、关键词等筛选项
- 卡牌墙展示卡图，点击后打开详情侧栏
- 详情页展示卡图、名称、描述、`CardID`、`DbfId`、相关牌、附魔牌和完整标签
- 支持在线维护筛选栏配置与标签映射配置

## 运行环境

- Docker Desktop / Docker Engine + Docker Compose
- 或 .NET 8 SDK（用于本地直接运行 Web 版）

## 资源要求

运行时依赖以下资源：

- `CardDefs.xml`
- `cardpng/`

资源根目录必须同时包含这两个项目。默认情况下，仓库根目录本身就是资源根目录。

## 快速开始

### 方式一：Docker Compose

在仓库根目录执行：

```powershell
docker compose up -d --build
```

启动后访问：

```text
http://localhost:5888
```

停止服务：

```powershell
docker compose down
```

### 方式二：本地直接运行 Web 版

```powershell
dotnet run --project .\webapp\HearthstoneCardSearchTool.Web.csproj
```

默认访问地址：

```text
http://localhost:5888
```

## Docker 说明

### `docker-compose.yml`

默认配置会：

- 构建当前仓库里的镜像
- 将仓库根目录挂载到容器内 `/data`（只读）
- 将 `./config` 挂载到容器内 `/config`
- 对外暴露 `5888` 端口

如果你想改端口，可以修改：

```yaml
ports:
  - "8090:5888"
```

### 构建镜像归档

如果需要导出 Docker 镜像 tar 包：

```powershell
.\build_docker_tar.bat
```

输出位置：

```text
dist/docker/hearthstone-card-search.tar
```

## 搜索说明

### 普通搜索

直接输入关键词即可，支持匹配：

- 中文卡名
- 英文卡名
- `CardID`
- `DbfId`
- 中文描述文本

示例：

```text
火球术
Fireball
CS2_029
315
发现
```

### 标签搜索

格式：

```text
标签名:值
```

示例：

```text
HEALTH:5
ATK:3
CARDTYPE:4
RARITY:5
SPELL_SCHOOL:2
```

### EnumID 搜索

格式：

```text
EnumID:值
```

示例：

```text
45:5
```

## 测试

```powershell
dotnet test .\tests\HearthstoneCardSearchTool.Tests.csproj
```

当前测试主要覆盖：

- 资源加载
- 搜索逻辑
- 标签 / EnumID 搜索
- 详情数据
- 映射配置与相关牌逻辑
- 图片路径选择逻辑

## 项目结构

```text
Hearthstone Card Search Tool/
|-- core/                     卡牌仓库、搜索和映射逻辑
|-- tests/                    单元测试
|-- webapp/                   ASP.NET Core Web 应用
|-- config/                   默认配置
|-- cardpng/                  卡图资源目录
|-- dist/                     构建输出目录
|-- CardDefs.xml              卡牌定义数据
|-- Dockerfile                Docker 镜像构建文件
|-- docker-compose.yml        Docker Compose 启动配置
|-- build_docker_tar.bat      Docker 镜像 tar 导出脚本
|-- README.md
```

## 已知注意事项

- 容器里的 `/data` 必须能读到 `CardDefs.xml` 和 `cardpng/`
- 如果 `cardpng/` 为空，页面仍可运行，但不会显示真实卡图
- `dist/` 可能保留历史构建产物

## 群晖部署

群晖部署说明见 `DEPLOY_SYNOLOGY.md`。
