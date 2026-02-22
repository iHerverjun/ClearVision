<script setup lang="ts">
import { RouterView } from "vue-router";
</script>

<template>
  <div
    class="h-screen w-screen overflow-hidden bg-[var(--color-background)] text-[var(--color-text)]"
  >
    <!-- Base Layout Router View -->
    <RouterView />
  </div>
</template>
