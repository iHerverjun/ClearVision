/**
 * UI组件库 - 基础UI组件
 * ClearVision 视觉检测软件
 */

// ==================== Button组件 ====================

/**
 * 创建按钮
 * @param {Object} options - 配置选项
 * @param {string} options.text - 按钮文本
 * @param {string} options.type - 按钮类型: primary, secondary, danger, icon
 * @param {Function} options.onClick - 点击回调
 * @param {string} options.className - 额外CSS类
 * @param {boolean} options.disabled - 是否禁用
 * @returns {HTMLButtonElement}
 */
export function createButton(options = {}) {
    const {
        text = '',
        type = 'primary',
        onClick = null,
        className = '',
        disabled = false,
        icon = null
    } = options;

    const button = document.createElement('button');
    button.className = `cv-btn cv-btn-${type} ${className}`;
    button.disabled = disabled;

    if (icon) {
        const iconSpan = document.createElement('span');
        iconSpan.className = 'cv-btn-icon';
        iconSpan.textContent = icon;
        button.appendChild(iconSpan);
    }

    if (text) {
        const textSpan = document.createElement('span');
        textSpan.className = 'cv-btn-text';
        textSpan.textContent = text;
        button.appendChild(textSpan);
    }

    if (onClick) {
        button.addEventListener('click', onClick);
    }

    return button;
}

// ==================== Input组件 ====================

/**
 * 创建输入框
 * @param {Object} options - 配置选项
 * @param {string} options.type - 输入类型: text, number, email, password
 * @param {string} options.placeholder - 占位文本
 * @param {string} options.value - 初始值
 * @param {Function} options.onChange - 变更回调
 * @param {Function} options.onBlur - 失焦回调
 * @returns {HTMLInputElement}
 */
export function createInput(options = {}) {
    const {
        type = 'text',
        placeholder = '',
        value = '',
        onChange = null,
        onBlur = null,
        className = '',
        min,
        max,
        step,
        disabled = false
    } = options;

    const input = document.createElement('input');
    input.type = type;
    input.className = `cv-input ${className}`;
    input.placeholder = placeholder;
    input.value = value;
    input.disabled = disabled;

    if (type === 'number') {
        if (min !== undefined) input.min = min;
        if (max !== undefined) input.max = max;
        if (step !== undefined) input.step = step;
    }

    if (onChange) {
        input.addEventListener('input', (e) => onChange(e.target.value, e));
    }

    if (onBlur) {
        input.addEventListener('blur', (e) => onBlur(e.target.value, e));
    }

    return input;
}

/**
 * 创建带标签的输入框
 * @param {Object} options
 * @returns {HTMLDivElement}
 */
export function createLabeledInput(options = {}) {
    const { label = '', required = false, ...inputOptions } = options;

    const container = document.createElement('div');
    container.className = 'cv-input-group';

    const labelEl = document.createElement('label');
    labelEl.className = 'cv-input-label';
    labelEl.innerHTML = `${label}${required ? '<span class="required">*</span>' : ''}`;

    const input = createInput(inputOptions);
    input.id = `input_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`;
    labelEl.htmlFor = input.id;

    container.appendChild(labelEl);
    container.appendChild(input);

    return container;
}

// ==================== Select组件 ====================

/**
 * 创建下拉选择框
 * @param {Object} options
 * @param {Array<{value, label}>} options.options - 选项列表
 * @returns {HTMLSelectElement}
 */
export function createSelect(options = {}) {
    const {
        options: items = [],
        value = '',
        onChange = null,
        className = '',
        placeholder = '请选择...'
    } = options;

    const select = document.createElement('select');
    select.className = `cv-select ${className}`;

    // 占位选项
    const placeholderOption = document.createElement('option');
    placeholderOption.value = '';
    placeholderOption.textContent = placeholder;
    placeholderOption.disabled = true;
    placeholderOption.selected = !value;
    select.appendChild(placeholderOption);

    items.forEach(item => {
        const option = document.createElement('option');
        option.value = item.value;
        option.textContent = item.label;
        if (item.value === value) {
            option.selected = true;
        }
        select.appendChild(option);
    });

    if (onChange) {
        select.addEventListener('change', (e) => onChange(e.target.value, e));
    }

    return select;
}

// ==================== Checkbox组件 ====================

