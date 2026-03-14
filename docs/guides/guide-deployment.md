# ClearVision 发布部署指南

> **作者**: 蘅芜君
> **版本**: V1.0
> **创建日期**: 2026-02-16
> **最后更新**: 2026-02-21
> **文档编号**: guide-deployment
> **状态**: 已完成

---
本文档详细介绍 ClearVision 的发布部署功能及使用方法。

---

## 📦 发布方式概览

ClearVision 支持以下三种发布方式：

| 方式 | 适用场景 | 优点 | 缺点 |
|------|----------|------|------|
| **单文件ZIP包** | 企业内部部署、快速分发 | 解压即用、无需安装、便携 | 无自动更新 |
| **MSIX安装包** | Windows商店发布、企业IT管理 | 自动更新、卸载干净、安全性高 | 需要签名证书 |
| **CI/CD自动发布** | 持续集成、开源项目 | 自动化、版本管理、多版本并存 | 需要GitHub环境 |

---

## 🚀 方式一：单文件ZIP包（推荐）

### 功能说明

将应用程序及其所有依赖打包为单个可执行文件，用户下载解压后即可直接运行，无需安装任何运行时。

**技术特点**：
- 自包含 .NET 8 运行时
- 单文件发布（Acme.Product.Desktop.exe）
- 原生代码预编译（ReadyToRun），启动更快
- 内部压缩，体积更小
- 支持 x64 Windows 10/11

### 本地构建步骤

#### 方法一：使用发布脚本（推荐）

```bash
# 1. 进入脚本目录
cd Acme.Product/scripts

# 2. 运行发布脚本
.\publish.bat
```

脚本会自动完成以下操作：
1. ✅ 清理旧的发布文件
2. ✅ 编译 Release 版本
3. ✅ 生成单文件可执行程序
4. ✅ 创建中文启动脚本
5. ✅ 打包为 ZIP 文件

**输出位置**：`publish/ClearVision-1.0.0-win-x64.zip`

#### 方法二：手动构建

```bash
# 1. 发布 Release 版本
dotnet publish Acme.Product/src/Acme.Product.Desktop/Acme.Product.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ./publish/ClearVision-1.0.0

# 2. 手动打包（可选）
Compress-Archive -Path ./publish/ClearVision-1.0.0/* -DestinationPath ./publish/ClearVision-1.0.0-win-x64.zip
```

### 用户使用说明

#### 安装步骤

1. **下载** `ClearVision-x.x.x-win-x64.zip` 文件
2. **解压** 到任意目录（例如 `C:\Tools\ClearVision`）
3. **运行** 双击 `启动 ClearVision.bat` 或直接运行 `Acme.Product.Desktop.exe`

#### 首次运行

首次运行时会自动完成以下初始化：
- 🔧 解压内嵌的运行时库（约10-20秒）
- 🌐 检查并安装 WebView2 运行时（如未安装）
- 📁 创建用户数据目录 `%USERPROFILE%\.clearvision`

#### 卸载

直接删除解压目录即可，无残留文件。

---

## 📱 方式二：MSIX 安装包

### 功能说明

MSIX 是微软推出的现代化应用打包格式，支持从 Windows 应用商店或本地安装。

**技术特点**：
- 沙箱运行，安全性高
- 支持增量更新
- 卸载彻底无残留
- 可发布到 Microsoft Store
- 支持企业部署（Intune、SCCM）

### 构建步骤

#### 前置要求

1. 安装 **Windows SDK**（10.0.19041.0 或更高）
2. 准备应用图标资源（见下方说明）
3. （可选）代码签名证书

#### 准备图标资源

在 `Acme.Product/src/Acme.Product.Desktop.Package/Images/` 目录放置以下图标文件：

```
Images/
├── Square44x44Logo.png      (44x44)
├── Square150x150Logo.png    (150x150)
├── Wide310x150Logo.png      (310x150)
├── StoreLogo.png            (50x50)
└── SplashScreen.png         (620x300)
```

