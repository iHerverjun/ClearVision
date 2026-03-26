import { DEFAULT_RECT_PARAM_KEYS, REGION_RECT_PARAM_KEYS } from './roiGeometry.mjs';

function readOperatorValue(operator, name, fallback = '') {
    const parameter = (operator?.parameters || []).find(item =>
        String(item?.name || item?.Name || '').toLowerCase() === String(name).toLowerCase());
    const value = parameter?.value ?? parameter?.Value ?? parameter?.defaultValue ?? parameter?.DefaultValue;
    return value == null ? fallback : value;
}

export function getOperatorRoiConfig(operator) {
    const type = String(operator?.type || operator?.operatorType || '').trim();

    if (type === 'RoiManager') {
        const shape = String(readOperatorValue(operator, 'Shape', 'Rectangle'));
        const editable = shape === 'Rectangle';
        return {
            supported: true,
            editable,
            shape,
            rectParamKeys: DEFAULT_RECT_PARAM_KEYS,
            subtitle: '拖拽框选矩形区域，自动同步到 X / Y / Width / Height',
            readonlyMessage: '图上编辑当前仅支持矩形 ROI，圆形/多边形仍使用参数输入'
        };
    }

    if (type === 'BoxFilter') {
        const filterMode = String(readOperatorValue(operator, 'FilterMode', 'Area'));
        const editable = filterMode.toLowerCase() === 'region';
        return {
            supported: true,
            editable,
            shape: 'Rectangle',
            rectParamKeys: REGION_RECT_PARAM_KEYS,
            subtitle: '拖拽框选矩形区域，自动同步到 RegionX / RegionY / RegionW / RegionH',
            readonlyMessage: '图上编辑当前仅支持 BoxFilter 的 Region 模式，请先把 FilterMode 切到 Region'
        };
    }

    return {
        supported: false,
        editable: false,
        shape: 'Rectangle',
        rectParamKeys: DEFAULT_RECT_PARAM_KEYS,
        subtitle: '拖拽框选矩形区域，自动同步到 X / Y / Width / Height',
        readonlyMessage: '当前节点不支持 ROI 图上编辑'
    };
}
