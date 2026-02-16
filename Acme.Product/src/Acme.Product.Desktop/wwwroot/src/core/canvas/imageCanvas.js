/**
 * 图像画布渲染器
 * 支持图像显示、缩放、平移、标注
 */

class ImageCanvas {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d');

        // 图像数据
        this.image = null;
        this.imageData = null;

        // 视图状态
        this.scale = 1;
        this.offset = { x: 0, y: 0 };
        this.minScale = 0.1;
        this.maxScale = 10;

        // 交互状态
        this.isDragging = false;
        this.dragStart = { x: 0, y: 0 };
        this.lastMouse = { x: 0, y: 0 };

        // 标注层
        this.overlays = [];
        this.selectedOverlay = null;

        // 【关键修复】记录是否有待处理的重置视图（当画布尺寸为0时）
        this._pendingResetView = false;

        // 事件处理器引用（用于销毁时移除）
        this._resizeHandler = this.resize.bind(this);
        this._mouseDownHandler = this.handleMouseDown.bind(this);
        this._mouseMoveHandler = this.handleMouseMove.bind(this);
        this._mouseUpHandler = this.handleMouseUp.bind(this);
        this._wheelHandler = this.handleWheel.bind(this);
        this._dblClickHandler = this.handleDoubleClick.bind(this);

        // 动画帧ID
        this._animationFrameId = null;

