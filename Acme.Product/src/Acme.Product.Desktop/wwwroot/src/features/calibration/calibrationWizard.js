const DEPRECATION_MESSAGE = '旧版 CalibrationWizard 已废弃，请改用 core/calibration/handEyeCalibWizard.js 作为唯一公开入口。';

console.warn(`[CalibrationWizard] ${DEPRECATION_MESSAGE}`);

export class CalibrationWizard {
    constructor() {
        throw new Error(DEPRECATION_MESSAGE);
    }
}

export function getCalibrationWizardDeprecationMessage() {
    return DEPRECATION_MESSAGE;
}
