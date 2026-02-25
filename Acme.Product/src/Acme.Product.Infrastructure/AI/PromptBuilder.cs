using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 构建发送给 AI 的 System Prompt
/// </summary>
public class PromptBuilder
{
    private readonly IOperatorFactory _operatorFactory;
    private static readonly JsonSerializerOptions _catalogJsonOptions = new()
    {
        WriteIndented = true
    };

    public PromptBuilder(IOperatorFactory operatorFactory)
    {
        _operatorFactory = operatorFactory;
    }

    /// <summary>
    /// 构建完整的 System Prompt
    /// </summary>
    public string BuildSystemPrompt(string? userDescription = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(GetRoleDefinition());
        sb.AppendLine();
        sb.AppendLine(GetDomainKnowledge());
        sb.AppendLine();
        sb.AppendLine(GetPhase1OperatorExtensions());
        sb.AppendLine();
        sb.AppendLine(GetPhase2OperatorExtensions());
        sb.AppendLine();
        sb.AppendLine(GetPhase3OperatorExtensions());
        sb.AppendLine();
        sb.AppendLine(GetOperatorCatalog(userDescription));
        sb.AppendLine();
        sb.AppendLine(GetConnectionRules());
        sb.AppendLine();
        sb.AppendLine(GetParameterInferenceGuide());
        sb.AppendLine();
        sb.AppendLine(GetOutputFormatSpec());
        sb.AppendLine();
        sb.AppendLine(GetFewShotExamples());

        return sb.ToString();
    }

