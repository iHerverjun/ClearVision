C:\Users\11234\Desktop\ClearVision\tools\sft\clearvision_sft_data.jsonl
此文件是我打算用来微调Qwen3.5 9B的训练素材，是我让AI辅助生成的。现在已经有1200条了，我希望再增加1200条，但是content的风格要改一下，我给你几个场景和示例，你以这个为标准去仿写，同时，我会提供给你我现在调用GLM5的满血API返回的推理结果，你可以从推理结果中截取有用信息来辅助语料库的完善。

示例1：总装分厂需要一个遥控器漏装检测，即在空调内机封箱前通过华睿相机获取流水线上三菱PLC发过来的触发信号拍照，执行yolo检测判断箱内有无遥控器，如OK给PLC发送合格信号，如不OK给PLC发送不合格信号。请帮我从算子库里挑选合适算子构建流程。
GLM5逻辑推理内容：用户需要构建一个空调内机遥控器漏装检测的工作流，具体需求：

1. 通过华睿相机拍照（触发信号来自三菱PLC）
2. 执行YOLO检测判断箱内有无遥控器
3. OK给PLC发送合格信号
4. 不OK给PLC发送不合格信号

这是一个典型的AI检测+分拣/分级场景（模式2+模式5的组合）

流程分析：
1. ImageAcquisition - 华睿相机拍照
2. ImageResize - 缩放到YOLO模型输入尺寸（通常是640x640）
3. DeepLearning - YOLO检测遥控器
4. ConditionalBranch - 根据检测结果判断OK/NG
5. MitsubishiMcCommunication - 发送OK信号（True分支）
6. MitsubishiMcCommunication - 发送NG信号（False分支）
7. ResultOutput - 结果输出

关键点：
- 用户明确提到"三菱PLC"，所以应该用MitsubishiMcCommunication而不是ModbusCommunication
- 需要YOLO检测，所以用DeepLearning算子
- 根据AI检测模式，DeepLearning前一般需要ImageResize

让我检查算子库中是否有MitsubishiMcCommunication：
在全量算子目录中找到了：
```json
{
  "operator_id": "MitsubishiMcCommunication",
  "name": "三菱MC通信",
  "category": "通信"
}
```

但是这个算子的详细参数不在优先目录中。我需要从全量目录获取信息，但由于没有详细参数，我会使用合理的默认参数配置。

让我构建工作流：

1. op_1: ImageAcquisition - 相机采集（华睿相机，触发模式为硬件触发）
2. op_2: ImageResize - 图像缩放（适配YOLO输入尺寸640x640）
3. op_3: DeepLearning - YOLO检测（检测遥控器）
4. op_4: ConditionalBranch - 条件分支（判断是否有遥控器）
5. op_5: MitsubishiMcCommunication - 发送OK信号
6. op_6: MitsubishiMcCommunication - 发送NG信号
7. op_7: ResultOutput - 结果输出

连接：
- op_1.Image -> op_2.Image
- op_2.Image -> op_3.Image
- op_3.DefectCount -> op_4.Value（判断是否检测到遥控器）
- op_4.True -> op_5.Data（有遥控器，发送OK）
- op_4.False -> op_6.Data（无遥控器，发送NG）
- op_3.Image -> op_7.Image

等等，我需要重新理解需求。用户说的是"判断箱内有无遥控器"：
- 有遥控器 = OK（合格）
- 无遥控器 = NG（不合格）

所以：
- 检测到遥控器（DefectCount > 0 或检测数量 > 0）= OK
- 没检测到遥控器 = NG

但是DeepLearning输出的DefectCount是"缺陷数量"，这里遥控器不是缺陷，而是目标物体。

让我重新看DeepLearning的输出：
- Defects: 缺陷列表
- DefectCount: 缺陷数量

在这个场景中，遥控器是目标物体，不是缺陷。但YOLO模型可以检测任何目标，所以：
- 如果检测到遥控器（DefectCount > 0），说明OK
- 如果没检测到遥控器（DefectCount = 0），说明NG

ConditionalBranch的配置：
- Condition: GreaterThan
- CompareValue: "0"（检测数量大于0表示有遥控器）
- FieldName: "DefectCount"

True分支 -> 发送OK信号
False分支 -> 发送NG信号

参数需要确认的：
- DeepLearning的ModelPath（YOLO模型路径）
- MitsubishiMcCommunication的IP地址、端口等通信参数

让我检查MitsubishiMcCommunication的详细参数...由于优先目录中没有这个算子的详细信息，我需要根据常见的三菱MC通信参数进行配置。三菱MC协议通常需要：
- IP地址
- 端口（默认5007或5000）
- 网络号
- PC号
- 起始软元件
- 软元件点数

但由于没有详细参数信息，我会设置一些基本参数并在parametersNeedingReview中标记需要确认的参数。

让我重新构建JSON：

