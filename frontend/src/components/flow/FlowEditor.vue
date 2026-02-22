<template>
  <div class="flow-editor-container" :class="{ 'is-click-connecting': isClickConnecting }">
    <VueFlow
      :id="FLOW_INSTANCE_ID"
      :nodes="nodes"
      :edges="edges"
      :apply-default="false"
      :is-valid-connection="isConnectionValid"
      :connection-mode="ConnectionMode.Strict"
      :connection-radius="64"
      :connection-line-options="connectionLineOptions"
      :default-edge-options="defaultEdgeOptions"
      :connect-on-click="true"
      :auto-pan-on-connect="true"
      @nodes-change="handleNodesChange"
      @edges-change="handleEdgesChange"
      @connect="onConnect"
      @node-click="onNodeClick"
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

      <!-- Custom Node Typography & Renderers -->
      <template #node-operator-node="props">
        <OperatorNode v-bind="props" />
      </template>

      <template #node-image-acquisition="props">
        <ImageAcquisitionNode v-bind="props" />
      </template>

      <template #node-group="props">
        <GroupNode v-bind="props" />
      </template>

      <!-- Custom Edges -->
      <template #edge-typed="props">
        <TypedEdge v-bind="props" />
      </template>
    </VueFlow>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
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
} from "@vue-flow/core";
import { Background } from "@vue-flow/background";
import { Controls } from "@vue-flow/controls";
import { MiniMap } from "@vue-flow/minimap";
import "@vue-flow/core/dist/style.css";
import "@vue-flow/core/dist/theme-default.css";
import "@vue-flow/controls/dist/style.css";
import "@vue-flow/minimap/dist/style.css";

import { useFlowStore } from "../../stores/flow";
import OperatorNode from "./nodes/OperatorNode.vue";
import GroupNode from "./nodes/GroupNode.vue";
import ImageAcquisitionNode from "./nodes/ImageAcquisitionNode.vue";
import TypedEdge from "./edges/TypedEdge.vue";
import { FLOW_INSTANCE_ID } from "./flow.constants";

const flowStore = useFlowStore();
const {
  applyNodeChanges,
  applyEdgeChanges,
  endConnection,
  connectionClickStartHandle,
  setCenter,
  getViewport,
} = useVueFlow(FLOW_INSTANCE_ID);

interface MiniMapClickEvent {
  event: MouseEvent;
  position: { x: number; y: number };
}

const nodes = computed({
  get: () => flowStore.nodes,
  set: (value) => flowStore.setNodes(value),
});
const edges = computed({
  get: () => flowStore.edges,
  set: (value) => flowStore.setEdges(value),
});

const isClickConnecting = computed(
  () => connectionClickStartHandle.value !== null,
);

const connectionLineOptions = {
  type: ConnectionLineType.Bezier,
  style: {
    stroke: "rgba(77, 148, 255, 0.9)",
    strokeWidth: 3.2,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  },
};

const defaultEdgeOptions: DefaultEdgeOptions = {
  type: "typed",
  markerEnd: {
    type: MarkerType.ArrowClosed,
    width: 20,
    height: 20,
    color: "#4D94FF",
  },
  interactionWidth: 44,
};

const PORT_TYPE_COLORS: Record<string, string> = {
  Image: "#4D94FF",
  Integer: "#00E676",
  Float: "#FFA726",
  Boolean: "#FF4D4D",
  String: "#BA68C8",
  Point: "#00BCD4",
  Unknown: "#9CA3AF",
};

interface PortLike {
  id?: string | number | null;
  type?: string | null;
}

const findPortType = (
  nodeId: string | null | undefined,
  handleId: string | null | undefined,
  direction: "inputs" | "outputs",
) => {
  if (!nodeId || !handleId) return null;

  const node = flowStore.getNodeById(nodeId);
  const ports = Array.isArray((node?.data as any)?.[direction])
    ? (((node?.data as any)[direction] ?? []) as PortLike[])
    : [];
  const port = ports.find((candidate) => `${candidate.id ?? ""}` === handleId);
  return port?.type ?? null;
};

const isCompatibleType = (
  sourceType: string | null | undefined,
  targetType: string | null | undefined,
) => {
  if (!sourceType || !targetType) return true;
  if (sourceType === "Unknown" || targetType === "Unknown") return true;
  return sourceType === targetType;
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
    "outputs",
  );
  const targetType = findPortType(
    connection.target,
    connection.targetHandle,
    "inputs",
  );

  return isCompatibleType(sourceType, targetType);
};

const buildEdgeId = (connection: Connection) =>
  `e-${connection.source}-${connection.sourceHandle ?? "na"}-${connection.target}-${connection.targetHandle ?? "na"}-${Date.now()}-${Math.random()
    .toString(16)
    .slice(2, 8)}`;

// Keep store synced with local Vue Flow internal events
const handleNodesChange = (changes: NodeChange[]) => {
  const nextNodes = applyNodeChanges(changes);
  const markDirty = changes.some((change) => change.type !== "select");
  flowStore.setNodes(nextNodes, markDirty);
};

const handleEdgesChange = (changes: EdgeChange[]) => {
  const nextEdges = applyEdgeChanges(changes);
  const markDirty = changes.some((change) => change.type !== "select");
  flowStore.setEdges(nextEdges, markDirty);
};

const onConnect = (connection: Connection) => {
  if (!isConnectionValid(connection)) {
    return;
  }

  const sourceType = findPortType(
    connection.source,
    connection.sourceHandle,
    "outputs",
  );
  const edgeColor =
    PORT_TYPE_COLORS[sourceType ?? "Unknown"] ?? PORT_TYPE_COLORS.Unknown;

  const newEdge: Edge = {
    ...connection,
    id: buildEdgeId(connection),
    type: "typed",
    interactionWidth: 32,
    markerEnd: {
      type: MarkerType.ArrowClosed,
      width: 18,
      height: 18,
      color: edgeColor,
    },
    data: {
      type: sourceType ?? "Unknown",
      color: edgeColor,
    },
  };

  flowStore.addEdge(newEdge);
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

const onPaneClick = (event?: MouseEvent) => {
  if (connectionClickStartHandle.value) {
    endConnection(event, true);
  }

  flowStore.selectNode(null);
};
</script>

<style scoped>
.flow-editor-container {
  width: 100%;
  height: 100%;
  background: transparent;
}

.clearvision-flow {
  /* Override default Vue Flow handles to match our aesthetic */
  --vf-handle: var(--accent-red, #ff4d4d);
  --vf-box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.flow-editor-container.is-click-connecting :deep(.clearvision-flow .vue-flow__pane) {
  cursor: crosshair;
}

/* Guard against parent clipping/hiding of custom handles */
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
  filter: drop-shadow(0 0 5px rgba(77, 148, 255, 0.32));
}

:deep(.clearvision-flow .vue-flow__connectionline) {
  pointer-events: none;
}

/* Custom Controls overrides for light mode */
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
