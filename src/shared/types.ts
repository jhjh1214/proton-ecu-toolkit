export type ConnectionState =
  | "disconnected"
  | "connecting"
  | "connected"
  | "error";

export interface PidDefinition {
  id: string; // 4-hex-char CID, e.g. "1101"
  name: string;
  unit: string;
  /** Number of data bytes expected after the CID echo (1 = byteA only, 2 = byteA+byteB). */
  byteLength: 1 | 2;
  /** Applies the PID's formula to the raw data bytes. */
  decode: (bytes: number[]) => number;
}

export interface PidReading {
  id: string;
  name: string;
  unit: string;
  value: number | null;
  rawHex: string | null;
  timestamp: number;
  error?: string;
}

export interface StatusMessage {
  type: "status";
  state: ConnectionState;
  port: string | null;
  detail?: string;
}

export interface PidsMessage {
  type: "pids";
  readings: PidReading[];
}

export type ServerMessage = StatusMessage | PidsMessage;