    private string GetDomainKnowledge() => """
        # 领域知识与设计模式

        ## ClearVision 平台概述
        ClearVision 是一个面向工业制造的在线视觉检测平台。
        主要服务行业：3C 电子（芯片/连接器/FPC）、汽车零部件（轴承/齿轮/密封件）、
        食品包装（标签/日期/瓶盖）、半导体（晶圆/引线框架）、PCB/SMT（焊点/元件）。
        平台通过节点式图形化编程（拖拽算子 + 连线）让工程师无需编码即可构建检测工作流。

        ## 常见工作流设计模式

        ### 模式 1：传统缺陷检测
        适用场景：产品表面划痕/污点/缺损/多余物检测
        典型流程：ImageAcquisition → Filtering(降噪) → Thresholding(分割) → BlobAnalysis(缺陷提取) → ResultJudgment(OK/NG) → ResultOutput
        关键点：Blob的MinArea/MaxArea需要根据缺陷大小设定；如果背景不均匀优先用AdaptiveThreshold

        ### 模式 2：AI 深度学习检测
        适用场景：复杂纹理表面、传统算法难以分割的缺陷
        典型流程：ImageAcquisition → ImageResize(适配模型输入尺寸) → DeepLearning → ConditionalBranch(缺陷数>0?) → ModbusCommunication(NG信号) / ResultOutput
        关键点：DeepLearning前一般需要ImageResize到模型要求的尺寸(如640×640)；不要在DeepLearning后面再接Thresholding

        ### 模式 3：尺寸测量
        适用场景：零件孔径/间距/角度/轮廓尺寸的精密测量
        典型流程：ImageAcquisition → Filtering → EdgeDetection → CircleMeasurement/LineMeasurement → Measurement(距离) → CoordinateTransform(像素→毫米) → ResultOutput
        关键点：测量类算子一般需要先做边缘检测预处理；最终要用CoordinateTransform将像素值转为物理尺寸(mm)

        ### 模式 4：条码/OCR识别
        适用场景：产品追溯、包装信息读取
        典型流程：ImageAcquisition → CodeRecognition/OcrRecognition → ConditionalBranch(内容匹配?) → DatabaseWrite/ModbusCommunication → ResultOutput

        ### 模式 5：分拣/分级
        适用场景：按检测结果发送不同信号
        典型流程：ImageAcquisition → (检测算子) → ConditionalBranch → ModbusCommunication(OK信号) / ModbusCommunication(NG信号) → ResultOutput
        关键点：ConditionalBranch的True/False两路分别连不同的通信算子

        ### 模式 6：多工位循环检测
        适用场景：同一产品多个位置检测、批量连续检测
        典型流程：CycleCounter → ImageAcquisition → (检测算子) → DatabaseWrite → ResultOutput

        ## 用户口语 → 算子映射速查

        | 用户表述 | 应选算子 |
        |---------|---------|
        | "拍照/取图/抓图" | ImageAcquisition |
        | "去噪/降噪/平滑/模糊" | Filtering 或 MedianBlur（椒盐噪声用MedianBlur） |
        | "二值化/黑白/分割前景" | Thresholding（均匀光照）或 AdaptiveThreshold（不均匀光照） |
        | "去毛刺/填孔/膨胀/腐蚀" | Morphology |
        | "找边/提轮廓/Canny" | EdgeDetection |
        | "找圆/圆孔/孔径" | CircleMeasurement |
        | "找线/直线/角度" | LineMeasurement |
        | "测距/量尺寸/长度" | Measurement |
        | "转毫米/物理坐标" | CoordinateTransform |
        | "AI检测/深度学习/YOLO" | DeepLearning |
        | "扫码/二维码/条码" | CodeRecognition |
        | "判断OK/NG/合格" | ResultJudgment 或 ConditionalBranch |
        | "发PLC/通信/发信号" | 看品牌：西门子→SiemensS7Communication，三菱→MitsubishiMcCommunication，欧姆龙→OmronFinsCommunication，通用→ModbusCommunication |
        | "存数据库/记录" | DatabaseWrite |
        | "缩放/放大/缩小" | ImageResize |
        | "裁剪/ROI" | ImageCrop 或 RoiManager |
        | "颜色/偏色/色差" | ColorDetection |
        | "标定/畸变校正" | CameraCalibration → Undistort |
        | "模板定位/找图" | TemplateMatching 或 ShapeMatching（需要旋转不变时用ShapeMatching） |
        | "对比度增强/直方图" | HistogramEqualization |

        ## 反面模式（必须避免的错误设计）

        1. **不要在 DeepLearning 后接 Thresholding**
           AI 模型已输出检测结果和置信度，再做二值化没有意义
        2. **测量算子不要直接连图像源**
           CircleMeasurement/LineMeasurement 需要先经过 EdgeDetection 预处理才能得到清晰边缘
        3. **需要物理尺寸的场景不要忘记 CoordinateTransform**
           如果用户提到"毫米""微米"等物理单位，最终必须经过坐标转换
        4. **通信算子不要串联使用**
           如果需要同时发 OK/NG 信号，应该从 ConditionalBranch 的 True/False 各连一个通信算子（并联），不要串联
        5. **缺陷检测流程不要遗漏 ResultOutput**
           每个完整工作流都应以 ResultOutput 结尾，用于汇总和展示结果
        6. **不要跳过预处理**
           工业图像通常有噪声，直接做边缘检测或Blob分析会产生大量误检，应先 Filtering
        """;

    private string GetPhase1OperatorExtensions() => """
        # Phase 1 Operator Extensions
        ## New workflow patterns
        1. Precision width measurement:
           ImageAcquisition -> Filtering -> CaliperTool -> WidthMeasurement -> UnitConvert -> ResultJudgment -> ResultOutput
        2. AI post-processing:
           ImageAcquisition -> DeepLearning -> BoxNms -> BoxFilter -> ResultJudgment -> ResultOutput
        3. Image quality gate:
           ImageAcquisition -> SharpnessEvaluation -> ConditionalBranch -> (continue or reject)
        4. Position-first inspection:
           ImageAcquisition -> ShapeMatching -> PositionCorrection -> follow-up inspection
        5. Calibration-assisted metrology:
           CalibrationLoader or NPointCalibration -> measurement operators -> UnitConvert -> ResultOutput
        ## Phrase mapping additions
        - "measure width/thickness/gap" => WidthMeasurement
        - "caliper/find edge pair" => CaliperTool
        - "point to line distance" => PointLineDistance
        - "line to line distance/parallelism" => LineLineDistance
        - "remove duplicate boxes / NMS" => BoxNms
        - "filter detections by class/area/score" => BoxFilter
        - "is image sharp / focus check / blur" => SharpnessEvaluation
        - "correct ROI position / offset compensation" => PositionCorrection
        - "N-point calibration / affine calibration" => NPointCalibration
        - "load calibration file" => CalibrationLoader
        - "pixel to mm / unit conversion" => UnitConvert
        - "cycle time / elapsed statistics" => TimerStatistics
        """;

