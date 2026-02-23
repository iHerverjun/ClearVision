<template>
  <div class="flow-editor-container" :class="{ 'is-connecting': isConnecting }">
    <VueFlow
      :id="FLOW_INSTANCE_ID"
      :nodes="nodes"
      :edges="edges"
      :apply-default="false"
      :is-valid-connection="isConnectionValid"
      :connection-mode="ConnectionMode.Strict"
      :connection-radius="80"
      :connection-line-options="connectionLineOptions"
      :default-edge-options="defaultEdgeOptions"
      :connect-on-click="false"
      :auto-pan-on-connect="true"
      @nodes-change="handleNodesChange"
      @edges-change="handleEdgesChange"
      @connect="onConnect"
      @connect-start="onConnectStart"
      @connect-end="onConnectEnd"
      @node-click="onNodeClick"
      @node-context-menu="onNodeContextMenu"
      @edge-context-menu="onEdgeContextMenu"
      @selection-context-menu="onSelectionContextMenu"
      @edge-double-click="onEdgeDoubleClick"
      @pane-click="onPaneClick"
      fit-view-on-init
      :default-zoom="1"
      :min-zoom="0.1"
      :max-zoom="4"
      class="clearvision-flow"
    >
      <Background :pattern-color="'#cbd5e1'" :gap="24" :size="1" />

      <Controls class="cv-controls" />

      <MiniMap
        class="cv-minimap"
        node-color="#FF4D4D"
        mask-color="rgba(245, 245, 247, 0.7)"
        :pannable="true"
        :zoomable="true"
        @click="onMiniMapClick"
      />

      <template #node-operator-node="props">
        <OperatorNode v-bind="props" />
      </template>

      <template #node-image-acquisition="props">
        <ImageAcquisitionNode v-bind="props" />
      </template>

      <template #node-group="props">
        <GroupNode v-bind="props" />
      </template>

      <template #node-reroute-node="props">
        <RerouteNode v-bind="props" />
      </template>

      <template #edge-typed="props">
        <TypedEdge v-bind="props" />
      </template>

      <template #edge-pathfinding="props">
        <PathFindingEdge v-bind="toPathfindingProps(props)" />
      </template>
    </VueFlow>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import {
  ConnectionLineType,
  ConnectionMode,
  MarkerType,
  VueFlow,
  useVueFlow,
  type DefaultEdgeOptions,
  type NodeChange,
  type EdgeChange,
  type Connection,
  type Edge,
  type Node,
  type OnConnectStartParams,
} from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import { PathFindingEdge } from '@vue-flow/pathfinding-edge';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';
import '@vue-flow/controls/dist/style.css';
import '@vue-flow/minimap/dist/style.css';

import { useFlowStore, type EdgeRenderStyle } from '../../stores/flow';
import OperatorNode from './nodes/OperatorNode.vue';
import GroupNode from './nodes/GroupNode.vue';
import ImageAcquisitionNode from './nodes/ImageAcquisitionNode.vue';
import RerouteNode from './nodes/RerouteNode.vue';
import TypedEdge from './edges/TypedEdge.vue';
import { FLOW_INSTANCE_ID } from './flow.constants';
import {
  getPortColor,
  getPortGlowColor,
  isTypeCompatible,
} from '../../config/portTypeRegistry';

const flowStore = useFlowStore();

const emit = defineEmits<{
  (
    event: 'node-context-menu',
    payload: { node: Node; originalEvent: MouseEvent },
  ): void;
  (
    event: 'edge-context-menu',
    payload: { edge: Edge; originalEvent: MouseEvent },
  ): void;
  (
    event: 'selection-context-menu',
    payload: { nodes: Node[]; edges: Edge[]; originalEvent: MouseEvent },
  ): void;
  (
    event: 'edge-double-click',
    payload: { edge: Edge; originalEvent: MouseEvent },
  ): void;
}>();

const {
  applyNodeChanges,
  applyEdgeChanges,
  connectionStartHandle,
  setCenter,
  getViewport,
  getSelectedEdges,
} = useVueFlow(FLOW_INSTANCE_ID);

interface MiniMapClickEvent {
  event: MouseEvent;
  position: { x: number; y: number };
}

interface PortLike {
  id?: string | number | null;
  type?: string | null;
}

