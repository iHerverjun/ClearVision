export const BridgeMessageType = {
  // Application
  AppReady: 'app.ready',
  AppClose: 'app.close',
  
  // Project & Flow
  ProjectLoad: 'project.load',
  ProjectSave: 'project.save',
  FlowExecute: 'flow.execute',
  FlowStop: 'flow.stop',
  FlowStatusUpdate: 'flow.status.update',
  
  // Settings & Status
  SettingsGet: 'settings.get',
  SettingsSave: 'settings.save',
  StatusUpdate: 'status.update',
  
  // Dialogs
  SelectFile: 'dialog.selectFile',
  SelectFolder: 'dialog.selectFolder',
  PickFileCommand: 'PickFileCommand',
  FilePickedEvent: 'FilePickedEvent',
  
  // Specific domains
  ImageStreamEvent: 'ImageStreamEvent',
  ImageStreamShared: 'image.stream.shared',
  AuthLogin: 'auth.login',
  AuthLogout: 'auth.logout',
  
  // AI
  AiGenerateFlow: 'GenerateFlowCommand',
  AiGenerateFlowResult: 'GenerateFlowResult',
  
  // Calibration
  CalibSolve: 'CalibSolveCommand',
  CalibSave: 'CalibSaveCommand',
  HandEyeSolve: 'HandEyeSolveCommand',
  HandEyeSave: 'HandEyeSaveCommand',
} as const;

export type BridgeMessageAction = typeof BridgeMessageType[keyof typeof BridgeMessageType];

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface BridgeMessage<T = any> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  [key: string]: any;
  type?: string; 
  messageType?: string;
  payload?: T;
  data?: T;
  requestId?: number;
  error?: string;
  timestamp?: string;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface RequestRecord {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  resolve: (value: any) => void;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  reject: (reason?: any) => void;
}
