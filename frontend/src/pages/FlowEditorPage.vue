<template>
  <div 
    class="page-container" 
    @contextmenu.prevent="handleContextMenu"
  >
    <!-- Left Sidebar: Operator Library -->
    <OperatorLibrary />

    <!-- Main Canvas Area -->
    <div class="canvas-wrapper" @drop="onDrop" @dragover.prevent>
      <FlowEditor 
        @node-context-menu="onNodeContextMenu"
        @edge-context-menu="onEdgeContextMenu"
        @selection-context-menu="onSelectionContextMenu"
        @edge-double-click="onEdgeDoubleClick"
      />
      <PropertyPanel />
      <LintPanel />
    </div>

    <!-- Context Menu -->
    <ContextMenu
      :is-visible="contextMenuState.isVisible"
      :position="contextMenuState.position"
      :context-type="contextMenuState.type"
      :target-id="contextMenuState.targetId"
      :selected-nodes="contextMenuState.selectedNodes"
      :selected-edges="contextMenuState.selectedEdges"
      @close="closeContextMenu"
      @add-node="handleAddNodeFromContext"
      @paste="handlePaste"
      @select-all="handleSelectAll"
      @copy="handleCopy"
      @duplicate="handleDuplicate"
      @copy-node-id="handleCopyNodeId"
      @view-output="handleViewOutput"
      @delete-node="handleDeleteNode"
      @delete-edge="handleDeleteEdge"
      @insert-reroute="handleInsertReroute"
      @set-edge-style="handleSetEdgeStyle"
      @group-nodes="handleGroupNodes"
      @copy-selection="handleCopySelection"
      @delete-selection="handleDeleteSelection"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from "vue";
import { MarkerType, useVueFlow } from "@vue-flow/core";
import FlowEditor from "../components/flow/FlowEditor.vue";
import ContextMenu from "../components/flow/ContextMenu.vue";
import { FLOW_INSTANCE_ID } from "../components/flow/flow.constants";
import OperatorLibrary from "../components/flow/OperatorLibrary.vue";
import PropertyPanel from "../components/flow/PropertyPanel.vue";
import LintPanel from "../components/flow/LintPanel.vue";
import { useFlowStore, type EdgeRenderStyle } from "../stores/flow";
import { useExecutionStore } from "../stores/execution";
import { getSchemaByType } from "../config/operatorSchema";
import { getPortColor } from "../config/portTypeRegistry";
import { resolveImageSource } from "../services/imageSource";
import type { Node, Edge } from "@vue-flow/core";

const flowStore = useFlowStore();
const executionStore = useExecutionStore();

// Use screenToFlowCoordinate to convert viewport client coordinates to flow coordinates.
const { screenToFlowCoordinate, getSelectedNodes, getSelectedEdges, addSelectedNodes } = useVueFlow(FLOW_INSTANCE_ID);

// Context Menu State
const contextMenuState = reactive({
  isVisible: false,
  position: { x: 0, y: 0 },
  type: 'pane' as 'pane' | 'node' | 'edge' | 'selection',
  targetId: null as string | null,
  selectedNodes: [] as Node[],
  selectedEdges: [] as Edge[],
});

// Clipboard
const clipboard = ref<{ nodes: Node[], edges: Edge[] } | null>(null);

interface EdgeDataLike {
  type?: string;
  sourceType?: string | null;
  targetType?: string | null;
  sourceColor?: string;
  targetColor?: string;
  routeStyle?: "bezier" | "smoothstep";
}

interface PortLike {
  id?: string | number | null;
  type?: string | null;
}

const buildEdgeId = (
  source: string,
  sourceHandle: string | null | undefined,
  target: string,
  targetHandle: string | null | undefined,
) =>
  `e-${source}-${sourceHandle ?? "na"}-${target}-${targetHandle ?? "na"}-${Date.now()}-${Math.random()
    .toString(16)
    .slice(2, 8)}`;

const resolveEdgeRenderStyle = (edge: Edge): EdgeRenderStyle => {
  if (edge.type === "pathfinding") return "pathfinding";
  const routeStyle = (edge.data as EdgeDataLike | undefined)?.routeStyle;
  return routeStyle === "smoothstep" ? "smoothstep" : "bezier";
};

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

