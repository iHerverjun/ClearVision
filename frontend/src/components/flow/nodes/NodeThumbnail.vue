<template>
  <div class="node-thumbnail" v-if="displayImageSrc" @click="openImageViewer">
    <img
      :src="displayImageSrc"
      :alt="altText"
      class="thumbnail-image"
      @error="handleImageError"
    />
    <div class="thumbnail-overlay">
      <ZoomInIcon class="zoom-icon" />
    </div>
  </div>
  <div class="node-thumbnail placeholder" v-else>
    <ImageIcon class="placeholder-icon" />
    <span class="placeholder-text">{{
      imageLoadFailed ? "预览加载失败" : "无预览"
    }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { ZoomInIcon, ImageIcon } from 'lucide-vue-next';
import { resolveImageSource } from '../../../services/imageSource';

interface Props {
  outputImage?: string | null;
  nodeId?: string;
  nodeName?: string;
}

const props = withDefaults(defineProps<Props>(), {
  outputImage: null,
  nodeId: '',
  nodeName: '节点',
});

const imageSrc = computed(() => resolveImageSource(props.outputImage));
const imageLoadFailed = ref<boolean>(false);
const displayImageSrc = computed(() =>
  imageLoadFailed.value ? '' : imageSrc.value,
);
const altText = computed(() => `${props.nodeName} 输出图像`);

watch(imageSrc, () => {
  imageLoadFailed.value = false;
});

const escapeHtml = (value: string): string => {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
};

const openImageViewer = () => {
  if (!displayImageSrc.value) return;

  const safeNodeName = escapeHtml(props.nodeName || '节点');
  const safeNodeId = escapeHtml(props.nodeId || '');
  const safeAlt = escapeHtml(altText.value);

  const win = window.open('', '_blank');
  if (win) {
    win.document.write(`
      <!DOCTYPE html>
      <html>
        <head>
          <title>${safeNodeName} - 输出图像</title>
          <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body {
              display: flex;
              flex-direction: column;
              justify-content: center;
              align-items: center;
              min-height: 100vh;
              background: #1a1a1a;
              font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
            }
            .header {
              position: fixed;
              top: 0;
              left: 0;
              right: 0;
              padding: 12px 24px;
              background: rgba(0, 0, 0, 0.8);
              backdrop-filter: blur(12px);
              display: flex;
              justify-content: space-between;
              align-items: center;
              z-index: 100;
            }
            .title {
              color: #fff;
              font-size: 14px;
              font-weight: 600;
            }
            .node-id {
              color: rgba(255, 255, 255, 0.5);
              font-size: 12px;
              font-family: monospace;
            }
            .close-btn {
              background: rgba(255, 255, 255, 0.1);
              border: none;
              color: #fff;
              padding: 8px 16px;
              border-radius: 6px;
              cursor: pointer;
              font-size: 13px;
              transition: background 0.2s;
            }
            .close-btn:hover {
              background: rgba(255, 255, 255, 0.2);
            }
            .image-container {
              margin-top: 60px;
              padding: 24px;
              display: flex;
              justify-content: center;
              align-items: center;
            }
            img {
              max-width: 100%;
              max-height: calc(100vh - 120px);
              border-radius: 8px;
              box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
            }
          </style>
        </head>
        <body>
          <div class="header">
            <div>
              <div class="title">${safeNodeName} - 输出图像</div>
              <div class="node-id">${safeNodeId}</div>
            </div>
            <button class="close-btn" onclick="window.close()">关闭</button>
          </div>
          <div class="image-container">
            <img src="${displayImageSrc.value}" alt="${safeAlt}" />
          </div>
        </body>
      </html>
    `);
    win.document.close();
  }
};

const handleImageError = () => {
  imageLoadFailed.value = true;
};
</script>

<style scoped>
.node-thumbnail {
  position: relative;
  width: 100%;
  max-width: 180px;
  margin: 0 auto;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  background: rgba(0, 0, 0, 0.02);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.06));
  transition: all 0.2s ease;
}

.node-thumbnail:hover {
  border-color: var(--accent-red, #ff4d4d);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.node-thumbnail:hover .thumbnail-overlay {
  opacity: 1;
}

.thumbnail-image {
  width: 100%;
  height: auto;
  min-height: 72px;
  max-height: 96px;
  object-fit: cover;
  display: block;
}

.thumbnail-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.zoom-icon {
  width: 24px;
  height: 24px;
  color: white;
}

.node-thumbnail.placeholder {
  height: 72px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  cursor: default;
}

.node-thumbnail.placeholder:hover {
  border-color: var(--border-glass, rgba(0, 0, 0, 0.06));
  box-shadow: none;
}

.placeholder-icon {
  width: 20px;
  height: 20px;
  color: var(--text-muted, #64748b);
  opacity: 0.5;
}

.placeholder-text {
  font-size: 10px;
  color: var(--text-muted, #64748b);
  opacity: 0.7;
}
</style>
