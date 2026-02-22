/**
 * outputRenderer.ts
 * 
 * Maps generic system ResultStore objects to UI components for the Inspection NodeOutputPanel.
 */

export interface InspectionResult {
  id: string;
  type: 'measurement' | 'ocr' | 'defect' | 'barcode';
  name: string;
  status: 'OK' | 'NG';
  value: any;
  timeMs: number;
  details?: Record<string, any>;
}

export const renderOutput = (result: InspectionResult) => {
  // Common mapping logic for different result types
  switch (result.type) {
    case 'measurement':
      return {
        icon: 'straighten',
        colorClass: result.status === 'OK' ? 'text-green-500' : 'text-red-500',
        borderClass: result.status === 'OK' ? 'border-l-green-500' : 'border-l-red-500',
        formattedValue: `${result.value} mm`,
      };
    case 'ocr':
      return {
        icon: 'text_fields',
        colorClass: result.status === 'OK' ? 'text-green-500' : 'text-red-500',
        borderClass: result.status === 'OK' ? 'border-l-green-500' : 'border-l-red-500',
        formattedValue: result.value as string,
      };
    case 'defect':
      return {
        icon: 'bug_report',
        colorClass: result.status === 'OK' ? 'text-green-500' : 'text-red-500',
        borderClass: result.status === 'OK' ? 'border-l-green-500' : 'border-l-red-500',
        formattedValue: result.status === 'NG' ? 'Defect Detected' : 'Clear',
      };
    case 'barcode':
      return {
        icon: 'qr_code_scanner',
        colorClass: result.status === 'OK' ? 'text-green-500' : 'text-red-500',
        borderClass: result.status === 'OK' ? 'border-l-green-500' : 'border-l-red-500',
        formattedValue: result.value as string,
      };
    default:
      return {
        icon: 'build',
        colorClass: 'text-gray-500',
        borderClass: 'border-l-gray-500',
        formattedValue: String(result.value),
      };
  }
};