```json
{
  "explanation": "华睿相机采集图像，缩放至YOLO输入尺寸，AI检测遥控器，条件分支判断有无遥控器，分别通过三菱MC协议发送OK/NG信号给PLC",
  "operators": [
    {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "华睿相机采集", "parameters": {"sourceType": "camera"}},
    {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
    {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "YOLO遥控器检测", "parameters": {"Confidence": "0.5", "InputSize": "640"}},
    {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "遥控器判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
    {"tempId": "op_5", "operatorType": "MitsubishiMcCommunication", "displayName": "发送OK信号", "parameters": {}},
    {"tempId": "op_6", "operatorType": "MitsubishiMcCommunication", "displayName": "发送NG信号", "parameters": {}},
    {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果输出", "parameters": {}}
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
    "op_1": ["cameraId"],
    "op_3": ["ModelPath", "TargetClasses"],
    "op_5": ["IpAddress", "Port", "NetworkNo", "PcNo", "StartDevice", "DeviceCount", "WriteValue"],
    "op_6": ["IpAddress", "Port", "NetworkNo", "PcNo", "StartDevice", "DeviceCount", "WriteValue"]
  }
}
```

等等，我需要再检查一下ImageResize算子是否在算子库中。让我查看优先目录...

ImageResize不在优先目录中，但在全量目录中：
```json
{
  "operator_id": "ImageResize",
  "name": "图像缩放",
  "category": "预处理"
}
```

根据规则，我可以在优先目录不满足时使用全量目录中的算子。

但是我发现优先目录中的DeepLearning算子已经有InputSize参数，可能不需要单独的ImageResize。让我再看看...

DeepLearning的参数中有：
- InputSize: 输入尺寸，默认640

这意味着DeepLearning可能会自动处理输入尺寸。但按照Phase 1的设计模式，AI检测流程中明确提到：
"DeepLearning前一般需要ImageResize到模型要求的尺寸(如640×640)"

所以我还是应该保留ImageResize。

让我重新检查ImageResize的端口信息...由于没有详细参数，我会假设它有Image输入和Image输出端口。

现在让我优化工作流：

1. 用户提到"华睿相机获取流水线上三菱PLC发过来的触发信号拍照"，这意味着相机是硬件触发模式，所以ImageAcquisition应该设置触发模式。

让我检查ImageAcquisition的详细参数：
```json
{
  "param_name": "sourceType",
  "display_name": "采集源",
  "type": "enum",
  "default_value": "file",
  "options": [
    {"label": "文件", "value": "file"},
    {"label": "相机", "value": "camera"}
  ]
},
{
  "param_name": "filePath",
  "display_name": "文件路径",
  "type": "file"
},
{
  "param_name": "cameraId",
  "display_name": "相机",
  "type": "cameraBinding"
}
```

没有triggerMode参数，所以我不需要设置触发模式。只需要设置sourceType为camera。

现在让我最终确认工作流：

```json
{
  "explanation": "华睿相机采集图像，缩放至YOLO输入尺寸，AI检测遥控器，条件分支判断有无遥控器，分别通过三菱MC协议发送OK/NG信号给PLC",
  "operators": [
    {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "华睿相机采集", "parameters": {"sourceType": "camera"}},
    {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
    {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "YOLO遥控器检测", "parameters": {"Confidence": "0.5", "InputSize": "640"}},
    {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "遥控器判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
    {"tempId": "op_5", "operatorType": "MitsubishiMcCommunication", "displayName": "发送OK信号", "parameters": {}},
    {"tempId": "op_6", "operatorType": "MitsubishiMcCommunication", "displayName": "发送NG信号", "parameters": {}},
    {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果输出", "parameters": {}}
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
    "op_1": ["cameraId"],
    "op_3": ["ModelPath", "TargetClasses"],
    "op_5": ["IpAddress", "Port", "WriteValue"],
    "op_6": ["IpAddress", "Port", "WriteValue"]
  }
}
```

等等，我需要再仔细检查一下ConditionalBranch的参数：

```json
{
  "param_name": "Condition",
  "display_name": "条件",
  "type": "enum",
  "default_value": "GreaterThan",
  "options": [
    {"label": "大于", "value": "GreaterThan"},
    {"label": "小于", "value": "LessThan"},
    {"label": "等于", "value": "Equal"},
    {"label": "不等于", "value": "NotEqual"},
    {"label": "包含", "value": "Contains"}
  ]
},
{
  "param_name": "CompareValue",
  "display_name": "比较值",
  "type": "string",
  "default_value": "0"
},
{
  "param_name": "FieldName",
  "display_name": "字段名",
  "type": "string",
  "default_value": ""
}
```

好的，我的配置是正确的：
- FieldName: "DefectCount" - 从上游数据中读取检测数量
- Condition: "GreaterThan" - 大于
- CompareValue: "0" - 与0比较

当DefectCount > 0时（检测到遥控器），走True分支发送OK信号。
当DefectCount = 0时（没检测到遥控器），走False分支发送NG信号。

这符合用户的需求。

最后，我需要检查ResultOutput的连接。根据反面模式第5条："缺陷检测流程不要遗漏ResultOutput"，每个完整工作流都应以ResultOutput结束。

我已经连接了op_3.Image -> op_7.Image，但没有连接Result。让我添加：

