import type { Node, Edge } from '@vue-flow/core';

// Interface definitions based on existing C# Core models
export interface LegacyPort {
  Id: string;
  Name: string;
  Description: string;
  DataType: string;
}

export interface LegacyNode {
  Id: string;
  Name: string;
  Type: string;
  Location: { X: number; Y: number };
  InputPorts: LegacyPort[];
  OutputPorts: LegacyPort[];
  Configuration: Record<string, any>;
}

export interface LegacyEdge {
  Id: string;
  SourceOperatorId: string;
  SourcePortId: string;
  TargetOperatorId: string;
  TargetPortId: string;
}

export interface LegacyProjectConfig {
  Version: string;
  Nodes: LegacyNode[];
  Edges: LegacyEdge[];
}

export class FlowSerializer {
  
  /**
   * Converts old C# backend flow Project JSON into the new Vue Flow Reactive formats
   */
  static legacyToVueFlow(legacyData: LegacyProjectConfig): { nodes: Node[], edges: Edge[] } {
    if (!legacyData || !legacyData.Nodes) {
      return { nodes: [], edges: [] };
    }

    const nodes: Node[] = legacyData.Nodes.map(node => {
      // Determine node type based on legacy type
      let vwType = 'operator-node';
      if (node.Type === 'ImageAcquisition') {
        vwType = 'image-acquisition';
      }

      return {
        id: node.Id,
        type: vwType,
        position: { x: node.Location?.X || 0, y: node.Location?.Y || 0 },
        data: {
          name: node.Name,
          category: this.inferCategory(node.Type),
          iconType: this.inferIcon(node.Type),
          inputs: (node.InputPorts || []).map((p, index) => ({
            id: this.normalizePortId(p.Id, node.Id, 'in', index),
            label: p.Name,
            type: p.DataType
          })),
          outputs: (node.OutputPorts || []).map((p, index) => ({
            id: this.normalizePortId(p.Id, node.Id, 'out', index),
            label: p.Name,
            type: p.DataType
          })),
          // We must hold the original Config blob to avoid dataloss on Save/Load
          legacyConfig: node.Configuration,
          rawType: node.Type
        }
      };
    });

    const edges: Edge[] = (legacyData.Edges || []).map((edge, index) => ({
      id: edge.Id || `e-${edge.SourceOperatorId}-${edge.TargetOperatorId}-${index}-${Date.now()}`,
      type: 'typed',
      source: edge.SourceOperatorId,
      sourceHandle: edge.SourcePortId || this.getFallbackPortId(legacyData.Nodes, edge.SourceOperatorId, 'out'),
      target: edge.TargetOperatorId,
      targetHandle: edge.TargetPortId || this.getFallbackPortId(legacyData.Nodes, edge.TargetOperatorId, 'in'),
      data: {
        // Resolve type from source port
        type: this.resolveSourcePortType(edge, legacyData.Nodes)
      }
    }));

    return { nodes, edges };
  }

  /**
   * Reconstitutes the pristine C# compatible JSON payload for saving
   */
  static vueFlowToLegacy(nodes: Node[], edges: Edge[], version: string = '1.0.0'): LegacyProjectConfig {
    const legacyNodes: LegacyNode[] = nodes.map(n => ({
      Id: n.id,
      Name: n.data?.name || n.id,
      Type: n.data.rawType || 'Unknown',
      Location: { X: n.position.x, Y: n.position.y },
      InputPorts: (Array.isArray(n.data?.inputs) ? n.data.inputs : []).map((p: any) => ({
        Id: p.id,
        Name: p.label,
        Description: '',
        DataType: p.type
      })),
      OutputPorts: (Array.isArray(n.data?.outputs) ? n.data.outputs : []).map((p: any) => ({
        Id: p.id,
        Name: p.label,
        Description: '',
        DataType: p.type
      })),
      Configuration: n.data.legacyConfig || {}
    }));

    const legacyEdges: LegacyEdge[] = edges.map(e => ({
      Id: e.id,
      SourceOperatorId: e.source,
      SourcePortId: e.sourceHandle || '',
      TargetOperatorId: e.target,
      TargetPortId: e.targetHandle || ''
    }));

    return {
      Version: version,
      Nodes: legacyNodes,
      Edges: legacyEdges
    };
  }

  // Helper methodologies for visual polish
  private static inferCategory(type: string): string {
    if (type.includes('Image')) return 'Acquisition';
    if (type.includes('Filter') || type.includes('Blur') || type.includes('Threshold')) return 'Processing';
    if (type.includes('Match') || type.includes('Detect')) return 'Vision';
    if (type.includes('Logic') || type.includes('Math')) return 'Logic';
    if (type.includes('Tcp') || type.includes('Output')) return 'Communication';
    return 'General';
  }

  private static inferIcon(type: string): string {
    if (type.includes('Image')) return 'camera';
    if (type.includes('Match')) return 'target';
    if (type.includes('Tcp')) return 'network';
    return 'box';
  }

  private static resolveSourcePortType(edge: LegacyEdge, nodes: LegacyNode[]): string {
    const sourceNode = nodes.find(n => n.Id === edge.SourceOperatorId);
    if (!sourceNode) return 'Unknown';
    const port = (sourceNode.OutputPorts || []).find(p => p.Id === edge.SourcePortId);
    return port ? port.DataType : 'Unknown';
  }

  private static normalizePortId(
    rawId: string | null | undefined,
    nodeId: string,
    direction: 'in' | 'out',
    index: number
  ): string {
    if (rawId && rawId.trim() !== '') {
      return rawId;
    }

    return `${nodeId}-${direction}-${index}`;
  }

  private static getFallbackPortId(
    nodes: LegacyNode[],
    nodeId: string,
    direction: 'in' | 'out',
  ): string | undefined {
    const node = nodes.find((candidate) => candidate.Id === nodeId);
    if (!node) return undefined;

    const ports = direction === 'out' ? node.OutputPorts : node.InputPorts;
    const firstPort = ports?.[0];
    if (!firstPort) return undefined;

    return this.normalizePortId(firstPort.Id, nodeId, direction, 0);
  }
}
