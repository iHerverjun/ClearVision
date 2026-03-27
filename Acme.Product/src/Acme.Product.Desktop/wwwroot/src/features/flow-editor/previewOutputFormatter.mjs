const DETECTION_KEYS = new Set(['detections']);
const SUPPRESSED_DETECTION_KEYS = new Set(['suppresseddetections']);

function hasKnownImageSignature(base64Text) {
    const sanitized = String(base64Text || '').replace(/\s+/g, '');
    if (sanitized.length < 32 || /[^A-Za-z0-9+/=]/.test(sanitized)) {
        return false;
    }

    if (typeof atob !== 'function') {
        return false;
    }

    const prefixLength = Math.min(64, sanitized.length);
    let sample = sanitized.slice(0, prefixLength);
    const paddingLength = sample.length % 4;
    if (paddingLength !== 0) {
        sample = sample.padEnd(sample.length + (4 - paddingLength), '=');
    }

    try {
        const decoded = atob(sample);
        if (!decoded || decoded.length < 4) {
            return false;
        }

        const bytes = Array.from(decoded.slice(0, 12)).map(char => char.charCodeAt(0));
        const ascii = decoded.slice(0, 12);

        return (
            (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) ||
            (bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff) ||
            (bytes[0] === 0x42 && bytes[1] === 0x4d) ||
            ascii.startsWith('GIF8') ||
            ascii.startsWith('RIFF')
        );
    } catch {
        return false;
    }
}

function normalizeOutputKey(key) {
    return String(key || '')
        .trim()
        .replace(/[\s_-]+/g, '')
        .toLowerCase();
}

function truncateText(text, maxLength) {
    const value = String(text ?? '');
    if (value.length <= maxLength) {
        return {
            text: value,
            title: null,
            truncated: false
        };
    }

    return {
        text: `${value.slice(0, maxLength)}...`,
        title: value,
        truncated: true
    };
}

function countNestedArrayItems(value) {
    if (Array.isArray(value)) {
        return value.length;
    }

    if (!value || typeof value !== 'object') {
        return 0;
    }

    const nestedArrays = [
        value.Detections,
        value.detections,
        value.Items,
        value.items
    ];

    for (const candidate of nestedArrays) {
        if (Array.isArray(candidate)) {
            return candidate.length;
        }
    }

    const countCandidates = [
        value.Count,
        value.count,
        value.Total,
        value.total,
        value.Length,
        value.length
    ];

    for (const candidate of countCandidates) {
        if (typeof candidate === 'number' && Number.isFinite(candidate)) {
            return candidate;
        }
    }

    return 0;
}

export function isPreviewImageLikePayload(value) {
    if (typeof value !== 'string') {
        return false;
    }

    const trimmed = value.trim();
    if (!trimmed) {
        return false;
    }

    if (trimmed.startsWith('data:image/')) {
        return true;
    }

    if (trimmed.startsWith('{') || trimmed.startsWith('[') || trimmed.includes('"Format"')) {
        return false;
    }

    return hasKnownImageSignature(trimmed);
}

export function formatPreviewOutputValue(key, value, options = {}) {
    const {
        stringMaxLength = 48
    } = options;

    const normalizedKey = normalizeOutputKey(key);

    if (DETECTION_KEYS.has(normalizedKey)) {
        return {
            text: `${countNestedArrayItems(value)} detections`,
            title: null,
            kind: 'detections'
        };
    }

    if (SUPPRESSED_DETECTION_KEYS.has(normalizedKey)) {
        return {
            text: `${countNestedArrayItems(value)} suppressed`,
            title: null,
            kind: 'suppressed'
        };
    }

    if (typeof value === 'number') {
        return {
            text: Number.isInteger(value) ? String(value) : value.toFixed(3),
            title: null,
            kind: 'number'
        };
    }

    if (typeof value === 'boolean') {
        return {
            text: value ? 'true' : 'false',
            title: null,
            kind: 'boolean'
        };
    }

    if (typeof value === 'string') {
        const stringValue = value.trim() || '--';
        const truncated = truncateText(stringValue, stringMaxLength);
        return {
            text: truncated.text,
            title: truncated.title,
            kind: 'string'
        };
    }

    if (Array.isArray(value)) {
        return {
            text: `${value.length} items`,
            title: null,
            kind: 'array'
        };
    }

    if (value && typeof value === 'object') {
        const fieldCount = Object.keys(value).length;
        return {
            text: fieldCount > 0 ? `${fieldCount} fields` : 'Object',
            title: null,
            kind: 'object'
        };
    }

    return {
        text: '--',
        title: null,
        kind: 'null'
    };
}

export function buildPreviewSummaryItems(outputs, options = {}) {
    const {
        maxItems = 3,
        stringMaxLength = 42,
        skipImageLikeValues = true
    } = options;

    if (!outputs || typeof outputs !== 'object') {
        return [];
    }

    const items = [];
    for (const [key, value] of Object.entries(outputs)) {
        if (items.length >= maxItems) {
            break;
        }

        if (skipImageLikeValues && typeof value === 'string' && isPreviewImageLikePayload(value)) {
            continue;
        }

        const formattedValue = formatPreviewOutputValue(key, value, { stringMaxLength });
        items.push({
            key,
            value: formattedValue.text,
            title: formattedValue.title,
            kind: formattedValue.kind
        });
    }

    return items;
}
