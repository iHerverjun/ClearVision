const URI_SCHEME_REGEX = /^(data:|blob:|https?:|file:)/i;
const WINDOWS_DRIVE_PATH_REGEX = /^[a-zA-Z]:[\\/]/;
const WINDOWS_UNC_PATH_REGEX = /^\\\\/;

const toFileUrl = (filePath: string): string => {
  const normalized = filePath.replace(/\\/g, '/');

  if (normalized.startsWith('//')) {
    return `file:${encodeURI(normalized)}`;
  }

  const prefix = normalized.startsWith('/') ? 'file://' : 'file:///';
  return `${prefix}${encodeURI(normalized)}`;
};

export const resolveImageSource = (raw: string | null | undefined): string => {
  if (!raw) {
    return '';
  }

  const trimmed = raw.trim();
  if (!trimmed) {
    return '';
  }

  if (URI_SCHEME_REGEX.test(trimmed)) {
    return trimmed;
  }

  if (WINDOWS_DRIVE_PATH_REGEX.test(trimmed) || WINDOWS_UNC_PATH_REGEX.test(trimmed)) {
    return toFileUrl(trimmed);
  }

  return `data:image/png;base64,${trimmed}`;
};
