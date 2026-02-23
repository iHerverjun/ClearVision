/**
 * portTypeRegistry.ts
 * 端口数据类型中央注册表。
 */

export type PortShape = 'circle' | 'diamond' | 'square';

export interface PortTypeConfig {
  color: string;
  shape: PortShape;
  label: string;
  compatibleWith?: string[];
}

const PORT_TYPE_REGISTRY: Record<string, PortTypeConfig> = {
  Image: {
    color: '#4D94FF',
    shape: 'circle',
    label: '图像',
  },
  Integer: {
    color: '#00E676',
    shape: 'diamond',
    label: '整数',
    compatibleWith: ['Float', 'Any'],
  },
  Float: {
    color: '#FFA726',
    shape: 'diamond',
    label: '浮点数',
    compatibleWith: ['Any'],
  },
  Boolean: {
    color: '#FF4D4D',
    shape: 'diamond',
    label: '布尔值',
    compatibleWith: ['Any'],
  },
  String: {
    color: '#BA68C8',
    shape: 'square',
    label: '字符串',
    compatibleWith: ['Any'],
  },
  Point: {
    color: '#00BCD4',
    shape: 'square',
    label: '点坐标',
    compatibleWith: ['Any'],
  },
  Rectangle: {
    color: '#26C6DA',
    shape: 'square',
    label: '矩形',
    compatibleWith: ['Any'],
  },
  Contour: {
    color: '#FF7043',
    shape: 'circle',
    label: '轮廓',
    compatibleWith: ['Any'],
  },
  PointList: {
    color: '#7C4DFF',
    shape: 'square',
    label: '点列表',
    compatibleWith: ['Any'],
  },
  DetectionResult: {
    color: '#FFD54F',
    shape: 'circle',
    label: '检测结果',
    compatibleWith: ['Any'],
  },
  DetectionList: {
    color: '#FFB300',
    shape: 'circle',
    label: '检测列表',
    compatibleWith: ['Any'],
  },
  CircleData: {
    color: '#009688',
    shape: 'circle',
    label: '圆数据',
    compatibleWith: ['Any'],
  },
  LineData: {
    color: '#795548',
    shape: 'circle',
    label: '线段数据',
    compatibleWith: ['Any'],
  },
  Any: {
    color: '#9CA3AF',
    shape: 'circle',
    label: '任意类型',
  },
};

const DEFAULT_CONFIG: PortTypeConfig = {
  color: '#9CA3AF',
  shape: 'circle',
  label: '未知',
};

export function getPortConfig(type: string | null | undefined): PortTypeConfig {
  if (!type) return DEFAULT_CONFIG;
  return PORT_TYPE_REGISTRY[type] ?? DEFAULT_CONFIG;
}

export function getPortColor(type: string | null | undefined): string {
  return getPortConfig(type).color;
}

export function getPortShape(type: string | null | undefined): PortShape {
  return getPortConfig(type).shape;
}

export function getShapeCategory(type: string | null | undefined): PortShape {
  return getPortShape(type);
}

export function hexToRgba(hex: string, alpha: number): string {
  const normalized = hex.replace('#', '');
  if (!/^[0-9a-fA-F]{6}$/.test(normalized)) {
    return `rgba(156, 163, 175, ${alpha})`;
  }

  const value = parseInt(normalized, 16);
  const r = (value >> 16) & 255;
  const g = (value >> 8) & 255;
  const b = value & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

export function getPortGlowColor(
  type: string | null | undefined,
  alpha = 0.38,
): string {
  return hexToRgba(getPortColor(type), alpha);
}

export function isTypeCompatible(
  sourceType: string | null | undefined,
  targetType: string | null | undefined,
): boolean {
  if (!sourceType || !targetType) return true;
  if (sourceType === 'Unknown' || targetType === 'Unknown') return true;
  if (sourceType === 'Any' || targetType === 'Any') return true;
  if (sourceType === targetType) return true;

  const sourceConfig = PORT_TYPE_REGISTRY[sourceType];
  if (sourceConfig?.compatibleWith?.includes(targetType)) return true;

  return false;
}

export const PORT_TYPES = PORT_TYPE_REGISTRY;
