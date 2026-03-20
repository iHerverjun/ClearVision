/**
 * 认证服务 - Token 管理和权限检查
 */

import {
    API_PORT_CANDIDATES,
    DEFAULT_API_PORT,
    buildLocalApiBaseUrl,
    getSavedApiPort,
    isHostInjectedEnvironment,
    saveApiPort
} from '../../core/messaging/apiConfig.js';
import httpClient from '../../core/messaging/httpClient.js';
import { clearAuthSession, getStoredToken, getStoredUser } from './authStorage.js';

function buildAppUrl(relativePath) {
    return new URL(relativePath, window.location.href).toString();
}

export function getToken() {
    return getStoredToken();
}

export function getCurrentUser() {
    return getStoredUser();
}

export function isAuthenticated() {
    return !!getToken();
}

export function hasRole(role) {
    const user = getCurrentUser();
    return user && user.role === role;
}

export function isAdmin() {
    return hasRole('Admin');
}

export function isEngineer() {
    return hasRole('Engineer') || hasRole('Admin');
}

export function isOperator() {
    const user = getCurrentUser();
    return user && (user.role === 'Operator' || user.role === 'Engineer' || user.role === 'Admin');
}

export async function logout() {
    try {
        if (getToken()) {
            await httpClient.post('/auth/logout');
        }
    } catch (error) {
        console.warn('[Auth] 服务端登出失败，将继续清理本地会话。', error);
    } finally {
        clearAuthSession();
        window.location.href = buildAppUrl('./login.html');
    }
}

export function getAuthHeaders() {
    const token = getToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
}

export const PermissionGuard = {
    canEdit() {
        return isEngineer();
    },

    canManageUsers() {
        return isAdmin();
    },

    canViewSettings() {
        return isEngineer();
    },

    canRunInspection() {
        return isOperator();
    }
};

export function initAuth() {
    const token = getToken();
    const user = getCurrentUser();

    if (!token || !user) {
        clearAuthSession();
        if (!window.location.pathname.includes('/login.html')) {
            window.location.href = buildAppUrl('./login.html');
        }
        return false;
    }

    window.currentUser = user;
    return true;
}

export async function validateTokenAsync() {
    const token = getToken();
    if (!token) return false;

    try {
        if (window.__API_BASE_URL__) {
            const response = await fetch(`${window.__API_BASE_URL__}/auth/me`, {
                method: 'GET',
                headers: { Authorization: `Bearer ${token}` }
            });
            return response.ok;
        }

        if (isHostInjectedEnvironment()) {
            const candidatePorts = [];
            const savedPort = getSavedApiPort();

            if (savedPort) {
                candidatePorts.push(savedPort);
            }

            API_PORT_CANDIDATES
                .filter(port => port !== savedPort)
                .forEach(port => candidatePorts.push(port));

            for (const port of candidatePorts) {
                try {
                    const response = await fetch(`${buildLocalApiBaseUrl(port)}/auth/me`, {
                        method: 'GET',
                        headers: { Authorization: `Bearer ${token}` }
                    });

                    if (response.ok) {
                        saveApiPort(port);
                        return true;
                    }
                } catch {
                    // Try the next candidate port.
                }
            }

            return false;
        }

        const { protocol, hostname, port } = window.location;
        const response = await fetch(`${protocol}//${hostname}:${port || DEFAULT_API_PORT}/api/auth/me`, {
            method: 'GET',
            headers: { Authorization: `Bearer ${token}` }
        });
        return response.ok;
    } catch (e) {
        console.warn('[Auth] Token 验证请求失败:', e.message);
        return false;
    }
}

window.currentUser = getCurrentUser();