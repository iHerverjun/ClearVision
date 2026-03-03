#!/usr/bin/env python3
"""
ClearVision SFT 训练数据生成器
================================
将种子样本通过口语化变异 + 参数随机化，批量生成适用于
Qwen3.5 0.8B LoRA 微调的 JSONL 训练数据。

用法:
    python generate_training_data.py                    # 默认生成 300 条
    python generate_training_data.py --count 500        # 指定数量
    python generate_training_data.py --output my.jsonl  # 指定输出文件

输出: clearvision_sft_data.jsonl（兼容 LLaMA-Factory / Unsloth）
作者: 蘅芜君
"""

import json
import random
import copy
import argparse
from pathlib import Path
from typing import Any

# ============================================================
# 1. 精简版 System Prompt（微调后模型使用）
#    原版 PromptBuilder 的 36KB Prompt 经微调后，
#    大部分领域知识已内化到权重中，只需保留动态部分。
# ============================================================

SYSTEM_PROMPT_FOR_SFT = """你是 ClearVision 工业视觉检测平台的工作流生成专家。
根据用户描述，从算子库选择合适算子，确定连接关系和参数，生成检测工作流 JSON。

规则：
1. 从 ImageAcquisition 开始，以 ResultOutput 结束
2. 只使用已注册算子，不可创造
3. 连线遵守端口类型兼容性
4. 输出纯 JSON，不含 Markdown 包装

输出格式：
{
  "explanation": "简要说明",
  "operators": [{"tempId":"op_1","operatorType":"...","displayName":"...","parameters":{}}],
  "connections": [{"sourceTempId":"op_1","sourcePortName":"...","targetTempId":"op_2","targetPortName":"..."}],
  "parametersNeedingReview": {"op_N": ["ParamName"]}
}"""


# ============================================================
# 2. 种子数据库 —— 覆盖 6 大设计模式
#    每个种子包含: 用户描述模板列表 + 标准答案 JSON
# ============================================================

