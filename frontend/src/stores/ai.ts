import { defineStore } from 'pinia';
import { ref } from 'vue';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';

export interface AiMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  flowJson?: string;
  usedTools?: string[];
  isError?: boolean;
}

export const useAiStore = defineStore('ai', () => {
  const messages = ref<AiMessage[]>([
    {
      id: 'greeting',
      role: 'assistant',
      content: 'Hello! I am ClearVision AI. How can I help you build your inspection workflow today?',
      timestamp: new Date().toISOString()
    }
  ]);
  
  const isGenerating = ref(false);
  const currentModel = ref('DeepSeek-V3');
  const lastGeneratedFlowJson = ref<string | null>(null);
  const lastUsedTools = ref<string[]>([]);

  async function sendPrompt(prompt: string) {
    if (!prompt.trim() || isGenerating.value) return;

    // Add user message
    messages.value.push({
      id: Date.now().toString(),
      role: 'user',
      content: prompt,
      timestamp: new Date().toISOString()
    });

    isGenerating.value = true;
    lastGeneratedFlowJson.value = null;
    lastUsedTools.value = [];

    try {
      const response = await webMessageBridge.sendMessage(
        BridgeMessageType.AiGenerateFlow,
        { prompt },
        true
      );

      lastGeneratedFlowJson.value = response.flowJson;
      lastUsedTools.value = response.usedTools || [];

      // Add assistant response
      messages.value.push({
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.rawResponse || 'Workflow generated successfully.',
        timestamp: new Date().toISOString(),
        flowJson: response.flowJson,
        usedTools: response.usedTools
      });
      
    } catch (error: any) {
      console.error('[AiStore] Generation failed:', error);
      messages.value.push({
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `Generation failed: ${error.message || 'Unknown error'}`,
        timestamp: new Date().toISOString(),
        isError: true
      });
    } finally {
      isGenerating.value = false;
    }
  }

  function clearHistory() {
    messages.value = [
      {
        id: 'greeting',
        role: 'assistant',
        content: 'Conversation cleared. How can I help you?',
        timestamp: new Date().toISOString()
      }
    ];
    lastGeneratedFlowJson.value = null;
    lastUsedTools.value = [];
  }

  return {
    messages,
    isGenerating,
    currentModel,
    lastGeneratedFlowJson,
    lastUsedTools,
    sendPrompt,
    clearHistory
  };
});
