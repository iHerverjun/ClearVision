import { defineStore } from 'pinia';
import { ref, computed, watch } from 'vue';
import type { Node, Edge } from '@vue-flow/core';
import { FlowSerializer } from '../services/flowSerializer';
import type { LegacyProjectConfig } from '../services/flowSerializer';

// Custom node type that extends Vue Flow Node with parentId support
interface FlowNode extends Node {
  parentId?: string;
}

export interface LintIssue {
  id: string;
  type: 'error' | 'warning' | 'info';
  message: string;
  nodeId?: string;
}

export type EdgeRenderStyle = 'bezier' | 'smoothstep' | 'pathfinding';

export const useFlowStore = defineStore('flow', () => {
  const nodes = ref<FlowNode[]>([]);
  const edges = ref<Edge[]>([]);
  const selectedNodeId = ref<string | null>(null);
  const isDirty = ref<boolean>(false);
  const lintIssues = ref<LintIssue[]>([]);
  const preferredEdgeStyle = ref<EdgeRenderStyle>('bezier');

  // ─── Connection Drag State ─────────────────
  const connectingSourceType = ref<string | null>(null);
  const connectingDirection = ref<'source' | 'target' | null>(null);

  const setConnectingType = (type: string | null, direction: 'source' | 'target' | null = null) => {
    connectingSourceType.value = type;
    connectingDirection.value = direction;
  };

  const clearConnectingType = () => {
    connectingSourceType.value = null;
    connectingDirection.value = null;
  };

  const setPreferredEdgeStyle = (style: EdgeRenderStyle) => {
    preferredEdgeStyle.value = style;
  };

  // Undo / Redo Stacks (Store serialized JSON strings)
  const history = ref<string[]>([]);
  const historyIndex = ref<number>(-1);
  const isUndoing = ref(false);

  // Core Actions
  const setNodes = (newNodes: FlowNode[], markDirty = true) => {
    nodes.value = newNodes;
    if (markDirty) {
      isDirty.value = true;
    }
  };

  const setEdges = (newEdges: Edge[], markDirty = true) => {
    edges.value = newEdges;
    if (markDirty) {
      isDirty.value = true;
    }
  };

  const addNode = (node: FlowNode) => {
    nodes.value.push(node);
    isDirty.value = true;
  };

  const addEdge = (edge: Edge) => {
    edges.value.push(edge);
    isDirty.value = true;
  };

  const removeNode = (id: string) => {
    nodes.value = nodes.value.filter(n => n.id !== id);
    edges.value = edges.value.filter(e => e.source !== id && e.target !== id);
    if (selectedNodeId.value === id) {
      selectedNodeId.value = null;
    }
    isDirty.value = true;
  };

  const removeEdge = (id: string) => {
    edges.value = edges.value.filter(e => e.id !== id);
    isDirty.value = true;
  };

  const selectNode = (id: string | null) => {
    selectedNodeId.value = id;
  };

  const clear = () => {
    nodes.value = [];
    edges.value = [];
    selectedNodeId.value = null;
    isDirty.value = false;
  };

  // Getters
  const selectedNode = computed(() => 
    nodes.value.find(n => n.id === selectedNodeId.value) || null
  );

  const getNodeById = (id: string) => nodes.value.find(n => n.id === id);

  // --- Serialization & History ---
  
  const validateFlow = () => {
    const issues: LintIssue[] = [];
    
    // Check for disconnected nodes (no edges connected to them) unless it's just a single node
    if (nodes.value.length > 1) {
      nodes.value.forEach(node => {
        // Exclude group nodes
        if (node.type === 'group') return;
        
        const hasEdges = edges.value.some(e => e.source === node.id || e.target === node.id);
        if (!hasEdges) {
          issues.push({
            id: `isolated-${node.id}`,
            type: 'warning',
            message: `Node "${node.data?.name || node.id}" is isolated and not connected to the flow.`,
            nodeId: node.id
          });
        }
      });
    }

    lintIssues.value = issues;
  };

  const snapshot = () => {
    if (isUndoing.value) return;
    const state = JSON.stringify({ nodes: nodes.value, edges: edges.value });
    
    // If we changed history via undo and then make a new action, drop future redos
    if (historyIndex.value < history.value.length - 1) {
      history.value = history.value.slice(0, historyIndex.value + 1);
    }
    
    history.value.push(state);
    // Limit history stack size
    if (history.value.length > 50) {
      history.value.shift();
    } else {
      historyIndex.value++;
    }
  };

  // Watch for changes and take snapshot
  watch([nodes, edges], () => {
    snapshot();
    validateFlow();
  }, { deep: true });

  const undo = () => {
    if (historyIndex.value > 0) {
      isUndoing.value = true;
      historyIndex.value--;
      const stateStr = history.value[historyIndex.value];
      if (stateStr) {
        const state = JSON.parse(stateStr);
        nodes.value = state.nodes;
        edges.value = state.edges;
        isDirty.value = true;
      }
      setTimeout(() => isUndoing.value = false, 0);
    }
  };

  const redo = () => {
    if (historyIndex.value < history.value.length - 1) {
      isUndoing.value = true;
      historyIndex.value++;
      const stateStr = history.value[historyIndex.value];
      if (stateStr) {
        const state = JSON.parse(stateStr);
        nodes.value = state.nodes;
        edges.value = state.edges;
        isDirty.value = true;
      }
      setTimeout(() => isUndoing.value = false, 0);
    }
  };

  const loadLegacyProject = (projectData: LegacyProjectConfig) => {
    const { nodes: newNodes, edges: newEdges } = FlowSerializer.legacyToVueFlow(projectData);
    nodes.value = newNodes;
    edges.value = newEdges;
    selectedNodeId.value = null;
    isDirty.value = false;
    history.value = [];
    historyIndex.value = -1;
    snapshot(); // Initial state
  };

  const buildLegacyProject = (): LegacyProjectConfig => {
    return FlowSerializer.vueFlowToLegacy(nodes.value, edges.value);
  };

  // --- Grouping ---
  
  const createGroup = (nodeIds: string[], groupName = 'Group') => {
    if (nodeIds.length === 0) return null;
    
    // Calculate bounding box for all nodes
    const selectedNodes = nodes.value.filter(n => nodeIds.includes(n.id));
    if (selectedNodes.length === 0) return null;
    
    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;
    
    selectedNodes.forEach(node => {
      const nodeWidth = (node as any).measured?.width || 200;
      const nodeHeight = (node as any).measured?.height || 100;
      minX = Math.min(minX, node.position.x);
      minY = Math.min(minY, node.position.y);
      maxX = Math.max(maxX, node.position.x + nodeWidth);
      maxY = Math.max(maxY, node.position.y + nodeHeight);
    });
    
    // Add padding
    const padding = 40;
    const groupX = minX - padding;
    const groupY = minY - padding - 40; // Extra space for header
    const groupWidth = maxX - minX + padding * 2;
    const groupHeight = maxY - minY + padding * 2 + 40;
    
    // Create group node
    const groupId = `group-${Date.now()}`;
    const groupNode: FlowNode = {
      id: groupId,
      type: 'group',
      position: { x: groupX, y: groupY },
      style: { 
        width: `${groupWidth}px`,
        height: `${groupHeight}px`
      },
      data: {
        label: groupName,
        color: 'rgba(232, 72, 85, 0.15)'
      }
    };
    
    // Add group node first
    nodes.value.push(groupNode);
    
    // Update child nodes to be relative to group and set parentId
    selectedNodes.forEach(node => {
      node.parentId = groupId;
      // Convert absolute position to relative position within group
      node.position = {
        x: node.position.x - groupX,
        y: node.position.y - groupY
      };
    });
    
    isDirty.value = true;
    return groupId;
  };
  
  const ungroupNodes = (groupId: string) => {
    const groupNode = nodes.value.find(n => n.id === groupId);
    if (!groupNode) return;
    
    // Find all child nodes
    const childNodes = nodes.value.filter(n => n.parentId === groupId);
    
    // Convert child positions back to absolute
    childNodes.forEach(node => {
      node.position = {
        x: node.position.x + groupNode.position.x,
        y: node.position.y + groupNode.position.y
      };
      node.parentId = undefined;
    });
    
    // Remove group node
    nodes.value = nodes.value.filter(n => n.id !== groupId);
    isDirty.value = true;
  };
  
  const updateNodeParent = (nodeId: string, parentId: string | null) => {
    const node = nodes.value.find(n => n.id === nodeId);
    if (!node) return;
    
    if (parentId) {
      const parentNode = nodes.value.find(n => n.id === parentId);
      if (parentNode) {
        // Convert to relative position
        node.position = {
          x: node.position.x - parentNode.position.x,
          y: node.position.y - parentNode.position.y
        };
        node.parentId = parentId;
      }
    } else {
      // Convert back to absolute position if removing from group
      if (node.parentId) {
        const oldParent = nodes.value.find(n => n.id === node.parentId);
        if (oldParent) {
          node.position = {
            x: node.position.x + oldParent.position.x,
            y: node.position.y + oldParent.position.y
          };
        }
      }
      node.parentId = undefined;
    }
    isDirty.value = true;
  };

  const canUndo = computed(() => historyIndex.value > 0);
  const canRedo = computed(() => historyIndex.value < history.value.length - 1);

  return {
    nodes,
    edges,
    selectedNodeId,
    isDirty,
    lintIssues,
    connectingSourceType,
    connectingDirection,
    preferredEdgeStyle,
    setNodes,
    setEdges,
    addNode,
    addEdge,
    removeNode,
    removeEdge,
    selectNode,
    clear,
    selectedNode,
    getNodeById,
    setConnectingType,
    clearConnectingType,
    setPreferredEdgeStyle,
    undo,
    redo,
    canUndo,
    canRedo,
    loadLegacyProject,
    buildLegacyProject,
    createGroup,
    ungroupNodes,
    updateNodeParent,
    validateFlow
  };
});