/**
 * 创建复选框
 * @param {Object} options
 * @returns {HTMLLabelElement}
 */
export function createCheckbox(options = {}) {
    const {
        label = '',
        checked = false,
        onChange = null,
        className = ''
    } = options;

    const container = document.createElement('label');
    container.className = `cv-checkbox ${className}`;

    const input = document.createElement('input');
    input.type = 'checkbox';
    input.className = 'cv-checkbox-input';
    input.checked = checked;

    const checkmark = document.createElement('span');
    checkmark.className = 'cv-checkbox-mark';

    const labelEl = document.createElement('span');
    labelEl.className = 'cv-checkbox-label';
    labelEl.textContent = label;

    container.appendChild(input);
    container.appendChild(checkmark);
    container.appendChild(labelEl);

    if (onChange) {
        input.addEventListener('change', (e) => onChange(e.target.checked, e));
    }

    return container;
}

// ==================== Toast通知组件 ====================

// 延迟初始化 toast 容器，确保 DOM 已就绪
let toastContainer = null;

function getToastContainer() {
    if (!toastContainer) {
        // 检查是否已存在
        toastContainer = document.getElementById('cv-toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'cv-toast-container';
            toastContainer.className = 'cv-toast-container';
            // 确保 body 存在
            if (document.body) {
                document.body.appendChild(toastContainer);
            } else {
                // 如果 body 还不存在，等待 DOMContentLoaded
                document.addEventListener('DOMContentLoaded', () => {
                    document.body.appendChild(toastContainer);
                });
            }
        }
    }
    return toastContainer;
}

/**
 * 显示Toast通知
 * @param {string} message - 消息内容
 * @param {string} type - 类型: success, error, warning, info
 * @param {number} duration - 显示时长(毫秒)
 */
export function showToast(message, type = 'info', duration = 3000) {
    const container = getToastContainer();
    if (!container || !container.parentNode) {
        // 容器还未挂载，延迟执行
        setTimeout(() => showToast(message, type, duration), 100);
        return null;
    }
    
    const toast = document.createElement('div');
    toast.className = `cv-toast cv-toast-${type}`;

    const icons = {
        success: '✓',
        error: '✗',
        warning: '⚠',
        info: 'ℹ'
    };

    toast.innerHTML = `
        <span class="cv-toast-icon">${icons[type]}</span>
        <span class="cv-toast-message">${message}</span>
        <button class="cv-toast-close">×</button>
    `;

    // 关闭按钮
    toast.querySelector('.cv-toast-close').addEventListener('click', () => {
        removeToast(toast);
    });

    container.appendChild(toast);

    // 自动移除
    setTimeout(() => {
        removeToast(toast);
    }, duration);

    return toast;
}

function removeToast(toast) {
    toast.classList.add('cv-toast-hiding');
    setTimeout(() => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }, 300);
}

// ==================== Loading组件 ====================

/**
 * 创建加载动画
 * @param {Object} options
 * @returns {HTMLDivElement}
 */
export function createLoading(options = {}) {
    const {
        size = 'medium',
        text = '加载中...',
        fullscreen = false,
        className = ''
    } = options;

    const loading = document.createElement('div');
    loading.className = `cv-loading cv-loading-${size} ${fullscreen ? 'cv-loading-fullscreen' : ''} ${className}`;

    loading.innerHTML = `
        <div class="cv-loading-spinner">
            <div class="cv-loading-dot"></div>
            <div class="cv-loading-dot"></div>
            <div class="cv-loading-dot"></div>
        </div>
        ${text ? `<span class="cv-loading-text">${text}</span>` : ''}
    `;

    return loading;
}

/**
 * 显示全屏Loading
 * @param {string} text
 * @returns {HTMLDivElement}
 */
export function showFullscreenLoading(text = '加载中...') {
    const loading = createLoading({ fullscreen: true, text });
    document.body.appendChild(loading);
    return loading;
}

/**
 * 隐藏Loading
 * @param {HTMLDivElement} loading
 */
export function hideLoading(loading) {
    if (loading && loading.parentNode) {
        loading.parentNode.removeChild(loading);
    }
}

// ==================== Modal组件 ====================

