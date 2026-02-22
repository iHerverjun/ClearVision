/**
 * Centralized schema definitions for all ClearVision Operators.
 * Migrated from legacy operatorLibrary.js (S4-002).
 */

export interface ParameterOption {
  label: string;
  value: string | number | boolean;
}

export interface ParameterSchema {
  name: string;
  displayName: string;
  type: string; // 'int' | 'float' | 'string' | 'bool' | 'enum' | 'file'
  dataType: string;
  defaultValue: any;
  min?: number;
  max?: number;
  description?: string;
  options?: ParameterOption[];
}

export interface OperatorSchema {
  type: string;
  displayName: string;
  category: string;
  icon: string;
  description: string;
  parameters: ParameterSchema[];
}

export const OPERATOR_SCHEMAS: OperatorSchema[] = [
  {
    type: "ImageAcquisition",
    displayName: "图像采集",
    category: "输入",
    icon: "camera",
    description: "从相机或文件获取图像",
    parameters: [
      {
        name: "sourceType",
        displayName: "采集源",
        type: "enum",
        dataType: "enum",
        defaultValue: "camera",
        options: [
          { label: "相机", value: "camera" },
          { label: "文件", value: "file" },
        ],
      },
      {
        name: "filePath",
        displayName: "文件路径",
        type: "file",
        dataType: "file",
        defaultValue: "",
        description: "支持 .bmp, .png, .jpg",
      },
      {
        name: "exposureTime",
        displayName: "曝光时间",
        type: "int",
        dataType: "int",
        defaultValue: 5000,
        min: 100,
        max: 1000000,
        description: "单位: us",
      },
      {
        name: "gain",
        displayName: "增益",
        type: "float",
        dataType: "float",
        defaultValue: 1.0,
        min: 0.0,
        max: 24.0,
      },
    ],
  },
  {
    type: "Filtering",
    displayName: "滤波",
    category: "预处理",
    icon: "box",
    description: "图像滤波降噪处理",
    parameters: [
      {
        name: "method",
        displayName: "滤波方法",
        type: "enum",
        dataType: "enum",
        defaultValue: "gaussian",
        options: [
          { label: "高斯滤波", value: "gaussian" },
          { label: "中值滤波", value: "median" },
          { label: "均值滤波", value: "mean" },
        ],
      },
      {
        name: "kernelSize",
        displayName: "核大小",
        type: "int",
        dataType: "int",
        defaultValue: 3,
        min: 3,
        max: 15,
        description: "必须为奇数",
      },
    ],
  },
  {
    type: "EdgeDetection",
    displayName: "边缘检测",
    category: "特征提取",
    icon: "fingerprint",
    description: "检测图像边缘特征",
    parameters: [
      {
        name: "algorithm",
        displayName: "算子类型",
        type: "enum",
        dataType: "enum",
        defaultValue: "canny",
        options: [
          { label: "Canny", value: "canny" },
          { label: "Sobel", value: "sobel" },
          { label: "Laplacian", value: "laplacian" },
        ],
      },
      {
        name: "threshold1",
        displayName: "阈值 1",
        type: "int",
        dataType: "int",
        defaultValue: 50,
        min: 0,
        max: 255,
      },
      {
        name: "threshold2",
        displayName: "阈值 2",
        type: "int",
        dataType: "int",
        defaultValue: 150,
        min: 0,
        max: 255,
      },
    ],
  },
  {
    type: "Thresholding",
    displayName: "二值化",
    category: "预处理",
    icon: "box",
    description: "图像阈值分割",
    parameters: [
      {
        name: "method",
        displayName: "阈值方法",
        type: "enum",
        dataType: "enum",
        defaultValue: "fixed",
        options: [
          { label: "固定阈值", value: "fixed" },
          { label: "Otsu", value: "otsu" },
          { label: "Adaptive", value: "adaptive" },
        ],
      },
      {
        name: "threshold",
        displayName: "阈值",
        type: "int",
        dataType: "int",
        defaultValue: 128,
        min: 0,
        max: 255,
      },
      {
        name: "invert",
        displayName: "反转结果",
        type: "bool",
        dataType: "bool",
        defaultValue: false,
      },
    ],
  },
  {
    type: "Morphology",
    displayName: "形态学",
    category: "预处理",
    icon: "box",
    description: "腐蚀、膨胀、开闭运算",
    parameters: [
      {
        name: "operation",
        displayName: "操作类型",
        type: "enum",
        dataType: "enum",
        defaultValue: "erode",
        options: [
          { label: "腐蚀", value: "erode" },
          { label: "膨胀", value: "dilate" },
          { label: "开运算", value: "open" },
          { label: "闭运算", value: "close" },
        ],
      },
      {
        name: "kernelSize",
        displayName: "核大小",
        type: "int",
        dataType: "int",
        defaultValue: 3,
        min: 3,
        max: 21,
      },
      {
        name: "iterations",
        displayName: "迭代次数",
        type: "int",
        dataType: "int",
        defaultValue: 1,
        min: 1,
        max: 10,
      },
    ],
  },
  {
    type: "BlobAnalysis",
    displayName: "Blob分析",
    category: "特征提取",
    icon: "target",
    description: "连通区域分析",
    parameters: [
      {
        name: "minArea",
        displayName: "最小面积",
        type: "int",
        dataType: "int",
        defaultValue: 100,
        min: 0,
      },
      {
        name: "maxArea",
        displayName: "最大面积",
        type: "int",
        dataType: "int",
        defaultValue: 100000,
        min: 0,
      },
      {
        name: "color",
        displayName: "目标颜色",
        type: "enum",
        dataType: "enum",
        defaultValue: "white",
        options: [
          { label: "白色", value: "white" },
          { label: "黑色", value: "black" },
        ],
      },
    ],
  },
  {
    type: "TemplateMatching",
    displayName: "模板匹配",
    category: "检测",
    icon: "target",
    description: "图像模板匹配定位",
    parameters: [
      {
        name: "method",
        displayName: "匹配方法",
        type: "enum",
        dataType: "enum",
        defaultValue: "ncc",
        options: [
          { label: "归一化相关 (NCC)", value: "ncc" },
          { label: "平方差 (SQDIFF)", value: "sqdiff" },
        ],
      },
      {
        name: "threshold",
        displayName: "匹配分数阈值",
        type: "float",
        dataType: "float",
        defaultValue: 0.8,
        min: 0.1,
        max: 1.0,
      },
      {
        name: "maxMatches",
        displayName: "最大匹配数",
        type: "int",
        dataType: "int",
        defaultValue: 1,
        min: 1,
        max: 100,
      },
    ],
  },
  {
    type: "Measurement",
    displayName: "测量",
    category: "检测",
    icon: "box",
    description: "几何尺寸测量",
    parameters: [
      {
        name: "type",
        displayName: "测量类型",
        type: "enum",
        dataType: "enum",
        defaultValue: "distance",
        options: [
          { label: "距离", value: "distance" },
          { label: "角度", value: "angle" },
          { label: "圆径", value: "radius" },
        ],
      },
    ],
  },
  {
    type: "DeepLearning",
    displayName: "深度学习",
    category: "AI检测",
    icon: "eye",
    description: "AI缺陷检测",
    parameters: [
      {
        name: "modelPath",
        displayName: "模型路径",
        type: "file",
        dataType: "file",
        defaultValue: "",
      },
      {
        name: "confidence",
        displayName: "置信度阈值",
        type: "float",
        dataType: "float",
        defaultValue: 0.5,
        min: 0.0,
        max: 1.0,
      },
    ],
  },
  {
    type: "ResultOutput",
    displayName: "结果输出",
    category: "输出",
    icon: "network",
    description: "输出检测结果",
    parameters: [
      {
        name: "format",
        displayName: "输出格式",
        type: "enum",
        dataType: "enum",
        defaultValue: "json",
        options: [
          { label: "JSON", value: "json" },
          { label: "CSV", value: "csv" },
          { label: "Text", value: "text" },
        ],
      },
      {
        name: "saveToFile",
        displayName: "保存到文件",
        type: "bool",
        dataType: "bool",
        defaultValue: true,
      },
    ],
  },
];

export function getSchemaByType(type: string): OperatorSchema | undefined {
  return OPERATOR_SCHEMAS.find((op) => op.type === type);
}

export function getCategories(): string[] {
  return [...new Set(OPERATOR_SCHEMAS.map((op) => op.category))];
}
