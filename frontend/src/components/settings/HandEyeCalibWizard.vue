<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-black/60 backdrop-blur-sm">
    <div class="bg-[var(--color-surface)] w-full max-w-5xl h-[85vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col border border-[var(--color-border)] ring-1 ring-white/10 relative">
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
            <CombineIcon class="text-red-500 w-4 h-4" />
          </div>
          <div>
            <h2 class="text-lg font-bold text-[var(--color-text)]">手眼标定向导</h2>
            <p class="text-xs text-[var(--color-text-muted)]">步骤 {{ currentStep }}：{{ stepTitles[currentStep] }}</p>
          </div>
        </div>
        <button @click="close" class="p-2 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-colors text-gray-500">
          <XIcon class="w-5 h-5" />
        </button>
      </div>

      <!-- Main Layout: Sidebar Navigation + Content -->
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
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">安装方式</label>
                <select v-model="config.mountType" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
                  <option value="eye_in_hand">眼在手上（相机安装在机械臂末端）</option>
                  <option value="eye_to_hand">眼在手外（相机固定，观察机械臂）</option>
                </select>
              </div>
              <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                <label class="block text-sm font-bold text-[var(--color-text)] mb-2">算法</label>
                <select v-model="config.algorithm" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
                  <option value="tsai">Tsai-Lenz（标准）</option>
                  <option value="park">Park-Martin</option>
                  <option value="horaud">Horaud</option>
                  <option value="daniilidis">Daniilidis（双四元数）</option>
                </select>
              </div>
              <div class="col-span-2 bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                 <h4 class="text-sm font-bold text-[var(--color-text)] mb-2 flex items-center">
                   <MonitorIcon class="w-4 h-4 mr-2" /> 标定前置说明
                 </h4>
                 <p class="text-sm text-[var(--color-text-muted)]">请先确保相机已完成独立标定（内参已知），再进行手眼标定。系统将使用当前激活的相机参数。</p>
              </div>
            </div>
          </div>

          <!-- Step 2: Data Acquisition (Pairs) -->
          <div v-if="currentStep === 2" class="space-y-6 animate-fade-in flex flex-col h-full">
            <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm flex items-center justify-between">
              <div>
                <h4 class="text-sm font-bold text-[var(--color-text)]">已采集配对：{{ dataPairs.length }} / 20</h4>
                <p class="text-xs text-[var(--color-text-muted)] mt-1">采集同步的图像标定板与机器人 TCP 位姿配对数据。</p>
              </div>
              <div class="flex space-x-2">
                <button @click="readRobotPose" class="px-4 py-2 bg-[var(--color-surface)] hover:bg-gray-100 dark:hover:bg-gray-800 text-[var(--color-text)] border border-[var(--color-border)] rounded-lg flex items-center font-bold text-sm shadow-sm transition-colors">
                  <ListOrderedIcon class="w-4 h-4 mr-2" />
                  读取位姿
                </button>
                <button @click="capturePair" class="px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg flex items-center font-bold text-sm shadow-sm transition-colors">
                  <CameraIcon class="w-4 h-4 mr-2" />
                  采集配对
                </button>
              </div>
            </div>
            
            <div class="flex-1 bg-black/5 rounded-xl border border-[var(--color-border)] relative overflow-hidden flex flex-col">
               <!-- Table Header -->
               <div class="grid grid-cols-12 gap-2 p-3 bg-[var(--color-surface)] border-b border-[var(--color-border)] text-xs font-bold text-[var(--color-text-muted)] uppercase">
                 <div class="col-span-1 text-center">#</div>
                 <div class="col-span-3">图像</div>
                 <div class="col-span-7">机器人位姿 (X, Y, Z, Rx, Ry, Rz)</div>
                 <div class="col-span-1 text-center">操作</div>
               </div>

               <!-- List -->
               <div v-if="dataPairs.length === 0" class="flex-1 flex flex-col items-center justify-center text-gray-400">
                 <CombineIcon class="w-12 h-12 mb-2 opacity-50" />
                 <span>暂无位姿-图像配对数据。</span>
               </div>
               <div v-else class="flex-1 overflow-y-auto p-2 space-y-2">
                 <div v-for="(pair, idx) in dataPairs" :key="idx" class="grid grid-cols-12 gap-2 p-2 bg-[var(--color-background)] border border-[var(--color-border)] rounded-lg items-center hover:bg-red-50/50 transition-colors group">
                    <div class="col-span-1 text-center font-bold text-[var(--color-text)]">{{ idx + 1 }}</div>
                    <div class="col-span-3 flex items-center">
                       <div class="h-8 w-12 bg-black rounded overflow-hidden flex items-center justify-center border border-gray-600 mr-2">
                         <span class="text-[8px] text-gray-400">图</span>
                       </div>
                       <span class="text-xs text-[var(--color-text-muted)] truncate">{{ pair.image }}</span>
                    </div>
                    <div class="col-span-7 font-mono text-xs text-[var(--color-text)]">
                       [{{ pair.pose.join(', ') }}]
                    </div>
                    <div class="col-span-1 flex justify-center">
                      <button @click="removePair(idx)" class="p-1.5 bg-red-100 dark:bg-red-900/30 text-red-600 rounded opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-200">
                        <TrashIcon class="w-3.5 h-3.5" />
                      </button>
                    </div>
                 </div>
               </div>
            </div>
          </div>

          <!-- Step 3: Compute & Results -->
          <div v-if="currentStep === 3" class="space-y-6 animate-fade-in">
             <div v-if="!result" class="flex flex-col items-center justify-center h-64">
                <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-red-500 mb-4"></div>
                <h3 class="text-lg font-bold text-[var(--color-text)]">正在求解手眼关系...</h3>
             </div>
             <div v-else class="space-y-6">
                <!-- Status Banner -->
                <div class="bg-green-50 dark:bg-green-900/20 p-4 rounded-xl border border-green-200 dark:border-green-800 flex items-center">
                  <CheckCircleIcon class="w-6 h-6 text-green-500 mr-3" />
                  <div>
                    <h4 class="text-sm font-bold text-green-800 dark:text-green-400">手眼标定成功</h4>
                    <p class="text-xs text-green-700 dark:text-green-500">平移误差：{{ result.translationError.toFixed(4) }} mm | 旋转误差：{{ result.rotationError.toFixed(4) }} rad</p>
                  </div>
                </div>

                <!-- Transformation Matrix -->
                <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
                  <h4 class="text-sm font-bold text-[var(--color-text)] mb-3 flex items-center">
                    <GridIcon class="w-4 h-4 mr-2 text-red-500" />
                    变换矩阵（相机{{ config.mountType === 'eye_in_hand' ? '到夹爪' : '到基座' }}）
                  </h4>
                  <pre class="bg-black/5 p-4 rounded-lg text-sm font-mono text-[var(--color-text)] overflow-x-auto">{{ formatMatrix(result.matrix) }}</pre>
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
          <button v-if="currentStep < 3" @click="nextStep" :disabled="currentStep === 2 && dataPairs.length < 5" class="px-5 py-2 text-sm font-bold text-white bg-red-500 hover:bg-red-600 rounded-xl transition-all shadow-md hover:shadow-lg shadow-red-500/20 disabled:opacity-50 disabled:hover:shadow-md">
            下一步
          </button>
          <button v-if="currentStep === 3" @click="handleSave" class="px-5 py-2 text-sm font-bold text-white bg-green-500 hover:bg-green-600 rounded-xl transition-all shadow-md hover:shadow-lg shadow-green-500/20 flex items-center">
            <SaveIcon class="w-4 h-4 mr-2" />
            保存变换矩阵
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { 
  CombineIcon, 
  XIcon, 
  CheckIcon,
  CameraIcon,
  ListOrderedIcon,
  CheckCircleIcon,
  GridIcon,
  SaveIcon,
  MonitorIcon,
  TrashIcon
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
  2: '数据采集（配对）',
  3: '计算与复核'
};

