import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig, type AxiosResponse } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000, // 30 seconds
  headers: {
    'Content-Type': 'application/json',
  },
});

const getCurrentHashRoute = () => {
  const currentHash = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash;

  return currentHash || '/';
};

// Request Interceptor
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Need to get token dynamically (Vue stores are tricky outside components)
    // Here we assume simple token from localStorage or using useAuthStore dynamically
    const token = localStorage.getItem('auth_token');
    
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error: AxiosError) => {
    return Promise.reject(error);
  }
);

// Response Interceptor
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    return response.data;
  },
  async (error: AxiosError) => {
    // Handle 401 Unauthorized
    if (error.response?.status === 401) {
      // Clear token and redirect to login
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
      
      // If we are in browser environment, redirect
      if (typeof window !== 'undefined') {
        const currentRoute = getCurrentHashRoute();
        if (!currentRoute.startsWith('/login')) {
          window.location.hash = `/login?redirect=${encodeURIComponent(currentRoute)}`;
        }
      }
    }
    
    // Optionally handle other global errors (403, 500, etc.)
    return Promise.reject(error);
  }
);