    private string GetPhase2OperatorExtensions() => """
        # Phase 2 Operator Extensions
        ## New workflow patterns
        12. Robot vision guidance:
            ImageAcquisition -> ShapeMatching -> PointAlignment/PointCorrection -> UnitConvert -> PlcCommunication -> ResultOutput
        13. Annular part defect inspection:
            ImageAcquisition -> CircleMeasurement(center) -> PolarUnwrap -> ShadingCorrection -> SurfaceDefectDetection -> ResultOutput
        14. Traditional surface defect detection (non-AI):
            ImageAcquisition -> ShadingCorrection -> SurfaceDefectDetection -> ResultJudgment -> ResultOutput
        ## Phrase mapping additions
        - "script / custom code / formula" => ScriptOperator
        - "trigger / start / timer trigger" => TriggerModule
        - "alignment / reference point offset" => PointAlignment
        - "correction / compensation / send to robot" => PointCorrection
        - "gap / pitch / lead spacing" => GapMeasurement
        - "unwrap ring / bottle cap / bearing ring" => PolarUnwrap
        - "uneven illumination / shading / flat field" => ShadingCorrection
        - "multi-frame average / temporal denoise" => FrameAveraging
        - "affine transform / rotate scale translate" => AffineTransform
        - "color measurement / deltaE / Lab" => ColorMeasurement
        - "surface defect / scratch / stain (traditional)" => SurfaceDefectDetection
        - "edge-pair defect / notch / bump" => EdgePairDefect
        - "rectangle / box / quadrilateral detection" => RectangleDetection
        - "translation-rotation calibration / hand-eye calibration" => TranslationRotationCalibration
        """;

    private string GetPhase3OperatorExtensions() => """
        # Phase 3 Operator Extensions
        ## New workflow patterns
        15. Large-area tiled inspection:
            ImageAcquisition -> ImageTiling -> ForEach(per-tile inspection) -> ResultJudgment -> ResultOutput
        16. Multi-view stitched inspection:
            ImageAcquisition(Image1) + ImageAcquisition(Image2) -> ImageStitching -> inspection -> ResultOutput
        17. Precision geometry chain:
            ImageAcquisition -> positioning -> GeoMeasurement(point/line/circle) -> UnitConvert -> ResultOutput
        ## Phrase mapping additions
        - "corner / vertex / corner point" => CornerDetection
        - "intersection / line crossing" => EdgeIntersection
        - "parallel lines / dual edge rails" => ParallelLineFind
        - "quadrilateral / polygon four-edge" => QuadrilateralFind
        - "geometry measurement / line-circle / circle-circle" => GeoMeasurement
        - "stitch / panorama / large image merge" => ImageStitching
        - "tiling / split grid / image blocks" => ImageTiling
        - "normalize image / standardize brightness" => ImageNormalize
        - "compose images / concat / channel merge" => ImageCompose
        - "pad border / expand image border" => CopyMakeBorder
        - "save text / export csv / save json log" => TextSave
        - "point set sort/filter/merge" => PointSetTool
        - "blob labeling / classify connected components" => BlobLabeling
        - "histogram / gray distribution" => HistogramAnalysis
        - "pixel statistics / roi mean brightness" => PixelStatistics
        """;

    private string GetRoleDefinition() => """
        # 角色定义

        你是 ClearVision 工业视觉检测平台的工作流生成专家。
        你的任务是：根据用户的自然语言描述，从算子库中选择合适的算子，
        确定它们的连接关系和参数配置，生成一个完整的视觉检测工作流。

        ## 工作流基本规则
        1. 每个工作流必须从"图像源"类算子开始（ImageAcquisition）
        2. 每个工作流必须以"结果输出"类算子结束（ResultOutput）
        3. 只使用下方算子目录中列出的算子，不能创造不存在的算子
        4. 连线时必须遵守端口类型兼容性规则
        5. 优先使用最简洁的算子组合完成任务
        """;

