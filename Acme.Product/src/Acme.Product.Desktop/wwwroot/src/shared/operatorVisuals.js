const DEFAULT_OPERATOR_ICON_PATH =
    'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z';
const DEFAULT_CATEGORY_ICON_PATH =
    'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z';
export const DEFAULT_OPERATOR_COLOR = '#1890ff';

function parseKeyValueBlocks(blocks) {
    const entries = [];
    for (const block of blocks) {
        const normalized = String(block || '').trim();
        if (!normalized) {
            continue;
        }

        for (const line of normalized.split('\n')) {
            const trimmed = line.trim();
            if (!trimmed) {
                continue;
            }

            const separatorIndex = trimmed.indexOf('|');
            if (separatorIndex <= 0) {
                continue;
            }

            const key = trimmed.slice(0, separatorIndex).trim();
            const value = trimmed.slice(separatorIndex + 1).trim();
            if (key && value) {
                entries.push([key, value]);
            }
        }
    }

    return Object.freeze(Object.fromEntries(entries));
}

const OPERATOR_ICON_ALIAS_BLOCKS = [
    `
BlobDetection|BlobAnalysis
BoundingBoxFilter|BoxFilter
CannyEdge|EdgeDetection
FindContours|ContourDetection
GaussianBlur|Filtering
MeasureDistance|Measurement
ModbusRtuCommunication|ModbusCommunication
OnnxInference|DeepLearning
Preprocessing|Filtering
ReadImage|ImageAcquisition
Script|ScriptOperator
SemanticSegmentation_local|SemanticSegmentation
TemplateMatch|TemplateMatching
Threshold|Thresholding
`
];

