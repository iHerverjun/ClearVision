/**
 * 认证服务 - Token管理和权限检查
 */

// Token存储键名
const TOKEN_KEY = 'cv_auth_token';
const USER_KEY = 'cv_current_user';

/**
 * 获取存储的Token
 */
export function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

/**
 * 获取当前用户信息
 */
export function getCurrentUser() {
    const userJson = localStorage.getItem(USER_KEY);
    return userJson ? JSON.parse(userJson) : null;
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
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
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
        // 构建 API URL (复制 httpClient 的发现逻辑)
        let baseUrl = window.__API_BASE_URL__;
        if (!baseUrl) {
            const { protocol, hostname, port } = window.location;
            if (protocol === 'file:' || protocol === 'chrome-extension:' || hostname === 'app.local') {
                const savedPort = localStorage.getItem('cv_api_port');
                baseUrl = `http://localhost:${savedPort || 5000}/api`;
            } else {
                baseUrl = `${protocol}//${hostname}:${port || 5000}/api`;
            }
        }

        const response = await fetch(`${baseUrl}/auth/me`, {
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