    private string GetParameterInferenceGuide() => """
        # 参数推理指南

        1. 数值提取规则
        - 用户明确给出的数值（如 "阈值100"、"间距0.5mm"）应优先映射到最相关参数。
        - 如果用户同时给出目标值与容差（如 "0.5mm，偏差±0.05mm"），应分别映射到目标值参数和容差参数。
        - 若同类参数有多个候选，优先选择名称语义最接近者，并在 parametersNeedingReview 中标记歧义项。

        2. 单位与换算
        - 当用户使用 mm/μm 等物理单位，而算子参数是像素时，不可臆造换算比例。
        - 应提示用户提供标定信息（如 PixelSize、CalibrationFile、CoordinateTransform 配置）。
        - 未具备标定前，可先保留物理值语义并在 parametersNeedingReview 中标记需要确认。

        3. 行业常见默认值参考（仅作兜底）
        - 3C/电子外观检测：Thresholding.UseOtsu=true，BlobAnalysis.MinArea 可从 20~100 起步。
        - 包装/OCR/条码：CodeRecognition.MaxResults=1，适当先做去噪与对比度增强。
        - 精密测量：优先补充标定算子（CoordinateTransform/NPointCalibration），避免直接输出物理尺寸结论。
        - AI检测：优先检查 ModelPath、InputSize、Confidence 等关键参数是否待人工确认。
        """;