const OPERATOR_ICON_BLOCKS = [
    `
AdaptiveThreshold|M3 5H1v14c0 1.1.9 2 2 2h14v-2H3V5zm4 14h14V5H7v14zM9 7h10v10H9V7z M11 15l2-3 2 3h-4zm2-6l2 3h-4l2-3z
AffineTransform|M3 3v18h18V3H3zm2 2h14v14H5V5z M6 18l6-12h6l-6 12H6z M9 15h4M12 9h4
Aggregator|M4 6h4v4H4V6zm0 8h4v4H4v-4zm12-4h4v4h-4v-4z M8 8h4v2H8V8zm0 8h4v-2H8v2z M12 12h4v-2h-4v2z
AkazeFeatureMatch|M4 8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm16 4c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zM8 16c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z M5.5 9.5l7-4M6 11l8 3 M15.5 7l3.5 5.5 M9 16.5l8-4
AngleMeasurement|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8 0-4.41 3.59-8 8-8s8 3.59 8 8-3.59 8-8 8zm-4.7-5.3l5.7-2.4 2.4-5.7-5.7 2.4-2.4 5.7zM14 11c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zM2 22h20v-2H2v2z
AnomalyDetection|M12 2L1 21h22L12 2zm0 4.5L18.53 19H5.47L12 6.5zm-1 4v4h2v-4h-2zm0 6h2v2h-2v-2z
ArcCaliper|M4 18h2v-4h4v-2H6V8H4v10zm14-10h-2v4h-4v2h4v4h2V8z M6 18a6 6 0 0 1 12 0h-2a4 4 0 0 0-8 0H6z
ArrayIndexer|M4 4h4v2H6v12h2v2H4V4zm16 0h-4v2h2v12h-2v2h4V4z M10 10h4v4h-4v-4z
BilateralFilter|M3 7v10h18V7H3zm2 2h5v6H5V9zm7 0h5v6h-5V9z M19 9h-5v6h5v-6z
BlobAnalysis|M12 2C6.48 2 2 6.48 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z
BlobLabeling|M6 2c-1.1 0-2 .9-2 2v2c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2H6zm10 0c-1.1 0-2 .9-2 2v2c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2h-2z M11 13H5v8h6v-8zm8 0h-6v8h6v-8z M1 11h2v10H1V11zm20 0h2v10h-2V11z M14 9H4V1h10v8z M11.5 5h-5v2h5V5
BoxFilter|M3 3h18v18H3V3zm2 2v14h14V5H5z M7 7h10v10H7V7z M9 9h6v6H9V9z M12 11v2
BoxNms|M5 5h10v10H5V5zm6 6h10v10H11V11z M14 8l-2 2 2 2 2-2-2-2z M8 14h2v2H8z M17 17h2v2h-2z
CalibrationLoader|M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z M12 18v-8M9 13l3-3 3 3
CaliperTool|M2 2v20h4V11h6v3l5-4-5-4v3H6V2H2zm20 8v12h-2V10h2z
CameraCalibration|M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z
CircleMeasurement|M12 4c-4.41 0-8 3.59-8 8s3.59 8 8 8 8-3.59 8-8-3.59-8-8-8zm0 14c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zm1-6v4h-2v-4H8l4-4 4 4h-3z
ClaheEnhancement|M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z M3 3h18v2H3V3z M12 1v4M8 1v4M16 1v4
CodeRecognition|M3 3h6v2H5v4H3V3zm12 0h6v6h-2V5h-4V3zM3 15h2v4h4v2H3v-6zm14 4v-2h4v-4h2v6h-6z M10 10h2v2h-2v-2zm4 0h2v2h-2v-2zm-2 2h2v2h-2v-2zm2 2h2v2h-2v-2z
ColorConversion|M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z
ColorDetection|M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z
ColorMeasurement|M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z M12 12l3 3v2h-2l-3-3z
Comment|M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H5.17L4 17.17V4h16v12z M7 9h10v2H7zM7 6h10v2H7z M7 12h7v2H7z
Comparator|M10 8h9v2h-9zm0 4h9v2h-9zm0 4h9v2h-9z M4 9l4 3-4 3v-2l2-1-2-1V9z
ConditionalBranch|M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z
ContourDetection|M3 5v14h18V5H3zm16 12H5V7h14v10zM7 9h4v4H7V9zm6 0h4v4h-4V9z M9 11h8v4H9v-4z
ContourExtrema|M12 2l3 3h-2v3h-2V5H9l3-3zm0 22l-3-3h2v-3h2v3h2l-3 3zM2 12l3-3v2h3v2H5v2l-3-3zm22 0l-3 3v-2h-3v-2h3V9l3 3z M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6z
ContourMeasurement|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M4.5 9h2c.83 0 1.5.67 1.5 1.5v3c0 .83-.67 1.5-1.5 1.5h-2 M19.5 9h-2c-.83 0-1.5.67-1.5 1.5v3c0 .83.67 1.5 1.5 1.5h2 M9 4.5v2c0 .83.67 1.5 1.5 1.5h3c.83 0 1.5-.67 1.5-1.5v-2 M9 19.5v-2c0-.83.67-1.5 1.5-1.5h3c.83 0 1.5.67 1.5 1.5v2
CoordinateTransform|M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm8.9 11c-.5-3.4-3.5-6-7-6.9v-2.2c1.4.3 2.8-.7 2.8-2.2 0-1.1-.9-2-2-2s-2 .9-2 2c0 1.4 1.4 2.5 2.8 2.2V12c-3.6.9-6.6 4-7 7H2v2h20v-2h-.1z
CopyMakeBorder|M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z M8 8h8v8H8V8z M3 3h4v2H3v-2z M17 3h4v2h-4v-2z M3 19h4v2H3v-2z M17 19h4v2h-4v-2z
CornerDetection|M3 3h6v2H5v4H3V3zm18 0h-6v2h4v4h2V3zM3 21h6v-2H5v-4H3v6zm18 0h-6v-2h4v-4h2v6z M12 8v3H9v2h3v3h2v-3h3v-2h-3V8h-2z M8 8h2v2H8z M14 14h2v2h-2z M8 14h2v2H8z M14 8h2v2h-2z
CycleCounter|M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zM6 12c0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3c-3.31 0-6-2.69-6-6z
DatabaseWrite|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.31-8.86c-1.77-.45-2.34-.94-2.34-1.67 0-.84.79-1.43 2.1-1.43 1.38 0 1.9.66 1.94 1.64h1.71c-.05-1.34-.87-2.57-2.49-2.97V5H10.9v1.69c-1.51.32-2.72 1.3-2.72 2.81 0 1.79 1.49 2.69 3.66 3.21 1.95.46 2.34 1.15 2.34 1.87 0 .53-.39 1.39-2.1 1.39-1.6 0-2.23-.72-2.32-1.64H8.04c.1 1.7 1.36 2.66 2.86 2.97V19h2.34v-1.67c1.52-.29 2.72-1.16 2.73-2.77-.01-2.2-1.9-2.96-3.66-3.42z
DeepLearning|M13 3c-4.97 0-9 4.03-9 9 0 2.58 1.08 4.9 2.82 6.53L5.4 19.95c-1.87-1.87-3-4.45-3-7.3 0-5.7 4.62-10.32 10.3-10.32S23 6.95 23 12.65c0 2.85-1.13 5.43-3 7.3l-1.42-1.42C20.32 16.9 21.4 14.58 21.4 12.65c0-4.97-4.03-9-9-9zm-4 4v3H6v2h3v3h2v-3h3V9h-3V6H9zm10 4v4h-2v2h2v3h2v-3h2v-2h-2v-4h-2z M12 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z M8 18h8v2H8v-2z
Delay|M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zM12.5 7H11v6l5.25 3.15.75-1.23-4.5-2.67z
DetectionSequenceJudge|M4 4h10v2H4V4zm0 4h10v2H4V8zm0 4h6v2H4v-2zm12-3l4 3-4 3v-2h-4v-2h4V9zm1 9H5c-1.1 0-2-.9-2-2V6h2v10h12v2z
DistanceTransform|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 15c-2.76 0-5-2.24-5-5h2c0 1.65 1.35 3 3 3s3-1.35 3-3h2c0 2.76-2.24 5-5 5zm0-10c-2.76 0-5 2.24-5 5H5c0-3.87 3.13-7 7-7s7 3.13 7 7h-2c0-2.76-2.24-5-5-5z
`
    ,
    `
DualModalVoting|M7 3h4v4H7V3zm6 0h4v4h-4V3z M4 9h6v2H4V9zm10 0h6v2h-6V9z M10 14h4v6h-4v-6z M12 13L9 11l6-2-3 4z
EdgeDetection|M3 17h18v2H3zm0-7h18v5H3zm0-7h18v5H3z
EdgeIntersection|M4 4l16 16m0-16L4 20 M12 9c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3zm0 4c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1z M5 19H3v-2l1.6-1.6L6.6 17 5 19zm14 0h2v-2l-1.6-1.6-2 1.6L19 19zM5 5H3v2l1.6 1.6L6.6 7 5 5zm14 0h2v2l-1.6 1.6-2-1.6L19 5z
EdgePairDefect|M3 4h18v2H3V4zm0 14h18v2H3v-2z M14.12 9.88l1.41-1.41L12 5l-3.54 3.54 1.41 1.41L12 7.83l2.12 2.05z M9.88 14.12L8.46 15.54 12 19l3.54-3.54-1.41-1.41L12 16.17l-2.12-2.05z M9 10L6 12l3 2v-4zm6 0v4l3-2-3-2z
EuclideanClusterExtraction|M6 6a2 2 0 1 0 .001 0zm10 0a2 2 0 1 0 .001 0zM10 16a2 2 0 1 0 .001 0zm-5 3c0-1.66 1.34-3 3-3h4c1.66 0 3 1.34 3 3v1H5v-1zm8-8h4v2h-4v-2zM7 11h4v2H7v-2z
FFT1D|M3 17h2V7H3v10zm4-4h2V5H7v8zm4 6h2V9h-2v10zm4-3h2V4h-2v12zm4 2h2V11h-2v7 M2 20h20v2H2z
Filtering|M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z
FisheyeCalibration|M12 4c-4.41 0-8 3.59-8 8h2c0-3.31 2.69-6 6-6V4zm0 16c4.41 0 8-3.59 8-8h-2c0 3.31-2.69 6-6 6v2zM4 12c0 4.41 3.59 8 8 8v-2c-3.31 0-6-2.69-6-6H4zm16 0c0-4.41-3.59-8-8-8v2c3.31 0 6 2.69 6 6h2z M8 8h8v8H8V8z
FisheyeUndistort|M12 4c4.41 0 8 3.59 8 8h-2c0-3.31-2.69-6-6-6s-6 2.69-6 6H4c0-4.41 3.59-8 8-8zm-7 12h14v2H5v-2zm2-4h10v2H7v-2zm3-4h4v2h-4V8z
ForEach|M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zM6 12c0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3c-3.31 0-6-2.69-6-6z M9 10h6v2H9zM9 14h6v2H9z
FrameAveraging|M5 5h10v10H5V5zm4 4h10v10H9V9zm-6 8h4v2H3v-2zm14-12h4v2h-4V5z
FrequencyFilter|M3 6h18v2H3V6zm4 4h10v2H7v-2zm3 4h4v6h-4v-6z M2 4l4 4-4 4V4zm20 0v8l-4-4 4-4z
GapMeasurement|M2 4h4v16H2V4zm16 0h4v16h-4V4zM8 11h8v2H8v-2zm-1 2l3 3v-2h4v2l3-3-3-3v2H10V9L7 13z
GeoMeasurement|M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83zM10 16h4v2h-4v-2zM12 11l2 3h-4l2-3z
GeometricFitting|M20 7h-2V5h-2v2h-2V5h-2v2h-2V5H8v2H6V5H4v2H2v2h2v2H2v2h2v2H2v2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v-2h-2v-2h2v-2h-2V9h2V7zm-4 8H8V9h8v6z M10 10.5c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5zm4 0c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5z
GeometricTolerance|M3 3h18v6H3V3zm2 2v2h14V5H5zm-2 6h8v10H3V11zm2 2v6h4v-6H5zm10-2h4v10h-4V11zm2 2v6h-2v-6h2z
GlcmTexture|M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z M10 7h4 M12 10v4 M7 10v4 M14 12h6
GradientShapeMatch|M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z M12 7v4H8L12 7z M12 17v-4h4L12 17z M16 11h-4V7l4 4z M8 13h4v4l-4-4z
HandEyeCalibration|M12 5c-4 0-7.27 2.11-9 5 1.73 2.89 5 5 9 5s7.27-2.11 9-5c-1.73-2.89-5-5-9-5zm0 8c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm0 4v5M9 20h6
HandEyeCalibrationValidator|M12 2l7 4v5c0 5-3.4 9.74-7 11-3.6-1.26-7-6-7-11V6l7-4zm0 6c-2.4 0-4.73 1.2-6 3 1.27 1.8 3.6 3 6 3s4.73-1.2 6-3c-1.27-1.8-3.6-3-6-3zm0 4c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1z
HistogramAnalysis|M5 19h14V5h2v16H3V5h2v14zM7 11h2v6H7v-6zm4-3h2v9h-2V8zm4-4h2v13h-2V4zm-8-3L8 3v4L4 4v4l4-3 5 5 6-6 1.4 1.4-7.4 7.4L8 6.8z
HistogramEqualization|M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z M3 21h18v-2H3v2z M2 3h2v16H2z
HttpRequest|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z M12 6h3v2h-3V6z
ImageAcquisition|M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z
ImageAdd|M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2zM3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z
ImageBlend|M2 6h20v12H2V6zm2 2v8h8V8H4zm10 0v8h6V8h-6z
ImageCompose|M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z M10 10h4v4h-4v-4z M12 6v2 M12 16v2 M6 12h2 M16 12h2
ImageCrop|M17 15h2v-2h-2v2zM7 11v6h6v-2H9v-4H7zM5 7v4h2V9h4V7H5zM17 7v4h2V9h-2V7z M3 3v4h2V5h2V3H3zM19 3v2h-2v2h2V3h-2zM19 19v-4h-2v2h-2v2h4zM3 19v-2h2v-2H3v4z
ImageDiff|M4 5h9v9H4V5zm7 5h9v9h-9v-9zM6 7h5v5H6V7zm7 5h5v5h-5v-5z M3 19h6v2H3v-2z
ImageNormalize|M5 10h3v9H5v-9zm5-4h3v13h-3V6zm5 2h3v11h-3V8z M4 4h16v2H4V4zm0 18h16v-2H4v2 M9 3l-2 2h4L9 3zm6 18l2-2h-4l2 2
ImageResize|M12 2l4 4h-3v4h-2V6H8l4-4zM4 14v4h4v-2H6v-2H4zm16 0v4h-4v-2h2v-2h2z M10 10l-4-4v3H4v-5h5v2H6l4 4-2 2zM14 14l4 4v-3h2v5h-5v-2h3l-4-4 2-2z
ImageRotate|M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L4.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z M12 10v4l3-2-3-2z
ImageSave|M19 12v7H5v-7H3v7c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2zm-6 .67l2.59-2.58L17 11.5l-5 5-5-5 1.41-1.41L11 12.67V3h2v9.67z
ImageStitching|M4 4h7v16H4V4zm9 0h7v16h-7V4z M10 7h4v2h-4V7zm0 4h4v2h-4v-2zm0 4h4v2h-4v-2z
ImageSubtract|M5 13h14v-2H5v2zM3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z
ImageTiling|M3 3h18v18H3V3zm8 16h8V11h-8v8zm0-10h8V5h-8v4zm-6 10h4V11H5v8zm0-10h4V5H5v4z M3 10h18 M10 3v18
InverseFFT1D|M3 7h2v10H3V7zm4 2h2v8H7V9zm4-4h2v12h-2V5zm4 6h2v6h-2v-6zm4-2h2v8h-2V9 M13 3l-4 4h3v4h2V7h3l-4-4
JsonExtractor|M10 4H7v4c0 1.1-.9 2-2 2v4c1.1 0 2 .9 2 2v4h3v-2H8v-2c0-1.1-.9-2-2-2 1.1 0 2-.9 2-2V6h2V4zm4 0h3v4c0 1.1.9 2 2 2v4c-1.1 0-2 .9-2 2v4h-3v-2h2v-2c0-1.1.9-2 2-2-1.1 0-2-.9-2-2V6h-2V4z M9 12h6v2H9v-2z
LaplacianSharpen|M12 2L2 22h20L12 2zm0 3.5L18.5 20h-13L12 5.5z
LawsTextureFilter|M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z M11 11h10l-4 5v4h-2v-4l-4-5z
LineLineDistance|M2 4h16v2H2V4zm4 14h16v2H6v-2zM8 8h2v8H8V8zm-2 2l3 3v-2h8v2l3-3-3-3v2H9V9L6 11z
LineMeasurement|M4 11h16v2H4zM4 8v8H2V8h2zm18 0v8h-2V8h2z
LocalDeformableMatching|M4 4h6v2H6v4H4V4zm10 0h6v6h-2V6h-4V4zM4 14h2v4h4v2H4v-6zm14 4v-4h2v6h-6v-2h4z M8 8c1 0 2 .5 3 1.5s2 1.5 3 1.5 2-.5 3-1.5 2-1.5 3-1.5v2c-1 0-2 .5-3 1.5s-2 1.5-3 1.5-2-.5-3-1.5-2-1.5-3-1.5V8z
LogicGate|M9 5v14H7V5h2zm4 0v14h-2V5h2zm4 0v14h-2V5h2z M4 11h16v2H4z
MathOperation|M3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z M8 12c0-2.21 1.79-4 4-4s4 1.79 4 4-1.79 4-4 4-4-1.79-4-4z M12 15l-3-3v2h6v-2l-3 3z
MeanFilter|M3 3h18v18H3V3zm2 2v14h14V5H5zm2 2h10v10H7V7zm5 2a3 3 0 1 0 .001 0z
Measurement|M2 20h2v-3l12.42-12.42c.39-.39.39-1.02 0-1.41l-1.17-1.17c-.39-.39-1.02-.39-1.41 0L3 14.42V18h3.58zM15 4l5 5-11 11H4v-5L15 4z
MedianBlur|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 16c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zm0-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z
MinEnclosingGeometry|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-3-4l-2-5 4-3 4 2 1 4-3 3H9z
`
    ,
    `
MitsubishiMcCommunication|M12 4l8 8-8 8-8-8 8-8zM6 12l6 6 6-6-6-6-6 6z
ModbusCommunication|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z
MorphologicalOperation|M12 2l-5 5h3v6h-3l5 5 5-5h-3V7h3l-5-5z M12 10v4h-2v-4h2z M10 12h4v-2h-4v2z
Morphology|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 11h-4v4h-2v-4H7v-2h4V7h2v4h4v2z
MqttPublish|M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-6 12l-4-4h3V6h2v4h3l-4 4z
NPointCalibration|M4 4h4v4H4V4zm6 2h2v2h-2V6zm6-2h4v4h-4V4zM4 16h4v4H4v-4zm12 0h4v4h-4v-4z M11 8v8h2V8h-2z M8 11h8v2H8v-2z
OcrRecognition|M3 3h18v6H3V3zm2 2v2h14V5H5zm4 7h2v6H9v-6zm4 0h2v6h-2v-6zm-8 4v2h14v-2H5zM5 11h2v3H5v-3zM17 11h2v3h-2v-3z
OmronFinsCommunication|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zM12 6v12c-3.31 0-6-2.69-6-6s2.69-6 6-6z
OrbFeatureMatch|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M9.5 8.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5zM14.5 12.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5z M10.5 10.5l3 3 M7 12H5M19 12h-2M12 7V5M12 19v-2
ParallelLineFind|M4 6h16v2H4V6zm0 10h16v2H4v-2z M11 2v4h2V2h-2zm0 16v4h2v-4h-2z M8 4l-4 4 4 4v-3h8v3l4-4-4-4v3H8V4zm0 10l-4 4 4 4v-3h8v3l4-4-4-4v3H8v-3z
PerspectiveTransform|M2 6l4-2 8 2 8-2v14l-8 2-8-2-4 2V6zm4 2v8l6 1.5V8.5L6 8z M12 12m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0
PhaseClosure|M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zM6 12c0 3.31 2.69 6 6 6v3l4-4-4-4v3c-2.21 0-4-1.79-4-4H6zm8-3h2v6h-2V9zm-4 2h2v4h-2v-4z
PixelStatistics|M3 3h18v18H3V3zm2 2v4h4V5H5zm6 0v4h4V5h-4zm6 0v4h4V5h-4zM5 11v4h4v-4H5zm6 0v4h4v-4h-4zm6 0v4h4v-4h-4zM5 17v2h4v-2H5zm6 0v2h4v-2h-4zm6 0v2h4v-2h-4z
PixelToWorldTransform|M4 4h8v8H4V4zm2 2v4h4V6H6zm8 1c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6zm0 2c-2.21 0-4 1.79-4 4h8c0-2.21-1.79-4-4-4zm0 10c1.66 0 3-1.34 3-3h-6c0 1.66 1.34 3 3 3
PlanarMatching|M3 6l9-3 9 3-9 3-9-3zm2 4.5l7 2.5 7-2.5V17l-7 2-7-2v-6.5zm7 .5l3 1v2l-3 1-3-1v-2l3-1z
PointAlignment|M3 12h4v2H3v-2zm14 0h4v2h-4v-2z M12 3h2v4h-2V3zm0 14h2v4h-2v-4z M10 10h4v4h-4v-4z
PointCorrection|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 8v4l3 3-1.5 1.5L10 13V8h2z M16 12h2 M6 12h2
PointLineDistance|M4 4h16v2H4V4zm8 5c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2z M11 13v6h2v-6h-2zM9 17l3 3 3-3V14h-2v3h-2v-3H9v3z
PointSetTool|M2 2h4v4H2V2zm6 0h4v4H8V2zm6 0h4v4h-4V2zm6 0h4v4h-4V2zM2 8h4v4H2V8zm18 0h4v4h-4V8zM2 14h4v4H2v-4zm18 0h4v4h-4v-4z M11 12l2-2 2 2-2 2-2-2z
PolarUnwrap|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 6c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6z M3 21h18v-2H3v2z M7 21l3-4v4H7zm6 0l3-4v4h-3z
PositionCorrection|M12 2L2 12l3 3 7-7 7 7 3-3-10-10z M12 22l-4-4h3v-4h2v4h3l-4 4z M8 12c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4z
PPFEstimation|M6 6a1.5 1.5 0 1 0 .001 0zm6 2a1.5 1.5 0 1 0 .001 0zm4 7a1.5 1.5 0 1 0 .001 0zM8 18a1.5 1.5 0 1 0 .001 0zm-1-11l4 1M12 9l3 5M9 17l6-1 M14 4h6v2h-6V4zm4 0v6h-2V4h2
PPFMatch|M6 6a1.5 1.5 0 1 0 .001 0zm8 3a1.5 1.5 0 1 0 .001 0zm-4 8a1.5 1.5 0 1 0 .001 0z M7 7l6 2M13 10l-3 6 M17 7l2 2 4-4 1.5 1.5L19 12 15.5 8.5 17 7z
PyramidShapeMatch|M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83z M6.8 12h10.4M8.5 9h7M5 15h14 M12 14v4M10 12v3M14 12v3M9 9v3M15 9v3M11 6v3M13 6v3
QuadrilateralFind|M4.5 4.5l11-2 4 13-14 3-1-14z M3 3c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zm12.5-2c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zm4.5 13c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zM4 19c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1z M10 10l4 4m0-4l-4 4
RansacPlaneSegmentation|M3 15l9-4 9 4-9 4-9-4zm9-10l6 3-6 3-6-3 6-3zm-5 11v2l5 2 5-2v-2l-5 2-5-2z
RectangleDetection|M3 5v14h18V5H3zm16 12H5V7h14v10z M8 10h8v4H8v-4z M12 8v2 M12 14v2 M8 12h2 M14 12h2 M7 9h2v2H7z M15 13h2v2h-2z
RegionClosing|M6 12a6 6 0 0 1 6-6c2.21 0 4.14 1.2 5.18 3H21l-4 4-4-4h2.18A4 4 0 1 0 16 15.46l1.41 1.41A5.96 5.96 0 0 1 12 18a6 6 0 0 1-6-6z
RegionComplement|M4 4h16v16H4V4zm4 4v8h8V8H8zm-3 3h2v2H5v-2zm12 0h2v2h-2v-2z
RegionDifference|M4 6h8v12H4V6zm8 0h8v12h-8V6zm-2 2v8h4V8h-4z
RegionDilation|M12 5l2 3h-4l2-3zm0 14l-2-3h4l-2 3zM5 12l3-2v4l-3-2zm14 0l-3 2v-4l3 2z M12 8c2.21 0 4 1.79 4 4s-1.79 4-4 4-4-1.79-4-4 1.79-4 4-4z
RegionErosion|M12 9l-2-3h4l-2 3zm0 6l2 3h-4l2-3zm6-3l3 2h-4v-4l1 2zm-12 0l-3-2h4v4l-1-2z M12 8c2.21 0 4 1.79 4 4s-1.79 4-4 4-4-1.79-4-4 1.79-4 4-4z
RegionIntersection|M8 6c-2.21 0-4 1.79-4 4v4h8v-4c0-2.21-1.79-4-4-4zm8 0c-2.21 0-4 1.79-4 4v4h8v-4c0-2.21-1.79-4-4-4z M8 16c2.21 0 4-1.79 4-4 0 2.21 1.79 4 4 4H8z
RegionOpening|M18 12a6 6 0 0 1-6 6c-2.21 0-4.14-1.2-5.18-3H3l4-4 4 4H8.82A4 4 0 1 0 8 8.59L6.59 7.18A5.96 5.96 0 0 1 12 6a6 6 0 0 1 6 6z
RegionSkeleton|M6 8c0-1.1.9-2 2-2h8c1.1 0 2 .9 2 2v8c0 1.1-.9 2-2 2H8c-1.1 0-2-.9-2-2V8zm5 1v6h2V9h-2zm-3 2h8v2H8v-2z
RegionUnion|M8 12c0-3.31 2.69-6 6-6 1.6 0 3.05.62 4.12 1.64-1.28-.41-2.74-.3-3.95.4A6 6 0 0 0 8 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.41 0 2.7.49 3.73 1.3C9.52 8.25 8 10 8 12zm12 0h2v2h-2v2h-2v-2h-2v-2h2V10h2v2z
ResultJudgment|M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2zM9 11l-4.2-4.2L5.2 5.4 9 9.2 12.8 5.4l1.4 1.4L9 11z
`
    ,
    `
ResultOutput|M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z
RoiManager|M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2z
RoiTransform|M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm14-2v2h4v4h2V5c0-1.1-.9-2-2-2h-4zM5 15H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm16 0v4h-4v2h4c1.1 0 2-.9 2-2v-4h-2z M10 10l4 4 M14 10l-4 4
ScriptOperator|M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z M12 5l-2 14h2l2-14z
SemanticSegmentation|M4 4h16v16H4V4zm2 2v12h12V6H6zm0 0h4v4H6V6zm4 4h4v4h-4v-4zm4 4h4v4h-4v-4zM6 14h4v4H6v-4z
SerialCommunication|M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z
ShadingCorrection|M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 14H4V6h16v12z M5 8h14 M5 12h14 M5 16h14 M12 5v14 M8 5v14 M16 5v14 M6 6c3 0 5 1.5 6 3s3 3 6 3
ShapeMatching|M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z M12 14l-4-4h8l-4 4z M8 8l4-4 4 4H8z M8 16l4 4 4-4H8z
SharpnessEvaluation|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 6c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm0 10c-2.21 0-4-1.79-4-4s1.79-4 4-4 4 1.79 4 4-1.79 4-4 4z M5.5 12h3M15.5 12h3M12 5.5v3M12 15.5v3
SiemensS7Communication|M4 4h16v16H4V4zm2 2v12h12V6H6zm2 2h8v2H8V8zm0 4h8v2H8v-2z
Statistics|M10 20h4V4h-4v16zm-6 0h4v-8H4v8zM16 9v11h4V9h-4z M12 2v2 M6 10v2 M18 7v2
StatisticalOutlierRemoval|M7 7a1.5 1.5 0 1 0 .001 0zm5 1a1.5 1.5 0 1 0 .001 0zm-3 6a1.5 1.5 0 1 0 .001 0zM5 17a1.5 1.5 0 1 0 .001 0zm10-1l5 5m0-5l-5 5 M8 8l3 1m-2 5l2-4m-4 7l2-2
StereoCalibration|M4 6h6v12H4V6zm10 0h6v12h-6V6zM7 8a2 2 0 1 0 .001 0zm10 0a2 2 0 1 0 .001 0z M10 12h4v2h-4v-2 M8 20h8v2H8v-2
StringFormat|M5 5h14v2H5V5zm0 12h14v2H5v-2zm0-6h9v2H5v-2z M16 11h2v2h-2z M5 8h4v2H5V8zm6 0h4v2h-4V8z
SubpixelEdgeDetection|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z
SurfaceDefectDetection|M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM4 18V6h16v12H4z M9 9h2v6H9V9zm7 0h-2L11 12l3 3h2l-3-3 3-3z
TcpCommunication|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z
TemplateMatching|M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 14H4V6h16v12z M15 9H9v6h6V9z M13 11h-2v2h2v-2z M8 5h2v2H8z M14 5h2v2h-2z M8 17h2v2H8z M14 17h2v2h-2z M5 8h2v2H5z M5 14h2v2H5z M17 8h2v2h-2z M17 14h2v2h-2z
TextSave|M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z M11 12H9v2h2v-2z M10 19v-2
Thresholding|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z
TimerStatistics|M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z M8 12h2v4H8z M14 9h2v7h-2z
TranslationRotationCalibration|M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83z M12 9v6M9 12h6 M10 15a4 4 0 0 1 4 0
TriggerModule|M7 2v11h3v9l7-12h-4l4-8z M10 2h4v8 M16 2h2 M6 2h2 M10 22v-4 M14 22v-4
TryCatch|M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z
TypeConvert|M12 4l-4 4h3v8H8l4 4 4-4h-3V8h3l-4-4z M6 12H4 M20 12h-2
Undistort|M21 5c-1.11-.35-2.33-.5-3.5-.5-1.95 0-4.05 0.4-5.5 1.5-1.45-1.1-3.55-1.5-5.5-1.5S2.45 4.9 1 6v14.65c0 .25.25.5.5.5.1 0 .15-.05.25-.05C3.1 20.45 5.05 20 6.5 20c1.95 0 4.05.4 5.5 1.5 1.35-.85 3.8-1.5 5.5-1.5 1.65 0 3.35.3 4.75 1.05.1.05.15.05.25.05.25 0 .5-.25.5-.5V6c-.6-.45-1.25-.75-2-1zm0 13.5c-1.1-.35-2.3-.5-3.5-.5-1.7 0-4.15.65-5.5 1.5V8c1.35-.85 3.8-1.5 5.5-1.5 1.2 0 2.4.15 3.5.5v11.5z
UnitConvert|M12 4l-4 4h3v8H8l4 4 4-4h-3V8h3l-4-4z M5 12H3 M21 12h-2 M12 12h-4 M16 12h-4
VariableIncrement|M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z
VariableRead|M9 7H5v10h4v-2H7V9h2V7zm6 0h4v10h-4v-2h2V9h-2V7z
VariableWrite|M7 7v2h2v6H7v2h6V7H7zm8 0v10h4V7h-4z
VoxelDownsample|M4 4l4-2 4 2-4 2-4-2zm8 0l4-2 4 2-4 2-4-2zM6 8l4-2 4 2-4 2-4-2zm6 8l4-2 4 2-4 2-4-2 M6 10v4l4 2v-4l-4-2zm8 6v-4l4-2v4l-4 2
WidthMeasurement|M2 6h2v12H2V6zm18 0h2v12h-2V6zM6 11h12v2H6v-2zM4 13l3 3v-2h10v2l3-3-3-3v2H7V9L4 13z
`
];
const CATEGORY_ICON_BLOCKS = [
    `
3D|M12 2l7 4v12l-7 4-7-4V6l7-4zm0 2.3L7 6.8l5 2.9 5-2.9-5-2.5zm-5 5v5.8l4 2.3V12L7 9.3zm6 8.1l4-2.3V9.3L13 12v5.4z
AI Inspection|M12 2l7 4v5c0 5-3.4 9.74-7 11-3.6-1.26-7-6-7-11V6l7-4zm-1 12l-2-2-1.4 1.4L11 17l5-5-1.4-1.4L11 14z
AI检测|M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6 10H6v-2h8v2zm4-4H6v-2h12v2z
Analysis|M5 9h3v10H5V9zm5-4h3v14h-3V5zm5 7h3v7h-3v-7zM3 21h18v-2H3v2z
Frequency|M3 12c2-4 4-4 6 0s4 4 6 0 4-4 6 0v2c-2-4-4-4-6 0s-4 4-6 0-4-4-6 0v-2z
Morphology|M12 2l-5 5h3v6h-3l5 5 5-5h-3V7h3l-5-5z M12 10v4h-2v-4h2z M10 12h4v-2h-4v2z
Region|M5 5h8v8H5V5zm6 6h8v8h-8v-8z
Texture|M4 4h4v4H4V4zm6 0h4v4h-4V4zm6 0h4v4h-4V4zM4 10h4v4H4v-4zm6 0h4v4h-4v-4zm6 0h4v4h-4v-4zM4 16h4v4H4v-4zm6 0h4v4h-4v-4zm6 0h4v4h-4v-4z
输入|M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z
预处理|M20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83zM3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25z
颜色处理|M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8z
分析|M5 9h3v10H5V9zm5-4h3v14h-3V5zm5 7h3v7h-3v-7zM3 21h18v-2H3v2z
变量|M6 4h12l-2 16H8L6 4zm3 2l1 12h4l1-12H9z
图像处理|M21 19V5H3v14h18zm-2-2H5V7h14v10zm-8-1l2.5-3.01L17 17H7l4-5z
定位|M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z
辅助|M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.5.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1.01c.52.4 1.08.73 1.69.98l.38 2.65c.04.24.25.42.5.42h4c.25 0 .46-.18.5-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1.01c.22.08.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z
控制|M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z
数据|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z
数据处理|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z
拆分组合|M3 3h18v18H3V3zm8 16h8V11h-8v8zm0-10h8V5h-8v4zm-6 10h4V11H5v8zm0-10h4V5H5v4z M3 10h18 M10 3v18
控制流|M20 12l-4-4v3H4v2h12v3l4-4zM4 6h16v2H4V6zm0 12h16v-2H4v2z
检测|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z
标定|M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z
测量|M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h2v4h2V8h2v4h2V8h2v4h2V8h2v8z
流程控制|M20 12l-4-4v3H4v2h12v3l4-4zM4 6h16v2H4V6zm0 12h16v-2H4v2z
特征提取|M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-8h-2V7h2v2z
匹配定位|M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z
通信|M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z
通用|M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm1 7V3.5L18.5 9H15z
逻辑工具|M9 5v14H7V5h2zm4 0v14h-2V5h2zm4 0v14h-2V5h2z M4 11h16v2H4z
输出|M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z
识别|M3 11h8V3H3v8zm2-6h4v4H5V5zM3 21h8v-8H3v8zm2-6h4v4H5v-4zM13 3v8h8V3h-8zm6 6h-4V5h4v4zM13 13h2v2h-2zM15 15h2v2h-2zM13 17h2v2h-2zM17 13h2v2h-2zM19 15h2v2h-2zM17 17h2v2h-2zM15 19h2v2h-2zM19 19h2v2h-2z
采集|M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z
`
];
const OPERATOR_COLOR_BLOCKS = [
    `
AdaptiveThreshold|#eb2f96
BlobAnalysis|#13c2c2
ColorConversion|#fa8c16
ColorDetection|#fa541c
DatabaseWrite|#595959
DeepLearning|#a0d911
EdgeDetection|#722ed1
Filtering|#1890ff
GeometricFitting|#eb2f96
HistogramEqualization|#2f54eb
ImageAcquisition|#52c41a
Measurement|#2f54eb
ModbusCommunication|#13c2c2
Morphology|#fa8c16
ResultOutput|#595959
RoiManager|#1890ff
SerialCommunication|#13c2c2
ShapeMatching|#52c41a
SubpixelEdgeDetection|#722ed1
TcpCommunication|#13c2c2
TemplateMatching|#f5222d
Thresholding|#eb2f96
`
];
const CATEGORY_COLOR_BLOCKS = [
    `
3D|#64748b
AI Inspection|#a0d911
AI检测|#a0d911
Analysis|#38bdf8
Frequency|#0ea5e9
Morphology|#fa8c16
Region|#14b8a6
Texture|#8b5cf6
输入|#52c41a
预处理|#1890ff
颜色处理|#fa541c
变量|#597ef7
图像处理|#1890ff
定位|#40a9ff
辅助|#8c8c8c
控制|#faad14
数据|#2f54eb
数据处理|#2f54eb
拆分组合|#722ed1
检测|#f5222d
标定|#1890ff
测量|#2f54eb
流程控制|#faad14
特征提取|#722ed1
匹配定位|#52c41a
通信|#13c2c2
通用|#8c8c8c
逻辑工具|#597ef7
输出|#595959
识别|#13c2c2
采集|#52c41a
`
];

