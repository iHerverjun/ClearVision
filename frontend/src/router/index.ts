import { createRouter, createWebHashHistory, type RouteRecordRaw } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const routes: Array<RouteRecordRaw> = [
  {
    path: '/',
    component: () => import('../layouts/MainLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        redirect: 'flow-editor'
      },
      {
        path: 'flow-editor',
        name: 'FlowEditor',
        component: () => import('../pages/FlowEditorPage.vue')
      },
      {
        path: 'inspection',
        name: 'Inspection',
        component: () => import('../pages/InspectionPage.vue')
      },
      {
        path: 'results',
        name: 'Results',
        component: () => import('../pages/ResultsPage.vue')
      },
      {
        path: 'projects',
        name: 'Projects',
        component: () => import('../pages/ProjectsPage.vue')
      },
      {
        path: 'ai-assistant',
        name: 'AiAssistant',
        component: () => import('../pages/AiPage.vue')
      },
      {
        path: 'ai',
        redirect: { name: 'AiAssistant' }
      },
      {
        path: 'settings',
        name: 'Settings',
        redirect: { name: 'FlowEditor', query: { modal: 'settings' } }
      }
    ]
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('../pages/LoginPage.vue'),
    meta: { requiresAuth: false }
  },
  {
    // Catch all
    path: '/:pathMatch(.*)*',
    redirect: '/flow-editor'
  }
];

const router = createRouter({
  history: createWebHashHistory(),
  routes
});

// Navigation Guards
router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore();
  
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next({ name: 'Login', query: { redirect: to.fullPath } });
  } 
  else if (to.name === 'Login' && authStore.isAuthenticated) {
    next({ name: 'FlowEditor' });
  } 
  else {
    next();
  }
});

export default router;
