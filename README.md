# 炉石卡牌浏览器

一个基于 WPF 和 .NET 8 的炉石卡牌本地浏览工具。

它会读取仓库根目录下的 `CardDefs.xml` 与 `cardpng/` 资源，提供中文界面的卡牌检索、筛选、详情查看，以及标签浏览能力。当前项目的主要目标是做一个不依赖安装环境、可以直接打包成便携版目录使用的 Windows 桌面程序。

## 项目定位

- 面向本地离线使用，不依赖在线 API。
- 面向 `CardDefs.xml` 原始数据浏览，而不是套牌管理。
- 优先保证便携版可直接复制、解压、双击运行。
- 支持没有图片资源的情况，没有图片时会显示文字占位卡片。

## 当前功能

- 支持按中文名、英文名、`CardID`、`DbfId`、卡牌描述进行普通搜索。
- 支持按标签检索，例如 `HEALTH:5`、`RARITY:5`、`SPELL_SCHOOL:2`。
- 支持按 `EnumID:值` 检索，例如 `45:5`。
- 支持标准 / 狂野两种模式切换。
- 支持法力值、职业、所属系列、稀有度、卡牌类型、种族、法术派系、是否可收藏、关键词等筛选项。
- 卡牌墙只显示卡图，点击后打开详情弹层。
- 详情页展示卡图、名称、描述、`CardID`、`DbfId`、关联卡、附魔卡和完整标签。
- 支持复制详情中的名称、`CardID`、`DbfId`。
- 为未知或缺失图片的卡牌提供文字占位展示。
- 搜索结果默认最多显示前 100 张卡，避免一次渲染过多内容。

## 运行环境

- Windows 10 或更高版本
- .NET 8 SDK
- PowerShell

说明：

- 该项目是 WPF 桌面程序，当前只面向 Windows。
- 仓库里使用的是 SDK 风格项目，可以直接用 `dotnet` 命令构建。

## 资源要求

程序运行依赖以下两个资源：

- `CardDefs.xml`
- `cardpng/`

资源定位规则：

- 程序会从当前工作目录、程序目录以及它们的父目录中向上查找。
- 只要某个目录同时包含 `CardDefs.xml` 和 `cardpng/`，程序就会把它视为资源根目录。

这意味着：

- 开发时可以直接在仓库根目录运行程序。
- 打包后的便携版目录中，也必须保留 `CardDefs.xml` 和 `cardpng/`。

## 快速开始

### 1. 进入项目目录

```powershell
Set-Location "E:\Hearthstone Card Search Tool"
```

### 2. 直接运行桌面程序

```powershell
dotnet run --project .\desktop\HearthstoneCardSearchTool.csproj
```

### 3. 编译 Release 版本

```powershell
dotnet build .\desktop\HearthstoneCardSearchTool.csproj -c Release
```

## 搜索说明

### 普通搜索

直接输入关键字即可，支持匹配：

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

说明：

- 左侧是原始标签名。
- 右侧按字符串匹配，不要求完全等于。
- 标签名会统一按英文大写标签处理。

### EnumID 搜索

格式：

```text
EnumID:值
```

示例：

```text
45:5
```

说明：

- 如果冒号左侧是纯数字，程序会把它当作 `EnumID` 检索。
- 这适合排查某个标签枚举值对应到了哪些卡牌。

## 筛选说明

界面顶部提供多组组合筛选项：

- 模式：标准 / 狂野
- 法力值：`0` 到 `10+`
- 职业
- 所属系列
- 稀有度
- 卡牌类型
- 随从种族
- 法术派系
- 是否可收藏
- 关键词

其中：

- 标准模式只显示当前白名单中的标准系列。
- 狂野模式会显示过滤后的全部可用系列。
- 附魔卡不会直接出现在普通结果中，但会在详情页中作为关联信息展示。

## 详情页说明

点击任意卡片后会打开详情弹层，包含以下信息：

- 卡图
- 中文卡名
- 中文描述
- `CardID`
- `DbfId`
- 衍生自 / 主卡牌
- 相关卡牌
- 附魔卡
- 完整标签列表

交互说明：

