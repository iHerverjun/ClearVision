<template>
  <div class="operator-library">
    <div class="library-header">
      <h3 class="title">算子库</h3>
      <div class="search-box">
        <SearchIcon class="search-icon" />
        <input
          type="text"
          v-model="searchQuery"
          placeholder="搜索算子..."
          class="search-input"
        />
      </div>
    </div>

    <div class="library-content">
      <div v-if="isLoading" class="empty-state">
        算子加载中...
      </div>

      <template v-if="!isLoading">
        <div
          v-for="(group, category) in filteredOperators"
          :key="category"
          class="category-group"
        >
          <div
            class="category-header"
            @click="toggleCategory(category as string)"
          >
            <span class="category-name">{{ category }}</span>
            <ChevronDownIcon
              class="chevron-icon"
              :class="{ 'is-collapsed': collapsedCategories[category as string] }"
            />
          </div>

          <div
            class="operator-list"
            v-show="!collapsedCategories[category as string]"
          >
            <div
              v-for="op in group"
              :key="op.type"
              class="operator-item"
              :draggable="true"
              @dragstart="onDragStart($event, op.type)"
            >
              <div class="op-icon-wrapper">
                <component :is="getIconComponent(op.icon)" class="op-icon" />
              </div>
              <div class="op-details">
                <div class="op-name">{{ op.displayName }}</div>
                <div class="op-desc">{{ op.description }}</div>
              </div>
            </div>
          </div>
        </div>
      </template>

      <div
        v-if="!isLoading && Object.keys(filteredOperators).length === 0"
        class="empty-state"
      >
        未找到算子
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, reactive, onMounted } from "vue";
import {
  SearchIcon,
  ChevronDownIcon,
  BoxIcon,
  CameraIcon,
  TargetIcon,
  NetworkIcon,
  FingerprintIcon,
  EyeIcon,
} from "lucide-vue-next";

import {
  getOperatorSchemas,
  loadOperatorSchemas,
  type OperatorSchema,
} from "../../config/operatorSchema";

const searchQuery = ref("");
const collapsedCategories = reactive<Record<string, boolean>>({});
const isLoading = ref(false);
const operatorSchemas = ref<OperatorSchema[]>(getOperatorSchemas());

const filteredOperators = computed(() => {
  const query = searchQuery.value.toLowerCase();
  const grouped: Record<string, OperatorSchema[]> = {};

  operatorSchemas.value.forEach((op) => {
    if (
      `${op.displayName || ""}`.toLowerCase().includes(query) ||
      `${op.description || ""}`.toLowerCase().includes(query)
    ) {
      if (!grouped[op.category]) {
        grouped[op.category] = [];
      }
      grouped[op.category]!.push(op);
    }
  });

  return grouped;
});

onMounted(async () => {
  isLoading.value = true;
  try {
    // Force refresh on each mount to recover from previous transient auth/network fallback.
    operatorSchemas.value = await loadOperatorSchemas(true);
  } finally {
    isLoading.value = false;
  }
});

const toggleCategory = (category: string) => {
  collapsedCategories[category] = !collapsedCategories[category];
};

const getIconComponent = (iconName: string) => {
  switch (iconName) {
    case "camera":
      return CameraIcon;
    case "target":
      return TargetIcon;
    case "fingerprint":
      return FingerprintIcon;
    case "network":
      return NetworkIcon;
    case "eye":
      return EyeIcon;
    default:
      return BoxIcon;
  }
};

const onDragStart = (event: DragEvent, operatorType: string) => {
  if (event.dataTransfer) {
    // We pass the type as simple text, but also you can pass full schema if needed
    event.dataTransfer.setData("application/vueflow", operatorType);
    event.dataTransfer.effectAllowed = "move";
  }
};
</script>

<style scoped>
.operator-library {
  width: 280px;
  height: 100%;
  background: var(--glass-bg, rgba(255, 255, 255, 0.6));
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border-right: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  display: flex;
  flex-direction: column;
}

.library-header {
  padding: 16px;
  border-bottom: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
}

.title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
  margin: 0 0 12px 0;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 10px;
  width: 14px;
  height: 14px;
  color: var(--text-muted, #64748b);
}

.search-input {
  width: 100%;
  height: 32px;
  padding: 0 12px 0 32px;
  background: rgba(255, 255, 255, 0.7);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.1));
  border-radius: 8px;
  font-family: inherit;
  font-size: 13px;
  color: var(--text-primary, #1c1c1e);
  outline: none;
  transition: all 0.2s ease;
}

.search-input:focus {
  border-color: var(--accent-red, #ff4d4d);
  box-shadow: 0 0 0 2px rgba(255, 77, 77, 0.1);
  background: #ffffff;
}

.library-content {
  flex: 1;
  overflow-y: auto;
  padding: 12px 0;
}

/* Custom Scrollbar for the library */
.library-content::-webkit-scrollbar {
  width: 6px;
}
.library-content::-webkit-scrollbar-track {
  background: transparent;
}
.library-content::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.1);
  border-radius: 3px;
}

.category-group {
  margin-bottom: 8px;
}

.category-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  cursor: pointer;
  user-select: none;
  transition: background-color 0.2s;
}

.category-header:hover {
  background: rgba(0, 0, 0, 0.02);
}

.category-name {
  font-size: 11px;
  font-weight: 700;
  color: var(--text-muted, #64748b);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.chevron-icon {
  width: 14px;
  height: 14px;
  color: var(--text-muted, #64748b);
  transition: transform 0.2s ease;
}

.chevron-icon.is-collapsed {
  transform: rotate(-90deg);
}

.operator-list {
  padding: 4px 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.operator-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 10px;
  background: rgba(255, 255, 255, 0.8);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.04));
  border-radius: 10px;
  cursor: grab;
  transition: all 0.2s ease;
}

.operator-item:hover {
  background: #ffffff;
  border-color: rgba(255, 77, 77, 0.2);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.04);
  transform: translateY(-1px);
}

.operator-item:active {
  cursor: grabbing;
  transform: scale(0.98);
}

.op-icon-wrapper {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  background: rgba(0, 0, 0, 0.03);
  border-radius: 8px;
  color: var(--accent-red, #ff4d4d);
}

.op-icon {
  width: 14px;
  height: 14px;
  stroke-width: 2.5px;
}

.op-details {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.op-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
}

.op-desc {
  font-size: 11px;
  color: var(--text-muted, #64748b);
  line-height: 1.3;
}

.empty-state {
  padding: 32px 16px;
  text-align: center;
  font-size: 13px;
  color: var(--text-muted, #64748b);
}
</style>
