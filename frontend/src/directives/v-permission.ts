import type { Directive, DirectiveBinding } from 'vue';
import { useAuthStore } from '../stores/auth';

/**
 * v-permission directive
 * Usage: v-permission="['admin', 'editor']" or v-permission="'admin'"
 * If the user does not have the required role, the element will be removed from the DOM.
 */
export const vPermission: Directive = {
  mounted(el: HTMLElement, binding: DirectiveBinding) {
    checkPermission(el, binding);
  },
  updated(el: HTMLElement, binding: DirectiveBinding) {
    checkPermission(el, binding);
  }
};

function checkPermission(el: HTMLElement, binding: DirectiveBinding) {
  const { value } = binding;
  const authStore = useAuthStore();
  
  // If no value provided, anyone can access
  if (!value) return;
  
  const requiredRoles = Array.isArray(value) ? value : [value];
  if (requiredRoles.length === 0) return;

  const hasRole = authStore.user?.roles?.some(role => requiredRoles.includes(role)) ?? false;

  // If the user doesn't have the required role, hide the element via CSS.
  // DO NOT use removeChild here, as it will crash Vue 3's virtual DOM patcher.
  if (!hasRole) {
    el.style.display = 'none';
    el.setAttribute('data-permission-hidden', 'true');
  } else {
    // If recovering from a hidden state (e.g. user logged in)
    if (el.getAttribute('data-permission-hidden') === 'true') {
      el.style.display = '';
      el.removeAttribute('data-permission-hidden');
    }
  }
}
