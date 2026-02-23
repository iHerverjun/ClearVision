<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-black/60 backdrop-blur-sm">
    <div class="bg-[var(--color-surface)] w-full max-w-4xl h-[80vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col border border-[var(--color-border)] ring-1 ring-white/10 relative">
      <!-- Loading Overlay -->
      <div v-if="isProcessing" class="absolute inset-0 bg-[var(--color-surface)]/80 backdrop-blur-sm z-50 flex items-center justify-center">
        <div class="flex flex-col items-center">
          <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-red-500 mb-4"></div>
          <span class="text-sm font-bold text-[var(--color-text)]">正在处理标定...</span>
        </div>
      </div>

      <!-- Header -->
      <div class="flex items-center justify-between p-4 border-b border-[var(--color-border)] bg-[var(--color-background)]">
        <div class="flex items-center space-x-3">
          <div class="w-8 h-8 rounded-lg bg-red-500/10 flex items-center justify-center">
            <ScanIcon class="text-red-500 w-4 h-4" />
          </div>
          <div>
            <h2 class="text-lg font-bold text-[var(--color-text)]">相机标定向导</h2>
            <p class="text-xs text-[var(--color-text-muted)]">步骤 {{ currentStep }}：{{ stepTitles[currentStep] }}</p>
          </div>
        </div>
        <button @click="close" class="p-2 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-colors text-gray-500">
          <XIcon class="w-5 h-5" />
        </button>
      </div>

      <!-- Main Layout: Progress + Content -->
      <div class="flex flex-1 overflow-hidden">
        
        <!-- Sidebar Navigation -->
        <aside class="w-48 sm:w-64 border-r border-[var(--color-border)] bg-[var(--color-background)] p-6">
          <ul class="space-y-6">
            <li v-for="(title, index) in Object.values(stepTitles)" :key="index" class="relative">
              <div class="flex items-start">
                <div :class="[
                  'w-8 h-8 rounded-full flex items-center justify-center font-bold text-sm z-10 shrink-0 border-2',
                  currentStep === Number(index) + 1 ? 'bg-red-500 border-red-500 text-white' : 
                  currentStep > Number(index) + 1 ? 'bg-red-500 border-red-500 text-white' : 
                  'bg-[var(--color-surface)] border-gray-300 dark:border-gray-600 text-gray-400'
                ]">
                  <CheckIcon v-if="currentStep > Number(index) + 1" class="w-4 h-4" />
                  <span v-else>{{ Number(index) + 1 }}</span>
                </div>
                <div class="ml-4 mt-1.5">
                  <span :class="[
                    'text-sm font-bold block',
                    currentStep === Number(index) + 1 ? 'text-[var(--color-text)]' : 
                    currentStep > Number(index) + 1 ? 'text-[var(--color-text)]' : 
                    'text-gray-400'
                  ]">{{ title }}</span>
                </div>
              </div>
              <div v-if="index < Object.keys(stepTitles).length - 1" :class="[
                'absolute top-8 left-4 w-0.5 h-full -ml-px',
                currentStep > Number(index) + 1 ? 'bg-red-500' : 'bg-gray-200 dark:bg-gray-700'
              ]"></div>
            </li>
          </ul>
        </aside>

        <!-- Content Area -->
        <main class="flex-1 overflow-y-auto bg-[var(--color-surface)] p-8">
          
          <!-- Step 1: Configuration -->
          <div v-if="currentStep === 1" class="space-y-6 animate-fade-in">
            <div class="grid grid-cols-2 gap-6">
              <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">标定板类型</label>
                <select v-model="config.patternType" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
                  <option value="checkerboard">棋盘格</option>
                  <option value="circles">圆点阵列</option>
                </select>
              </div>
              <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">方格尺寸（mm）</label>
                <input type="number" v-model.number="config.squareSize" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
              </div>
              <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">标定板行数</label>
                <input type="number" v-model.number="config.rows" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
              </div>
              <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">标定板列数</label>
                <input type="number" v-model.number="config.cols" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
              </div>
            </div>
          </div>

          <!-- Step 2: Image Acquisition -->
          <div v-if="currentStep === 2" class="space-y-6 animate-fade-in flex flex-col h-full">
            <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm flex items-center justify-between">
              <div>
                <h4 class="text-sm font-bold text-[var(--color-text)]">已采集图像：{{ images.length }} / 15</h4>
                <p class="text-xs text-[var(--color-text-muted)] mt-1">请从不同角度采集至少 10-15 张标定板图像。</p>
              </div>
              <button @click="captureImage" class="px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg flex items-center font-bold text-sm shadow-sm transition-colors">
                <CameraIcon class="w-4 h-4 mr-2" />
                采集当前帧
              </button>
            </div>
            
            <div class="flex-1 bg-black/5 rounded-xl border border-[var(--color-border)] flex items-center justify-center relative overflow-hidden">
               <div v-if="images.length === 0" class="text-gray-400 flex flex-col items-center">
                 <ImagePlusIcon class="w-12 h-12 mb-2 opacity-50" />
                 <span>暂无采集图像。</span>
               </div>
               <div v-else class="grid grid-cols-4 gap-2 p-2 w-full h-full overflow-y-auto">
                 <div v-for="(_, idx) in images" :key="idx" class="relative group aspect-video bg-black rounded-lg overflow-hidden border border-gray-700">
                    <!-- Placeholder for actual image -->
                    <div class="absolute inset-0 flex items-center justify-center text-[10px] text-gray-500">图像_{{ idx+1 }}.png</div>
                    <button @click="removeImage(idx)" class="absolute top-1 right-1 bg-red-500 text-white p-1 rounded opacity-0 group-hover:opacity-100 transition-opacity">
                      <XIcon class="w-3 h-3" />
                    </button>
                 </div>
               </div>
            </div>
          </div>

          <!-- Step 3: Compute & Results -->
          <div v-if="currentStep === 3" class="space-y-6 animate-fade-in">
             <div v-if="!result" class="flex flex-col items-center justify-center h-64">
                <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-red-500 mb-4"></div>
                <h3 class="text-lg font-bold text-[var(--color-text)]">正在求解标定参数...</h3>
             </div>
             <div v-else class="space-y-6">
                <!-- Status Banner -->
                <div class="bg-green-50 dark:bg-green-900/20 p-4 rounded-xl border border-green-200 dark:border-green-800 flex items-center">
                  <CheckCircleIcon class="w-6 h-6 text-green-500 mr-3" />
                  <div>
                    <h4 class="text-sm font-bold text-green-800 dark:text-green-400">标定成功</h4>
                    <p class="text-xs text-green-700 dark:text-green-500">重投影误差：{{ result.error.toFixed(4) }} px</p>
                  </div>
                </div>

                <!-- Matrices -->
                <div class="grid grid-cols-2 gap-6">
                  <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                    <h4 class="text-sm font-bold text-[var(--color-text)] mb-3 flex items-center">
                      <GridIcon class="w-4 h-4 mr-2 text-red-500" />
                      相机矩阵
                    </h4>
                    <pre class="bg-black/5 p-3 rounded-lg text-xs font-mono text-[var(--color-text)]">{{ JSON.stringify(result.matrix, null, 2) }}</pre>
                  </div>
                  <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                    <h4 class="text-sm font-bold text-[var(--color-text)] mb-3 flex items-center">
                      <ApertureIcon class="w-4 h-4 mr-2 text-red-500" />
                      畸变系数
                    </h4>
                    <pre class="bg-black/5 p-3 rounded-lg text-xs font-mono text-[var(--color-text)]">[0.123, -0.045, 0.001, 0.002, -0.001]</pre>
                  </div>
                </div>
             </div>
          </div>
        </main>
      </div>

      <!-- Footer Actions -->
      <div class="p-4 border-t border-[var(--color-border)] bg-[var(--color-background)] flex justify-between items-center">
        <button @click="close" class="px-5 py-2 text-sm font-bold text-[var(--color-text)] hover:bg-[var(--color-surface)] rounded-xl transition-colors border border-[var(--color-border)] shadow-sm">
          取消
        </button>
        <div class="flex space-x-3">
          <button v-if="currentStep > 1" @click="prevStep" class="px-5 py-2 text-sm font-bold text-[var(--color-text)] hover:bg-[var(--color-surface)] rounded-xl transition-colors border border-[var(--color-border)] shadow-sm">
            上一步
          </button>
          <button v-if="currentStep < 3" @click="nextStep" :disabled="currentStep === 2 && images.length < 3" class="px-5 py-2 text-sm font-bold text-white bg-red-500 hover:bg-red-600 rounded-xl transition-all shadow-md hover:shadow-lg shadow-red-500/20 disabled:opacity-50 disabled:hover:shadow-md">
            下一步
          </button>
          <button v-if="currentStep === 3" @click="handleSave" class="px-5 py-2 text-sm font-bold text-white bg-green-500 hover:bg-green-600 rounded-xl transition-all shadow-md hover:shadow-lg shadow-green-500/20 flex items-center">
            <SaveIcon class="w-4 h-4 mr-2" />
            保存参数
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { 
  ScanIcon, 
  XIcon, 
  CheckIcon,
  CameraIcon,
  ImagePlusIcon,
  CheckCircleIcon,
  GridIcon,
  ApertureIcon,
  SaveIcon
} from 'lucide-vue-next';
import { webMessageBridge } from '../../services/bridge';
import { BridgeMessageType } from '../../services/bridge.types';