/**
 * 创建模态框
 * @param {Object} options
 * @param {string} options.title - 标题
 * @param {HTMLElement|string} options.content - 内容
 * @param {Array<HTMLElement>} options.footer - 底部按钮数组
 * @param {Function} options.onClose - 关闭回调
 * @param {string} options.width - 宽度样式 (如 "600px")
 * @returns {HTMLDivElement}
 */
export function createModal(options = {}) {
    const {
        title = '',
        content = '',
        footer = null,
        onClose = null,
        width = null,
        onBeforeClose = null,
        onDispose = null,
        closeOnOverlayClick = true,
        closeOnEscape = true
    } = options;

    const overlay = document.createElement('div');
    overlay.className = 'cv-modal-overlay';
    overlay.setAttribute('role', 'presentation');

    const modal = document.createElement('div');
    modal.className = 'cv-modal';
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    if (width) modal.style.width = width;

    // Header
    const header = document.createElement('div');
    header.className = 'cv-modal-header';
    header.innerHTML = `
        <h3 class="cv-modal-title">${title}</h3>
        <button class="cv-modal-close" title="关闭">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M18 6L6 18M6 6L18 18" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
        </button>
    `;
    
    // Close event
    const closeBtn = header.querySelector('.cv-modal-close');
    const closeHandler = () => {
        closeModal(overlay);
    };
    closeBtn.addEventListener('click', closeHandler);

    // Body
    const body = document.createElement('div');
    body.className = 'cv-modal-body';
    if (typeof content === 'string') {
        body.innerHTML = content;
    } else if (content instanceof HTMLElement) {
        body.appendChild(content);
    }

    modal.appendChild(header);
    modal.appendChild(body);

    // Footer (optional)
    if (footer) {
        const footerEl = document.createElement('div');
        footerEl.className = 'cv-modal-footer';
        
        if (Array.isArray(footer)) {
            footer.forEach(btn => footerEl.appendChild(btn));
        } else if (footer instanceof HTMLElement) {
            footerEl.appendChild(footer);
        }
        
        modal.appendChild(footerEl);
    }

    overlay.appendChild(modal);
    document.body.appendChild(overlay);

    const keydownHandler = (e) => {
        if (e.key === 'Escape') {
            closeHandler();
        }
    };

    overlay.__cvModalState = {
        isClosing: false,
        isDisposed: false,
        onBeforeClose,
        onClose,
        onDispose,
        keydownHandler
    };

    if (closeOnOverlayClick) {
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) {
                closeHandler();
            }
        });
    }

    if (closeOnEscape) {
        document.addEventListener('keydown', keydownHandler);
    }

    // Show animation
    setTimeout(() => overlay.classList.add('show'), 10);

    return overlay;
}

/**
 * 关闭模态框
 * @param {HTMLDivElement} modalOverlay 
 */
export function closeModal(modalOverlay) {
    if (!modalOverlay) return;

    const modalState = modalOverlay.__cvModalState || null;
    if (modalState?.isClosing) {
        return;
    }

    if (typeof modalState?.onBeforeClose === 'function') {
        try {
            const shouldContinue = modalState.onBeforeClose();
            if (shouldContinue === false) {
                return;
            }
        } catch (error) {
            console.warn('[UI] Modal onBeforeClose failed:', error);
        }
    }

    if (modalState) {
        modalState.isClosing = true;
        if (typeof modalState.keydownHandler === 'function') {
            document.removeEventListener('keydown', modalState.keydownHandler);
        }
    }

    modalOverlay.classList.remove('show');
    setTimeout(() => {
        if (modalOverlay.parentNode) {
            modalOverlay.parentNode.removeChild(modalOverlay);
        }

        if (modalState && !modalState.isDisposed) {
            modalState.isDisposed = true;

            try {
                modalState.onClose?.();
            } catch (error) {
                console.warn('[UI] Modal onClose failed:', error);
            }

            try {
                modalState.onDispose?.();
            } catch (error) {
                console.warn('[UI] Modal onDispose failed:', error);
            }
        }

        delete modalOverlay.__cvModalState;
    }, 300);
}

// ==================== 导出所有组件 ====================

export default {
    createButton,
    createInput,
    createLabeledInput,
    createSelect,
    createCheckbox,
    showToast,
    createLoading,
    showFullscreenLoading,
    hideLoading,
    createModal,
    closeModal
};
