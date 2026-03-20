const DEPRECATION_MESSAGE = '旧版 AiGenerationDialog 已废弃，请改用 features/ai/aiPanel.js 作为唯一 AI 生成入口。';

console.warn(`[AiGenerationDialog] ${DEPRECATION_MESSAGE}`);

export class AiGenerationDialog {
    constructor() {
        throw new Error(DEPRECATION_MESSAGE);
    }
}

export function getAiGenerationDialogDeprecationMessage() {
    return DEPRECATION_MESSAGE;
}
