<template>
  <Teleport to="body">
    <div
      v-if="isVisible"
      class="context-menu"
      :style="{ left: `${position.x}px`, top: `${position.y}px` }"
      @click.stop
    >
      <template v-if="contextType === 'pane'">
        <div class="menu-item" @click="handleAddNode">
          <PlusIcon class="menu-icon" />
          <span>添加节点</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handlePaste" :class="{ disabled: !hasClipboard }">
          <ClipboardIcon class="menu-icon" />
          <span>粘贴</span>
          <span class="shortcut">Ctrl+V</span>
        </div>
        <div class="menu-item" @click="handleSelectAll">
          <CheckSquareIcon class="menu-icon" />
          <span>全选</span>
          <span class="shortcut">Ctrl+A</span>
        </div>
      </template>

      <template v-else-if="contextType === 'node'">
        <div class="menu-item" @click="handleCopy">
          <CopyIcon class="menu-icon" />
          <span>复制</span>
          <span class="shortcut">Ctrl+C</span>
        </div>
        <div class="menu-item" @click="handleDuplicate">
          <CopyPlusIcon class="menu-icon" />
          <span>复制节点</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handleCopyNodeId">
          <HashIcon class="menu-icon" />
          <span>复制节点 ID</span>
        </div>
        <div class="menu-item" @click="handleViewOutput">
          <ImageIcon class="menu-icon" />
          <span>查看输出</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item danger" @click="handleDeleteNode">
          <TrashIcon class="menu-icon" />
          <span>删除</span>
          <span class="shortcut">Del</span>
        </div>
      </template>

      <template v-else-if="contextType === 'edge'">
        <div class="menu-item danger" @click="handleDeleteEdge">
          <TrashIcon class="menu-icon" />
          <span>删除连接</span>
        </div>
        <div class="menu-item" @click="handleInsertReroute">
          <PlusIcon class="menu-icon" />
          <span>插入 Reroute 节点</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-section-title">连线样式</div>
        <div
          class="menu-item style-item"
          :class="{ active: currentEdgeStyle === 'bezier' }"
          @click="handleSetEdgeStyle('bezier')"
        >
          <CheckIcon class="menu-icon active-check" />
          <span>Bezier</span>
        </div>
        <div
          class="menu-item style-item"
          :class="{ active: currentEdgeStyle === 'smoothstep' }"
          @click="handleSetEdgeStyle('smoothstep')"
        >
          <CheckIcon class="menu-icon active-check" />
          <span>SmoothStep</span>
        </div>
        <div
          class="menu-item style-item"
          :class="{ active: currentEdgeStyle === 'pathfinding' }"
          @click="handleSetEdgeStyle('pathfinding')"
        >
          <CheckIcon class="menu-icon active-check" />
          <span>Pathfinding</span>
        </div>
      </template>

      <template v-else-if="contextType === 'selection'">
        <div class="menu-item" @click="handleGroupNodes">
          <PlusIcon class="menu-icon" />
          <span>创建分组</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handleCopySelection">
          <CopyIcon class="menu-icon" />
          <span>复制</span>
          <span class="shortcut">Ctrl+C</span>
        </div>
        <div class="menu-item danger" @click="handleDeleteSelection">
          <TrashIcon class="menu-icon" />
          <span>批量删除</span>
          <span class="shortcut">Del</span>
        </div>
      </template>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import type { Edge, Node } from '@vue-flow/core';
import {
  CheckIcon,
  CheckSquareIcon,
  ClipboardIcon,
  CopyIcon,
  CopyPlusIcon,
  HashIcon,
  ImageIcon,
  PlusIcon,
  TrashIcon,
} from 'lucide-vue-next';
import { useExecutionStore } from '../../stores/execution';
import { useFlowStore, type EdgeRenderStyle } from '../../stores/flow';
import { resolveImageSource } from '../../services/imageSource';

interface Props {
  isVisible: boolean;
  position: { x: number; y: number };
  contextType: 'pane' | 'node' | 'edge' | 'selection';
  targetId?: string | null;
  selectedNodes?: Node[];
  selectedEdges?: Edge[];
}

interface EdgeDataLike {
  routeStyle?: EdgeRenderStyle;
}

const props = withDefaults(defineProps<Props>(), {
  isVisible: false,
  position: () => ({ x: 0, y: 0 }),
  contextType: 'pane',
  targetId: null,
  selectedNodes: () => [],
  selectedEdges: () => [],
});

const emit = defineEmits<{
  close: [];
  addNode: [position: { x: number; y: number }];
  paste: [];
  selectAll: [];
  copy: [nodeId: string];
  duplicate: [nodeId: string];
  copyNodeId: [nodeId: string];
  viewOutput: [nodeId: string];
  deleteNode: [nodeId: string];
  deleteEdge: [edgeId: string];
  insertReroute: [edgeId: string, screenPosition: { x: number; y: number }];
  setEdgeStyle: [edgeId: string, style: EdgeRenderStyle];
  groupNodes: [nodeIds: string[]];
  copySelection: [nodeIds: string[]];
  deleteSelection: [nodeIds: string[], edgeIds: string[]];
}>();

const flowStore = useFlowStore();
const executionStore = useExecutionStore();

const clipboard = ref<{ nodes: Node[]; edges: Edge[] } | null>(null);
const hasClipboard = computed(
  () => clipboard.value !== null && clipboard.value.nodes.length > 0,
);

const currentEdgeStyle = computed<EdgeRenderStyle>(() => {
  const selectedEdge = props.selectedEdges[0];
  if (!selectedEdge) return 'bezier';
  if (selectedEdge.type === 'pathfinding') return 'pathfinding';
  return (selectedEdge.data as EdgeDataLike | undefined)?.routeStyle ===
    'smoothstep'
    ? 'smoothstep'
    : 'bezier';
});