export const OPERATOR_ICON_ALIASES = parseKeyValueBlocks(OPERATOR_ICON_ALIAS_BLOCKS);
export const OPERATOR_ICON_PATHS = parseKeyValueBlocks(OPERATOR_ICON_BLOCKS);
export const CATEGORY_ICON_PATHS = parseKeyValueBlocks(CATEGORY_ICON_BLOCKS);

const OPERATOR_COLORS = parseKeyValueBlocks(OPERATOR_COLOR_BLOCKS);
const CATEGORY_COLORS = parseKeyValueBlocks(CATEGORY_COLOR_BLOCKS);

function resolveOperatorAlias(type) {
    if (!type) {
        return '';
    }

    return OPERATOR_ICON_ALIASES[type] || type;
}

export function getOperatorIconPath(type, category = null) {
    const resolvedType = resolveOperatorAlias(type);
    if (resolvedType && OPERATOR_ICON_PATHS[resolvedType]) {
        return OPERATOR_ICON_PATHS[resolvedType];
    }

    return getCategoryIconPath(category);
}

export function getCategoryIconPath(category) {
    if (category && CATEGORY_ICON_PATHS[category]) {
        return CATEGORY_ICON_PATHS[category];
    }

    return DEFAULT_CATEGORY_ICON_PATH;
}

