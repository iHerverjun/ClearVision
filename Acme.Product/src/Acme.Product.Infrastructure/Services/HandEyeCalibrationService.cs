using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 标定点对
/// </summary>
public class CalibrationPoint
{
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double PhysicalX { get; set; }
    public double PhysicalY { get; set; }
}

/// <summary>
/// 手眼标定解算结果
/// </summary>
public class HandEyeCalibrationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }

    // 整体像素均方根误差与单独维度轴上的最大误差 (物理单位)
    public double MeanErrorX { get; set; }
    public double MeanErrorY { get; set; }
    public double Rmse { get; set; }
}

/// <summary>
/// 手眼标定内部实体 (用于持久化)
/// 注意：为兼容现有 CoordinateTransformOperator 读取的反序列化 JSON 结构而设计
/// </summary>
public class HandEyeCalibrationModel
{
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
}

public interface IHandEyeCalibrationService
{
    /// <summary>
    /// 解算标定参数
    /// </summary>
    Task<HandEyeCalibrationResult> SolveAsync(List<CalibrationPoint> points);

    /// <summary>
    /// 保存标定文件 (.json)
    /// </summary>
    Task<bool> SaveCalibrationAsync(HandEyeCalibrationResult result, string fileName);
}

/// <summary>
/// 手眼标定服务实现。
/// 
/// 备注: 考虑到历史 CoordinateTransformOperator 算子兼容问题，目前的标定假设只有平移(Origin)
/// 和缩放(Scale)，不包含旋转角及仿射变换投影（假定相机正交固定架设于物理运动系之上）。
/// 随着未来 CoordinateTransformOperator 的升级，本解算处可扩展为 6DOF 等更复杂的仿射投影运算。
/// </summary>
public class HandEyeCalibrationService : IHandEyeCalibrationService
{
    public Task<HandEyeCalibrationResult> SolveAsync(List<CalibrationPoint> points)
    {
        if (points == null || points.Count < 2)
        {
            return Task.FromResult(new HandEyeCalibrationResult
            {
                Success = false,
                Message = "点位不足，至少需要 2 个不重合的点位进行标定。"
            });
        }

        // 使用最小二乘法(Least Squares)独立求解 X 轴和 Y 轴的线性回归 y = kx + b
        // 其中 x 为 Pixel 坐标，y 为 Physical 物理坐标
        // PhysicalX = ScaleX * PixelX + OriginX
        // PhysicalY = ScaleY * PixelY + OriginY

        int n = points.Count;

        // 计算 X 轴
        double sumPx = points.Sum(p => p.PixelX);
        double sumPhyx = points.Sum(p => p.PhysicalX);
        double sumPxPhyx = points.Sum(p => p.PixelX * p.PhysicalX);
        double sumPxPx = points.Sum(p => p.PixelX * p.PixelX);

        // 计算 Y 轴
        double sumPy = points.Sum(p => p.PixelY);
        double sumPhyy = points.Sum(p => p.PhysicalY);
        double sumPyPhyy = points.Sum(p => p.PixelY * p.PhysicalY);
        double sumPyPy = points.Sum(p => p.PixelY * p.PixelY);

        double denomX = (n * sumPxPx - sumPx * sumPx);
        double denomY = (n * sumPyPy - sumPy * sumPy);

        if (Math.Abs(denomX) < 1e-10 || Math.Abs(denomY) < 1e-10)
        {
            return Task.FromResult(new HandEyeCalibrationResult
            {
                Success = false,
                Message = "提供的标定点共线或过于集中，无法解算有效的缩放比例，请分散点击位置重新采点。"
            });
        }

        double scaleX = (n * sumPxPhyx - sumPx * sumPhyx) / denomX;
        double originX = (sumPhyx - scaleX * sumPx) / n;

        double scaleY = (n * sumPyPhyy - sumPy * sumPhyy) / denomY;
        double originY = (sumPhyy - scaleY * sumPy) / n;

        // 计算所有点位的拟合误差
        double errSqSum = 0;
        double errTotalX = 0;
        double errTotalY = 0;

        foreach (var p in points)
        {
            double fitPhyX = scaleX * p.PixelX + originX;
            double fitPhyY = scaleY * p.PixelY + originY;

            double errX = Math.Abs(fitPhyX - p.PhysicalX);
            double errY = Math.Abs(fitPhyY - p.PhysicalY);

            errTotalX += errX;
            errTotalY += errY;
            errSqSum += (errX * errX) + (errY * errY);
        }

        return Task.FromResult(new HandEyeCalibrationResult
        {
            Success = true,
            Message = "解算成功",
            OriginX = originX,
            OriginY = originY,
            ScaleX = scaleX,
            ScaleY = scaleY,
            MeanErrorX = errTotalX / n,
            MeanErrorY = errTotalY / n,
            Rmse = Math.Sqrt(errSqSum / n)
        });
    }

    public async Task<bool> SaveCalibrationAsync(HandEyeCalibrationResult result, string fileName)
    {
        if (!result.Success)
            return false;

        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }

            // 补充后缀保障
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            // 如果仅给出纯文件名，则追加到 AppData 库中，否则如果是绝对路径则直接使用原始路径
            string fullPath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(appData, fileName);
            string? directoryPath = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var model = new HandEyeCalibrationModel
            {
                OriginX = result.OriginX,
                OriginY = result.OriginY,
                ScaleX = result.ScaleX,
                ScaleY = result.ScaleY
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
            await File.WriteAllTextAsync(fullPath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[保存标定失败] {ex.Message}");
            return false;
        }
    }
}
