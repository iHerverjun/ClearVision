import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { apiClient } from '../services/api';
import { ENDPOINTS } from '../services/endpoints';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';

export interface User {
  id: string;
  username: string;
  roles?: string[];
  permissions?: string[];
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null);
  const token = ref<string | null>(localStorage.getItem('auth_token'));
  
  const isAuthenticated = computed(() => !!token.value);
  const isAdmin = computed(() => user.value?.roles?.includes('admin') || false);
  const currentUser = computed(() => user.value);

  function setAuthData(newToken: string, newUserData: User) {
    token.value = newToken;
    user.value = newUserData;
    localStorage.setItem('auth_token', newToken);
    localStorage.setItem('auth_user', JSON.stringify(newUserData));
  }

  function clearAuthData() {
    token.value = null;
    user.value = null;
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
  }

  async function login(username: string, password: string):Promise<boolean> {
    try {
      // Always login through REST to obtain a valid backend session token.
      // WebView2 bridge login messages are optional and should not be the source of truth.
      const w = window as any;
      const isWebViewBridgeReady = w.chrome && w.chrome.webview;

      const res: any = await apiClient.post(ENDPOINTS.Auth.Login, { username, password });
      if (res && res.token) {
        setAuthData(res.token, res.user || { id: username, username });

        if (isWebViewBridgeReady) {
          webMessageBridge.sendMessage(BridgeMessageType.AuthLogin, { username }, false)
            .catch(() => { /* optional notification; backend may ignore */ });
        }

        return true;
      }

      return false;
      
    } catch (err: any) {
      if (err?.response?.status === 401) {
        return false;
      }

      console.error('[AuthStore] Login failed:', err);
      throw new Error(err.response?.data?.message || err.message || '登录失败');
    }
  }

  async function logout() {
    try {
      const w = window as any;
      const isWebViewBridgeReady = w.chrome && w.chrome.webview;
      await apiClient.post(ENDPOINTS.Auth.Logout, {}).catch(() => {});

      if (isWebViewBridgeReady) {
        await webMessageBridge.sendMessage(BridgeMessageType.AuthLogout, {}, false)
          .catch(() => {});
      }
    } finally {
      clearAuthData();
    }
  }

  async function checkAuth() {
    if (!token.value) {
      return;
    }

    try {
      const storedUser = localStorage.getItem('auth_user');
      if (storedUser) {
        user.value = JSON.parse(storedUser);
      }

      // Validate local token with backend to avoid stale session after app restart.
      const me: any = await apiClient.get(ENDPOINTS.Auth.Me);
      const resolvedRole = typeof me?.role === 'string' ? me.role.toLowerCase() : undefined;

      const normalizedUser: User = {
        id: String(me?.userId ?? me?.id ?? user.value?.id ?? ''),
        username: String(me?.username ?? user.value?.username ?? ''),
        roles: resolvedRole ? [resolvedRole] : user.value?.roles,
        permissions: user.value?.permissions,
      };

      user.value = normalizedUser;
      localStorage.setItem('auth_user', JSON.stringify(normalizedUser));
    } catch (err) {
      clearAuthData();
    }
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    currentUser,
    login,
    logout,
    checkAuth
  };
});
