<template>
  <div class="results-page flex flex-1 h-full w-full overflow-hidden relative">
    <ResultsFilterSidebar />
    <ResultsMainView />
    <ResultsDetailPanel />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import ResultsFilterSidebar from '../components/results/ResultsFilterSidebar.vue';
import ResultsMainView from '../components/results/ResultsMainView.vue';
import ResultsDetailPanel from '../components/results/ResultsDetailPanel.vue';
import { useResultsStore } from '../stores/results';

const resultsStore = useResultsStore();

onMounted(async () => {
  await resultsStore.loadRecords();
});
</script>

<style scoped>
.results-page {
  /* Fills the .content-area from MainLayout */
}
</style>
