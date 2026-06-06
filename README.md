# Kinon

快捷键查看工具 — 基于无边框窗口模式的全局键盘钩子

## 功能概述

- 全局低级键盘钩子捕获所有按键组合
- 内存缓存 + SQLite 批量写入
- 置顶无焦点弹窗展示快捷键使用频率
- 按程序分组 / 频率排序 / "已记住"置底
- 用户自定义快捷键与动作执行（关机、运行程序等）
- 无边框全屏游戏兼容，无反作弊风险

## 项目结构

```
Kinon/
├── Kinon/                   # 主项目
│   ├── KeyboardHook.cs      # 全局键盘钩子
│   ├── OverlayForm.cs       # 置顶弹窗窗口
│   ├── Database/            # 数据库模块
│   ├── Models/              # 数据模型
│   └── Config/              # 配置界面
├── Kinon.Tests/             # 单元测试
├── 框架.docx                # 设计文档
└── README.md
```

## 技术栈

- .NET 8 / .NET Framework 4.8 (Windows)
- WinForms / WPF
- SQLite (Microsoft.Data.Sqlite)
- Win32 API (SetWindowsHookEx)

## 开发环境

需要 Visual Studio 2022 或 JetBrains Rider，.NET SDK 8.0+
