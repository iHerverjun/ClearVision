export const DEFAULT_API_PORT = 5000;
export const MAX_DISCOVERABLE_API_PORT = 5010;
export const API_PORT_STORAGE_KEY = 'cv_api_port';
export const API_PORT_CANDIDATES = Array.from(
    { length: MAX_DISCOVERABLE_API_PORT - DEFAULT_API_PORT + 1 },
    (_, index) => DEFAULT_API_PORT + index
);

export function getSavedApiPort() {
    try {
        const savedPort = window.localStorage.getItem(API_PORT_STORAGE_KEY);
        return savedPort ? Number.parseInt(savedPort, 10) : null;
    } catch {
        return null;
    }
}

export function saveApiPort(port) {
    if (!Number.isInteger(port)) {
        return;
    }

    try {
        window.localStorage.setItem(API_PORT_STORAGE_KEY, port.toString());
    } catch {
        // Ignore storage failures in restricted environments.
    }
}

export function buildLocalApiBaseUrl(port = DEFAULT_API_PORT) {
    return `http://localhost:${port}/api`;
}

export function isHostInjectedEnvironment() {
    const { protocol, hostname } = window.location;
    return protocol === 'file:' || protocol === 'chrome-extension:' || hostname === 'app.local';
}