const withEdgeStyle = (
  edge: Edge,
  style: EdgeRenderStyle,
) => {
  const existingData = (edge.data as EdgeDataLike | undefined) ?? {};
  const sourceType =
    existingData.sourceType ??
    findPortType(edge.source, edge.sourceHandle, "outputs") ??
    existingData.type ??
    "Unknown";
  const targetType =
    existingData.targetType ??
    findPortType(edge.target, edge.targetHandle, "inputs") ??
    sourceType;
  const sourceColor = existingData.sourceColor ?? getPortColor(sourceType);
  const targetColor = existingData.targetColor ?? getPortColor(targetType);
  const routeStyle =
    style === "smoothstep"
      ? "smoothstep"
      : style === "bezier"
        ? "bezier"
        : existingData.routeStyle === "smoothstep"
          ? "smoothstep"
          : "bezier";

  return {
    ...edge,
    type: style === "pathfinding" ? "pathfinding" : "typed",
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
      ...existingData,
      type: sourceType,
      sourceType,
      targetType,
      sourceColor,
      targetColor,
      routeStyle,
    },
  } as Edge;
};

const insertRerouteForEdge = (
  edgeId: string,
  screenPosition?: { x: number; y: number },
) => {
  const originalEdge = flowStore.edges.find((edge) => edge.id === edgeId);
  if (!originalEdge) return;

  const sourceType =
    (originalEdge.data as EdgeDataLike | undefined)?.sourceType ??
    findPortType(originalEdge.source, originalEdge.sourceHandle, "outputs") ??
    (originalEdge.data as EdgeDataLike | undefined)?.type ??
    "Any";
  const targetType =
    (originalEdge.data as EdgeDataLike | undefined)?.targetType ??
    findPortType(originalEdge.target, originalEdge.targetHandle, "inputs") ??
    sourceType;
  const style = resolveEdgeRenderStyle(originalEdge);

  const rerouteId = `reroute-${Date.now()}-${Math.random().toString(16).slice(2, 7)}`;
  const flowPosition = screenPosition
    ? screenToFlowCoordinate(screenPosition)
    : (() => {
        const sourceNode = flowStore.getNodeById(originalEdge.source);
        const targetNode = flowStore.getNodeById(originalEdge.target);
        if (!sourceNode || !targetNode) {
          return { x: 0, y: 0 };
        }
        return {
          x: (sourceNode.position.x + targetNode.position.x) / 2,
          y: (sourceNode.position.y + targetNode.position.y) / 2,
        };
      })();

  const rerouteNode: Node = {
    id: rerouteId,
    type: "reroute-node",
    position: {
      x: flowPosition.x - 8,
      y: flowPosition.y - 8,
    },
    data: {
      name: "Reroute",
      category: "Routing",
      rawType: "Reroute",
      rerouteType: sourceType,
      inputs: [{ id: "in", label: "In", type: sourceType }],
      outputs: [{ id: "out", label: "Out", type: sourceType }],
      legacyConfig: {},
    },
  };

  const firstEdge = withEdgeStyle(
    {
      id: buildEdgeId(
        originalEdge.source,
        originalEdge.sourceHandle,
        rerouteId,
        "in",
      ),
      source: originalEdge.source,
      sourceHandle: originalEdge.sourceHandle,
      target: rerouteId,
      targetHandle: "in",
      data: {
        sourceType,
        targetType: sourceType,
      },
    } as Edge,
    style,
  );

  const secondEdge = withEdgeStyle(
    {
      id: buildEdgeId(
        rerouteId,
        "out",
        originalEdge.target,
        originalEdge.targetHandle,
      ),
      source: rerouteId,
      sourceHandle: "out",
      target: originalEdge.target,
      targetHandle: originalEdge.targetHandle,
      data: {
        sourceType,
        targetType,
      },
    } as Edge,
    style,
  );

  flowStore.addNode(rerouteNode);
  flowStore.setEdges(
    flowStore.edges
      .filter((edge) => edge.id !== edgeId)
      .concat([firstEdge, secondEdge]),
  );
};

