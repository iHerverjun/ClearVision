const THEME_STORAGE_KEY = 'cv_theme';
const DEFAULT_THEME = 'dark';
const VALID_THEMES = new Set(['light', 'dark']);

function getRoot() {
    return document.documentElement;
}

export function normalizeTheme(theme, fallback = DEFAULT_THEME) {
    const candidate = `${theme || ''}`.trim().toLowerCase();
    return VALID_THEMES.has(candidate) ? candidate : fallback;
}

export function getStoredTheme() {
    try {
        return normalizeTheme(window.localStorage.getItem(THEME_STORAGE_KEY), null);
    } catch {
        return null;
    }
}

export function getAppliedTheme() {
    return normalizeTheme(getRoot().dataset.theme, DEFAULT_THEME);
}

export function setStoredTheme(theme) {
    try {
        window.localStorage.setItem(THEME_STORAGE_KEY, normalizeTheme(theme));
    } catch {
        // Ignore storage failures in restricted environments.
    }
}

export function applyTheme(theme, { persist = false } = {}) {
    const nextTheme = normalizeTheme(theme);
    getRoot().dataset.theme = nextTheme;
    getRoot().style.colorScheme = nextTheme;

    if (persist) {
        setStoredTheme(nextTheme);
    }

    return nextTheme;
}

export function bootstrapTheme() {
    const initialTheme = getStoredTheme() || normalizeTheme(getRoot().dataset.theme, DEFAULT_THEME);
    return applyTheme(initialTheme);
}

export function toggleTheme() {
    const nextTheme = getAppliedTheme() === 'dark' ? 'light' : 'dark';
    return applyTheme(nextTheme, { persist: true });
}

export async function syncThemeWithSettings(loadSettings, { expectedTheme = getAppliedTheme() } = {}) {
    if (typeof loadSettings !== 'function') {
        return getAppliedTheme();
    }

    try {
        const settings = await loadSettings();
        const configuredTheme = normalizeTheme(settings?.general?.theme, null);

        if (!configuredTheme) {
            return getAppliedTheme();
        }

        const normalizedExpectedTheme = normalizeTheme(expectedTheme, null);
        if (normalizedExpectedTheme && getAppliedTheme() !== normalizedExpectedTheme) {
            return getAppliedTheme();
        }

        return applyTheme(configuredTheme, { persist: true });
    } catch {
        return getAppliedTheme();
    }
}

export function bindThemeToggle(button, onThemeChanged) {
    if (!button || button.dataset.cvThemeBound === 'true') {
        return;
    }

    button.dataset.cvThemeBound = 'true';
    button.addEventListener('click', () => {
        const previousTheme = getAppliedTheme();
        const nextTheme = previousTheme === 'dark' ? 'light' : 'dark';

        if (typeof onThemeChanged === 'function') {
            onThemeChanged({ previousTheme, nextTheme });
            return;
        }

        applyTheme(nextTheme, { persist: true });
    });
}

export { DEFAULT_THEME, THEME_STORAGE_KEY };