SEED_DATABASE: list[dict[str, Any]] = [
    # ---- 模式 1: 传统缺陷检测 ----
    {
        "pattern": "traditional_defect",
        "user_templates": [
            "检测产品表面缺陷，用相机拍照后分析",
            "我要做表面检测，找划痕和污点",
            "帮我建一个缺陷检测流程，用Blob分析",
            "产品外观检查，看有没有瑕疵",
            "做一个简单的缺陷检测工作流",
        ],
        "answer": {
            "explanation": "相机采集图像，滤波降噪，二值化分离缺陷区域，Blob分析统计缺陷，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "Filtering", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
                {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
                {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷Blob分析", "parameters": {"MinArea": "50", "MaxArea": "5000"}},
                {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {"op_4": ["MinArea", "MaxArea"]},
        },
    },
    # ---- 模式 1 变体: 带形态学的缺陷检测 ----
    {
        "pattern": "traditional_defect_morph",
        "user_templates": [
            "检测表面缺陷，需要去毛刺处理",
            "产品表面有划痕，要用膨胀腐蚀去噪后检测",
            "做缺陷检测，二值化后需要形态学处理再分析",
        ],
        "answer": {
            "explanation": "采集图像，滤波降噪，二值化分割，形态学去毛刺，Blob分析缺陷，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "Filtering", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
                {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
                {"tempId": "op_4", "operatorType": "Morphology", "displayName": "形态学去噪", "parameters": {"Operation": "Opening", "KernelSize": "3"}},
                {"tempId": "op_5", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "100", "MaxArea": "10000"}},
                {"tempId": "op_6", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "Image", "targetTempId": "op_5", "targetPortName": "Image"},
                {"sourceTempId": "op_5", "sourcePortName": "BlobCount", "targetTempId": "op_6", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {"op_5": ["MinArea", "MaxArea"]},
        },
    },
    # ---- 模式 2: AI 深度学习检测 ----
    {
        "pattern": "ai_detection",
        "user_templates": [
            "用AI模型检测缺陷",
            "用深度学习做缺陷检测",
            "用YOLO模型来检测产品质量",
            "用训练好的AI模型推理检测",
            "我有个YOLO模型，帮我搭个AI检测流程",
        ],
        "answer": {
            "explanation": "相机采集后缩放至AI输入尺寸，深度学习模型推理检测缺陷，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
                {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "AI缺陷检测", "parameters": {"Confidence": "0.5", "UseGpu": "true"}},
                {"tempId": "op_4", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            ],
            "parametersNeedingReview": {"op_3": ["ModelPath", "InputSize"]},
        },
    },
    # ---- 模式 2 变体: AI检测 + 分拣信号 ----
    {
        "pattern": "ai_detection_sorting",
        "user_templates": [
            "用AI模型检测产品缺陷，有缺陷发NG信号，没缺陷发OK信号给PLC",
            "深度学习检测后根据结果发PLC信号",
            "YOLO检测缺陷，通过Modbus通知PLC分拣",
            "AI检测后分拣，OK和NG分别发不同信号",
        ],
        "answer": {
            "explanation": "相机采集后缩放至AI输入尺寸，深度学习推理检测缺陷，条件分支判断缺陷数量，分别通过Modbus发送NG/OK信号",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
                {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "AI缺陷检测", "parameters": {"Confidence": "0.5", "UseGpu": "true"}},
                {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "缺陷判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
                {"tempId": "op_5", "operatorType": "ModbusCommunication", "displayName": "发送NG", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "1"}},
                {"tempId": "op_6", "operatorType": "ModbusCommunication", "displayName": "发送OK", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "0"}},
                {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
                {"sourceTempId": "op_4", "sourcePortName": "True", "targetTempId": "op_5", "targetPortName": "Data"},
                {"sourceTempId": "op_4", "sourcePortName": "False", "targetTempId": "op_6", "targetPortName": "Data"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_7", "targetPortName": "Image"},
            ],
            "parametersNeedingReview": {
                "op_3": ["ModelPath", "InputSize"],
                "op_5": ["IpAddress", "Port"],
                "op_6": ["IpAddress", "Port"],
            },
        },
    },
    # ---- 模式 3: 尺寸测量 ----
    {
        "pattern": "measurement",
        "user_templates": [
            "测量零件上两孔之间的距离，结果转换为毫米并输出",
            "检测圆孔直径，转成物理尺寸",
            "测量两个孔的间距，要转换成mm",
            "做孔径测量，结果要物理单位",
        ],
        "answer": {
            "explanation": "相机采集图像，滤波降噪，边缘检测，圆孔检测，距离测量，坐标转换为物理尺寸，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "exposureTime": "10000"}},
                {"tempId": "op_2", "operatorType": "GaussianBlur", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
                {"tempId": "op_3", "operatorType": "EdgeDetection", "displayName": "边缘检测", "parameters": {"Threshold1": "50", "Threshold2": "150", "ApertureSize": "3"}},
                {"tempId": "op_4", "operatorType": "CircleMeasurement", "displayName": "圆孔检测", "parameters": {"MinRadius": "10", "MaxRadius": "100", "Method": "HoughCircle"}},
                {"tempId": "op_5", "operatorType": "Measurement", "displayName": "距离测量", "parameters": {"MeasureType": "PointToPoint"}},
                {"tempId": "op_6", "operatorType": "CoordinateTransform", "displayName": "坐标转换", "parameters": {"PixelSize": "0.02"}},
                {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "测量结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "Center", "targetTempId": "op_5", "targetPortName": "Image"},
                {"sourceTempId": "op_5", "sourcePortName": "Distance", "targetTempId": "op_6", "targetPortName": "PixelX"},
                {"sourceTempId": "op_6", "sourcePortName": "PhysicalX", "targetTempId": "op_7", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {
                "op_4": ["MinRadius", "MaxRadius"],
                "op_6": ["PixelSize", "CalibrationFile"],
            },
        },
    },
    # ---- 模式 3 变体: 线测量 ----
    {
        "pattern": "line_measurement",
        "user_templates": [
            "测量零件的宽度",
            "检测产品边缘到边缘的距离",
            "找两条直线，测量它们之间的间距",
            "做宽度测量，用卡尺工具",
        ],
        "answer": {
            "explanation": "相机采集图像，滤波降噪，边缘检测，直线检测，宽度测量，单位转换，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
                {"tempId": "op_2", "operatorType": "Filtering", "displayName": "滤波降噪", "parameters": {"KernelSize": "5"}},
                {"tempId": "op_3", "operatorType": "EdgeDetection", "displayName": "边缘检测", "parameters": {"Threshold1": "50", "Threshold2": "150"}},
                {"tempId": "op_4", "operatorType": "CaliperTool", "displayName": "卡尺工具", "parameters": {}},
                {"tempId": "op_5", "operatorType": "WidthMeasurement", "displayName": "宽度测量", "parameters": {}},
                {"tempId": "op_6", "operatorType": "UnitConvert", "displayName": "单位转换", "parameters": {}},
                {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "测量结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "Result", "targetTempId": "op_5", "targetPortName": "Input"},
                {"sourceTempId": "op_5", "sourcePortName": "Width", "targetTempId": "op_6", "targetPortName": "PixelValue"},
                {"sourceTempId": "op_6", "sourcePortName": "PhysicalValue", "targetTempId": "op_7", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {"op_6": ["PixelSize", "CalibrationFile"]},
        },
    },
    # ---- 模式 4: 条码/OCR 识别 ----
    {
        "pattern": "barcode_ocr",
        "user_templates": [
            "扫描产品二维码，通过Modbus发给PLC",
            "读取产品条形码，发送到PLC",
            "扫码识别，把结果通过通信发出去",
            "读取包装上的二维码信息",
        ],
        "answer": {
            "explanation": "相机采集，条码识别提取文本，Modbus TCP协议发送给PLC",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "CodeRecognition", "displayName": "二维码识别", "parameters": {"CodeType": "QR", "MaxResults": "1"}},
                {"tempId": "op_3", "operatorType": "ModbusCommunication", "displayName": "Modbus发送", "parameters": {"Protocol": "TCP", "Port": "502", "FunctionCode": "WriteMultiple"}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_3", "targetPortName": "Data"},
            ],
            "parametersNeedingReview": {"op_3": ["IpAddress"]},
        },
    },
    # ---- 模式 4 变体: OCR 文字识别 ----
    {
        "pattern": "ocr_recognition",
        "user_templates": [
            "识别产品上的日期文字",
            "OCR读取包装上的批号",
            "做文字识别检测印刷内容",
            "读取标签上的文字信息并记录",
        ],
        "answer": {
            "explanation": "相机采集图像，OCR识别文字内容，判断是否匹配预期，存入数据库，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
                {"tempId": "op_2", "operatorType": "OcrRecognition", "displayName": "文字识别", "parameters": {}},
                {"tempId": "op_3", "operatorType": "ConditionalBranch", "displayName": "内容匹配判断", "parameters": {}},
                {"tempId": "op_4", "operatorType": "DatabaseWrite", "displayName": "记录到数据库", "parameters": {"DbType": "SQLite"}},
                {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "识别结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_3", "targetPortName": "Value"},
                {"sourceTempId": "op_3", "sourcePortName": "True", "targetTempId": "op_4", "targetPortName": "Data"},
                {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_5", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {
                "op_3": ["CompareValue"],
                "op_4": ["ConnectionString", "TableName"],
            },
        },
    },
    # ---- 模式 5: 分拣（传统检测+PLC通信） ----
    {
        "pattern": "sorting",
        "user_templates": [
            "检测产品后分拣，合格品和不良品发不同信号",
            "缺陷检测后通过PLC分拣OK和NG",
            "检测完给PLC发信号，合格和不合格分开",
        ],
        "answer": {
            "explanation": "采集图像，滤波，二值化，Blob分析缺陷数，判断OK/NG，分别通过Modbus通信发送对应信号",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
                {"tempId": "op_2", "operatorType": "Filtering", "displayName": "滤波降噪", "parameters": {"KernelSize": "5"}},
                {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
                {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "50"}},
                {"tempId": "op_5", "operatorType": "ConditionalBranch", "displayName": "OK/NG判断", "parameters": {"FieldName": "BlobCount", "Condition": "GreaterThan", "CompareValue": "0"}},
                {"tempId": "op_6", "operatorType": "ModbusCommunication", "displayName": "发送NG", "parameters": {"FunctionCode": "WriteSingle", "WriteValue": "1"}},
                {"tempId": "op_7", "operatorType": "ModbusCommunication", "displayName": "发送OK", "parameters": {"FunctionCode": "WriteSingle", "WriteValue": "0"}},
                {"tempId": "op_8", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Value"},
                {"sourceTempId": "op_5", "sourcePortName": "True", "targetTempId": "op_6", "targetPortName": "Data"},
                {"sourceTempId": "op_5", "sourcePortName": "False", "targetTempId": "op_7", "targetPortName": "Data"},
                {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_8", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {
                "op_4": ["MinArea", "MaxArea"],
                "op_6": ["IpAddress", "Port", "RegisterAddress"],
                "op_7": ["IpAddress", "Port", "RegisterAddress"],
            },
        },
    },
    # ---- 模式 6: 循环检测 + 数据库记录 ----
    {
        "pattern": "cycle_database",
        "user_templates": [
            "对产品连续拍照10次，记录每次的检测结果到数据库",
            "循环检测产品，每次结果存库",
            "批量连续拍照检测，存入数据库",
            "连续检测并记录每次结果",
        ],
        "answer": {
            "explanation": "循环计数器控制拍照次数，每次采集后二值化分析，结果写入数据库，输出统计",
            "operators": [
                {"tempId": "op_1", "operatorType": "CycleCounter", "displayName": "循环计数", "parameters": {"CycleLimit": "10", "AutoReset": "true"}},
                {"tempId": "op_2", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
                {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
                {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "100"}},
                {"tempId": "op_5", "operatorType": "ResultJudgment", "displayName": "OK/NG判定", "parameters": {"FieldName": "BlobCount", "Condition": "LessThanOrEqual", "ThresholdValue": "0"}},
                {"tempId": "op_6", "operatorType": "DatabaseWrite", "displayName": "记录到数据库", "parameters": {"DbType": "SQLite", "TableName": "InspectionResults"}},
                {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "输出结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "CycleCount", "targetTempId": "op_7", "targetPortName": "Result"},
                {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Value"},
                {"sourceTempId": "op_5", "sourcePortName": "IsOk", "targetTempId": "op_6", "targetPortName": "Data"},
            ],
            "parametersNeedingReview": {
                "op_4": ["MinArea", "MaxArea"],
                "op_6": ["ConnectionString", "TableName"],
            },
        },
    },
    # ---- 模式额外: 模板匹配定位 ----
    {
        "pattern": "template_matching",
        "user_templates": [
            "用模板匹配找到产品位置再做检测",
            "先做模板定位，然后检查缺陷",
            "找图定位后再做检测",
            "用模板匹配确定工件位置",
        ],
        "answer": {
            "explanation": "采集图像，模板匹配定位产品，裁剪ROI区域，在ROI内进行缺陷检测，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
                {"tempId": "op_2", "operatorType": "TemplateMatching", "displayName": "模板匹配", "parameters": {}},
                {"tempId": "op_3", "operatorType": "ImageCrop", "displayName": "ROI裁剪", "parameters": {}},
                {"tempId": "op_4", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
                {"tempId": "op_5", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "50"}},
                {"tempId": "op_6", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Position", "targetTempId": "op_3", "targetPortName": "Region"},
                {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
                {"sourceTempId": "op_4", "sourcePortName": "Image", "targetTempId": "op_5", "targetPortName": "Image"},
                {"sourceTempId": "op_5", "sourcePortName": "BlobCount", "targetTempId": "op_6", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {
                "op_2": ["TemplatePath"],
                "op_5": ["MinArea", "MaxArea"],
            },
        },
    },
    # ---- 模式额外: 颜色检测 ----
    {
        "pattern": "color_detection",
        "user_templates": [
            "检测产品颜色是否正确",
            "做颜色检查，判断有没有偏色",
            "产品颜色对比，看是否合格",
            "检测表面颜色异常",
        ],
        "answer": {
            "explanation": "采集图像，颜色检测分析色差，判断是否合格，输出结果",
            "operators": [
                {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
                {"tempId": "op_2", "operatorType": "ColorDetection", "displayName": "颜色检测", "parameters": {}},
                {"tempId": "op_3", "operatorType": "ResultJudgment", "displayName": "合格判定", "parameters": {}},
                {"tempId": "op_4", "operatorType": "ResultOutput", "displayName": "颜色检测结果", "parameters": {}},
            ],
            "connections": [
                {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
                {"sourceTempId": "op_2", "sourcePortName": "Result", "targetTempId": "op_3", "targetPortName": "Value"},
                {"sourceTempId": "op_3", "sourcePortName": "IsOk", "targetTempId": "op_4", "targetPortName": "Result"},
            ],
            "parametersNeedingReview": {"op_2": ["TargetColor", "Tolerance"]},
        },
    },
]


# ============================================================
# 3. 变异引擎 —— 对种子数据进行口语化改写 + 参数随机化
# ============================================================

# 口语化前缀/后缀变体（模拟真实用户的不同说法）
PREFIXES = [
    "", "帮我", "我想", "请帮我", "能不能", "我要", "帮忙",
    "麻烦你", "我需要", "搭建一个", "创建一个", "构建一个",
]

SUFFIXES = [
    "", "的流程", "工作流", "的工作流", "的检测流程",
    "的方案", "，谢谢", "，帮我搭建", "吧",
]

# 行业/产品场景词（增加多样性）
INDUSTRY_CONTEXTS = [
    "", "芯片", "连接器", "FPC", "PCB", "轴承", "齿轮",
    "密封件", "瓶盖", "标签", "螺丝", "弹簧", "电路板",
    "手机壳", "注塑件", "冲压件", "焊点", "引脚", "端子",
]

# 参数随机化范围
PARAM_RANGES = {
    "KernelSize": ["3", "5", "7"],
    "MinArea": ["20", "50", "100", "200", "500"],
    "MaxArea": ["1000", "3000", "5000", "10000", "50000"],
    "Confidence": ["0.3", "0.4", "0.5", "0.6", "0.7", "0.8"],
    "Width": ["320", "416", "640", "1280"],
    "Height": ["320", "416", "640", "1280"],
    "Threshold1": ["30", "50", "80", "100"],
    "Threshold2": ["100", "150", "200", "250"],
    "MinRadius": ["5", "10", "20", "30"],
    "MaxRadius": ["50", "80", "100", "200"],
    "CycleLimit": ["5", "10", "20", "50", "100"],
    "exposureTime": ["5000", "10000", "20000", "50000"],
    "PixelSize": ["0.01", "0.02", "0.05", "0.1"],
}

# 触发模式随机化
TRIGGER_MODES = ["Hardware", "Software"]


def mutate_user_description(template: str) -> str:
    """对用户描述模板进行口语化变异"""
    prefix = random.choice(PREFIXES)
    suffix = random.choice(SUFFIXES)
    industry = random.choice(INDUSTRY_CONTEXTS)

    desc = template
    # 随机插入行业上下文
    if industry and random.random() > 0.4:
        desc = desc.replace("产品", f"{industry}产品", 1)
        if "产品" not in desc:
            desc = f"{industry}{desc}"

    return f"{prefix}{desc}{suffix}".strip()


def mutate_parameters(answer: dict) -> dict:
    """对答案中的参数进行随机化"""
    mutated = copy.deepcopy(answer)

    for op in mutated["operators"]:
        params = op.get("parameters", {})
        for key, value in list(params.items()):
            if key in PARAM_RANGES:
                params[key] = random.choice(PARAM_RANGES[key])
            elif key == "triggerMode":
                params[key] = random.choice(TRIGGER_MODES)

    return mutated


def generate_sample(seed: dict) -> dict:
    """从一个种子生成一条训练样本"""
    # 随机选择一个用户描述模板并变异
    template = random.choice(seed["user_templates"])
    user_desc = mutate_user_description(template)

    # 参数随机化
    mutated_answer = mutate_parameters(seed["answer"])

    return {
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT_FOR_SFT},
            {"role": "user", "content": user_desc},
            {"role": "assistant", "content": json.dumps(mutated_answer, ensure_ascii=False)},
        ]
    }


# ============================================================
# 4. 主生成逻辑
# ============================================================

def generate_dataset(count: int = 300, seed_value: int = 42) -> list[dict]:
    """生成指定数量的训练数据"""
    random.seed(seed_value)
    dataset = []

    # 确保每个种子模式至少生成 min_per_seed 条数据
    min_per_seed = max(5, count // len(SEED_DATABASE))

    for seed in SEED_DATABASE:
        for _ in range(min_per_seed):
            sample = generate_sample(seed)
            dataset.append(sample)

    # 随机补充剩余数量
    remaining = count - len(dataset)
    for _ in range(max(0, remaining)):
        seed = random.choice(SEED_DATABASE)
        sample = generate_sample(seed)
        dataset.append(sample)

    # 打乱顺序
    random.shuffle(dataset)
    return dataset[:count]


def validate_dataset(dataset: list[dict]) -> tuple[int, int]:
    """验证数据集格式正确性"""
    valid, invalid = 0, 0
    for sample in dataset:
        try:
            msgs = sample["messages"]
            assert len(msgs) == 3
            assert msgs[0]["role"] == "system"
            assert msgs[1]["role"] == "user"
            assert msgs[2]["role"] == "assistant"
            # 验证 assistant 消息是合法 JSON
            parsed = json.loads(msgs[2]["content"])
            assert "operators" in parsed
            assert "connections" in parsed
            valid += 1
        except Exception:
            invalid += 1

    return valid, invalid


def main():
    parser = argparse.ArgumentParser(
        description="ClearVision SFT 训练数据生成器",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python generate_training_data.py                    # 生成 300 条
  python generate_training_data.py --count 500        # 生成 500 条
  python generate_training_data.py --seed 123         # 指定随机种子
  python generate_training_data.py --output my.jsonl  # 指定输出文件
        """,
    )
    parser.add_argument("--count", type=int, default=300, help="生成样本数量 (默认: 300)")
    parser.add_argument("--output", type=str, default="clearvision_sft_data.jsonl", help="输出文件路径")
    parser.add_argument("--seed", type=int, default=42, help="随机种子 (默认: 42)")
    args = parser.parse_args()

    print(f"=" * 60)
    print(f"  ClearVision SFT 训练数据生成器")
    print(f"=" * 60)
    print(f"  种子模式数量: {len(SEED_DATABASE)}")
    print(f"  目标生成数量: {args.count}")
    print(f"  随机种子:     {args.seed}")
    print(f"  输出文件:     {args.output}")
    print(f"=" * 60)

    # 生成数据
    dataset = generate_dataset(count=args.count, seed_value=args.seed)

    # 验证
    valid, invalid = validate_dataset(dataset)
    print(f"\n  ✅ 有效样本: {valid}")
    if invalid > 0:
        print(f"  ❌ 无效样本: {invalid}")

    # 统计模式分布
    pattern_counts: dict[str, int] = {}
    for sample in dataset:
        user_msg = sample["messages"][1]["content"]
        # 简单统计（通过关键词匹配）
        if "AI" in user_msg or "深度学习" in user_msg or "YOLO" in user_msg:
            key = "AI检测"
        elif "测量" in user_msg or "距离" in user_msg or "宽度" in user_msg or "孔径" in user_msg:
            key = "尺寸测量"
        elif "扫码" in user_msg or "条码" in user_msg or "二维码" in user_msg or "OCR" in user_msg or "文字" in user_msg:
            key = "条码/OCR"
        elif "循环" in user_msg or "连续" in user_msg or "批量" in user_msg:
            key = "循环检测"
        elif "模板" in user_msg or "定位" in user_msg or "找图" in user_msg:
            key = "模板匹配"
        elif "颜色" in user_msg or "偏色" in user_msg or "色差" in user_msg:
            key = "颜色检测"
        elif "分拣" in user_msg or "信号" in user_msg:
            key = "分拣通信"
        else:
            key = "缺陷检测"
        pattern_counts[key] = pattern_counts.get(key, 0) + 1

    print(f"\n  📊 模式分布:")
    for pattern, cnt in sorted(pattern_counts.items(), key=lambda x: -x[1]):
        bar = "█" * (cnt // 3)
        print(f"     {pattern:10s} {cnt:4d} {bar}")

    # 写入文件
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    with open(output_path, "w", encoding="utf-8") as f:
        for sample in dataset:
            f.write(json.dumps(sample, ensure_ascii=False) + "\n")

    file_size_kb = output_path.stat().st_size / 1024
    print(f"\n  📁 已保存到: {output_path.resolve()}")
    print(f"  📦 文件大小: {file_size_kb:.1f} KB")
    print(f"\n  下一步: 使用此文件进行 LoRA 微调")
    print(f"  参考配置: tools/sft/lora_config.yaml")
    print(f"=" * 60)


if __name__ == "__main__":
    main()
