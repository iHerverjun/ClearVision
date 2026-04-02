using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace Acme.Product.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class LOH_GC_Benchmark
    {
        private Mat _dummyImage;

        [GlobalSetup]
        public void Setup()
        {
            // Simulate an industrial camera typical resolution (e.g., 2048 x 1536, 1 channel 8-bit)
            _dummyImage = new Mat(1536, 2048, MatType.CV_8UC1);
            // Fill with random bytes to prevent trivial sorts
            _dummyImage.Randu(new Scalar(0), new Scalar(255));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _dummyImage?.Dispose();
        }

        // ============================================
        // 1. The OLD implementation (Allocates new byte[])
        // ============================================
        [Benchmark(Baseline = true)]
        public double Old_CalculateMedian()
        {
            // 将图像数据展平并排序
            var data = new byte[_dummyImage.Total()];
            Marshal.Copy(_dummyImage.Data, data, 0, data.Length);
            Array.Sort(data);

            if (data.Length % 2 == 0)
            {
                return (data[data.Length / 2 - 1] + data[data.Length / 2]) / 2.0;
            }
            else
            {
                return data[data.Length / 2];
            }
        }

        // ============================================
        // 2. The NEW implementation (ArrayPool)
        // ============================================
        [Benchmark]
        public double New_CalculateMedian_ArrayPool()
        {
            int length = (int)_dummyImage.Total();
            if (length == 0) return 0;

            // 使用内存池优化巨量像素分配，缓解 LOH 和 GC 暂停
            var pooledData = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            try
            {
                Marshal.Copy(_dummyImage.Data, pooledData, 0, length);
                // 仅对有效长度进行排序，忽略内存池分配的末尾冗余数据
                Array.Sort(pooledData, 0, length);

                if (length % 2 == 0)
                {
                    return (pooledData[length / 2 - 1] + pooledData[length / 2]) / 2.0;
                }
                else
                {
                    return pooledData[length / 2];
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(pooledData);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<LOH_GC_Benchmark>();
        }
    }
}