const onDrop = (event: DragEvent) => {
  event.preventDefault();

  const operatorType = event.dataTransfer?.getData("application/vueflow");

  if (!operatorType) return;

  const target = event.target as HTMLElement | null;
  if (!target?.closest(".clearvision-flow")) {
    return;
  }

  const position = screenToFlowCoordinate({
    x: event.clientX,
    y: event.clientY,
  });

  const schema = getSchemaByType(operatorType);
  if (!schema) {
    console.warn(`[FlowEditorPage] Unknown operator dragged: ${operatorType}`);
    return;
  }

  const nodeId = `${operatorType}-${Date.now()}-${Math.random()
    .toString(16)
    .slice(2, 8)}`;

  // Build default configuration from schema
  const defaultLegacyConfig: Record<string, any> = {};
  schema.parameters.forEach((p) => {
    defaultLegacyConfig[p.name] = p.defaultValue;
  });

  // Decide visualization type (fallback to basic operator-node if not a special Acquisition node)
  const vwType =
    schema.category === "输入" ||
    schema.category === "Acquisition" ||
    operatorType === "ImageAcquisition"
      ? "image-acquisition"
      : "operator-node";

  flowStore.addNode({
    id: nodeId,
    type: vwType,
    position,
    data: {
      name: schema.displayName,
      category: schema.category,
      iconType: schema.icon,
      rawType: operatorType,
      // Minimal defaults for ports; usually populated further from backend data,
      // but we spawn 1 IN and 1 OUT by default logic unless it's a pure source or sink
      inputs:
        schema.category === "输入"
          ? []
          : [{ id: `${nodeId}-in-0`, label: "Input", type: "Image" }],
      outputs:
        schema.category === "输出"
          ? []
          : [{ id: `${nodeId}-out-0`, label: "Output", type: "Image" }],
      legacyConfig: defaultLegacyConfig as Record<string, any>,
    },
  });
};

// Context Menu Handlers
const handleContextMenu = (event: MouseEvent) => {
  // Only show pane context menu if clicking on the canvas wrapper itself
  const target = event.target as HTMLElement;
  const isOnCanvas = target.closest('.canvas-wrapper') && !target.closest('.vue-flow__node') && !target.closest('.vue-flow__edge');
  
  if (isOnCanvas) {
    const selectedNodes = getSelectedNodes.value;
    const selectedEdges = getSelectedEdges.value;
    
    // Check if we have multi-selection
    if (selectedNodes.length > 1) {
      contextMenuState.type = 'selection';
      contextMenuState.selectedNodes = selectedNodes;
      contextMenuState.selectedEdges = selectedEdges;
    } else {
      contextMenuState.type = 'pane';
      contextMenuState.selectedNodes = [];
      contextMenuState.selectedEdges = [];
    }
    
    contextMenuState.isVisible = true;
    contextMenuState.position = { x: event.clientX, y: event.clientY };
    contextMenuState.targetId = null;
  }
};

const onNodeContextMenu = (event: { node: Node; originalEvent: MouseEvent }) => {
  contextMenuState.isVisible = true;
  contextMenuState.position = { x: event.originalEvent.clientX, y: event.originalEvent.clientY };
  contextMenuState.type = 'node';
  contextMenuState.targetId = event.node.id;
  contextMenuState.selectedNodes = [event.node];
  contextMenuState.selectedEdges = [];
};

const onEdgeContextMenu = (event: { edge: Edge; originalEvent: MouseEvent }) => {
  contextMenuState.isVisible = true;
  contextMenuState.position = { x: event.originalEvent.clientX, y: event.originalEvent.clientY };
  contextMenuState.type = 'edge';
  contextMenuState.targetId = event.edge.id;
  contextMenuState.selectedNodes = [];
  contextMenuState.selectedEdges = [event.edge];
};

const onSelectionContextMenu = (event: { nodes: Node[]; edges: Edge[]; originalEvent: MouseEvent }) => {
  contextMenuState.isVisible = true;
  contextMenuState.position = { x: event.originalEvent.clientX, y: event.originalEvent.clientY };
  contextMenuState.type = 'selection';
  contextMenuState.targetId = null;
  contextMenuState.selectedNodes = event.nodes;
  contextMenuState.selectedEdges = event.edges;
};

const onEdgeDoubleClick = (event: { edge: Edge; originalEvent: MouseEvent }) => {
  insertRerouteForEdge(event.edge.id, {
    x: event.originalEvent.clientX,
    y: event.originalEvent.clientY,
  });
};

const closeContextMenu = () => {
  contextMenuState.isVisible = false;
};

