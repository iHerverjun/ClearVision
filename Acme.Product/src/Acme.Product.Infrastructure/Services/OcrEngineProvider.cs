// OcrEngineProvider.cs
// PaddleOCRSharp 引擎单例管理
// 作者：蘅芜君

using Microsoft.Extensions.Logging;
using PaddleOCRSharp;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// PaddleOCR 引擎提供者 (单例模式)
/// 负责在应用程序生命周期内全局管理 OCR 模型，防止内存 OOM 泄露，
/// 同时使用 lock 保证初始化和识别的线程安全性。
/// </summary>
public sealed class OcrEngineProvider : IDisposable
{
    private readonly ILogger<OcrEngineProvider> _logger;
    private PaddleOCREngine? _engine;
    private readonly object _lockObj = new object();
    private bool _isDisposed = false;

    public OcrEngineProvider(ILogger<OcrEngineProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取底层的 PaddleOCREngine，如果是首次访问则懒加载并初始化。
    /// </summary>
    public PaddleOCREngine GetEngine()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OcrEngineProvider));

        if (_engine == null)
        {
            lock (_lockObj)
            {
                if (_engine == null)
                {
                    try
                    {
                        _logger.LogInformation("[OcrEngineProvider] 正在初始化 PaddleOCREngine 服务...");

                        // 采用默认的 OCR 参数与模型字典 (自动提取到执行目录)
                        OCRModelConfig? config = null; // 默认自带模型
                        OCRParameter oCRParameter = new OCRParameter();
                        oCRParameter.cpu_math_library_num_threads = 4;// 限制线程数以防止占满CPU
                        oCRParameter.enable_mkldnn = true;

                        _engine = new PaddleOCREngine(config, oCRParameter);
                        _logger.LogInformation("[OcrEngineProvider] PaddleOCREngine 初始化完成！");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[OcrEngineProvider] PaddleOCREngine 初始化失败！");
                        throw;
                    }
                }
            }
        }
        return _engine;
    }

    /// <summary>
    /// 线程安全地执行图片文字识别。
    /// </summary>
    public OCRResult DetectText(byte[] imageBytes)
    {
        lock (_lockObj)
        {
            var engine = GetEngine();
            return engine.DetectText(imageBytes);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        lock (_lockObj)
        {
            if (!_isDisposed)
            {
                _engine?.Dispose();
                _engine = null;
                _isDisposed = true;
                _logger.LogInformation("[OcrEngineProvider] PaddleOCREngine 已成功释放。");
            }
        }
    }
}
