<template>
  <div
    class="glass-card"
    :class="{
      'glass-card--interactive': interactive,
      'glass-card--hover-glow': hoverGlow,
    }"
  >
    <slot></slot>
  </div>
</template>

<script setup lang="ts">
defineProps({
  interactive: {
    type: Boolean,
    default: false,
  },
  hoverGlow: {
    type: Boolean,
    default: false,
  },
});
</script>

<style scoped>
.glass-card {
  background: var(--glass-bg, rgba(255, 255, 255, 0.7));
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid var(--glass-border, rgba(0, 0, 0, 0.05));
  border-radius: var(--radius-lg, 16px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.04);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  overflow: hidden;
}

:global(.dark) .glass-card {
  background: rgba(30, 30, 36, 0.65);
  border: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
}

.glass-card--interactive {
  cursor: pointer;
}

.glass-card--interactive:hover {
  background: var(--glass-bg-hover, rgba(255, 255, 255, 0.95));
  transform: translateY(-2px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.08);
}

:global(.dark) .glass-card--interactive:hover {
  background: rgba(40, 40, 48, 0.75);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.3);
}

.glass-card--hover-glow:hover {
  border-color: rgba(255, 77, 77, 0.4); /* #FF4D4D Red glow */
  box-shadow:
    0 8px 32px rgba(255, 77, 77, 0.1),
    0 0 0 1px rgba(255, 77, 77, 0.2);
}
</style>
