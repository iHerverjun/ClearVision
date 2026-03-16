/**
 * 认证服务 - Token管理和权限检查
 */

import {
    API_PORT_CANDIDATES,
    DEFAULT_API_PORT,
    buildLocalApiBaseUrl,
    getSavedApiPort,
    isHostInjectedEnvironment,
    saveApiPort
} from '../../core/messaging/apiConfig.js';
import { clearAuthSession, getStoredToken, getStoredUser } from './authStorage.js';

/**
 * 获取存储的Token
 */
export function getToken() {
    return getStoredToken();
}

/**
 * 获取当前用户信息
 */
export function getCurrentUser() {
    return getStoredUser();
}

/**
 * 检查是否已登录
 */
export function isAuthenticated() {
    return !!getToken();
}

/**
 * 检查当前用户是否有指定角色
 */
export function hasRole(role) {
    const user = getCurrentUser();
    return user && user.role === role;
}

/**
 * 检查是否是管理员
 */
export function isAdmin() {
    return hasRole('Admin');
}

/**
 * 检查是否是工程师
 */
export function isEngineer() {
    return hasRole('Engineer') || hasRole('Admin');
}

/**
 * 检查是否是操作员（或更高权限）
 */
export function isOperator() {
    const user = getCurrentUser();
    return user && (user.role === 'Operator' || user.role === 'Engineer' || user.role === 'Admin');
}

/**
 * 登出
 */
export function logout() {
    clearAuthSession();
    window.location.href = '/login.html';
}

/**
 * 获取带认证的请求头
 */
export function getAuthHeaders() {
    const token = getToken();
    return token ? { 'Authorization': `Bearer ${token}` } : {};
}

/**
 * 权限守卫 - 检查特定权限
 */
export const PermissionGuard = {
    /**
     * 是否可以编辑项目（Engineer/Admin）
     */
    canEdit() {
        return isEngineer();
    },
    
    /**
     * 是否可以管理用户（Admin）
     */
    canManageUsers() {
        return isAdmin();
    },
    
    /**
     * 是否可以查看系统设置
     */
    canViewSettings() {
        return isEngineer();
    },
    
    /**
     * 是否可以运行检测（所有角色）
     */
    canRunInspection() {
        return isOperator();
    }
};

/**
 * 初始化认证状态检查
 * 应在应用启动时调用
 */
export function initAuth() {
    // 检查是否已登录
    if (!isAuthenticated()) {
        // 未登录，跳转到登录页
        if (!window.location.pathname.includes('/login.html')) {
            window.location.href = '/login.html';
        }
        return false;
    }
    
    // 已登录，更新全局用户信息
    window.currentUser = getCurrentUser();
    return true;
}

/**
 * 异步验证 Token 有效性
 * 调用后端 /api/auth/me 端点
 * @returns {Promise<boolean>}
 */
export async function validateTokenAsync() {
    const token = getToken();
    if (!token) return false;

    try {
        if (window.__API_BASE_URL__) {
            const response = await fetch(`${window.__API_BASE_URL__}/auth/me`, {
                method: 'GET',
                headers: { 'Authorization': `Bearer ${token}` }
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
                        headers: { 'Authorization': `Bearer ${token}` }
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
            headers: { 'Authorization': `Bearer ${token}` }
        });
        return response.ok;
    } catch (e) {
        console.warn('[Auth] Token 验证请求失败:', e.message);
        return false;
    }
}

/**
 * 全局用户信息对象
 * 供其他模块访问当前用户信息
 */
window.currentUser = getCurrentUser();
