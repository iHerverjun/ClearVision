<template>
  <Teleport to="body">
    <div
      v-if="isVisible"
      class="context-menu"
      :style="{ left: `${position.x}px`, top: `${position.y}px` }"
      @click.stop
    >
      <!-- 鐢诲竷绌虹櫧澶勫彸閿?-->
      <template v-if="contextType === 'pane'">
        <div class="menu-item" @click="handleAddNode">
          <PlusIcon class="menu-icon" />
          <span>娣诲姞鑺傜偣</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handlePaste" :class="{ disabled: !hasClipboard }">
          <ClipboardIcon class="menu-icon" />
          <span>绮樿创</span>
          <span class="shortcut">Ctrl+V</span>
        </div>
        <div class="menu-item" @click="handleSelectAll">
          <CheckSquareIcon class="menu-icon" />
          <span>全选</span>
          <span class="shortcut">Ctrl+A</span>
        </div>
      </template>

      <!-- 鑺傜偣涓婂彸閿?-->
      <template v-else-if="contextType === 'node'">
        <div class="menu-item" @click="handleCopy">
          <CopyIcon class="menu-icon" />
          <span>澶嶅埗</span>
          <span class="shortcut">Ctrl+C</span>
        </div>
        <div class="menu-item" @click="handleDuplicate">
          <CopyPlusIcon class="menu-icon" />
          <span>澶嶅埗鑺傜偣</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handleCopyNodeId">
          <HashIcon class="menu-icon" />
          <span>澶嶅埗鑺傜偣ID</span>
        </div>
        <div class="menu-item" @click="handleViewOutput">
          <ImageIcon class="menu-icon" />
          <span>鏌ョ湅杈撳嚭</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item danger" @click="handleDeleteNode">
          <TrashIcon class="menu-icon" />
          <span>鍒犻櫎</span>
          <span class="shortcut">Del</span>
        </div>
      </template>

      <!-- 杩炵嚎涓婂彸閿?-->
      <template v-else-if="contextType === 'edge'">
        <div class="menu-item danger" @click="handleDeleteEdge">
          <TrashIcon class="menu-icon" />
          <span>鍒犻櫎杩炵嚎</span>
        </div>
      </template>

      <!-- 澶氶€夊悗鍙抽敭 -->
      <template v-else-if="contextType === 'selection'">
        <div class="menu-item" @click="handleGroupNodes">
          <GroupIcon class="menu-icon" />
          <span>鍒涘缓鍒嗙粍</span>
        </div>
        <div class="menu-divider"></div>
        <div class="menu-item" @click="handleCopySelection">
          <CopyIcon class="menu-icon" />
          <span>澶嶅埗</span>
          <span class="shortcut">Ctrl+C</span>
        </div>
        <div class="menu-item danger" @click="handleDeleteSelection">
          <TrashIcon class="menu-icon" />
          <span>鎵归噺鍒犻櫎</span>
          <span class="shortcut">Del</span>
        </div>
      </template>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useFlowStore } from '../../stores/flow';
import { useExecutionStore } from '../../stores/execution';
import {
  PlusIcon,
  ClipboardIcon,
  CheckSquareIcon,
  CopyIcon,
  CopyPlusIcon,
  HashIcon,
  ImageIcon,
  TrashIcon,
  GroupIcon,
} from 'lucide-vue-next';
import type { Node, Edge } from '@vue-flow/core';
import { resolveImageSource } from '../../services/imageSource';

// Props
interface Props {
  isVisible: boolean;
  position: { x: number; y: number };
  contextType: 'pane' | 'node' | 'edge' | 'selection';
  targetId?: string | null;
  selectedNodes?: Node[];
  selectedEdges?: Edge[];
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
  groupNodes: [nodeIds: string[]];
  copySelection: [nodeIds: string[]];
  deleteSelection: [nodeIds: string[], edgeIds: string[]];
}>();

const flowStore = useFlowStore();
const executionStore = useExecutionStore();

// 鍓创鏉跨姸鎬?
const clipboard = ref<{ nodes: Node[], edges: Edge[] } | null>(null);
const hasClipboard = computed(() => clipboard.value !== null && clipboard.value.nodes.length > 0);

// 鍏抽棴鑿滃崟
const closeMenu = () => {
  emit('close');
};

// 鐢诲竷鎿嶄綔
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

// 鑺傜偣鎿嶄綔
const handleCopy = () => {
  if (props.targetId) {
    const node = flowStore.getNodeById(props.targetId);
    if (node) {
      clipboard.value = {
        nodes: [JSON.parse(JSON.stringify(node))],
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
  if (props.targetId) {
    const outputImage = executionStore.getNodeOutputImage(props.targetId);
    if (outputImage) {
      const imageSrc = resolveImageSource(outputImage);
      if (!imageSrc) {
        return;
      }

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
    emit('viewOutput', props.targetId);
  }
  closeMenu();
};

const handleDeleteNode = () => {
  if (props.targetId) {
    emit('deleteNode', props.targetId);
  }
  closeMenu();
};

// 杩炵嚎鎿嶄綔
const handleDeleteEdge = () => {
  if (props.targetId) {
    emit('deleteEdge', props.targetId);
  }
  closeMenu();
};

// 澶氶€夋搷浣?
const handleGroupNodes = () => {
  const nodeIds = props.selectedNodes.map(n => n.id);
  if (nodeIds.length > 1) {
    emit('groupNodes', nodeIds);
  }
  closeMenu();
};

const handleCopySelection = () => {
  const nodeIds = props.selectedNodes.map(n => n.id);
  clipboard.value = {
    nodes: JSON.parse(JSON.stringify(props.selectedNodes)),
    edges: JSON.parse(JSON.stringify(
      props.selectedEdges.filter(e => 
        nodeIds.includes(e.source) && nodeIds.includes(e.target)
      )
    )),
  };
  emit('copySelection', nodeIds);
  closeMenu();
};

const handleDeleteSelection = () => {
  const nodeIds = props.selectedNodes.map(n => n.id);
  const edgeIds = props.selectedEdges.map(e => e.id);
  emit('deleteSelection', nodeIds, edgeIds);
  closeMenu();
};

// 鐐瑰嚮澶栭儴鍏抽棴
const handleClickOutside = (_e: MouseEvent) => {
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
  min-width: 180px;
  background: var(--glass-bg, rgba(255, 255, 255, 0.95));
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.08));
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  padding: 6px;
  font-family: "Inter", sans-serif;
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
  color: #EF4444;
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
  color: #EF4444;
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
</style>
