<template>
  <div class="projects-page flex flex-1 h-full w-full overflow-hidden relative">
    <ProjectSidebar />
    <ProjectDashboard />
  </div>
</template>

<script setup lang="ts">
import ProjectSidebar from '../components/projects/ProjectSidebar.vue';
import ProjectDashboard from '../components/projects/ProjectDashboard.vue';
</script>

<style scoped>
.projects-page {
  /* Fills the .content-area from MainLayout */
}
</style>
