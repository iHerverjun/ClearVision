// TestHelpers.cs
// 将 ImageWrapper 放入 inputs 字典
// 作者：蘅芜君

using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public static class TestHelpers
{
    /// <summary>
    /// 创建测试用的算子参数
    /// </summary>
    public static Parameter CreateParameter(string name, object value, string dataType = "String")
    {
        return new Parameter(Guid.NewGuid(), name, name, "", dataType, value);
    }

    public static Parameter CreateParameter(string name, string displayName, string dataType, object defaultValue, object minValue, object maxValue, bool isRequired = true)
    {
        return new Parameter(Guid.NewGuid(), name, displayName, "", dataType, defaultValue, minValue, maxValue, isRequired);
    }
    /// <summary>
    /// 创建一个纯色测试图像的 ImageWrapper
    /// </summary>
    public static ImageWrapper CreateTestImage(int width = 200, int height = 200, Scalar? color = null)
    {
        var c = color ?? new Scalar(128, 128, 128);
        var mat = new Mat(height, width, MatType.CV_8UC3, c);
        return new ImageWrapper(mat);
    }

    /// <summary>
    /// 创建包含简单几何形状的测试图像（用于边缘检测、轮廓检测等）
    /// </summary>
    public static ImageWrapper CreateShapeTestImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 200), 60, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    /// <summary>
    /// 创建灰度梯度图像（用于阈值测试）
    /// </summary>
    public static ImageWrapper CreateGradientTestImage()
    {
        var mat = new Mat(200, 200, MatType.CV_8UC3);
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 200; x++)
            {
                byte val = (byte)(x * 255 / 200);
                mat.Set(y, x, new Vec3b(val, val, val));
            }
        return new ImageWrapper(mat);
    }

    /// <summary>
    /// 将 ImageWrapper 放入 inputs 字典
    /// </summary>
    public static Dictionary<string, object> CreateImageInputs(ImageWrapper image, string key = "Image")
    {
        return new Dictionary<string, object> { { key, image } };
    }
}
