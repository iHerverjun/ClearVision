<template>
  <div class="inspection-page flex flex-1 h-full w-full overflow-hidden relative">
    <InspectionControls />
    <ImageViewer />
    <NodeOutputPanel />
  </div>
</template>

<script setup lang="ts">
import InspectionControls from '../components/inspection/InspectionControls.vue';
import ImageViewer from '../components/inspection/ImageViewer.vue';
import NodeOutputPanel from '../components/inspection/NodeOutputPanel.vue';
</script>

<style scoped>
.inspection-page {
  /* Fills the .content-area from MainLayout */
}
</style>
