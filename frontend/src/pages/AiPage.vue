<template>
  <div class="ai-page flex flex-1 h-full w-full overflow-hidden relative">
    <AiChatSidebar />
    <AiFlowCanvas />
    <AiInsightsPanel />
  </div>
</template>

<script setup lang="ts">
import AiChatSidebar from '../components/ai/AiChatSidebar.vue';
import AiFlowCanvas from '../components/ai/AiFlowCanvas.vue';
import AiInsightsPanel from '../components/ai/AiInsightsPanel.vue';
</script>

<style scoped>
.ai-page {
  /* Fills the .content-area from MainLayout */
}
</style>