// Context Menu Action Handlers
const handleAddNodeFromContext = (position: { x: number; y: number }) => {
  // Convert screen position to canvas position
  const canvasPos = screenToFlowCoordinate(position);
  console.log('[FlowEditorPage] Add node at', canvasPos);
  // TODO: Show node picker dialog
};

const handlePaste = () => {
  if (!clipboard.value) return;
  
  const offset = { x: 20, y: 20 };
  const newNodes = clipboard.value.nodes.map(node => ({
    ...node,
    id: `${node.id}-copy-${Date.now()}`,
    position: {
      x: node.position.x + offset.x,
      y: node.position.y + offset.y,
    },
  }));
  
  newNodes.forEach(node => flowStore.addNode(node));
};

const handleSelectAll = () => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  flowStore.nodes.forEach((node: any) => {
    addSelectedNodes([node]);
  });
};

const handleCopy = (nodeId: string) => {
  const node = flowStore.getNodeById(nodeId);
  if (node) {
    clipboard.value = {
      nodes: [JSON.parse(JSON.stringify(node))],
      edges: [],
    };
  }
};

const handleDuplicate = (nodeId: string) => {
  const node = flowStore.getNodeById(nodeId);
  if (node) {
    const newNode = {
      ...JSON.parse(JSON.stringify(node)),
      id: `${nodeId}-dup-${Date.now()}`,
      position: {
        x: node.position.x + 20,
        y: node.position.y + 20,
      },
    };
    flowStore.addNode(newNode);
  }
};

const handleCopyNodeId = (nodeId: string) => {
  navigator.clipboard.writeText(nodeId);
};

const handleViewOutput = (nodeId: string) => {
  const outputImage = executionStore.getNodeOutputImage(nodeId);
  if (outputImage) {
    const imageSrc = resolveImageSource(outputImage);
    if (!imageSrc) {
      return;
    }

    const win = window.open('', '_blank');
    if (win) {
      win.document.write(`
        <html>
          <head><title>节点输出 - ${nodeId}</title></head>
          <body style="margin:0;display:flex;justify-content:center;align-items:center;min-height:100vh;background:#1a1a1a;">
            <img src="${imageSrc}" style="max-width:100%;max-height:100vh;">
          </body>
        </html>
      `);
      win.document.close();
    }
  }
};

const handleDeleteNode = (nodeId: string) => {
  flowStore.removeNode(nodeId);
};

const handleDeleteEdge = (edgeId: string) => {
  flowStore.removeEdge(edgeId);
};

const handleInsertReroute = (
  edgeId: string,
  screenPosition: { x: number; y: number },
) => {
  insertRerouteForEdge(edgeId, screenPosition);
};

const handleSetEdgeStyle = (edgeId: string, style: EdgeRenderStyle) => {
  const updated = flowStore.edges.map((edge) => {
    if (edge.id !== edgeId) {
      return edge;
    }
    return withEdgeStyle(edge, style);
  });

  flowStore.setPreferredEdgeStyle(style);
  flowStore.setEdges(updated);
};

const handleGroupNodes = (nodeIds: string[]) => {
  if (nodeIds.length > 1) {
    flowStore.createGroup(nodeIds);
  }
};

const handleCopySelection = (nodeIds: string[]) => {
  const nodes = flowStore.nodes.filter(n => nodeIds.includes(n.id));
  const edges = flowStore.edges.filter(e => 
    nodeIds.includes(e.source) && nodeIds.includes(e.target)
  );
  clipboard.value = {
    nodes: JSON.parse(JSON.stringify(nodes)),
    edges: JSON.parse(JSON.stringify(edges)),
  };
};

const handleDeleteSelection = (nodeIds: string[], edgeIds: string[]) => {
  nodeIds.forEach(id => flowStore.removeNode(id));
  edgeIds.forEach(id => flowStore.removeEdge(id));
};
</script>

<style scoped>
.page-container {
  width: 100%;
  height: 100%;
  display: flex;
  overflow: hidden;
  border-radius: 24px;
  background: var(--bg-primary, #f5f5f7);
}

.canvas-wrapper {
  flex: 1;
  position: relative;
  height: 100%;
  /* Fix vue-flow projection relative boundary */
  overflow: hidden;
}
</style>
