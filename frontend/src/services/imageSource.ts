const URI_SCHEME_REGEX = /^(data:|blob:|https?:|file:)/i;
const WINDOWS_DRIVE_PATH_REGEX = /^[a-zA-Z]:[\\/]/;
const WINDOWS_UNC_PATH_REGEX = /^\\\\/;
const RAW_BASE64_REGEX = /^[A-Za-z0-9+/=_-]+$/;

const stripWrappingQuotes = (value: string): string => {
  if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1).trim();
  }

  return value;
};

const toFileUrl = (filePath: string): string => {
  const normalized = filePath.replace(/\\/g, '/');

  if (normalized.startsWith('//')) {
    return `file:${encodeURI(normalized)}`;
  }

  const prefix = normalized.startsWith('/') ? 'file://' : 'file:///';
  return `${prefix}${encodeURI(normalized)}`;
};

const normalizeBase64Payload = (raw: string): string => {
  let normalized = raw.trim();

  const markerIndex = normalized.toLowerCase().indexOf('base64,');
  if (markerIndex >= 0) {
    normalized = normalized.slice(markerIndex + 'base64,'.length);
  }

  normalized = normalized.replace(/\s+/g, '');
  normalized = normalized.replace(/-/g, '+').replace(/_/g, '/');

  const remainder = normalized.length % 4;
  if (remainder > 0) {
    normalized = normalized.padEnd(normalized.length + (4 - remainder), '=');
  }

  return normalized;
};

const inferImageMimeType = (base64Payload: string): string => {
  if (base64Payload.startsWith('/9j/')) return 'image/jpeg';
  if (base64Payload.startsWith('iVBORw0K')) return 'image/png';
  if (base64Payload.startsWith('R0lGOD')) return 'image/gif';
  if (base64Payload.startsWith('Qk')) return 'image/bmp';
  if (base64Payload.startsWith('UklGR')) return 'image/webp';
  return 'image/png';
};

const tryResolveJsonPayload = (raw: string): string => {
  if (!raw.startsWith('{') || !raw.endsWith('}')) {
    return '';
  }

  try {
    const payload = JSON.parse(raw) as Record<string, unknown>;
    const candidates = [
      payload.imageBase64,
      payload.outputImageBase64,
      payload.previewImageBase64,
      payload.ImageBase64,
      payload.OutputImageBase64,
      payload.PreviewImageBase64,
      payload.base64,
      payload.Base64,
      payload.data,
      payload.Data,
    ];

    for (const candidate of candidates) {
      if (typeof candidate !== 'string' || !candidate.trim()) {
        continue;
      }

      const resolved = resolveImageSource(candidate);
      if (resolved) {
        return resolved;
      }
    }
  } catch {
    return '';
  }

  return '';
};

export const resolveImageSource = (
  raw: string | null | undefined,
): string => {
  if (!raw) {
    return '';
  }

  const trimmed = stripWrappingQuotes(raw.trim());
  if (!trimmed) {
    return '';
  }

  const jsonResolved = tryResolveJsonPayload(trimmed);
  if (jsonResolved) {
    return jsonResolved;
  }

  if (URI_SCHEME_REGEX.test(trimmed)) {
    if (trimmed.toLowerCase().startsWith('data:') && trimmed.includes(',')) {
      const separatorIndex = trimmed.indexOf(',');
      const header = trimmed.slice(0, separatorIndex);
      const payload = normalizeBase64Payload(trimmed.slice(separatorIndex + 1));

      if (header.toLowerCase().includes(';base64')) {
        return `${header},${payload}`;
      }

      return trimmed;
    }

    return trimmed;
  }

  if (
    WINDOWS_DRIVE_PATH_REGEX.test(trimmed) ||
    WINDOWS_UNC_PATH_REGEX.test(trimmed)
  ) {
    return toFileUrl(trimmed);
  }

  const normalizedPayload = normalizeBase64Payload(trimmed);
  if (normalizedPayload.length < 16 || !RAW_BASE64_REGEX.test(normalizedPayload)) {
    return '';
  }

  const mimeType = inferImageMimeType(normalizedPayload);
  return `data:${mimeType};base64,${normalizedPayload}`;
};