const nodes = computed({
  get: () => flowStore.nodes,
  set: (value) => flowStore.setNodes(value),
});

const edges = computed({
  get: () => flowStore.edges,
  set: (value) => flowStore.setEdges(value),
});

const isConnecting = computed(() => connectionStartHandle.value !== null);

const resolvePreviewLineType = () => {
  if (flowStore.preferredEdgeStyle === 'smoothstep') {
    return ConnectionLineType.SmoothStep;
  }
  return ConnectionLineType.Bezier;
};

const resolveEdgeType = (style: EdgeRenderStyle) => {
  return style === 'pathfinding' ? 'pathfinding' : 'typed';
};

const resolveRouteStyle = (style: EdgeRenderStyle) => {
  return style === 'smoothstep' ? 'smoothstep' : 'bezier';
};

const connectionLineOptions = computed(() => {
  const activeColor = flowStore.connectingSourceType
    ? getPortColor(flowStore.connectingSourceType)
    : 'rgba(77, 148, 255, 0.95)';
  const glowColor = flowStore.connectingSourceType
    ? getPortGlowColor(flowStore.connectingSourceType, 0.55)
    : 'rgba(77, 148, 255, 0.42)';

  return {
    type: resolvePreviewLineType(),
    style: {
      stroke: activeColor,
      strokeWidth: 3.3,
      strokeLinecap: 'round' as const,
      strokeLinejoin: 'round' as const,
      filter: `drop-shadow(0 0 8px ${glowColor})`,
    },
  };
});

const defaultEdgeOptions = computed<DefaultEdgeOptions>(() => ({
  type: resolveEdgeType(flowStore.preferredEdgeStyle),
  markerEnd: {
    type: MarkerType.ArrowClosed,
    width: 20,
    height: 20,
    color: '#4D94FF',
  },
  interactionWidth: 44,
  data: {
    routeStyle: resolveRouteStyle(flowStore.preferredEdgeStyle),
  },
}));

const findPortType = (
  nodeId: string | null | undefined,
  handleId: string | null | undefined,
  direction: 'inputs' | 'outputs',
) => {
  if (!nodeId || !handleId) return null;

  const node = flowStore.getNodeById(nodeId);
  const ports = Array.isArray((node?.data as any)?.[direction])
    ? (((node?.data as any)[direction] ?? []) as PortLike[])
    : [];
  const port = ports.find((candidate) => `${candidate.id ?? ''}` === handleId);
  return port?.type ?? null;
};

const isConnectionValid = (connection: Connection) => {
  if (
    !connection.source ||
    !connection.target ||
    !connection.sourceHandle ||
    !connection.targetHandle
  ) {
    return false;
  }

  if (connection.source === connection.target) {
    return false;
  }

  const duplicated = flowStore.edges.some(
    (edge) =>
      edge.source === connection.source &&
      edge.target === connection.target &&
      edge.sourceHandle === connection.sourceHandle &&
      edge.targetHandle === connection.targetHandle,
  );

  if (duplicated) {
    return false;
  }

  const sourceType = findPortType(
    connection.source,
    connection.sourceHandle,
    'outputs',
  );
  const targetType = findPortType(
    connection.target,
    connection.targetHandle,
    'inputs',
  );

  return isTypeCompatible(sourceType, targetType);
};

const buildEdgeId = (connection: Connection) =>
  `e-${connection.source}-${connection.sourceHandle ?? 'na'}-${connection.target}-${connection.targetHandle ?? 'na'}-${Date.now()}-${Math.random()
    .toString(16)
    .slice(2, 8)}`;

const handleNodesChange = (changes: NodeChange[]) => {
  const nextNodes = applyNodeChanges(changes);
  const markDirty = changes.some((change) => change.type !== 'select');
  flowStore.setNodes(nextNodes, markDirty);
};

const handleEdgesChange = (changes: EdgeChange[]) => {
  const nextEdges = applyEdgeChanges(changes);
  const markDirty = changes.some((change) => change.type !== 'select');
  flowStore.setEdges(nextEdges, markDirty);
};

