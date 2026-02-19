using Acme.Product.Application.DTOs;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 为 AI 生成的算子自动计算画布坐标（拓扑分层布局）
/// </summary>
public class AutoLayoutService
{
    private const double LayerWidth = 220.0;   // 每层的水平间距
    private const double NodeHeight = 160.0;   // 每个节点的垂直间距
    private const double StartX = 100.0;       // 起始 X 坐标
    private const double StartY = 120.0;       // 起始 Y 坐标

    /// <summary>
    /// 为流程 DTO 中的所有算子分配坐标
    /// </summary>
    public void ApplyLayout(OperatorFlowDto flowDto)
    {
        if (flowDto.Operators == null || flowDto.Operators.Count == 0)
            return;

        var layers = ComputeTopologicalLayers(flowDto);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            for (int nodeIndex = 0; nodeIndex < layer.Count; nodeIndex++)
            {
                var operatorId = layer[nodeIndex];
                var op = flowDto.Operators.FirstOrDefault(o => o.Id == operatorId);
                if (op != null)
                {
                    op.X = StartX + layerIndex * LayerWidth;
                    op.Y = StartY + nodeIndex * NodeHeight;
                }
            }
        }
    }

    private List<List<Guid>> ComputeTopologicalLayers(OperatorFlowDto flowDto)
    {
        var operatorIds = flowDto.Operators.Select(o => o.Id).ToList();

        // 计算每个算子的入度
        var inDegree = operatorIds.ToDictionary(id => id, _ => 0);

        // 构建邻接表
        var adjacency = operatorIds.ToDictionary(id => id, _ => new List<Guid>());

        if (flowDto.Connections != null)
        {
            foreach (var conn in flowDto.Connections)
            {
                if (adjacency.ContainsKey(conn.SourceOperatorId) && inDegree.ContainsKey(conn.TargetOperatorId))
                {
                    adjacency[conn.SourceOperatorId].Add(conn.TargetOperatorId);
                    inDegree[conn.TargetOperatorId]++;
                }
            }
        }

        // Kahn 算法拓扑排序，同一层的节点并行
        var layers = new List<List<Guid>>();
        var currentQueue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (currentQueue.Count > 0)
        {
            var currentLayer = new List<Guid>();
            var nextQueue = new Queue<Guid>();

            while (currentQueue.Count > 0)
            {
                var node = currentQueue.Dequeue();
                currentLayer.Add(node);

                foreach (var neighbor in adjacency[node])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        nextQueue.Enqueue(neighbor);
                }
            }

            if (currentLayer.Count > 0)
                layers.Add(currentLayer);

            currentQueue = nextQueue;
        }

        // 处理未被排入的孤立节点或环路中的节点
        var layouted = layers.SelectMany(l => l).ToHashSet();
        var remaining = operatorIds.Where(id => !layouted.Contains(id)).ToList();
        if (remaining.Count > 0)
            layers.Add(remaining);

        return layers;
    }
}
