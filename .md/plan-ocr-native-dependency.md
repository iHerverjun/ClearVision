# OCR 单元测试环境原生依赖修复计划

> **作者**: 蘅芜君
> **版本**: V1.0
> **创建日期**: 2026-02-20
> **最后更新**: 2026-02-21
> **文档编号**: plan-ocr-native-dependency
> **状态**: 进行中

---
## 1. 当前问题背景 (The Problem)
在 Sprint 6 的 `S6-004` 任务中，我们为 `OcrRecognitionOperator` 编写了集成与性能测试。但在执行 `dotnet test` 时，测试持续失败，核心原因是：
**`System.DllNotFoundException: Unable to load DLL 'PaddleOCR' or one of its dependencies. (0x8007007E)`**

## 2. 问题根因分析 (Root Cause)
1. **PInvoke 机制与加载路径**：`PaddleOCRSharp` 底层依赖于 C++ 编译的非托管 DLL（如 `OpenCvSharpExtern.dll`, `onnxruntime.dll`, `PaddleOCR.dll` 等）。在正常的类库或桌面程序中，这些 DLL 随 NuGet 包被放入 `runtimes\win-x64\native` 中，程序能正常加载。
2. **xUnit (testhost.exe) 沙箱隔离**：测试执行时，xUnit 的宿主进程 `testhost.exe` 的工作路径和程序集探测逻辑与普通应用不同。由于安全和隔离机制，它无法自动去所在深层级目录（`runtimes/...`）去寻找非.NET的原生 C++ 库。
3. **拷贝产生的冲突**：如果我们利用脚本或 `csproj` 把这些 C++ DLL 暴力拷贝到 `bin\Debug\net8.0` 根目录里，xUnit 的程序集扫描器在寻找测试用例时，会试图把这些非托管 DLL 当作 .NET DLL 通过反射去读取，结果会引发致命的 `BadImageFormatException` 并导致 `TESTRUNABORT` 框架崩溃。

这就是为什么刚才的报错从 `0x8007007E (找不到DLL)` 和 `TESTRUNABORT (测试框架崩溃)` 之间反复横跳的原因。

## 3. 之前尝试过的方案及其失败原因 (Failed Attempts)
- **配置 `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`**：导致所有系统 C++ DLL 被散落到根目录，直接触发刚才的 xUnit 扫描崩溃退出。
- **构造函数内调用 `SetDllDirectory` Win32 API**：.NET 8 的 PInvoke 默认采用了绕过此 API 的行为库标志，导致动态指定的目录被无视。
- **启动前动态修改 `PATH` 环境变量**：由于 xUnit 进程内缓存限制或安全机制加载顺序，此时修改的 `PATH` 对后续的非托管 DLL 加载失败起到作用甚微。
- **利用纯原生控制台测试 (OcrTestConsole)**：我们创建了一个没带 xUnit 的裸控制台脚本，发现引擎就能直接正常跑起来。这最终证实了**完全是 xUnit 框架本身的装箱沙盒隔离与底层 C++ DLL 解析起了冲突**。

## 4. 最终优雅破局的方案规划 (Proposed Solution)

为了一劳永逸且优雅地解决带有大量 C++ 非托管依赖在 xUnit 测试中运行的问题，必须避免硬拷贝文件，我们将采用下述现代 .NET 的机制解决：

### 阶段一：环境深度还原清理 (Clean Environment)
- 彻底清理测试模块 `Acme.Product.Tests` 的全部残留代码，移除掉我刚才留在 `OcrRecognitionOperatorTests.cs` 中脏乱差的复制逻辑、`PATH` 修改和 Win32 API 声明。
- 完全删除掉已经被 C++ 文件污染了的 `bin` 文件夹。

### 阶段二：利用 `NativeLibrary.SetDllImportResolver` 手动干预加载
我们将利用跨平台且被 .NET Core 推荐的非托管解析器回调方案。在单元测试首次启动（Fixture 组装或静态构造阶段）时：

```csharp
// 拦截 PaddleOCR.dll 的请求，直接利用绝对路径为其精确装载所需的 C++ 库，这样无需环境变量，也不怕 xUnit 扫描
System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
    typeof(PaddleOCREngine).Assembly, 
    (libraryName, assembly, searchPath) => 
    {
        // 遇到特定库时，主动跳转到 runtimes\win-x64\native 目录下加载
        if (libraryName == "PaddleOCR") 
        {
           // ... NativeLibrary.Load(absolutePath);
        }
    }
);
```

### 阶段三：独立挂载模型字典 (Mock inference Directory)
由于引擎在底层初始化时，默认向本应是程序的当前目录下寻找 `inference` (模型及字典等)，在测试项目中我们需要将这部分只读资源拷贝或做软链接交由它使用。

### 阶段四：验证基准指标
一旦底层 DLL 和模型被接管正常打通，就能看到期望的报告了：
1. 观察 `Integration_Accuracy_Should_Recognize_IndustrialText` (识别批号、日期等准确率是否达成 >= 95%)。
2. 观察 `Performance_1920x1080_InferenceTime_ShouldBe_Under_500ms` (是否能被稳控在 500 ms 极速推理内)。
