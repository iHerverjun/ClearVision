/**
 * 算子元数据配置：
 * 1) 默认使用本地 fallback（10 个核心算子）
 * 2) 启动后尝试从后端 /api/operators/metadata 动态加载完整列表
 */
import { apiClient } from "../services/api";

export interface ParameterOption {
  label: string;
  value: string | number | boolean;
}

export interface ParameterSchema {
  name: string;
  displayName: string;
  type: string; // int | float | string | bool | enum | file | ...
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

interface BackendParameterOption {
  label?: string;
  value?: string | number | boolean;
}

interface BackendParameterDefinition {
  name?: string;
  displayName?: string;
  dataType?: string;
  defaultValue?: any;
  minValue?: any;
  maxValue?: any;
  description?: string;
  options?: BackendParameterOption[];
}

interface BackendOperatorMetadata {
  type?: string;
  displayName?: string;
  description?: string;
  category?: string;
  iconName?: string;
  parameters?: BackendParameterDefinition[];
}

export const OPERATOR_SCHEMAS: OperatorSchema[] = [
  {
    type: "ImageAcquisition",
    displayName: "图像采集",
    category: "输入",
    icon: "camera",
    description: "从相机或文件采集图像",
    parameters: [
      { name: "sourceType", displayName: "采集源", type: "enum", dataType: "enum", defaultValue: "camera", options: [{ label: "相机", value: "camera" }, { label: "文件", value: "file" }] },
      { name: "filePath", displayName: "文件路径", type: "file", dataType: "file", defaultValue: "" },
      { name: "exposureTime", displayName: "曝光时间", type: "int", dataType: "int", defaultValue: 5000, min: 100, max: 1000000 },
      { name: "gain", displayName: "增益", type: "float", dataType: "float", defaultValue: 1.0, min: 0, max: 24 },
    ],
  },
  {
    type: "Filtering",
    displayName: "滤波",
    category: "预处理",
    icon: "box",
    description: "图像滤波去噪",
    parameters: [
      { name: "method", displayName: "滤波方法", type: "enum", dataType: "enum", defaultValue: "gaussian", options: [{ label: "高斯滤波", value: "gaussian" }, { label: "中值滤波", value: "median" }, { label: "均值滤波", value: "mean" }] },
      { name: "kernelSize", displayName: "核大小", type: "int", dataType: "int", defaultValue: 3, min: 1, max: 31 },
    ],
  },
  {
    type: "EdgeDetection",
    displayName: "边缘检测",
    category: "特征提取",
    icon: "fingerprint",
    description: "提取图像边缘",
    parameters: [
      { name: "algorithm", displayName: "算法", type: "enum", dataType: "enum", defaultValue: "canny", options: [{ label: "Canny", value: "canny" }, { label: "Sobel", value: "sobel" }] },
      { name: "threshold1", displayName: "阈值1", type: "int", dataType: "int", defaultValue: 50, min: 0, max: 255 },
      { name: "threshold2", displayName: "阈值2", type: "int", dataType: "int", defaultValue: 150, min: 0, max: 255 },
    ],
  },
  {
    type: "Thresholding",
    displayName: "二值化",
    category: "预处理",
    icon: "box",
    description: "图像阈值分割",
    parameters: [
      { name: "method", displayName: "阈值方法", type: "enum", dataType: "enum", defaultValue: "fixed", options: [{ label: "固定阈值", value: "fixed" }, { label: "Otsu", value: "otsu" }, { label: "自适应", value: "adaptive" }] },
      { name: "threshold", displayName: "阈值", type: "int", dataType: "int", defaultValue: 128, min: 0, max: 255 },
      { name: "invert", displayName: "反转结果", type: "bool", dataType: "bool", defaultValue: false },
    ],
  },
  {
    type: "Morphology",
    displayName: "形态学",
    category: "预处理",
    icon: "box",
    description: "腐蚀、膨胀、开闭运算",
    parameters: [
      { name: "operation", displayName: "操作类型", type: "enum", dataType: "enum", defaultValue: "erode", options: [{ label: "腐蚀", value: "erode" }, { label: "膨胀", value: "dilate" }, { label: "开运算", value: "open" }, { label: "闭运算", value: "close" }] },
      { name: "kernelSize", displayName: "核大小", type: "int", dataType: "int", defaultValue: 3, min: 1, max: 31 },
      { name: "iterations", displayName: "迭代次数", type: "int", dataType: "int", defaultValue: 1, min: 1, max: 10 },
    ],
  },
  {
    type: "BlobAnalysis",
    displayName: "Blob分析",
    category: "特征提取",
    icon: "target",
    description: "连通域分析",
    parameters: [
      { name: "minArea", displayName: "最小面积", type: "int", dataType: "int", defaultValue: 100, min: 0 },
      { name: "maxArea", displayName: "最大面积", type: "int", dataType: "int", defaultValue: 100000, min: 0 },
    ],
  },
  {
    type: "TemplateMatching",
    displayName: "模板匹配",
    category: "检测",
    icon: "target",
    description: "模板匹配定位",
    parameters: [
      { name: "method", displayName: "匹配方法", type: "enum", dataType: "enum", defaultValue: "ncc", options: [{ label: "NCC", value: "ncc" }, { label: "SQDIFF", value: "sqdiff" }] },
      { name: "threshold", displayName: "匹配阈值", type: "float", dataType: "float", defaultValue: 0.8, min: 0.1, max: 1.0 },
    ],
  },
  {
    type: "Measurement",
    displayName: "测量",
    category: "检测",
    icon: "box",
    description: "几何测量",
    parameters: [
      { name: "type", displayName: "测量类型", type: "enum", dataType: "enum", defaultValue: "distance", options: [{ label: "距离", value: "distance" }, { label: "角度", value: "angle" }, { label: "半径", value: "radius" }] },
    ],
  },
  {
    type: "DeepLearning",
    displayName: "深度学习",
    category: "AI检测",
    icon: "eye",
    description: "AI模型检测",
    parameters: [
      { name: "modelPath", displayName: "模型路径", type: "file", dataType: "file", defaultValue: "" },
      { name: "confidence", displayName: "置信度", type: "float", dataType: "float", defaultValue: 0.5, min: 0, max: 1 },
    ],
  },
  {
    type: "ResultOutput",
    displayName: "结果输出",
    category: "输出",
    icon: "network",
    description: "输出检测结果",
    parameters: [
      { name: "format", displayName: "输出格式", type: "enum", dataType: "enum", defaultValue: "json", options: [{ label: "JSON", value: "json" }, { label: "CSV", value: "csv" }, { label: "文本", value: "text" }] },
      { name: "saveToFile", displayName: "保存到文件", type: "bool", dataType: "bool", defaultValue: true },
    ],
  },
];

let dynamicOperatorSchemas: OperatorSchema[] | null = null;
let loadPromise: Promise<OperatorSchema[]> | null = null;

function normalizeDataType(rawType?: string): string {
  const value = (rawType || "").trim().toLowerCase();
  switch (value) {
    case "int":
    case "int32":
    case "int64":
    case "integer":
    case "long":
      return "int";
    case "double":
    case "float":
    case "decimal":
      return "float";
    case "bool":
    case "boolean":
      return "bool";
    case "enum":
      return "enum";
    case "file":
      return "file";
    default:
      return value || "string";
  }
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return undefined;
}

function defaultValueForType(dataType: string) {
  switch (dataType) {
    case "int":
    case "float":
      return 0;
    case "bool":
      return false;
    case "enum":
    case "file":
    case "cameraBinding":
    case "string":
    default:
      return "";
  }
}

function mapIconName(iconName?: string): string {
  const name = (iconName || "").toLowerCase();
  if (!name) return "box";
  if (name.includes("camera")) return "camera";
  if (name.includes("network") || name.includes("comm") || name.includes("modbus") || name.includes("tcp") || name.includes("serial") || name.includes("http") || name.includes("mqtt")) return "network";
  if (name.includes("target") || name.includes("measure") || name.includes("match") || name.includes("blob")) return "target";
  if (name.includes("edge") || name.includes("feature") || name.includes("fingerprint") || name.includes("contour")) return "fingerprint";
  if (name.includes("ai") || name.includes("deep") || name.includes("ocr") || name.includes("learn")) return "eye";
  return "box";
}

function mapBackendParameter(param: BackendParameterDefinition): ParameterSchema {
  const dataType = normalizeDataType(param.dataType);
  const options = Array.isArray(param.options)
    ? param.options
        .filter((option) => option && option.label !== undefined && option.value !== undefined)
        .map((option) => ({
          label: String(option.label),
          value: option.value as string | number | boolean,
        }))
    : undefined;

  const defaultValue =
    param.defaultValue !== undefined
      ? param.defaultValue
      : (options && options.length > 0
          ? options[0]?.value ?? defaultValueForType(dataType)
          : defaultValueForType(dataType));

  return {
    name: String(param.name || "param"),
    displayName: String(param.displayName || param.name || "参数"),
    type: dataType,
    dataType,
    defaultValue,
    min: toNumber(param.minValue),
    max: toNumber(param.maxValue),
    description: param.description ? String(param.description) : undefined,
    options: options && options.length > 0 ? options : undefined,
  };
}

function mapBackendToSchema(items: BackendOperatorMetadata[]): OperatorSchema[] {
  return items
    .filter((item) => item && item.type)
    .map((item) => ({
      type: String(item.type),
      displayName: String(item.displayName || item.type || "未命名算子"),
      category: String(item.category || "未分类"),
      icon: mapIconName(item.iconName),
      description: String(item.description || ""),
      parameters: Array.isArray(item.parameters)
        ? item.parameters.map(mapBackendParameter)
        : [],
    }));
}

export async function loadOperatorSchemas(forceReload = false): Promise<OperatorSchema[]> {
  if (!forceReload && dynamicOperatorSchemas && dynamicOperatorSchemas.length > 0) {
    return dynamicOperatorSchemas;
  }

  if (loadPromise) {
    return loadPromise;
  }

  loadPromise = apiClient.get("/api/operators/metadata")
    .then((raw) => {
      if (!Array.isArray(raw)) {
        throw new Error("算子元数据格式无效");
      }
      const mapped = mapBackendToSchema(raw as BackendOperatorMetadata[]);
      if (mapped.length === 0) {
        throw new Error("Operator metadata is empty.");
      }
      dynamicOperatorSchemas = mapped;
      return dynamicOperatorSchemas;
    })
    .catch((error) => {
      console.warn("[operatorSchema] 后端元数据加载失败，已使用本地回退配置。", error);
      // Avoid sticky fallback cache so later retries can recover.
      dynamicOperatorSchemas = null;
      return OPERATOR_SCHEMAS;
    })
    .finally(() => {
      loadPromise = null;
    });

  return loadPromise;
}

export function getOperatorSchemas(): OperatorSchema[] {
  return dynamicOperatorSchemas && dynamicOperatorSchemas.length > 0
    ? dynamicOperatorSchemas
    : OPERATOR_SCHEMAS;
}

export function getSchemaByType(type: string): OperatorSchema | undefined {
  const schemas = getOperatorSchemas();
  return schemas.find((op) => op.type === type) || OPERATOR_SCHEMAS.find((op) => op.type === type);
}

export function getCategories(): string[] {
  return [...new Set(getOperatorSchemas().map((op) => op.category))];
}