实际上，ResultOutput可以接收多个输入：
- Image: 图像
- Result: 结果
- Text: 文本
- Data: 数据

我可以将DeepLearning的结果连接到ResultOutput：
- op_3.DefectCount -> op_7.Result（或op_7.Data）

或者将ConditionalBranch的结果连接：
- op_4.True/False -> op_7.Result

但ConditionalBranch的输出是Any类型，而且它已经有两条分支连接到通信算子了。如果再连接到ResultOutput，需要考虑流程逻辑。

实际上，按照示例3的模式，ResultOutput主要用于汇总和展示结果，可以接收图像和检测结果。让我保持当前的连接方式，只连接图像。

最终工作流：

```json
{
  "explanation": "华睿相机采集图像，缩放至YOLO输入尺寸，AI检测遥控器，条件分支判断有无遥控器，分别通过三菱MC协议发送OK/NG信号给PLC",
  "operators": [
    {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "华睿相机采集", "parameters": {"sourceType": "camera"}},
    {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
    {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "YOLO遥控器检测", "parameters": {"Confidence": "0.5", "InputSize": "640"}},
    {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "遥控器判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
    {"tempId": "op_5", "operatorType": "MitsubishiMcCommunication", "displayName": "发送OK信号", "parameters": {}},
    {"tempId": "op_6", "operatorType": "MitsubishiMcCommunication", "displayName": "发送NG信号", "parameters": {}},
    {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果输出", "parameters": {}}
  ],
  "connections": [
    {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
    {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
    {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
    {"sourceTempId": "op_4", "sourcePortName": "True", "targetTempId": "op_5", "targetPortName": "Data"},
    {"sourceTempId": "op_4", "sourcePortName": "False", "targetTempId": "op_6", "targetPortName": "Data"},
    {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_7", "targetPortName": "Image"},
    {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_7", "targetPortName": "Result"}
  ],
  "parametersNeedingReview": {
    "op_1": ["cameraId"],
    "op_3": ["ModelPath", "TargetClasses"],
    "op_5": ["IpAddress", "Port", "WriteValue"],
    "op_6": ["IpAddress", "Port", "WriteValue"]
  }
}
```

我添加了op_3.DefectCount -> op_7.Result的连接，这样ResultOutput可以记录检测到的遥控器数量。

现在让我再检查一下是否有遗漏：

1. ✅ ImageAcquisition开始
2. ✅ ResultOutput结束
3. ✅ 使用了算子库中的算子
4. ✅ 端口类型兼容（Image->Image, Integer->Any, Any->Any）
5. ✅ 没有在DeepLearning后接Thresholding
6. ✅ 通信算子并联使用（从ConditionalBranch的True/False各连一个）
7. ✅ 包含ResultOutput

看起来工作流是完整的。让我最后确认一下参数：

ImageAcquisition:
- sourceType: "camera" ✅

ImageResize:
- Width: "640" ✅
- Height: "640" ✅

DeepLearning:
- Confidence: "0.5" ✅ (在默认值范围内)
- InputSize: "640" ✅ (在320-1280范围内)
- ModelPath: 需要用户确认 ✅
- TargetClasses: 需要用户确认（遥控器类别名称）✅

ConditionalBranch:
- FieldName: "DefectCount" ✅
- Condition: "GreaterThan" ✅
- CompareValue: "0" ✅

MitsubishiMcCommunication:
- 需要用户确认IP地址、端口、写入值等参数 ✅

ResultOutput:
- 使用默认参数 ✅

工作流看起来完整且正确。

GLM5设计思路内容：{
  "explanation": "华睿相机采集图像，缩放至YOLO输入尺寸，AI检测遥控器，条件分支判断有无遥控器，分别通过三菱MC协议发送OK/NG信号给PLC",
  "operators": [
    {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "华睿相机采集", "parameters": {"sourceType": "camera"}},
    {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
    {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "YOLO遥控器检测", "parameters": {"Confidence": "0.5", "InputSize": "640"}},
    {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "遥控器判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
    {"tempId": "op_5", "operatorType": "MitsubishiMcCommunication", "displayName": "发送OK信号", "parameters": {}},
    {"tempId": "op_6", "operatorType": "MitsubishiMcCommunication", "displayName": "发送NG信号", "parameters": {}},
    {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果输出", "parameters": {}}
  ],
  "connections": [
    {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
    {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
    {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
    {"sourceTempId": "op_4", "sourcePortName": "True", "targetTempId": "op_5", "targetPortName": "Data"},
    {"sourceTempId": "op_4", "sourcePortName": "False", "targetTempId": "op_6", "targetPortName": "Data"},
    {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_7", "targetPortName": "Image"},
    {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_7", "targetPortName": "Result"}
  ],
  "parametersNeedingReview": {
    "op_1": ["cameraId"],
    "op_3": ["ModelPath", "TargetClasses"],
    "op_5": ["IpAddress", "Port", "WriteValue"],
    "op_6": ["IpAddress", "Port", "WriteValue"]
  }
}

示例2：