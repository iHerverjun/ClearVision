<template>
  <div class="login-wrapper">
    <!-- Animated background elements for modern look -->
    <div class="bg-shape shape-1"></div>
    <div class="bg-shape shape-2"></div>
    <div class="bg-shape shape-3"></div>

    <div class="login-container glass-panel">
      <div class="login-header">
        <h1 class="logo-text">Clear<span>Vision</span></h1>
        <p class="subtitle">AI 驱动的机器视觉平台</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="input-group">
          <label for="username">用户名</label>
          <input
            type="text"
            id="username"
            v-model="username"
            required
            autocomplete="username"
            placeholder="请输入用户名"
            ref="usernameInput"
          />
        </div>

        <div class="input-group">
          <label for="password">密码</label>
          <input
            type="password"
            id="password"
            v-model="password"
            required
            autocomplete="current-password"
            placeholder="请输入密码"
          />
        </div>

        <div v-if="errorMessage" class="error-message">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
          {{ errorMessage }}
        </div>

        <button type="submit" class="login-btn" :disabled="isLoading">
          <span v-if="isLoading" class="spinner"></span>
          <span v-else>登录</span>
        </button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter, useRoute } from "vue-router";
import { useAuthStore } from "../stores/auth";

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

const username = ref("");
const password = ref("");
const errorMessage = ref("");
const isLoading = ref(false);
const usernameInput = ref<HTMLInputElement | null>(null);

onMounted(() => {
  if (usernameInput.value) {
    usernameInput.value.focus();
  }
});

const handleLogin = async () => {
  errorMessage.value = "";
  isLoading.value = true;

  try {
    const success = await authStore.login(username.value, password.value);
    if (success) {
      // Navigate to intended route or default
      const redirect = (route.query.redirect as string) || "/flow-editor";
      router.push(redirect);
    } else {
      errorMessage.value = "用户名或密码错误";
    }
  } catch (err: any) {
    errorMessage.value =
      err.message || "登录失败，请检查连接。";
  } finally {
    isLoading.value = false;
  }
};
</script>

<style scoped>
/* Glassmorphism & Modern Theme */
.login-wrapper {
  position: relative;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(
    --bg-primary,
    #1e1e24
  ); /* Fallback to dark if var missing */
  overflow: hidden;
  font-family:
    "Inter",
    system-ui,
    -apple-system,
    sans-serif;
}

/* Abstract Background Shapes */
.bg-shape {
  position: absolute;
  filter: blur(80px);
  z-index: 0;
  opacity: 0.5;
  border-radius: 50%;
  animation: float 10s infinite ease-in-out alternate;
}

.shape-1 {
  width: 400px;
  height: 400px;
  background: radial-gradient(circle, #e84855 0%, rgba(232, 72, 85, 0) 70%);
  top: -100px;
  left: -100px;
}

.shape-2 {
  width: 500px;
  height: 500px;
  background: radial-gradient(circle, #2b2b36 0%, rgba(43, 43, 54, 0) 70%);
  bottom: -150px;
  right: -100px;
  animation-delay: -5s;
}

.shape-3 {
  width: 300px;
  height: 300px;
  background: radial-gradient(circle, #e84855 0%, rgba(232, 72, 85, 0) 70%);
  bottom: 20%;
  left: 20%;
  opacity: 0.3;
  animation-delay: -2s;
}

@keyframes float {
  0% {
    transform: translateY(0) scale(1);
  }
  100% {
    transform: translateY(-30px) scale(1.1);
  }
}

/* Glass Panel */
.glass-panel {
  position: relative;
  z-index: 1;
  width: 100%;
  max-width: 420px;
  padding: 40px;
  background: rgba(30, 30, 36, 0.6);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  box-shadow: 0 24px 48px rgba(0, 0, 0, 0.4);
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.logo-text {
  font-size: 32px;
  font-weight: 700;
  color: #ffffff;
  margin: 0 0 8px 0;
  letter-spacing: -0.5px;
}

.logo-text span {
  color: #e84855; /* Cinnabar Red */
}

.subtitle {
  color: #9ba1a6;
  font-size: 14px;
  margin: 0;
}

/* Form Styles */
.login-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.input-group label {
  color: #d1d5db;
  font-size: 13px;
  font-weight: 500;
  margin-left: 4px;
}

.input-group input {
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 12px 16px;
  color: white;
  font-size: 15px;
  outline: none;
  transition: all 0.2s ease;
}

.input-group input:focus {
  border-color: #e84855;
  background: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(232, 72, 85, 0.2);
}

.input-group input::placeholder {
  color: #6b7280;
}

/* Error Message */
.error-message {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #ef4444;
  background: rgba(239, 68, 68, 0.1);
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 13px;
  border: 1px solid rgba(239, 68, 68, 0.2);
}

/* Login Button */
.login-btn {
  background: #e84855;
  color: white;
  border: none;
  border-radius: 8px;
  padding: 14px;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  margin-top: 8px;
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 48px;
}

.login-btn:hover:not(:disabled) {
  background: #f05461;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(232, 72, 85, 0.3);
}

.login-btn:active:not(:disabled) {
  transform: translateY(0);
}

.login-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

/* Spinner */
.spinner {
  width: 20px;
  height: 20px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 50%;
  border-top-color: white;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