const closeMenu = () => {
  emit('close');
};

const handleAddNode = () => {
  emit('addNode', props.position);
  closeMenu();
};

const handlePaste = () => {
  if (hasClipboard.value) {
    emit('paste');
  }
  closeMenu();
};

const handleSelectAll = () => {
  emit('selectAll');
  closeMenu();
};

const handleCopy = () => {
  if (props.targetId) {
    const node = flowStore.getNodeById(props.targetId);
    if (node) {
      clipboard.value = {
        nodes: [JSON.parse(JSON.stringify(node)) as Node],
        edges: [],
      };
    }
    emit('copy', props.targetId);
  }
  closeMenu();
};

const handleDuplicate = () => {
  if (props.targetId) {
    emit('duplicate', props.targetId);
  }
  closeMenu();
};

const handleCopyNodeId = () => {
  if (props.targetId) {
    navigator.clipboard.writeText(props.targetId).catch(console.error);
    emit('copyNodeId', props.targetId);
  }
  closeMenu();
};

const handleViewOutput = () => {
  if (!props.targetId) {
    closeMenu();
    return;
  }

  const outputImage = executionStore.getNodeOutputImage(props.targetId);
  if (outputImage) {
    const imageSrc = resolveImageSource(outputImage);
    if (imageSrc) {
      const win = window.open('', '_blank');
      if (win) {
        win.document.write(`
          <html>
            <head><title>节点输出 - ${props.targetId}</title></head>
            <body style="margin:0;display:flex;justify-content:center;align-items:center;min-height:100vh;background:#1a1a1a;">
              <img src="${imageSrc}" style="max-width:100%;max-height:100vh;">
            </body>
          </html>
        `);
        win.document.close();
      }
    }
  }

  emit('viewOutput', props.targetId);
  closeMenu();
};

const handleDeleteNode = () => {
  if (props.targetId) {
    emit('deleteNode', props.targetId);
  }
  closeMenu();
};

const handleDeleteEdge = () => {
  if (props.targetId) {
    emit('deleteEdge', props.targetId);
  }
  closeMenu();
};

const handleInsertReroute = () => {
  if (props.targetId) {
    emit('insertReroute', props.targetId, props.position);
  }
  closeMenu();
};

const handleSetEdgeStyle = (style: EdgeRenderStyle) => {
  if (props.targetId) {
    emit('setEdgeStyle', props.targetId, style);
  }
  closeMenu();
};

const handleGroupNodes = () => {
  const nodeIds = props.selectedNodes.map((node) => node.id);
  if (nodeIds.length > 1) {
    emit('groupNodes', nodeIds);
  }
  closeMenu();
};

const handleCopySelection = () => {
  const nodeIds = props.selectedNodes.map((node) => node.id);
  clipboard.value = {
    nodes: JSON.parse(JSON.stringify(props.selectedNodes)) as Node[],
    edges: JSON.parse(
      JSON.stringify(
        props.selectedEdges.filter(
          (edge) => nodeIds.includes(edge.source) && nodeIds.includes(edge.target),
        ),
      ),
    ) as Edge[],
  };
  emit('copySelection', nodeIds);
  closeMenu();
};

const handleDeleteSelection = () => {
  const nodeIds = props.selectedNodes.map((node) => node.id);
  const edgeIds = props.selectedEdges.map((edge) => edge.id);
  emit('deleteSelection', nodeIds, edgeIds);
  closeMenu();
};

const handleClickOutside = () => {
  if (props.isVisible) {
    closeMenu();
  }
};

onMounted(() => {
  document.addEventListener('click', handleClickOutside);
  document.addEventListener('contextmenu', handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
  document.removeEventListener('contextmenu', handleClickOutside);
});
</script>

<style scoped>
.context-menu {
  position: fixed;
  z-index: 9999;
  min-width: 204px;
  background: var(--glass-bg, rgba(255, 255, 255, 0.95));
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.08));
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  padding: 6px;
  font-family: 'Inter', sans-serif;
  animation: menu-appear 0.15s ease-out;
}

@keyframes menu-appear {
  from {
    opacity: 0;
    transform: scale(0.95) translateY(-4px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

.menu-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary, #1c1c1e);
  cursor: pointer;
  transition: all 0.15s ease;
}

.menu-item:hover {
  background: rgba(0, 0, 0, 0.05);
}

.menu-item.danger {
  color: #ef4444;
}

.menu-item.danger:hover {
  background: rgba(239, 68, 68, 0.1);
}

.menu-item.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.menu-item.disabled:hover {
  background: transparent;
}

.menu-icon {
  width: 16px;
  height: 16px;
  color: var(--text-muted, #64748b);
  flex-shrink: 0;
}

.menu-item.danger .menu-icon {
  color: #ef4444;
}

.menu-item span:first-of-type {
  flex: 1;
}

.shortcut {
  font-size: 11px;
  color: var(--text-muted, #64748b);
  font-weight: 400;
}

.menu-divider {
  height: 1px;
  background: var(--border-glass, rgba(0, 0, 0, 0.08));
  margin: 4px 8px;
}

.menu-section-title {
  padding: 8px 14px 4px;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted, #64748b);
  letter-spacing: 0.2px;
}

.style-item {
  padding-top: 8px;
  padding-bottom: 8px;
}

.style-item .active-check {
  opacity: 0;
}

.style-item.active {
  background: rgba(77, 148, 255, 0.12);
  color: #2563eb;
}

.style-item.active .active-check {
  opacity: 1;
  color: #2563eb;
}
</style>