> 💡 可以使用在线工具生成：[App Icon Generator](https://appicon.co/)

#### 构建命令

```bash
# 1. 还原包
dotnet restore Acme.Product/src/Acme.Product.Desktop.Package/Acme.Product.Desktop.Package.csproj

# 2. 发布主项目
dotnet publish Acme.Product/src/Acme.Product.Desktop/Acme.Product.Desktop.csproj -c Release

# 3. 构建 MSIX 包
dotnet build Acme.Product/src/Acme.Product.Desktop.Package/Acme.Product.Desktop.Package.csproj -c Release

# 4. 输出位置
# Acme.Product/src/Acme.Product.Desktop.Package/AppPackages/
```

#### 签名（正式发布必需）

```bash
# 使用自签名证书（测试用）
New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=Acme" `
  -KeyUsage DigitalSignature `
  -FriendlyName "ClearVision Test Cert" `
  -CertStoreLocation "Cert:\CurrentUser\My"

# 签名 MSIX
SignTool sign /fd SHA256 /a /f certificate.pfx /p password ClearVision-x.x.x.msix
```

### 用户使用说明

#### 安装步骤

1. **下载** `ClearVision-x.x.x.msix` 文件
2. **双击** 安装包（Windows 10 1809+）
3. 按照向导完成安装
4. 从 **开始菜单** 启动 ClearVision

#### 更新

- **自动更新**：如配置更新服务器，应用会自动检查并安装更新
- **手动更新**：下载新版本 MSIX 文件，双击安装即可覆盖更新

#### 卸载

1. 打开 **设置 > 应用 > 应用和功能**
2. 搜索 "ClearVision"
3. 点击 **卸载**

---

## 🤖 方式三：CI/CD 自动发布

### 功能说明

通过 GitHub Actions 实现自动化构建、测试和发布。

**工作流程**：
```
代码提交/打标签
    ↓
触发 GitHub Actions
    ↓
┌─────────────────┐
│  1. 构建 Debug   │
│  2. 运行单元测试 │
│  3. 运行UI测试   │
└─────────────────┘
    ↓
┌─────────────────┐
│  4. 构建 Release │
│  5. 单文件发布   │
│  6. 打包ZIP     │
└─────────────────┘
    ↓
创建 GitHub Release（仅标签触发）
```

### 使用方法

#### 自动触发

| 操作 | 触发结果 |
|------|----------|
| `git push origin main` | 构建并保存Artifact（30天） |
| `git push origin develop` | 构建并保存Artifact（30天） |
| `git tag v1.0.0 && git push origin v1.0.0` | 构建 + 创建GitHub Release |
| Pull Request | 仅构建和测试 |

#### 手动触发

1. 打开 GitHub 仓库页面
2. 点击 **Actions** 标签
3. 选择 **ClearVision CI/CD** 工作流
4. 点击 **Run workflow**
5. 输入版本号（可选）
6. 点击 **Run workflow**

#### 下载构建产物

1. 打开 GitHub 仓库页面
2. 点击 **Actions** 标签
3. 选择最新的成功运行
4. 在 **Artifacts** 部分下载 `ClearVision-Build-x.x.x`

#### 创建正式Release

```bash
# 1. 更新版本号（在代码中）
# 编辑 Acme.Product/src/Acme.Product.Desktop/Acme.Product.Desktop.csproj

# 2. 提交更改
git add .
git commit -m "Release v1.0.0"

# 3. 创建标签
git tag v1.0.0

# 4. 推送标签（这会触发自动发布）
git push origin v1.0.0

# 5. 等待 CI 完成，GitHub Release 会自动创建
```

---

## ⚙️ 配置文件说明

### 1. 项目文件 (Acme.Product.Desktop.csproj)

```xml
<PropertyGroup>
  <!-- 发布配置 -->
  <PublishSingleFile>true</PublishSingleFile>          <!-- 单文件 -->
  <SelfContained>true</SelfContained>                  <!-- 自包含运行时 -->
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>       <!-- x64平台 -->
  <PublishReadyToRun>true</PublishReadyToRun>          <!-- 预编译优化 -->
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile> <!-- 压缩 -->
  
  <!-- 版本信息 -->
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>

<!-- Release 优化 -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DebugType>none</DebugType>          <!-- 不包含调试符号 -->
  <Optimize>true</Optimize>             <!-- 启用优化 -->
</PropertyGroup>
```

### 2. CI/CD 配置 (.github/workflows/ci.yml)

```yaml
# 主要任务
jobs:
  build-and-test:    # 构建和测试
  release-build:     # Release构建（仅main分支和标签）
  create-release:    # 创建Release（仅标签）
  code-quality:      # 代码质量检查（仅PR）

# 环境变量
env:
  SOLUTION_PATH: Acme.Product/Acme.Product.sln
  DESKTOP_PROJECT: Acme.Product/src/Acme.Product.Desktop/Acme.Product.Desktop.csproj
```

---

## 📊 文件大小对比

| 发布方式 | 文件大小 | 依赖要求 |
|----------|----------|----------|
| 单文件ZIP（未压缩） | ~150-200 MB | 无 |
| 单文件ZIP（压缩后） | ~50-70 MB | 无 |
| MSIX包 | ~50-70 MB | Windows 10 1809+ |
| 框架依赖（非自包含） | ~20-30 MB | 需要预装.NET 8 |

---

## 🔧 故障排除

### 构建失败

#### 错误：找不到 Windows SDK

**解决方案**：
```bash
# 安装 Windows SDK
winget install Microsoft.WindowsSDK
```

#### 错误：缺少图标文件

**解决方案**：
```bash
# 创建占位图标目录
mkdir Acme.Product/src/Acme.Product.Desktop.Package/Images
# 放置所需图标文件（或从其他项目复制）
```

#### 错误：MSIX签名失败

**解决方案**：
```bash
# 测试时可禁用签名
# 在 Package.appxmanifest 中:
# <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
```

### 运行时问题

#### 首次启动慢

**原因**：单文件应用首次需要解压内嵌的运行时库

**优化**：
- 使用 ReadyToRun 已在编译时优化
- 建议将应用放在SSD硬盘上
- 解压过程只在首次运行发生

#### WebView2 初始化失败

**解决方案**：
```bash
# 手动安装 WebView2 Runtime
winget install Microsoft.EdgeWebView2Runtime
```

---

## 📚 最佳实践

### 版本管理

1. **语义化版本**：使用 `v1.0.0` 格式
2. **预发布版本**：使用 `v1.0.0-beta.1` 格式
3. **每次发布更新**：
   - `AssemblyVersion`（程序集版本）
   - `AssemblyFileVersion`（文件版本）
   - Git 标签

### 发布流程建议

```
开发分支 (develop)
    ↓ 功能开发完成
    ↓ 合并到 main
main 分支
    ↓ 测试验证
    ↓ 打标签 v1.0.0
    ↓ 推送标签
自动触发 CI/CD
    ↓ 构建 → 测试 → 发布
GitHub Release 自动生成
    ↓ 下载 ZIP/MSIX 分发
```

### 分发策略

| 用户类型 | 推荐方式 | 原因 |
|----------|----------|------|
| 内部测试 | ZIP包 | 快速迭代，无需安装 |
| 企业用户 | MSIX | IT管理方便，可组策略部署 |
| 公开用户 | GitHub Release | 版本透明，自动更新 |

---

## 📞 技术支持

如有问题，请参考：
- ?? [????](./guide-user.md) - ????
- ?? [?????](../OPERATOR_CATALOG.md) - ???????????
- ?? [??????](../operators/CATALOG.md) - ????????
- ?? [????](#????) - ????
- ?? [GitHub Issues](https://github.com/your-repo/issues) - ????

---

**文档版本**：v1.0.0  
**最后更新**：2026-02-12  
**适用版本**：ClearVision 1.0.0+
