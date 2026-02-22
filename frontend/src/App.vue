<script setup lang="ts">
import { RouterView } from "vue-router";
import SettingsModal from "./components/settings/SettingsModal.vue";
</script>

<template>
  <div
    class="h-screen w-screen overflow-hidden bg-[var(--color-background)] text-[var(--color-text)] relative"
  >
    <!-- Base Layout Router View -->
    <RouterView />

    <!-- Global Modals -->
    <SettingsModal />
  </div>
</template>