        this.initialize();
    }

    /**
     * 初始化画布
     */
    initialize() {
        this.resize();
        window.addEventListener('resize', this._resizeHandler);

        // 绑定事件
        this.canvas.addEventListener('mousedown', this._mouseDownHandler);
        this.canvas.addEventListener('mousemove', this._mouseMoveHandler);
        this.canvas.addEventListener('mouseup', this._mouseUpHandler);
        this.canvas.addEventListener('wheel', this._wheelHandler);
        this.canvas.addEventListener('dblclick', this._dblClickHandler);

        // 开始渲染循环
        this.render();
    }

    /**
     * 销毁画布，清理所有事件监听和动画循环
     */
    destroy() {
        // 停止渲染循环
        if (this._animationFrameId) {
            cancelAnimationFrame(this._animationFrameId);
            this._animationFrameId = null;
        }

        // 移除窗口事件监听
        window.removeEventListener('resize', this._resizeHandler);

        // 移除画布事件监听
        this.canvas.removeEventListener('mousedown', this._mouseDownHandler);
        this.canvas.removeEventListener('mousemove', this._mouseMoveHandler);
        this.canvas.removeEventListener('mouseup', this._mouseUpHandler);
        this.canvas.removeEventListener('wheel', this._wheelHandler);
        this.canvas.removeEventListener('dblclick', this._dblClickHandler);

        // 清理资源
        this.image = null;
        this.imageData = null;
        this.overlays = [];
        this.selectedOverlay = null;
    }

    /**
     * 调整画布大小
     */
    resize() {
        const container = this.canvas.parentElement;
        const newWidth = container.clientWidth;
        const newHeight = container.clientHeight;
        
        // 【关键修复】如果尺寸从0变为非0且有待处理的重置视图，执行重置
        const wasZero = this.canvas.width === 0 || this.canvas.height === 0;
        const isNowNonZero = newWidth > 0 && newHeight > 0;
        
        this.canvas.width = newWidth;
        this.canvas.height = newHeight;
        
        // 如果之前因为尺寸为0而延迟了resetView，现在重新尝试
        if (wasZero && isNowNonZero && this._pendingResetView && this.image) {
            console.log('[ImageCanvas] 画布尺寸变为非0，执行延迟的重置视图');
            this._pendingResetView = false;
            this.resetView();
        } else {
            this.render();
        }
    }

    /**
     * 加载图像
     */
    loadImage(imageSource) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            
            img.onload = () => {
                this.image = img;
                this.resetView();
                this.render();
                resolve(img);
            };
            
            img.onerror = () => {
                reject(new Error('图像加载失败'));
            };
            
            if (typeof imageSource === 'string') {
                img.src = imageSource;
            } else if (imageSource instanceof Blob) {
                img.src = URL.createObjectURL(imageSource);
            } else if (imageSource instanceof ArrayBuffer) {
                const blob = new Blob([imageSource]);
                img.src = URL.createObjectURL(blob);
            } else if (imageSource instanceof Uint8Array) {
                const blob = new Blob([imageSource]);
                img.src = URL.createObjectURL(blob);
            }
        });
    }

    /**
     * 加载图像数据（字节数组）
     */
    loadImageData(byteArray, format = 'png') {
        const blob = new Blob([byteArray], { type: `image/${format}` });
        return this.loadImage(blob);
    }

    /**
     * 从共享缓冲区加载图像 (RGBA格式)
     */
    loadImageFromBuffer(buffer, width, height) {
        try {
            // buffer 是 ArrayBuffer
            // 假设格式为 RGBA (4 bytes per pixel)
            const pixelData = new Uint8ClampedArray(buffer);
            const imageData = new ImageData(pixelData, width, height);
            
            createImageBitmap(imageData).then(bitmap => {
                this.image = bitmap;
                // 如果是第一帧，重置视图；否则保持视图状态以支持视频流
                // 但这里简单处理，如果是第一次加载才重置
                if (this.scale === 1 && this.offset.x === 0 && this.offset.y === 0) {
                    this.resetView();
                }
                this.render();
            }).catch(err => {
                console.error('CreateImageBitmap failed:', err);
            });
        } catch (e) {
            console.error('loadImageFromBuffer failed:', e);
        }
    }

    /**
     * 重置视图
     */
    resetView() {
        if (!this.image) return;
        
        const canvasWidth = this.canvas.width;
        const canvasHeight = this.canvas.height;
        const imageWidth = this.image.width;
        const imageHeight = this.image.height;
        
        // 【关键修复】如果画布尺寸为0（容器隐藏时），不计算缩放，等到可见时再处理
        if (canvasWidth === 0 || canvasHeight === 0) {
            console.warn('[ImageCanvas] 画布尺寸为0, 延迟重置视图');
            this._pendingResetView = true; // 标记有待处理的重置
            return;
        }
        
        // 成功重置视图，清除待处理标志
        this._pendingResetView = false;
        
        // 计算适应画布的缩放比例
        const scaleX = canvasWidth / imageWidth;
        const scaleY = canvasHeight / imageHeight;
        this.scale = Math.min(scaleX, scaleY) * 0.9; // 留一些边距
        
        // 居中显示
        this.offset.x = (canvasWidth - imageWidth * this.scale) / 2;
        this.offset.y = (canvasHeight - imageHeight * this.scale) / 2;
        
        this.render();
    }

    /**
     * 缩放到适应屏幕
     */
    fitToScreen() {
        this.resetView();
    }

    /**
     * 缩放到实际大小
     */
    actualSize() {
        if (!this.image) return;
        this.scale = 1;
        this.offset.x = (this.canvas.width - this.image.width) / 2;
        this.offset.y = (this.canvas.height - this.image.height) / 2;
        this.render();
    }

    /**
     * 添加标注
     */
    addOverlay(type, x, y, width, height, options = {}) {
        const overlay = {
            id: `overlay_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
            type, // 'rectangle', 'circle', 'polygon', 'text'
            x, y, width, height,
            color: options.color || '#ff0000',
            lineWidth: options.lineWidth || 2,
            fill: options.fill || false,
            fillColor: options.fillColor || 'rgba(255, 0, 0, 0.2)',
            text: options.text || '',
            visible: true,
            ...options
        };
        
        this.overlays.push(overlay);
        this.render();
        return overlay;
    }

    /**
     * 删除标注
     */
    removeOverlay(overlayId) {
        this.overlays = this.overlays.filter(o => o.id !== overlayId);
        if (this.selectedOverlay === overlayId) {
            this.selectedOverlay = null;
        }
        this.render();
    }

    /**
     * 清空标注
     */
    clearOverlays() {
        this.overlays = [];
        this.selectedOverlay = null;
        this.render();
    }

    /**
     * 绘制图像
     */
    drawImage() {
        if (!this.image) {
            return;
        }
        
        // 绘制图像
        this.ctx.save();
        this.ctx.translate(this.offset.x, this.offset.y);
        this.ctx.scale(this.scale, this.scale);
        this.ctx.drawImage(this.image, 0, 0);
        this.ctx.restore();
    }

    /**
     * 绘制标注
     */
    drawOverlays() {
        if (!this.image) return;
        
        this.ctx.save();
        this.ctx.translate(this.offset.x, this.offset.y);
        this.ctx.scale(this.scale, this.scale);
        
        this.overlays.forEach(overlay => {
            if (!overlay.visible) return;
            
            this.ctx.strokeStyle = overlay.color;
            this.ctx.lineWidth = overlay.lineWidth / this.scale; // 保持线宽恒定
            this.ctx.fillStyle = overlay.fillColor;
            
            switch (overlay.type) {
                case 'rectangle':
                    this.ctx.beginPath();
                    this.ctx.rect(overlay.x, overlay.y, overlay.width, overlay.height);
                    if (overlay.fill) {
                        this.ctx.fill();
                    }
                    this.ctx.stroke();
                    
                    // 绘制标签文本
                    if (overlay.text) {
                        this.ctx.fillStyle = overlay.color;
                        this.ctx.font = '14px sans-serif';
                        this.ctx.textBaseline = 'bottom';
                        this.ctx.fillText(overlay.text, overlay.x, overlay.y - 2);
                    }
                    break;
                    
                case 'circle':
                    this.ctx.beginPath();
                    this.ctx.arc(
                        overlay.x + overlay.width / 2,
                        overlay.y + overlay.height / 2,
                        Math.min(overlay.width, overlay.height) / 2,
                        0,
                        Math.PI * 2
                    );
                    if (overlay.fill) {
                        this.ctx.fill();
                    }
                    this.ctx.stroke();
                    break;
                    
                case 'text':
                    this.ctx.fillStyle = overlay.color;
                    this.ctx.font = `${overlay.fontSize || 14}px sans-serif`;
                    this.ctx.fillText(overlay.text, overlay.x, overlay.y);
                    break;
            }
            
            // 绘制选中高亮
            if (overlay.id === this.selectedOverlay) {
                this.ctx.strokeStyle = '#1890ff';
                this.ctx.lineWidth = 3 / this.scale;
                this.ctx.setLineDash([5 / this.scale, 5 / this.scale]);
                this.ctx.strokeRect(overlay.x - 5, overlay.y - 5, overlay.width + 10, overlay.height + 10);
                this.ctx.setLineDash([]);
            }
        });
        
        this.ctx.restore();
    }

    /**
     * 渲染循环
     */
    render() {
        // 清空画布
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        
        // 绘制背景 - 浅色主题
        this.ctx.fillStyle = '#f5f5f5';
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
        
        // 绘制图像
        this.drawImage();
        
        // 绘制标注
        this.drawOverlays();
        
        // 显示信息
        this.drawInfo();

        this._animationFrameId = requestAnimationFrame(() => this.render());
    }

    /**
     * 绘制信息
     */
    drawInfo() {
        if (!this.image) return;
        
        this.ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
        this.ctx.fillRect(10, 10, 200, 60);
        
        this.ctx.fillStyle = '#fff';
        this.ctx.font = '12px sans-serif';
        this.ctx.textAlign = 'left';
        this.ctx.textBaseline = 'top';
        
        this.ctx.fillText(`尺寸: ${this.image.width} x ${this.image.height}`, 15, 15);
        this.ctx.fillText(`缩放: ${(this.scale * 100).toFixed(1)}%`, 15, 35);
        this.ctx.fillText(`标注: ${this.overlays.length}`, 15, 55);
    }

    /**
     * 处理鼠标按下
     */
    handleMouseDown(e) {
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left - this.offset.x) / this.scale;
        const y = (e.clientY - rect.top - this.offset.y) / this.scale;
        
        // 检查是否点击了标注
        for (let i = this.overlays.length - 1; i >= 0; i--) {
            const overlay = this.overlays[i];
            if (x >= overlay.x && x <= overlay.x + overlay.width &&
                y >= overlay.y && y <= overlay.y + overlay.height) {
                this.selectedOverlay = overlay.id;
                this.isDragging = true;
                this.dragStart = { x: x - overlay.x, y: y - overlay.y };
                this.render();
                return;
            }
        }
        
        this.selectedOverlay = null;
        this.isDragging = true;
        this.dragStart = { x: e.clientX, y: e.clientY };
        this.lastMouse = { x: e.clientX, y: e.clientY };
    }

    /**
     * 处理鼠标移动
     */
    handleMouseMove(e) {
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left - this.offset.x) / this.scale;
        const y = (e.clientY - rect.top - this.offset.y) / this.scale;
        
        if (this.isDragging) {
            if (this.selectedOverlay) {
                // 拖拽标注
                const overlay = this.overlays.find(o => o.id === this.selectedOverlay);
                if (overlay) {
                    overlay.x = x - this.dragStart.x;
                    overlay.y = y - this.dragStart.y;
                }
            } else {
                // 平移画布
                const dx = e.clientX - this.lastMouse.x;
                const dy = e.clientY - this.lastMouse.y;
                this.offset.x += dx;
                this.offset.y += dy;
                this.lastMouse = { x: e.clientX, y: e.clientY };
            }
        }
    }

    /**
     * 处理鼠标释放
     */
    handleMouseUp() {
        this.isDragging = false;
    }

    /**
     * 处理滚轮缩放
     */
    handleWheel(e) {
        e.preventDefault();
        
        const rect = this.canvas.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        
        const delta = e.deltaY > 0 ? 0.9 : 1.1;
        const newScale = Math.max(this.minScale, Math.min(this.maxScale, this.scale * delta));
        
        if (newScale !== this.scale) {
            // 以鼠标位置为中心缩放
            this.offset.x = mouseX - (mouseX - this.offset.x) * (newScale / this.scale);
            this.offset.y = mouseY - (mouseY - this.offset.y) * (newScale / this.scale);
            this.scale = newScale;
        }
    }

    /**
     * 处理双击
     */
    handleDoubleClick() {
        this.fitToScreen();
    }

    /**
     * 获取当前视图状态
     */
    getViewState() {
        return {
            scale: this.scale,
            offset: { ...this.offset }
        };
    }

    /**
     * 设置视图状态
     */
    setViewState(state) {
        this.scale = state.scale;
        this.offset = { ...state.offset };
        this.render();
    }

    /**
     * 清空画布
     */
    clear() {
        this.image = null;
        this.overlays = [];
        this.selectedOverlay = null;
        this.resetView();
    }
}

export default ImageCanvas;
export { ImageCanvas };
