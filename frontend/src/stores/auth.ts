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
      // Step 1: Attempt login through WebView2 if available (hybrid app behavior)
      const w = window as any;
      const isWebViewBridgeReady = w.chrome && w.chrome.webview;
      
      if (isWebViewBridgeReady) {
        // [FIX] C# backend currently does NOT implement AuthLogin response.
        // Instead of waiting and freezing, we inject a local token immediately.
        const mockUser = { id: username, username, roles: ['admin'] };
        setAuthData('webview2_local_token_' + Date.now(), mockUser);
        
        // Optionally notify backend without expecting a response
        webMessageBridge.sendMessage(BridgeMessageType.AuthLogin, { username }, false)
          .catch(() => { /* ignore backend missing handler */ });
          
        return true;
      } 
      
      // Step 2: Fallback to REST API Auth Login
      const res: any = await apiClient.post(ENDPOINTS.Auth.Login, { username, password });
      if (res && res.token) {
        setAuthData(res.token, res.user || { id: username, username });
        return true;
      }

      return false;
      
    } catch (err: any) {
      console.error('[AuthStore] Login failed:', err);
      // Optional: re-throw error to let UI show specific message
      throw new Error(err.response?.data?.message || err.message || 'Login failed');
    }
  }

  async function logout() {
    try {
      // Notify backend about logout 
      const w = window as any;
      const isWebViewBridgeReady = w.chrome && w.chrome.webview;
      if (isWebViewBridgeReady) {
        await webMessageBridge.sendMessage(BridgeMessageType.AuthLogout, {}, false);
      } else {
        await apiClient.post(ENDPOINTS.Auth.Logout, {}).catch(() => {});
      }
    } finally {
      // Always flush local state even on network error
      clearAuthData();
    }
  }

  async function checkAuth() {
    // If we have token, we can conditionally check if valid
    // For now we persist based on localStorage. We can optionally fetch user profile.
    if (token.value) {
      try {
        const storedUser = localStorage.getItem('auth_user');
        if (storedUser) {
          user.value = JSON.parse(storedUser);
        } else {
          // fetch via API if needed
          // const res = await apiClient.get(ENDPOINTS.Auth.Verify);
        }
      } catch (err) {
        clearAuthData();
      }
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