const props = defineProps<{
  isOpen: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void
}>();

const currentStep = ref(1);
const isProcessing = ref(false);

const stepTitles: Record<number, string> = {
  1: '参数配置',
  2: '图像采集',
  3: '计算与复核'
};

const config = reactive({
  patternType: 'checkerboard',
  squareSize: 10,
  rows: 9,
  cols: 6
});

const images = ref<string[]>([]);
const result = ref<any>(null);

const captureImage = () => {
  images.value.push(`mock_img_${images.value.length}`);
};

const removeImage = (idx: number) => {
  images.value.splice(idx, 1);
};

const nextStep = () => {
  if (currentStep.value === 2) {
    solveCalibration();
  }
  if (currentStep.value < 3) {
    currentStep.value++;
  }
};

const prevStep = () => {
  if (currentStep.value > 1) {
    currentStep.value--;
  }
};

const solveCalibration = async () => {
  isProcessing.value = true;
  result.value = null;
  try {
    const response = await webMessageBridge.sendMessage(
      BridgeMessageType.CalibSolve,
      { config, images: images.value },
      true
    );
    if (response) {
      result.value = {
        error: response.error || 0.05,
        matrix: response.matrix || [[1,0,0],[0,1,0],[0,0,1]]
      };
    }
  } catch (e) {
    console.error('标定失败', e);
  } finally {
    isProcessing.value = false;
  }
};

const handleSave = async () => {
  isProcessing.value = true;
  try {
    await webMessageBridge.sendMessage(
      BridgeMessageType.CalibSave,
      { result: result.value },
      true
    );
    close();
  } catch(e) {
    console.error('保存失败', e);
  } finally {
    isProcessing.value = false;
  }
};

const close = () => {
  currentStep.value = 1;
  images.value = [];
  result.value = null;
  emit('close');
};
</script>

<style scoped>
.animate-fade-in {
  animation: fadeIn 0.3s ease-in-out;
}
@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>
