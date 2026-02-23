<template>
  <section class="flex-1 relative bg-[var(--color-background)] overflow-hidden flex flex-col">
    <div class="absolute inset-0 bg-dots opacity-30 pointer-events-none"></div>

    <div class="absolute top-6 left-6 flex flex-col bg-[var(--color-surface)] rounded-lg shadow-sm border border-[var(--color-border)] z-10">
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] rounded-t-lg border-b border-[var(--color-border)] transition-colors" @click="zoomIn">
        <ZoomInIcon class="w-5 h-5" />
      </button>
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] border-b border-[var(--color-border)] transition-colors" @click="zoomOut">
        <ZoomOutIcon class="w-5 h-5" />
      </button>
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] rounded-b-lg transition-colors" @click="resetZoom">
        <MaximizeIcon class="w-5 h-5" />
      </button>
    </div>

    <div class="flex-1 relative overflow-auto p-10">
      <div class="min-h-full min-w-full flex items-start justify-center transition-transform duration-150 origin-top" :style="{ transform: `scale(${zoomLevel})` }">
        <div v-if="previewNodes.length === 0" class="text-sm text-[var(--color-text-muted)] mt-24">
          暂无 AI 流程预览
        </div>

        <div v-else class="w-full max-w-5xl">
          <div class="mb-4 text-xs text-[var(--color-text-muted)]">
            预览节点：{{ previewNodes.length }} | 连线：{{ edgeCount }}
          </div>

          <div class="grid gap-4 grid-cols-1 md:grid-cols-2 xl:grid-cols-3">
            <div
              v-for="node in previewNodes"
              :key="node.id"
              class="bg-[var(--color-surface)] rounded-xl shadow-md border border-[var(--color-border)] overflow-hidden"
            >
              <div class="h-8 bg-[var(--color-background)] rounded-t-lg flex items-center px-3 border-b border-[var(--color-border)]">
                <SettingsIcon class="w-4 h-4 text-red-500 mr-2" />
                <span class="text-xs font-bold text-[var(--color-text)] truncate">{{ node.name }}</span>
              </div>
              <div class="p-3 space-y-1">
                <div class="text-[10px] text-[var(--color-text-muted)]">类型：{{ node.type }}</div>
                <div class="text-[10px] text-[var(--color-text-muted)]">输入：{{ node.inputs }}</div>
                <div class="text-[10px] text-[var(--color-text-muted)]">输出：{{ node.outputs }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="absolute bottom-8 left-1/2 transform -translate-x-1/2 z-20">
      <button
        class="bg-red-500 hover:bg-red-600 text-white px-6 py-3 rounded-full shadow-lg flex items-center space-x-2 transition-transform hover:-translate-y-1 font-medium disabled:opacity-50 disabled:cursor-not-allowed"
        :disabled="!parsedFlow"
        @click="applyToFlow"
      >
        <CheckSquareIcon class="w-5 h-5" />
        <span>应用到流程</span>
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
  ZoomInIcon,
  ZoomOutIcon,
  MaximizeIcon,
  CheckSquareIcon,
  SettingsIcon,
} from 'lucide-vue-next';
import { useAiStore } from '../../stores/ai';
import { useFlowStore } from '../../stores/flow';
import { FlowSerializer, type LegacyProjectConfig } from '../../services/flowSerializer';

const aiStore = useAiStore();
const flowStore = useFlowStore();
const router = useRouter();

const zoomLevel = ref(1);

type ParsedFlow =
  | { kind: 'legacy'; payload: LegacyProjectConfig }
  | { kind: 'vueflow'; payload: { nodes: any[]; edges: any[] } };

const parsedFlow = computed<ParsedFlow | null>(() => {
  if (!aiStore.lastGeneratedFlowJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(aiStore.lastGeneratedFlowJson);
    if (parsed?.Nodes && parsed?.Edges) {
      return { kind: 'legacy', payload: parsed as LegacyProjectConfig };
    }

    if (Array.isArray(parsed?.nodes) && Array.isArray(parsed?.edges)) {
      return {
        kind: 'vueflow',
        payload: {
          nodes: parsed.nodes,
          edges: parsed.edges,
        },
      };
    }
  } catch (error) {
    console.error('[AiFlowCanvas] Failed to parse generated flow JSON:', error);
  }

  return null;
});

const previewNodes = computed(() => {
  const parsed = parsedFlow.value;
  if (!parsed) {
    return [];
  }

  if (parsed.kind === 'legacy') {
    return parsed.payload.Nodes.map((node) => ({
      id: node.Id,
      name: node.Name,
      type: node.Type,
      inputs: node.InputPorts?.length || 0,
      outputs: node.OutputPorts?.length || 0,
    }));
  }

  return parsed.payload.nodes.map((node) => ({
    id: String(node.id),
    name: String(node.data?.name || node.id),
    type: String(node.data?.rawType || node.type || '未知'),
    inputs: Array.isArray(node.data?.inputs) ? node.data.inputs.length : 0,
    outputs: Array.isArray(node.data?.outputs) ? node.data.outputs.length : 0,
  }));
});

const edgeCount = computed(() => {
  const parsed = parsedFlow.value;
  if (!parsed) {
    return 0;
  }
  return parsed.kind === 'legacy' ? parsed.payload.Edges.length : parsed.payload.edges.length;
});

const zoomIn = () => {
  zoomLevel.value = Math.min(zoomLevel.value + 0.1, 2.5);
};

const zoomOut = () => {
  zoomLevel.value = Math.max(zoomLevel.value - 0.1, 0.4);
};

const resetZoom = () => {
  zoomLevel.value = 1;
};

const applyToFlow = async () => {
  if (!parsedFlow.value) {
    return;
  }

  if (parsedFlow.value.kind === 'legacy') {
    flowStore.loadLegacyProject(parsedFlow.value.payload);
  } else {
    const legacy = FlowSerializer.vueFlowToLegacy(
      parsedFlow.value.payload.nodes as any,
      parsedFlow.value.payload.edges as any,
    );
    flowStore.loadLegacyProject(legacy);
  }

  await router.push({ name: 'FlowEditor' });
};
</script>

<style scoped>
.bg-dots {
  background-image: radial-gradient(var(--color-border) 1px, transparent 1px);
  background-size: 20px 20px;
}
</style>
