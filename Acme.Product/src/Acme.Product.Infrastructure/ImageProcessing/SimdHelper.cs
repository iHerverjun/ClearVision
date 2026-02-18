// SimdHelper.cs
// SIMD 辅助函数 - 使用 System.Runtime.Intrinsics 加速图像处理
// 作者：蘅芜君

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// SIMD 辅助类 - 提供硬件加速的图像处理函数
/// 
/// 支持的指令集:
/// - SSE2: 基础整数/浮点运算
/// - SSSE3: Shuffle 指令用于 LUT 查表
/// - AVX2: 256位宽向量运算 (可选)
/// 
/// 使用方式:
/// 1. 检查 IsSupported 属性
/// 2. 调用相应函数
/// 3. 提供标量回退路径
/// </summary>
public static class SimdHelper
{
    #region 能力检测

    /// <summary>
    /// 检查是否支持 SIMD 加速
    /// </summary>
    public static bool IsSupported => Sse2.IsSupported && Ssse3.IsSupported;

    /// <summary>
    /// 获取 SIMD 能力信息
    /// </summary>
    public static string GetCapabilities()
    {
        var caps = new List<string>();
        if (Sse2.IsSupported) caps.Add("SSE2");
        if (Ssse3.IsSupported) caps.Add("SSSE3");
        if (Avx.IsSupported) caps.Add("AVX");
        if (Avx2.IsSupported) caps.Add("AVX2");
        return string.Join(", ", caps);
    }

    #endregion

    #region LUT 查表加速 (SSSE3 Shuffle)

    /// <summary>
    /// 使用 SSSE3 Shuffle 指令加速 LUT 查表
    /// 
    /// Shuffle 可以将 16 字节的 LUT 按索引查表，一次处理 16 个像素
    /// 比标量循环快 5-10 倍
    /// </summary>
    /// <param name="srcPtr">源数据指针 (索引值 0-15)</param>
    /// <param name="dstPtr">目标数据指针</param>
    /// <param name="lut">查找表 (16 字节)</param>
    /// <param name="count">像素数量</param>
    public static unsafe void LookupTableSimd(byte* srcPtr, byte* dstPtr, byte[] lut, int count)
    {
        if (!Ssse3.IsSupported)
        {
            // 标量回退
            for (int i = 0; i < count; i++)
            {
                dstPtr[i] = lut[srcPtr[i]];
            }
            return;
        }

        // 将 LUT 加载为 128 位向量
        fixed (byte* lutPtr = lut)
        {
            var lutVec = Sse2.LoadVector128(lutPtr);

            int i = 0;
            // 每次处理 16 字节
            for (; i <= count - 16; i += 16)
            {
                // 加载 16 个索引
                var indices = Sse2.LoadVector128(srcPtr + i);

                // SSSE3 Shuffle: 按索引从 LUT 中取值
                var result = Ssse3.Shuffle(lutVec, indices);

                // 存储结果
                Sse2.Store(dstPtr + i, result);
            }

            // 处理剩余像素
            for (; i < count; i++)
            {
                dstPtr[i] = lut[srcPtr[i]];
            }
        }
    }

    /// <summary>
    /// 使用 SIMD 同时查两个 LUT 表并取最大值
    /// </summary>
    /// <param name="lsbPtr">低 4 位索引指针</param>
    /// <param name="msbPtr">高 4 位索引指针</param>
    /// <param name="dstPtr">目标指针</param>
    /// <param name="lutLow">低 LUT (16 字节)</param>
    /// <param name="lutHigh">高 LUT (16 字节)</param>
    /// <param name="count">像素数量</param>
    public static unsafe void LookupTableMaxSimd(
        byte* lsbPtr, byte* msbPtr, byte* dstPtr,
        byte[] lutLow, byte[] lutHigh, int count)
    {
        if (!Ssse3.IsSupported)
        {
            // 标量回退
            for (int idx = 0; idx < count; idx++)
            {
                byte lowVal = lutLow[lsbPtr[idx]];
                byte highVal = lutHigh[msbPtr[idx]];
                dstPtr[idx] = Math.Max(lowVal, highVal);
            }
            return;
        }

        fixed (byte* lutLowPtr = lutLow)
        fixed (byte* lutHighPtr = lutHigh)
        {
            var lutLowVec = Sse2.LoadVector128(lutLowPtr);
            var lutHighVec = Sse2.LoadVector128(lutHighPtr);

            int idx = 0;
            for (; idx <= count - 16; idx += 16)
            {
                // 加载索引
                var lsbIndices = Sse2.LoadVector128(lsbPtr + idx);
                var msbIndices = Sse2.LoadVector128(msbPtr + idx);

                // 查表
                var lowVals = Ssse3.Shuffle(lutLowVec, lsbIndices);
                var highVals = Ssse3.Shuffle(lutHighVec, msbIndices);

                // 取最大值
                var result = Sse2.Max(lowVals, highVals);

                Sse2.Store(dstPtr + idx, result);
            }

            // 处理剩余
            for (; idx < count; idx++)
            {
                byte lowVal = lutLow[lsbPtr[idx]];
                byte highVal = lutHigh[msbPtr[idx]];
                dstPtr[idx] = Math.Max(lowVal, highVal);
            }
        }
    }

