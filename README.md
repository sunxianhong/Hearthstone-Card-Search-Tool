# 炉石卡牌搜索台

这个仓库现在是 `Tauri + React + TypeScript + Rust` 版本，不再保留旧 Python 桌面程序。

## 当前界面目标

- 单页网页式整体滚动
- 卡牌墙只显示卡图
- 弹窗点击空白处关闭
- 弹窗只保留基础信息和完整标签
- 便携版可直接拷贝目录测试

## 开发启动

```powershell
Set-Location "E:\Hearthstone Card Search Tool\app"
npm.cmd run tauri:dev
```

## 生成便携版

```powershell
powershell -ExecutionPolicy Bypass -File .\build_exe.ps1
```

生成结果：

- `dist/HearthstoneCardSearchTool/HearthstoneCardSearchTool.exe`
- `dist/HearthstoneCardSearchTool/CardDefs.xml`
- `dist/HearthstoneCardSearchTool/cardpng/`

## 数据来源

- `CardDefs.xml`
- `cardpng/`

## 目录说明

- `app/`：桌面前端和 Rust 数据层
- `dist/`：便携版输出目录
