export const ENDPOINTS = {
  // Auth
  Auth: {
    Login: '/api/auth/login',
    Logout: '/api/auth/logout',
    Me: '/api/auth/me',
    Verify: '/api/auth/verify',
  },
  // Projects
  Project: {
    List: '/api/projects',
    Get: (id: string) => `/api/projects/${id}`,
    Create: '/api/projects',
    Update: (id: string) => `/api/projects/${id}`,
    Delete: (id: string) => `/api/projects/${id}`,
  },
  // Flows
  Flow: {
    Execute: '/api/flow/execute',
    Status: '/api/flow/status',
  },
  // Settings
  Settings: {
    Get: '/api/settings',
    Update: '/api/settings',
  },
  // Users
  User: {
    List: '/api/users',
    Get: (id: string) => `/api/users/${id}`,
    Create: '/api/users',
    Update: (id: string) => `/api/users/${id}`,
    Delete: (id: string) => `/api/users/${id}`,
  },
  // Deep Learning / AI 
  AI: {
    Generate: '/api/ai/generate'
  }
};
