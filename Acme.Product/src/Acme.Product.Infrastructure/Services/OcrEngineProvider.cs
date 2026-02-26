using Microsoft.Extensions.Logging;
using PaddleOCRSharp;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Thread-safe singleton wrapper around PaddleOCR engine.
/// </summary>
public sealed class OcrEngineProvider : IDisposable
{
    private readonly ILogger<OcrEngineProvider> _logger;
    private readonly object _syncRoot = new();
    private PaddleOCREngine? _engine;
    private bool _isDisposed;

    public OcrEngineProvider(ILogger<OcrEngineProvider> logger)
    {
        _logger = logger;
    }

    public PaddleOCREngine GetEngine()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return EnsureEngineLocked();
        }
    }

    public OCRResult DetectText(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var engine = EnsureEngineLocked();
            return engine.DetectText(imageBytes);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _engine?.Dispose();
            _engine = null;
            _isDisposed = true;
            _logger.LogInformation("[OcrEngineProvider] PaddleOCR engine disposed.");
        }
    }

    private PaddleOCREngine EnsureEngineLocked()
    {
        if (_engine != null)
        {
            return _engine;
        }

        try
        {
            _logger.LogInformation("[OcrEngineProvider] Initializing PaddleOCR engine.");
            OCRModelConfig? config = null;
            var parameter = new OCRParameter
            {
                cpu_math_library_num_threads = 4,
                enable_mkldnn = true
            };

            _engine = new PaddleOCREngine(config, parameter);
            _logger.LogInformation("[OcrEngineProvider] PaddleOCR engine initialized.");
            return _engine;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OcrEngineProvider] Failed to initialize PaddleOCR engine.");
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(OcrEngineProvider));
        }
    }
}