    private string GetOperatorCatalog(string? userDescription)
    {
        // 从 OperatorFactory 获取所有已注册算子的元数据，动态生成算子目录
        // 当用户描述明确时，只注入高相关算子详情，并保留全量索引作为 fallback
        var allMetadata = _operatorFactory
            .GetAllMetadata()
            .OrderBy(m => m.Type.ToString())
            .ToList();

        var relevantMetadata = string.IsNullOrWhiteSpace(userDescription)
            ? allMetadata
            : GetRelevantOperators(userDescription)
                .OrderBy(m => m.Type.ToString())
                .ToList();

        var sb = new StringBuilder();

        if (string.IsNullOrWhiteSpace(userDescription))
        {
            sb.AppendLine("# 算子目录（你只能使用以下算子）");
        }
        else
        {
            sb.AppendLine("# 优先算子目录（根据当前需求动态裁剪）");
            sb.AppendLine("以下为当前需求高相关算子。");
            sb.AppendLine("如果需要的算子不在列表中，仍可使用其他已注册算子。");
        }

        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(SerializeOperatorCatalog(relevantMetadata, includeFullDetails: true));
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(userDescription))
        {
            sb.AppendLine();
            sb.AppendLine("# 全量算子目录（fallback 索引）");
            sb.AppendLine("当优先目录不满足需求时，可从以下全量索引选择其他已注册算子。");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(SerializeOperatorCatalog(allMetadata, includeFullDetails: false));
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private List<OperatorMetadata> GetRelevantOperators(string userDescription)
    {
        var allMetadata = _operatorFactory.GetAllMetadata().ToList();
        if (allMetadata.Count == 0 || string.IsNullOrWhiteSpace(userDescription))
            return allMetadata;

        var keywords = ExtractKeywords(userDescription);
        var matched = allMetadata
            .Where(metadata => IsRelevantByKeywords(metadata, keywords))
            .ToList();

        if (matched.Count < 8)
        {
            var categoryHints = keywords
                .Select(GetCategoryHint)
                .Where(hint => !string.IsNullOrWhiteSpace(hint))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (categoryHints.Count > 0)
            {
                matched.AddRange(allMetadata.Where(metadata =>
                    categoryHints.Any(hint => ContainsIgnoreCase(metadata.Category, hint!))));
            }
        }

        matched.AddRange(GetCoreOperators(allMetadata));

        var distinct = matched
            .GroupBy(metadata => metadata.Type)
            .Select(group => group.First())
            .ToList();

        return distinct.Count > 0 ? distinct : allMetadata;
    }

    private static HashSet<string> ExtractKeywords(string description)
    {
        var normalized = description.ToLowerInvariant();
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(normalized, @"[\p{L}\p{Nd}_]+"))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2)
                keywords.Add(token);
        }

        AddIntentTokensIfMatched(keywords, normalized, ["测量", "尺寸", "宽度", "间距", "孔径", "公差", "精度", "mm", "um", "μm"], ["measurement", "gap", "distance", "width", "caliper"]);
        AddIntentTokensIfMatched(keywords, normalized, ["缺陷", "划痕", "污点", "瑕疵", "检测", "NG"], ["defect", "blob", "threshold"]);
        AddIntentTokensIfMatched(keywords, normalized, ["plc", "通信", "modbus", "s7", "三菱", "欧姆龙", "串口", "tcp"], ["communication", "modbus", "siemens", "mitsubishi", "omron"]);
        AddIntentTokensIfMatched(keywords, normalized, ["ocr", "条码", "二维码", "扫码", "识别", "文字"], ["ocr", "code", "barcode", "recognition"]);
        AddIntentTokensIfMatched(keywords, normalized, ["ai", "深度学习", "yolo", "模型", "推理"], ["ai", "deeplearning", "inference"]);
        AddIntentTokensIfMatched(keywords, normalized, ["标定", "校准", "畸变", "坐标变换"], ["calibration", "undistort", "coordinate"]);

        return keywords;
    }

    private static void AddIntentTokensIfMatched(
        HashSet<string> keywords,
        string normalizedDescription,
        IEnumerable<string> triggers,
        IEnumerable<string> tokensToAdd)
    {
        if (!triggers.Any(trigger => normalizedDescription.Contains(trigger, StringComparison.OrdinalIgnoreCase)))
            return;

        foreach (var token in tokensToAdd)
            keywords.Add(token);
    }

    private static bool IsRelevantByKeywords(OperatorMetadata metadata, HashSet<string> keywords)
    {
        if (keywords.Count == 0)
            return false;

        if (keywords.Any(keyword =>
                ContainsIgnoreCase(metadata.DisplayName, keyword) ||
                ContainsIgnoreCase(metadata.Description, keyword) ||
                ContainsIgnoreCase(metadata.Category, keyword)))
        {
            return true;
        }

        return metadata.Keywords != null &&
               metadata.Keywords.Any(operatorKeyword =>
                   keywords.Any(keyword => ContainsIgnoreCase(operatorKeyword, keyword)));
    }

    private static bool ContainsIgnoreCase(string? source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
            return false;

        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetCategoryHint(string keyword)
    {
        if (keyword.Contains("measure", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("测量", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("尺寸", StringComparison.OrdinalIgnoreCase))
        {
            return "测量";
        }

        if (keyword.Contains("defect", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("缺陷", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("检测", StringComparison.OrdinalIgnoreCase))
        {
            return "检测";
        }

        if (keyword.Contains("communication", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("plc", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("通信", StringComparison.OrdinalIgnoreCase))
        {
            return "通信";
        }

        if (keyword.Contains("ocr", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("barcode", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("识别", StringComparison.OrdinalIgnoreCase))
        {
            return "识别";
        }

        if (keyword.Contains("calibration", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("标定", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("校准", StringComparison.OrdinalIgnoreCase))
        {
            return "标定";
        }

        return null;
    }

    private static List<OperatorMetadata> GetCoreOperators(IEnumerable<OperatorMetadata> allMetadata)
    {
        var coreTypes = new HashSet<OperatorType>
        {
            OperatorType.ImageAcquisition,
            OperatorType.ResultOutput,
            OperatorType.ResultJudgment,
            OperatorType.ConditionalBranch
        };

        return allMetadata
            .Where(metadata => coreTypes.Contains(metadata.Type))
            .ToList();
    }

    private static string SerializeOperatorCatalog(IEnumerable<OperatorMetadata> metadata, bool includeFullDetails)
    {
        if (!includeFullDetails)
        {
            var fallbackCatalog = metadata.Select(m => new
            {
                operator_id = m.Type.ToString(),
                name = m.DisplayName,
                category = m.Category
            });

            return JsonSerializer.Serialize(fallbackCatalog, _catalogJsonOptions);
        }

        var detailedCatalog = metadata.Select(m => new
        {
            operator_id = m.Type.ToString(),
            name = m.DisplayName,
            category = m.Category,
            description = m.Description,
            keywords = m.Keywords ?? Array.Empty<string>(),
            inputs = m.InputPorts.Select(p => new
            {
                port_name = p.Name,
                display_name = p.DisplayName,
                data_type = p.DataType.ToString(),
                required = p.IsRequired
            }),
            outputs = m.OutputPorts.Select(p => new
            {
                port_name = p.Name,
                display_name = p.DisplayName,
                data_type = p.DataType.ToString()
            }),
            parameters = m.Parameters.Select(p => new
            {
                param_name = p.Name,
                display_name = p.DisplayName,
                type = p.DataType,
                default_value = p.DefaultValue?.ToString(),
                required = p.IsRequired,
                description = p.Description ?? string.Empty,
                min_value = p.MinValue?.ToString(),
                max_value = p.MaxValue?.ToString(),
                options = p.Options?.Select(o => new { label = o.Label, value = o.Value })
            })
        });

        return JsonSerializer.Serialize(detailedCatalog, _catalogJsonOptions);
    }

    private string GetConnectionRules() => """
        # 端口类型兼容性规则

        ## 数据类型说明
        - Image：图像数据（绿色端口）
        - Integer / Float：数值类型（橙色端口），Integer 和 Float 可以互连
        - Boolean：布尔值（红色端口）
        - String：字符串（蓝色端口）
        - Point / Rectangle：几何类型（粉色端口），Point 和 Rectangle 可以互连
        - Contour：轮廓数据（紫色端口）
        - Any：任意类型（灰色端口），可以连接任何类型

        ## 连线约束
        - 一个输入端口只能接收一条连线
        - 一个输出端口可以连接到多个输入端口（扇出）
        - 不允许环路（流程必须是有向无环图）
        - 不同类型的端口不可连接（除非其中一方是 Any）
        """;

    private string GetOutputFormatSpec() => """
        # 输出格式规范（严格遵守）

        你必须只输出一个合法的 JSON 对象，不包含任何 Markdown 代码块标记、
        解释性文字前缀或后缀。JSON 结构如下：

        ```
        {
          "explanation": "简要解释为什么选择这些算子和连接方式（50字以内）",
          "operators": [
            {
              "tempId": "op_1",
              "operatorType": "ImageAcquisition",
              "displayName": "图像采集",
              "parameters": {
                "sourceType": "camera",
                "triggerMode": "Software"
              }
            }
          ],
          "connections": [
            {
              "sourceTempId": "op_1",
              "sourcePortName": "Image",
              "targetTempId": "op_2",
              "targetPortName": "Image"
            }
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "Confidence"]
          }
        }
        ```

        ## 关于参数配置 (parameters)
        - 根据用户的描述推断合理的参数值。如果描述中包含数量、尺寸、大小等信息，应转换为相应的数值
        - 数值类型参数的 value 必须在对应算子信息的 `min_value` 到 `max_value` 之间
        - 枚举(enum)类型参数的 value 必须是对应算子信息的 `options` 数组中列出的 `value` 之一
        - 未在 user 提示中明确或无法推断的参数，如果它有 `default_value`，可以省略或填入默认值

        ## 关于 parametersNeedingReview
        列出你无法从用户描述中确定明确值、必须由用户提供真实环境数据的参数。
        例如：文件路径、IP 地址、模型文件路径、特定尺寸阈值等。

        ## 关于 tempId
        格式固定为 op_1, op_2, op_3...，按算子在流程中的执行顺序递增。

        ## 关于 operatorType
        必须与算子目录中的 operator_id 字段完全一致（大小写敏感）。

        ## 关于端口名
        必须与算子目录中的 port_name 字段完全一致（大小写敏感）。
        """;

    private string GetFewShotExamples() => """
        # 示例（学习这些示例的格式和思路）

        ## 示例 1
        用户描述："检测产品表面缺陷，用相机拍照后分析"

        正确输出：
        {
          "explanation": "相机采集图像，预处理降噪，二值化分离缺陷，Blob分析统计缺陷数量，最终输出结果",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "Filtering", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷Blob分析", "parameters": {"MinArea": "50", "MaxArea": "5000"}},
            {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinArea", "MaxArea"]
          }
        }

        ## 示例 2
        用户描述："扫描产品二维码，通过Modbus发给PLC"

        正确输出：
        {
          "explanation": "相机采集，条码识别提取文本，Modbus TCP协议发送给PLC",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "CodeRecognition", "displayName": "二维码识别", "parameters": {"CodeType": "QR", "MaxResults": "1"}},
            {"tempId": "op_3", "operatorType": "ModbusCommunication", "displayName": "Modbus发送", "parameters": {"Protocol": "TCP", "Port": "502", "FunctionCode": "WriteMultiple"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_3", "targetPortName": "Data"}
          ],
          "parametersNeedingReview": {
            "op_3": ["IpAddress"]
          }
        }

        ## 示例 3
        用户描述："用AI模型检测产品缺陷，有缺陷发NG信号，没缺陷发OK信号给PLC"

        正确输出：
        {
          "explanation": "相机采集后缩放至AI输入尺寸，深度学习推理检测缺陷，条件分支判断缺陷数量，分别通过Modbus发送NG/OK信号",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
            {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "AI缺陷检测", "parameters": {"Confidence": "0.5", "UseGpu": "true"}},
            {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "缺陷判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
            {"tempId": "op_5", "operatorType": "ModbusCommunication", "displayName": "发送NG", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "1"}},
            {"tempId": "op_6", "operatorType": "ModbusCommunication", "displayName": "发送OK", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "0"}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
            {"sourceTempId": "op_4", "sourcePortName": "True", "targetTempId": "op_5", "targetPortName": "Data"},
            {"sourceTempId": "op_4", "sourcePortName": "False", "targetTempId": "op_6", "targetPortName": "Data"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_7", "targetPortName": "Image"}
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "InputSize"],
            "op_5": ["IpAddress", "Port"],
            "op_6": ["IpAddress", "Port"]
          }
        }

        ## 示例 4
        用户描述："测量零件上两孔之间的距离，结果转换为毫米并输出"

        正确输出：
        {
          "explanation": "相机采集图像，高斯滤波降噪，Canny边缘检测，检测两个圆形孔，测量距离，坐标转换为物理尺寸，输出结果",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "exposureTime": "10000"}},
            {"tempId": "op_2", "operatorType": "GaussianBlur", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "EdgeDetection", "displayName": "边缘检测", "parameters": {"Threshold1": "50", "Threshold2": "150", "ApertureSize": "3"}},
            {"tempId": "op_4", "operatorType": "CircleMeasurement", "displayName": "圆孔检测", "parameters": {"MinRadius": "10", "MaxRadius": "100", "Method": "HoughCircle"}},
            {"tempId": "op_5", "operatorType": "Measurement", "displayName": "距离测量", "parameters": {"MeasureType": "PointToPoint"}},
            {"tempId": "op_6", "operatorType": "CoordinateTransform", "displayName": "坐标转换", "parameters": {"PixelSize": "0.02"}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "测量结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "Center", "targetTempId": "op_5", "targetPortName": "Image"},
            {"sourceTempId": "op_5", "sourcePortName": "Distance", "targetTempId": "op_6", "targetPortName": "PixelX"},
            {"sourceTempId": "op_6", "sourcePortName": "PhysicalX", "targetTempId": "op_7", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinRadius", "MaxRadius"],
            "op_6": ["PixelSize", "CalibrationFile"]
          }
        }

        ## 示例 5
        用户描述："对产品连续拍照10次，记录每次的检测结果到数据库"

        正确输出：
        {
          "explanation": "循环计数器控制10次拍照，每次采集后二值化分析，结果写入数据库，计数递增",
          "operators": [
            {"tempId": "op_1", "operatorType": "CycleCounter", "displayName": "循环计数", "parameters": {"CycleLimit": "10", "AutoReset": "true"}},
            {"tempId": "op_2", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
            {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "100"}},
            {"tempId": "op_5", "operatorType": "ResultJudgment", "displayName": "OK/NG判定", "parameters": {"FieldName": "BlobCount", "Condition": "LessThanOrEqual", "ThresholdValue": "0"}},
            {"tempId": "op_6", "operatorType": "DatabaseWrite", "displayName": "记录到数据库", "parameters": {"DbType": "SQLite", "TableName": "InspectionResults"}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "输出结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "CycleCount", "targetTempId": "op_7", "targetPortName": "Result"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Value"},
            {"sourceTempId": "op_5", "sourcePortName": "IsOk", "targetTempId": "op_6", "targetPortName": "Data"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinArea", "MaxArea"],
            "op_6": ["ConnectionString", "TableName"]
          }
        }
        """;
}