const onConnect = (connection: Connection) => {
  if (!isConnectionValid(connection)) {
    return;
  }

  const sourceType = findPortType(
    connection.source,
    connection.sourceHandle,
    'outputs',
  );
  const targetType = findPortType(
    connection.target,
    connection.targetHandle,
    'inputs',
  );
  const sourceColor = getPortColor(sourceType);
  const targetColor = getPortColor(targetType);
  const preferredStyle = flowStore.preferredEdgeStyle;

  const newEdge: Edge = {
    ...connection,
    id: buildEdgeId(connection),
    type: resolveEdgeType(preferredStyle),
    interactionWidth: 36,
    markerEnd: {
      type: MarkerType.ArrowClosed,
      width: 18,
      height: 18,
      color: targetColor,
    },
    style: {
      stroke: sourceColor,
      strokeWidth: 2.5,
    },
    data: {
      type: sourceType ?? 'Unknown',
      sourceType,
      targetType,
      sourceColor,
      targetColor,
      routeStyle: resolveRouteStyle(preferredStyle),
    },
  };

  flowStore.addEdge(newEdge);
};

const onConnectStart = ({
  nodeId,
  handleId,
  handleType,
}: OnConnectStartParams) => {
  if (!nodeId || !handleId || !handleType) {
    flowStore.clearConnectingType();
    return;
  }

  const direction = handleType === 'source' ? 'outputs' : 'inputs';
  const activeType = findPortType(nodeId, handleId, direction);
  flowStore.setConnectingType(activeType, handleType);
};

const onConnectEnd = () => {
  flowStore.clearConnectingType();
};

const onMiniMapClick = ({ position }: MiniMapClickEvent) => {
  const viewport = getViewport();
  void setCenter(position.x, position.y, {
    zoom: viewport.zoom,
    duration: 180,
  });
};

const onNodeClick = (event: { node: { id: string } }) => {
  flowStore.selectNode(event.node.id);
};

const onNodeContextMenu = (event: {
  event: MouseEvent | TouchEvent;
  node: Node;
}) => {
  if (!(event.event instanceof MouseEvent)) return;
  event.event.preventDefault();
  emit('node-context-menu', {
    node: event.node,
    originalEvent: event.event,
  });
};

const onEdgeContextMenu = (event: {
  event: MouseEvent | TouchEvent;
  edge: Edge;
}) => {
  if (!(event.event instanceof MouseEvent)) return;
  event.event.preventDefault();
  emit('edge-context-menu', {
    edge: event.edge,
    originalEvent: event.event,
  });
};

const onSelectionContextMenu = (event: {
  event: MouseEvent;
  nodes: Node[];
}) => {
  event.event.preventDefault();
  emit('selection-context-menu', {
    nodes: event.nodes,
    edges: getSelectedEdges.value,
    originalEvent: event.event,
  });
};

const onEdgeDoubleClick = (event: {
  event: MouseEvent | TouchEvent;
  edge: Edge;
}) => {
  if (!(event.event instanceof MouseEvent)) return;
  emit('edge-double-click', {
    edge: event.edge,
    originalEvent: event.event,
  });
};

const onPaneClick = () => {
  flowStore.clearConnectingType();
  flowStore.selectNode(null);
};

const toPathfindingProps = (props: unknown) => props as any;
</script>

<style scoped>
.flow-editor-container {
  width: 100%;
  height: 100%;
  background: transparent;
}

.clearvision-flow {
  --vf-handle: var(--accent-red, #ff4d4d);
  --vf-box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.flow-editor-container.is-connecting
  :deep(.clearvision-flow .vue-flow__pane) {
  cursor: crosshair;
}

:deep(.clearvision-flow .vue-flow__node) {
  overflow: visible;
}

:deep(.clearvision-flow .vue-flow__handle) {
  opacity: 1 !important;
  visibility: visible !important;
}

:deep(.clearvision-flow .vue-flow__connection-path) {
  stroke-linecap: round;
  stroke-linejoin: round;
}

:deep(.clearvision-flow .vue-flow__connectionline) {
  pointer-events: none;
}

:deep(.cv-controls .vue-flow__controls-button) {
  background: var(--glass-bg, rgba(255, 255, 255, 0.7));
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  color: var(--text-primary, #1c1c1e);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
}

:deep(.cv-controls .vue-flow__controls-button:hover) {
  background: var(--hover-overlay, rgba(0, 0, 0, 0.04));
}

:deep(.cv-minimap) {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.04);
  pointer-events: auto;
}
</style>