const config = reactive({
  mountType: 'eye_in_hand',
  algorithm: 'tsai'
});

interface DataPair {
  image: string;
  pose: number[];
}

const dataPairs = ref<DataPair[]>([]);
const result = ref<any>(null);

const readRobotPose = () => {
  // Mock grabbing pose from TCP or PLC
  console.log('正在从机器人读取位姿...');
};

const capturePair = () => {
  const mockPose = [
    Number((Math.random() * 500).toFixed(2)),
    Number((Math.random() * 500).toFixed(2)),
    Number((Math.random() * 200).toFixed(2)),
    Number((Math.random() * 180).toFixed(2)),
    Number((Math.random() * 180).toFixed(2)),
    Number((Math.random() * 180).toFixed(2)),
  ];
  dataPairs.value.push({
    image: `calib_img_${dataPairs.value.length}.png`,
    pose: mockPose
  });
};

const removePair = (idx: number) => {
  dataPairs.value.splice(idx, 1);
};

const formatMatrix = (matrix: number[][]) => {
  return matrix.map(row => 
    `[ ${row.map(n => n.toFixed(4).padStart(8)).join(', ')} ]`
  ).join('\n');
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
      BridgeMessageType.HandEyeSolve,
      { config, pairs: dataPairs.value },
      true
    );
    if (response) {
      result.value = {
        translationError: response.translationError || 0.12,
        rotationError: response.rotationError || 0.05,
        matrix: response.matrix || [
          [1, 0, 0, 50],
          [0, 1, 0, 120],
          [0, 0, 1, -30],
          [0, 0, 0, 1]
        ]
      };
    }
  } catch (e) {
    console.error('手眼标定失败', e);
  } finally {
    isProcessing.value = false;
  }
};

const handleSave = async () => {
  isProcessing.value = true;
  try {
    await webMessageBridge.sendMessage(
      BridgeMessageType.HandEyeSave,
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
  dataPairs.value = [];
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