    #endregion

    #region 相似度累加加速 (SSE2)

    /// <summary>
    /// 使用 SSE2 加速特征点响应值累加
    /// 
    /// 将 byte 扩展为 short 后累加，避免溢出
    /// 一次处理 16 个 byte (扩展为 16 个 short)
    /// </summary>
    /// <param name="responsePtr">响应值指针 (byte)</param>
    /// <param name="dstPtr">累加目标指针 (short)</param>
    /// <param name="count">元素数量</param>
    public static unsafe void AccumulateSimilaritySimd(byte* responsePtr, short* dstPtr, int count)
    {
        if (!Sse2.IsSupported)
        {
            // 标量回退
            for (int j = 0; j < count; j++)
            {
                dstPtr[j] += (short)responsePtr[j];
            }
            return;
        }

        var zero = Vector128<byte>.Zero;

        int i = 0;
        // 每次处理 16 个 byte，产生 16 个 short (需要两组运算)
        for (; i <= count - 16; i += 16)
        {
            // 加载 16 字节响应值
            var src8 = Sse2.LoadVector128(responsePtr + i);

            // UnpackLow: 将低 8 字节扩展为 8 个 short
            var lo16 = Sse2.UnpackLow(src8, zero).AsInt16();
            var hi16 = Sse2.UnpackHigh(src8, zero).AsInt16();

            // 加载当前累加值
            var dstLo = Sse2.LoadVector128(dstPtr + i);
            var dstHi = Sse2.LoadVector128(dstPtr + i + 8);

            // 累加
            var resLo = Sse2.Add(dstLo, lo16);
            var resHi = Sse2.Add(dstHi, hi16);

            // 存储
            Sse2.Store(dstPtr + i, resLo);
            Sse2.Store(dstPtr + i + 8, resHi);
        }

        // 处理剩余
        for (; i < count; i++)
        {
            dstPtr[i] += (short)responsePtr[i];
        }
    }

    /// <summary>
    /// 初始化累加缓冲区为零 (使用 SIMD)
    /// </summary>
    /// <param name="dstPtr">目标指针</param>
    /// <param name="count">元素数量</param>
    public static unsafe void ZeroMemorySimd(short* dstPtr, int count)
    {
        if (!Sse2.IsSupported)
        {
            // 标量回退
            for (int j = 0; j < count; j++)
            {
                dstPtr[j] = 0;
            }
            return;
        }

        var zero = Vector128<short>.Zero;

        int i = 0;
        // 每次清零 8 个 short (128 位)
        for (; i <= count - 8; i += 8)
        {
            Sse2.Store(dstPtr + i, zero);
        }

        // 处理剩余
        for (; i < count; i++)
        {
            dstPtr[i] = 0;
        }
    }

    #endregion

    #region 批量处理辅助函数

    /// <summary>
    /// 计算最优 SIMD 处理块大小
    /// </summary>
    /// <param name="totalCount">总元素数</param>
    /// <param name="vectorSize">向量大小 (字节)</param>
    /// <returns>块大小</returns>
    public static int GetOptimalBlockSize(int totalCount, int vectorSize = 16)
    {
        // 确保块大小是向量大小的整数倍
        // 同时考虑 CPU 缓存行大小 (通常 64 字节)
        int cacheLineSize = 64;
        int elementsPerCacheLine = cacheLineSize / vectorSize;
        int blockSize = Math.Max(vectorSize * elementsPerCacheLine, 256);
        return Math.Min(blockSize, totalCount);
    }

    /// <summary>
    /// 获取处理器 SIMD 特性报告
    /// </summary>
    public static string GetReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SIMD Support Report:");
        sb.AppendLine($"  SSE2: {Sse2.IsSupported}");
        sb.AppendLine($"  SSE3: {Sse3.IsSupported}");
        sb.AppendLine($"  SSSE3: {Ssse3.IsSupported}");
        sb.AppendLine($"  SSE4.1: {Sse41.IsSupported}");
        sb.AppendLine($"  SSE4.2: {Sse42.IsSupported}");
        sb.AppendLine($"  AVX: {Avx.IsSupported}");
        sb.AppendLine($"  AVX2: {Avx2.IsSupported}");
        sb.AppendLine($"  AVX-512F: {Avx512F.IsSupported}");
        sb.AppendLine($"  Active: {GetCapabilities()}");
        return sb.ToString();
    }

    #endregion
}
