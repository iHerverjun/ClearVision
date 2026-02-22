<template>
  <button
    class="icon-btn"
    :class="[
      `icon-btn--${variant}`,
      `icon-btn--${size}`,
      { 'is-active': active, 'is-loading': loading },
    ]"
    :disabled="disabled || loading"
    :title="title"
    @click="$emit('click', $event)"
  >
    <span v-if="loading" class="icon-btn__spinner"></span>
    <slot v-else></slot>
  </button>
</template>

<script setup lang="ts">
defineProps({
  variant: {
    type: String,
    default: "ghost",
    validator: (val: string) =>
      ["ghost", "primary", "danger", "glass"].includes(val),
  },
  size: {
    type: String,
    default: "md",
    validator: (val: string) => ["sm", "md", "lg"].includes(val),
  },
  active: Boolean,
  disabled: Boolean,
  loading: Boolean,
  title: String,
});

defineEmits(["click"]);
</script>

<style scoped>
.icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.2, 0, 0, 1);
  color: var(--text-muted, #64748b);
  background: transparent;
  outline: none;
  position: relative;
  overflow: hidden;
}

.icon-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Sizes */
.icon-btn--sm {
  width: 28px;
  height: 28px;
  /* Icon sizing assumes SVG is passed via slot */
  font-size: 14px;
}

.icon-btn--md {
  width: 36px;
  height: 36px;
  font-size: 18px;
}

.icon-btn--lg {
  width: 44px;
  height: 44px;
  font-size: 22px;
}

/* Variants */
.icon-btn--ghost:hover:not(:disabled) {
  background: var(--hover-overlay, rgba(0, 0, 0, 0.05));
  color: var(--text-primary, #1c1c1e);
}

.icon-btn--ghost.is-active {
  background: var(--active-overlay, rgba(255, 77, 77, 0.1));
  color: var(--accent-red, #ff4d4d);
}

.icon-btn--primary {
  background: var(--accent-red, #ff4d4d);
  color: #ffffff;
}

.icon-btn--primary:hover:not(:disabled) {
  background: var(--accent-red-hover, #e63946);
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(255, 77, 77, 0.25);
}

.icon-btn--danger {
  color: #ef4444;
}

.icon-btn--danger:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.1);
}

.icon-btn--glass {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(8px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  color: var(--text-primary, #1c1c1e);
}

.icon-btn--glass:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.9);
  border-color: rgba(0, 0, 0, 0.1);
  color: var(--accent-red, #ff4d4d);
}

/* Default legacy dark overrides */
:global(.dark) .icon-btn--ghost {
  color: #9ba1a6;
}
:global(.dark) .icon-btn--ghost:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.08);
  color: #ffffff;
}
:global(.dark) .icon-btn--glass {
  background: rgba(40, 40, 48, 0.5);
  border-color: rgba(255, 255, 255, 0.05);
  color: #9ba1a6;
}
:global(.dark) .icon-btn--glass:hover:not(:disabled) {
  background: rgba(60, 60, 70, 0.6);
  border-color: rgba(255, 255, 255, 0.15);
  color: #ffffff;
}

/* Spinner */
.icon-btn__spinner {
  width: 60%;
  height: 60%;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: s-spin 0.75s linear infinite;
}

@keyframes s-spin {
  to {
    transform: rotate(360deg);
  }
}

/* Ensure SVG icons passed via slot take correct size */
:deep(svg) {
  width: 1em;
  height: 1em;
  stroke-width: 2px;
  transition: all 0.2s ease;
}
</style>