export function getOperatorColor(type, category = null) {
    const resolvedType = resolveOperatorAlias(type);
    if (resolvedType && OPERATOR_COLORS[resolvedType]) {
        return OPERATOR_COLORS[resolvedType];
    }

    if (category && CATEGORY_COLORS[category]) {
        return CATEGORY_COLORS[category];
    }

    return DEFAULT_OPERATOR_COLOR;
}

function cloneParameters(parameters) {
    if (!Array.isArray(parameters)) {
        return [];
    }

    return parameters.map(parameter => ({ ...parameter }));
}

function clonePorts(ports, fallbackName) {
    if (!Array.isArray(ports) || ports.length === 0) {
        return [{ name: fallbackName, type: 'Any' }];
    }

    return ports.map((port, index) => ({
        name: port.name || port.Name || `${fallbackName}${index + 1}`,
        type: port.dataType || port.DataType || port.type || port.Type || 'Any'
    }));
}

export function buildOperatorNodeConfig(type, data = null) {
    const resolvedType = type || data?.type || data?.Type || '';
    const category = data?.category || data?.Category || null;
    const title =
        data?.displayName ||
        data?.DisplayName ||
        data?.name ||
        data?.Name ||
        resolvedType;

    return {
        title,
        color: data?.color || getOperatorColor(resolvedType, category),
        iconPath: data?.iconPath || getOperatorIconPath(resolvedType, category),
        parameters: cloneParameters(data?.parameters || data?.Parameters),
        inputs: clonePorts(data?.inputPorts || data?.InputPorts, 'input'),
        outputs: clonePorts(data?.outputPorts || data?.OutputPorts, 'output')
    };
}
