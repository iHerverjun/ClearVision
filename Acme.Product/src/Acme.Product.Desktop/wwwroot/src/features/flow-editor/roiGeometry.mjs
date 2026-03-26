export const ROI_HANDLE_NAMES = [
    'nw',
    'n',
    'ne',
    'e',
    'se',
    's',
    'sw',
    'w'
];

export const DEFAULT_RECT_PARAM_KEYS = {
    x: 'X',
    y: 'Y',
    width: 'Width',
    height: 'Height'
};

export const REGION_RECT_PARAM_KEYS = {
    x: 'RegionX',
    y: 'RegionY',
    width: 'RegionW',
    height: 'RegionH'
};

export function clamp(value, min, max) {
    if (!Number.isFinite(value)) {
        return min;
    }

    return Math.min(Math.max(value, min), max);
}

export function normalizeRectFromPoints(start, end) {
    const x1 = Number(start?.x ?? 0);
    const y1 = Number(start?.y ?? 0);
    const x2 = Number(end?.x ?? 0);
    const y2 = Number(end?.y ?? 0);

    return {
        x: Math.min(x1, x2),
        y: Math.min(y1, y2),
        width: Math.abs(x2 - x1),
        height: Math.abs(y2 - y1)
    };
}

export function clampRectToBounds(rect, bounds, minSize = 1) {
    const maxWidth = Math.max(minSize, Number(bounds?.width ?? 0));
    const maxHeight = Math.max(minSize, Number(bounds?.height ?? 0));
    const width = clamp(Number(rect?.width ?? minSize), minSize, maxWidth);
    const height = clamp(Number(rect?.height ?? minSize), minSize, maxHeight);
    const x = clamp(Number(rect?.x ?? 0), 0, Math.max(0, maxWidth - width));
    const y = clamp(Number(rect?.y ?? 0), 0, Math.max(0, maxHeight - height));

    return {
        x,
        y,
        width,
        height
    };
}

export function translateRect(rect, delta, bounds, minSize = 1) {
    return clampRectToBounds({
        x: Number(rect?.x ?? 0) + Number(delta?.x ?? 0),
        y: Number(rect?.y ?? 0) + Number(delta?.y ?? 0),
        width: Number(rect?.width ?? minSize),
        height: Number(rect?.height ?? minSize)
    }, bounds, minSize);
}

export function resizeRectByHandle(rect, handle, point, bounds, minSize = 1) {
    const left = Number(rect?.x ?? 0);
    const top = Number(rect?.y ?? 0);
    const right = left + Number(rect?.width ?? minSize);
    const bottom = top + Number(rect?.height ?? minSize);

    let nextLeft = left;
    let nextTop = top;
    let nextRight = right;
    let nextBottom = bottom;

    const nextX = Number(point?.x ?? left);
    const nextY = Number(point?.y ?? top);

    if (handle.includes('w')) {
        nextLeft = nextX;
    }
    if (handle.includes('e')) {
        nextRight = nextX;
    }
    if (handle.includes('n')) {
        nextTop = nextY;
    }
    if (handle.includes('s')) {
        nextBottom = nextY;
    }

    if (nextRight - nextLeft < minSize) {
        if (handle.includes('w')) {
            nextLeft = nextRight - minSize;
        } else {
            nextRight = nextLeft + minSize;
        }
    }

    if (nextBottom - nextTop < minSize) {
        if (handle.includes('n')) {
            nextTop = nextBottom - minSize;
        } else {
            nextBottom = nextTop + minSize;
        }
    }

    return clampRectToBounds({
        x: nextLeft,
        y: nextTop,
        width: nextRight - nextLeft,
        height: nextBottom - nextTop
    }, bounds, minSize);
}

export function roundRect(rect) {
    return {
        x: Math.round(Number(rect?.x ?? 0)),
        y: Math.round(Number(rect?.y ?? 0)),
        width: Math.max(1, Math.round(Number(rect?.width ?? 1))),
        height: Math.max(1, Math.round(Number(rect?.height ?? 1)))
    };
}

function normalizeRectParamKeys(paramKeys = DEFAULT_RECT_PARAM_KEYS) {
    return {
        x: paramKeys?.x || DEFAULT_RECT_PARAM_KEYS.x,
        y: paramKeys?.y || DEFAULT_RECT_PARAM_KEYS.y,
        width: paramKeys?.width || DEFAULT_RECT_PARAM_KEYS.width,
        height: paramKeys?.height || DEFAULT_RECT_PARAM_KEYS.height
    };
}

export function rectFromParams(values, paramKeys = DEFAULT_RECT_PARAM_KEYS) {
    const keys = normalizeRectParamKeys(paramKeys);
    return roundRect({
        x: Number(values?.[keys.x] ?? values?.x ?? 0),
        y: Number(values?.[keys.y] ?? values?.y ?? 0),
        width: Number(values?.[keys.width] ?? values?.width ?? 1),
        height: Number(values?.[keys.height] ?? values?.height ?? 1)
    });
}

export function rectToParams(rect, paramKeys = DEFAULT_RECT_PARAM_KEYS) {
    const normalized = roundRect(rect);
    const keys = normalizeRectParamKeys(paramKeys);
    return {
        [keys.x]: normalized.x,
        [keys.y]: normalized.y,
        [keys.width]: normalized.width,
        [keys.height]: normalized.height
    };
}

export function screenToImagePoint(point, viewport) {
    const scale = Number(viewport?.scale ?? 1) || 1;
    const offsetX = Number(viewport?.offset?.x ?? 0);
    const offsetY = Number(viewport?.offset?.y ?? 0);

    return {
        x: (Number(point?.x ?? 0) - offsetX) / scale,
        y: (Number(point?.y ?? 0) - offsetY) / scale
    };
}

export function getRectHandlePoints(rect) {
    const x = Number(rect?.x ?? 0);
    const y = Number(rect?.y ?? 0);
    const width = Number(rect?.width ?? 0);
    const height = Number(rect?.height ?? 0);
    const centerX = x + width / 2;
    const centerY = y + height / 2;
    const right = x + width;
    const bottom = y + height;

    return {
        nw: { x, y },
        n: { x: centerX, y },
        ne: { x: right, y },
        e: { x: right, y: centerY },
        se: { x: right, y: bottom },
        s: { x: centerX, y: bottom },
        sw: { x, y: bottom },
        w: { x, y: centerY }
    };
}

