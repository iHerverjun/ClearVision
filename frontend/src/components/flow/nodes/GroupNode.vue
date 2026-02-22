<template>
  <div 
    class="group-node" 
    :class="{ 
      'is-selected': selected,
      'is-collapsed': isCollapsed 
    }"
    :style="groupStyle"
  >
    <!-- Group Header -->
    <div class="group-header" @dblclick="startEditing">
      <div v-if="!isEditing" class="group-title">
        <FolderIcon class="folder-icon" />
        <span class="group-name">{{ data.label || 'Group' }}</span>
        <span class="child-count" v-if="childCount > 0">({{ childCount }})</span>
      </div>
      <input
        v-else
        ref="nameInput"
        v-model="groupName"
        class="group-name-input"
        @blur="finishEditing"
        @keydown.enter="finishEditing"
        @keydown.escape="cancelEditing"
      />
      
      <!-- Collapse Toggle -->
      <button class="collapse-btn" @click.stop="toggleCollapse">
        <ChevronDownIcon class="collapse-icon" :class="{ 'is-collapsed': isCollapsed }" />
      </button>
      
      <!-- Color Picker -->
      <div class="color-picker-wrapper">
        <button class="color-btn" @click.stop="showColorPicker = !showColorPicker">
          <PaletteIcon class="palette-icon" />
        </button>
        <div v-if="showColorPicker" class="color-picker-dropdown">
          <button
            v-for="color in groupColors"
            :key="color.value"
            class="color-option"
            :style="{ backgroundColor: color.value }"
            @click.stop="selectColor(color.value)"
            :title="color.name"
          />
        </div>
      </div>
    </div>
    
    <!-- Group Content Area (children container) -->
    <div v-show="!isCollapsed" class="group-content">
      <!-- Children will be positioned inside this area by Vue Flow -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue';
import { FolderIcon, ChevronDownIcon } from 'lucide-vue-next';
import { useFlowStore } from '../../../stores/flow';

// Custom color palette icon (lucide doesn't have PaletteIcon, use alternative)
const PaletteIcon = {
  template: `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.555C21.965 6.012 17.461 2 12 2z"/></svg>`
};

const props = defineProps({
  id: String,
  data: {
    type: Object,
    default: () => ({})
  },
  selected: Boolean
});

const emit = defineEmits(['update:data']);

const flowStore = useFlowStore();

// State
const isEditing = ref(false);
const isCollapsed = ref(false);
const showColorPicker = ref(false);
const groupName = ref(props.data.label || 'Group');
const nameInput = ref<HTMLInputElement | null>(null);
const selectedColor = ref(props.data.color || 'rgba(232, 72, 85, 0.15)');

// Group color options (Cinnabar-inspired palette)
const groupColors = [
  { name: 'Cinnabar', value: 'rgba(232, 72, 85, 0.15)' },
  { name: 'Sage', value: 'rgba(76, 175, 80, 0.15)' },
  { name: 'Sky', value: 'rgba(33, 150, 243, 0.15)' },
  { name: 'Amber', value: 'rgba(255, 193, 7, 0.15)' },
  { name: 'Violet', value: 'rgba(156, 39, 176, 0.15)' },
  { name: 'Slate', value: 'rgba(96, 125, 139, 0.15)' }
];

// Computed
const childCount = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return (flowStore.nodes as any[]).filter((n: any) => n.parentId === props.id).length;
});

const groupStyle = computed(() => ({
  backgroundColor: selectedColor.value,
  borderColor: selectedColor.value.replace('0.15', '0.4') || 'rgba(232, 72, 85, 0.4)'
}));

// Methods
const startEditing = () => {
  isEditing.value = true;
  groupName.value = props.data.label || 'Group';
  nextTick(() => {
    nameInput.value?.focus();
    nameInput.value?.select();
  });
};

const finishEditing = () => {
  isEditing.value = false;
  emit('update:data', {
    ...props.data,
    label: groupName.value
  });
};

const cancelEditing = () => {
  isEditing.value = false;
  groupName.value = props.data.label || 'Group';
};

const toggleCollapse = () => {
  isCollapsed.value = !isCollapsed.value;
  emit('update:data', {
    ...props.data,
    collapsed: isCollapsed.value
  });
};

const selectColor = (color: string) => {
  selectedColor.value = color;
  showColorPicker.value = false;
  emit('update:data', {
    ...props.data,
    color: color
  });
};

// Initialize from data
onMounted(() => {
  if (props.data.collapsed) {
    isCollapsed.value = true;
  }
  if (props.data.color) {
    selectedColor.value = props.data.color;
  }
});

// Watch for external data changes
watch(() => props.data, (newData) => {
  if (newData.collapsed !== undefined) {
    isCollapsed.value = newData.collapsed;
  }
  if (newData.color) {
    selectedColor.value = newData.color;
  }
}, { deep: true });
</script>

<style scoped>
.group-node {
  min-width: 200px;
  min-height: 120px;
  border-radius: 12px;
  border: 2px dashed;
  background-color: rgba(232, 72, 85, 0.1);
  transition: all 0.2s ease;
  position: relative;
}

.group-node.is-selected {
  border-style: solid;
  box-shadow: 0 0 0 2px rgba(232, 72, 85, 0.5);
}

.group-node.is-collapsed {
  min-height: auto;
}

.group-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 10px 10px 0 0;
  cursor: move;
  user-select: none;
}

.group-title {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  color: rgba(255, 255, 255, 0.9);
  font-size: 13px;
  font-weight: 500;
}

.folder-icon {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.7);
}

.group-name {
  color: inherit;
}

.child-count {
  color: rgba(255, 255, 255, 0.5);
  font-size: 11px;
}

.group-name-input {
  flex: 1;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(232, 72, 85, 0.5);
  border-radius: 4px;
  padding: 2px 6px;
  color: white;
  font-size: 13px;
  font-weight: 500;
  outline: none;
}

.group-name-input:focus {
  border-color: #E84855;
  box-shadow: 0 0 0 2px rgba(232, 72, 85, 0.3);
}

.collapse-btn,
.color-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  background: transparent;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  color: rgba(255, 255, 255, 0.6);
  transition: all 0.15s ease;
}

.collapse-btn:hover,
.color-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.9);
}

.collapse-icon {
  width: 16px;
  height: 16px;
  transition: transform 0.2s ease;
}

.collapse-icon.is-collapsed {
  transform: rotate(-90deg);
}

.palette-icon {
  width: 16px;
  height: 16px;
}

.color-picker-wrapper {
  position: relative;
}

.color-picker-dropdown {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  display: flex;
  gap: 4px;
  padding: 8px;
  background: rgba(30, 30, 30, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  z-index: 100;
}

.color-option {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
  transition: all 0.15s ease;
}

.color-option:hover {
  transform: scale(1.15);
  border-color: rgba(255, 255, 255, 0.5);
}

.group-content {
  min-height: 80px;
  padding: 12px;
}
</style>
