<template>
  <div class="results-page flex flex-1 h-full w-full overflow-hidden relative">
    <ResultsFilterSidebar />
    <ResultsMainView />
    <ResultsDetailPanel />
  </div>
</template>

<script setup lang="ts">
import ResultsFilterSidebar from '../components/results/ResultsFilterSidebar.vue';
import ResultsMainView from '../components/results/ResultsMainView.vue';
import ResultsDetailPanel from '../components/results/ResultsDetailPanel.vue';
</script>

<style scoped>
.results-page {
  /* Fills the .content-area from MainLayout */
}
</style>