- 点击遮罩空白区域可关闭详情弹层。
- 按 `Esc` 可关闭详情弹层。
- 点击名称、`CardID`、`DbfId` 可快速复制。

## 测试

运行测试：

```powershell
dotnet test .\desktop-tests\HearthstoneCardSearchTool.Tests.csproj
```

当前测试覆盖的内容主要包括：

- 资源加载
- 基础筛选是否可用
- 普通搜索 / 标签搜索 / EnumID 搜索
- 详情数据是否可取
- 系列映射与标准 / 狂野集合逻辑

注意：

- 如果 `cardpng/` 目录下没有任何 `.png` 文件，和图片相关的测试会失败。
- 如果你只是调试搜索逻辑，没有卡图资源也能启动程序，但界面会显示文字占位卡片。

## 生成便携版

### 方式一：直接运行 PowerShell 打包脚本

```powershell
powershell -ExecutionPolicy Bypass -File .\build_exe.ps1
```

### 方式二：运行批处理脚本

```powershell
.\build_portable.bat
```

打包行为：

- 使用 `dotnet publish` 生成 `win-x64` 自包含单文件应用。
- 将 .NET / WPF 运行时并入最终 exe。
- 将 `CardDefs.xml` 和 `cardpng/` 复制到便携版目录。

便携版输出目录：

- `dist/炉石卡牌浏览器/炉石卡牌浏览器.exe`
- `dist/炉石卡牌浏览器/CardDefs.xml`
- `dist/炉石卡牌浏览器/cardpng/`

便携版特点：

- 根目录尽量精简。
- 除 `exe`、`CardDefs.xml` 和 `cardpng/` 外，不再铺开大量运行时文件。

## 项目结构

```text
Hearthstone Card Search Tool/
|-- desktop/                  WPF 桌面程序
|-- desktop-core/             卡牌仓库、搜索和映射逻辑
|-- desktop-tests/            单元测试
|-- cardpng/                  卡图资源目录
|-- dist/                     打包输出目录
|-- CardDefs.xml              卡牌定义数据
|-- build_exe.ps1             便携版打包脚本
|-- build_portable.bat        便携版打包入口
`-- HearthstoneCardSearchTool.slnx
```

各目录职责：

- `desktop/`
  - WPF 界面层
  - 负责卡牌墙、筛选条、详情弹层、复制交互
- `desktop-core/`
  - 数据解析
  - 搜索逻辑
  - 标签与枚举映射
  - 资源定位
- `desktop-tests/`
  - 回归测试
  - 搜索行为验证
  - 映射逻辑验证

## 核心实现说明

### 数据加载

- `CardRepository.Load(...)` 会读取 `CardDefs.xml`。
- 程序会扫描 `cardpng/` 下的 `.png` 文件，建立 `CardID -> 图片路径` 索引。
- 载入时会清洗卡牌文本中的 HTML 标签与特殊符号。

### 搜索策略

- 空输入时：浏览全库。
- `标签:值`：按标签值匹配。
- `数字:值`：按 `EnumID` 匹配。
- 其他输入：按常规文本搜索。

### 关系构建

程序会在加载后额外构建关联关系，用于详情页展示：

- 反向引用卡牌
- 前向关联卡牌
- 附魔卡
- 同名后缀衍生卡

## 已知注意事项

- 这是一个 Windows 专用桌面工具，不支持 Linux / macOS。
- 运行和打包都依赖本地的 `CardDefs.xml` 与 `cardpng/`。
- 如果 `cardpng/` 为空，程序仍可运行，但不会显示真实卡图。
- 仓库里的 `dist/` 可能保留历史构建目录，重新打包时只会覆盖当前目标输出目录。
- 当前代码中部分旧中文字符串存在编码遗留问题，但不影响打包结构和可执行文件命名。

## 后续可扩展方向

- 增加多关键词组合搜索语法。
- 增加标签列表导出。
- 增加详情页中的标签点击反查。
- 增加卡图缺失诊断工具。
- 增加按更新包自动刷新标准系列白名单的能力。

## 许可证

当前仓库未单独声明开源许可证。如需公开分发，建议补充 `LICENSE` 文件并明确资源使用范围。
