function getOperators(flowData) {
    if (Array.isArray(flowData?.operators)) return flowData.operators;
    if (Array.isArray(flowData?.Operators)) return flowData.Operators;
    if (Array.isArray(flowData?.nodes)) return flowData.nodes;
    if (Array.isArray(flowData?.Nodes)) return flowData.Nodes;
    return [];
}

function getConnections(flowData) {
    if (Array.isArray(flowData?.connections)) return flowData.connections;
    if (Array.isArray(flowData?.Connections)) return flowData.Connections;
    return [];
}

function readOperatorId(operator) {
    return operator?.id ?? operator?.Id ?? null;
}

function readOperatorType(operator) {
    return String(operator?.type ?? operator?.Type ?? '').trim();
}

function readSourceOperatorId(connection) {
    return connection?.sourceOperatorId
        ?? connection?.SourceOperatorId
        ?? connection?.source
        ?? connection?.Source
        ?? null;
}

function readTargetOperatorId(connection) {
    return connection?.targetOperatorId
        ?? connection?.TargetOperatorId
        ?? connection?.target
        ?? connection?.Target
        ?? null;
}

function collectRelevantOperatorIds(flowData, targetNodeId) {
    const connections = getConnections(flowData);
    const visited = new Set();
    const stack = [String(targetNodeId || '')];

    while (stack.length > 0) {
        const current = stack.pop();
        if (!current || visited.has(current)) {
            continue;
        }

        visited.add(current);
        connections.forEach(connection => {
            const targetId = String(readTargetOperatorId(connection) || '');
            if (targetId !== current) {
                return;
            }

            const sourceId = String(readSourceOperatorId(connection) || '');
            if (sourceId) {
                stack.push(sourceId);
            }
        });
    }

    return visited;
}

export function createWireSequenceParameterPatch(flowData, targetNodeId, finalParameters = {}) {
    const relevantIds = collectRelevantOperatorIds(flowData, targetNodeId);
    const boxNms = getOperators(flowData).find(operator =>
        relevantIds.has(String(readOperatorId(operator) || '')) &&
        readOperatorType(operator) === 'BoxNms');

    if (!boxNms) {
        return null;
    }

    const parameters = {};
    if (Object.prototype.hasOwnProperty.call(finalParameters, 'BoxNms.ScoreThreshold')) {
        parameters.ScoreThreshold = finalParameters['BoxNms.ScoreThreshold'];
    }
    if (Object.prototype.hasOwnProperty.call(finalParameters, 'BoxNms.IouThreshold')) {
        parameters.IouThreshold = finalParameters['BoxNms.IouThreshold'];
    }

    if (Object.keys(parameters).length === 0) {
        return null;
    }

    return {
        operatorId: readOperatorId(boxNms),
        parameters
    };
}

export function buildWireSequenceFollowupHint({
    scenarioKey = 'wire-sequence-terminal',
    diagnosticCodes = [],
    suggestions = [],
    finalParameters = {},
    missingResources = []
} = {}) {
    const lines = [
        '请基于当前线序模板继续修改，只允许调整参数，不要增删线序核心节点。'
    ];

    if (scenarioKey) {
        lines.push(`场景：${scenarioKey}。`);
    }

    const normalizedDiagnostics = Array.isArray(diagnosticCodes)
        ? diagnosticCodes.filter(Boolean)
        : [];
    if (normalizedDiagnostics.length > 0) {
        lines.push(`当前诊断：${normalizedDiagnostics.join('、')}。`);
    }

    const parameterEntries = Object.entries(finalParameters || {}).filter(([key]) =>
        key === 'BoxNms.ScoreThreshold' || key === 'BoxNms.IouThreshold');
    if (parameterEntries.length > 0) {
        lines.push('建议直接修改以下参数：');
        parameterEntries.forEach(([key, value]) => {
            lines.push(`- ${key} = ${value}`);
        });
    } else if (Array.isArray(suggestions) && suggestions.length > 0) {
        lines.push('优先按以下参数建议继续修正：');
        suggestions
            .filter(item => item?.parameterName)
            .slice(0, 4)
            .forEach(item => {
                const parameterName = String(item.parameterName);
                const reason = String(item.reason || item.expectedImprovement || '').trim();
                lines.push(`- ${parameterName}${reason ? `：${reason}` : ''}`);
            });
    }

    if (Array.isArray(missingResources) && missingResources.length > 0) {
        lines.push('当前仍缺以下资源：');
        missingResources.forEach(item => {
            const resourceKey = String(item?.resourceKey || item?.ResourceKey || '').trim();
            const description = String(item?.description || item?.Description || '').trim();
            lines.push(`- ${resourceKey || '资源'}${description ? `：${description}` : ''}`);
        });
    }

    lines.push('不要改写 ExpectedLabels、ExpectedCount、ModelPath、LabelsPath。');
    return lines.join('\n');
}
