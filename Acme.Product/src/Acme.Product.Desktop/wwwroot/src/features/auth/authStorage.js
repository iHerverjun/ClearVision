const TOKEN_KEY = 'cv_auth_token';
const USER_KEY = 'cv_current_user';

function getSessionStore() {
    try {
        return window.sessionStorage;
    } catch {
        return null;
    }
}

function getLocalStore() {
    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

function migrateLegacyValue(key) {
    const sessionStore = getSessionStore();
    const localStore = getLocalStore();

    const sessionValue = sessionStore?.getItem(key);
    if (sessionValue) {
        return sessionValue;
    }

    const legacyValue = localStore?.getItem(key);
    if (!legacyValue) {
        return null;
    }

    sessionStore?.setItem(key, legacyValue);
    localStore?.removeItem(key);
    return legacyValue;
}

export function getStoredToken() {
    return migrateLegacyValue(TOKEN_KEY);
}

export function getStoredUser() {
    const userJson = migrateLegacyValue(USER_KEY);
    return userJson ? JSON.parse(userJson) : null;
}

export function storeAuthSession(token, user) {
    const sessionStore = getSessionStore();
    const localStore = getLocalStore();

    if (token) {
        sessionStore?.setItem(TOKEN_KEY, token);
        localStore?.removeItem(TOKEN_KEY);
    }

    if (user) {
        sessionStore?.setItem(USER_KEY, JSON.stringify(user));
        localStore?.removeItem(USER_KEY);
    }
}

export function clearAuthSession() {
    const sessionStore = getSessionStore();
    const localStore = getLocalStore();

    sessionStore?.removeItem(TOKEN_KEY);
    sessionStore?.removeItem(USER_KEY);
    localStore?.removeItem(TOKEN_KEY);
    localStore?.removeItem(USER_KEY);
}

