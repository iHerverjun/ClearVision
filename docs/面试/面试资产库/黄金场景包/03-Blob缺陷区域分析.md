# 黄金场景包 03：Blob 缺陷区域分析

> 成熟度：受控演示场景。
> 证据等级：算子测试 + 现有技术笔记。

---

## 1. 这个场景的定位

这个场景适合拿来说明“传统视觉里如何从分割结果走到区域分析”，而不是拿来包装成一个已经完整落地的缺陷项目。

当前最可靠的证据来自：

- `Acme.Product/tests/Acme.Product.Tests/Operators/BlobDetectionOperatorTests.cs`
- `docs/面试/BlobDetection-技术笔记.md`

---

## 2. 输入样本

当前公开测试样本主要有三类：

- 合成圆形前景
- 合成矩形前景
- `CreateShapeTestImage()` 这类多形状受控样图

这些样本的价值是：

- 圆形用于讲 `Circularity`
- 矩形用于讲 `Rectangularity`
- 多形状样图用于讲“通过特征而不是只靠面积做筛选”

---

## 3. 场景目标

这个场景最适合讲的不是“识别具体物体类别”，而是：

1. 先把前景区域分出来
2. 再按区域特征进行筛选、计数或缺陷判定

这正好能把 `Threshold / Morphology / BlobDetection` 串成一条工业视觉里常见的链路。

---

## 4. 建议演示骨架

### 4.1 一句话版

通过二值化和形态学预处理把前景分离出来，再用 `BlobDetection` 计算面积、圆度、矩形度等特征，最后输出计数和特征结果。

### 4.2 建议演示流程 JSON 骨架

```json
{
  "operators": [
    { "tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "图像采集" },
    {
      "tempId": "op_2",
      "operatorType": "Thresholding",
      "displayName": "二值化",
      "parameters": {
        "Threshold": "127",
        "MaxValue": "255",
        "UseOtsu": "false"
      }
    },
    {
      "tempId": "op_3",
      "operatorType": "Morphology",
      "displayName": "形态学处理",
      "parameters": {
        "Operation": "Close",
        "KernelSize": "3",
        "Iterations": "1"
      }
    },
    {
      "tempId": "op_4",
      "operatorType": "BlobAnalysis",
      "displayName": "Blob 分析",
      "parameters": {
        "MinArea": "100",
        "MaxArea": "100000",
        "OutputDetailedFeatures": "true"
      }
    },
    {
      "tempId": "op_5",
      "operatorType": "ResultOutput",
      "displayName": "结果输出",
      "parameters": {
        "Format": "JSON",
        "SaveToFile": "false"
      }
    }
  ]
}
```

---

## 5. 关键参数

| 参数 | 当前意义 | 面试里怎么讲 |
|---|---|---|
| `Threshold.Threshold` | 前景分割阈值 | 影响 Blob 是不是能被稳定分出来 |
| `Morphology.Operation` | 开/闭/腐蚀/膨胀 | 影响去噪、补洞、边界平滑 |
| `BlobDetection.MinArea / MaxArea` | 面积筛选 | 第一层过滤 |
| `BlobDetection.MinCircularity` | 圆度筛选 | 适合近圆目标 |
| `BlobDetection.MinRectangularity` | 矩形度筛选 | 适合长方形 / 矩形目标 |
| `OutputDetailedFeatures` | 输出特征开关 | 便于现场解释为什么被筛掉 |

---

## 6. 预期输出

公开测试里可以直接讲的结果包括：

- 对合成圆形，`BlobCount = 1`，`Circularity > 0.99`
- 对合成矩形，`BlobCount = 1`，`Rectangularity > 0.95`
- 对设置了 `MinRectangularity = 0.9` 的多形状样图，能把圆形过滤掉，只保留矩形

---

## 7. 验收标准

1. 前景能被稳定分离，不出现整图误白。
2. `BlobCount` 与预期形状数量一致。
3. 圆形/矩形样本的关键特征值达到阈值。
4. 输出不只是一张图，还能给出可解释的 Blob 特征。

---

## 8. 搭建耗时与调参轮次

### 8.1 当前可公开状态

- `搭建耗时`：没有正式计时记录。
- `调参轮次`：当前公开证据主要是算子测试与技术笔记，不是现场轮次日志。

### 8.2 面试建议口径

> 这个场景我会明确说是“受控演示包”，核心价值在于证明我能把传统视觉里最典型的“分割 -> 形态学 -> Blob 特征分析”链路讲清楚，并且现有测试能对上关键特征值。

---

## 9. 最适合拿来回答的追问

- BlobDetection 和 FindContours 有什么关系？
- 为什么 Blob 特别依赖前面的分割质量？
- 为什么同一个目标带孔洞时，不能简单地说成多个 Blob？

对应材料：

- `docs/面试/BlobDetection-技术笔记.md`
- `算子资料/算子知识笔记/10-Morphology-技术笔记.md`
- `算子资料/算子知识笔记/12-FindContours-技术笔记.md`

