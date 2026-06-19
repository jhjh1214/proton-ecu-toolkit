import { SerialPort } from "serialport";

export interface SerialTransportOptions {
  path: string;
  baudRate?: number;
}

const PROMPT = ">";
const DEFAULT_BAUD_RATE = 38400;

/**
 * Raw serial framing only. Writes a line, reads bytes until the ELM327's
 * '>' prompt shows up, and hands back whatever text arrived in between.
 * Has no idea what an AT command or a KWP2000 service byte is - that
 * belongs to the layers above.
 */
export class SerialTransport {
  private port: SerialPort | null = null;
  private buffer = "";
  private pending: {
    resolve: (value: string) => void;
    reject: (reason: Error) => void;
    timer: NodeJS.Timeout;
  } | null = null;
  private queue: Promise<void> = Promise.resolve();

  constructor(private readonly options: SerialTransportOptions) {}

  static listPorts() {
    return SerialPort.list();
  }

  get isOpen(): boolean {
    return this.port?.isOpen ?? false;
  }

  open(): Promise<void> {
    return new Promise((resolve, reject) => {
      const port = new SerialPort(
        {
          path: this.options.path,
          baudRate: this.options.baudRate ?? DEFAULT_BAUD_RATE,
          autoOpen: false,
        },
        (err) => {
          if (err) reject(err);
        },
      );
      port.on("data", (chunk: Buffer) => this.onData(chunk));
      port.on("error", (err) => this.failPending(err));
      port.on("close", () => this.failPending(new Error("SerialTransport: port closed")));
      port.open((err) => {
        if (err) {
          reject(err);
          return;
        }
        this.port = port;
        resolve();
      });
    });
  }

  close(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (!this.port?.isOpen) {
        resolve();
        return;
      }
      this.port.close((err) => {
        if (err) reject(err);
        else resolve();
      });
    });
  }

  /**
   * Writes one line (CR-terminated) and resolves with everything received
   * before the next '>' prompt. Calls are serialized - only one in flight
   * at a time, since the ELM327 is strictly half-duplex.
   */
  send(line: string, timeoutMs = 2000): Promise<string> {
    const previous = this.queue;
    let release!: () => void;
    this.queue = new Promise<void>((res) => {
      release = res;
    });
    return previous.then(() => this.sendNow(line, timeoutMs)).finally(() => release());
  }

  private sendNow(line: string, timeoutMs: number): Promise<string> {
    const port = this.port;
    if (!port?.isOpen) {
      return Promise.reject(new Error("SerialTransport: port is not open"));
    }
    return new Promise((resolve, reject) => {
      this.buffer = "";
      const timer = setTimeout(() => {
        this.pending = null;
        reject(new Error(`SerialTransport: timed out waiting for response to "${line}"`));
      }, timeoutMs);
      this.pending = { resolve, reject, timer };
      port.write(`${line}\r`, (err) => {
        if (err) this.failPending(err);
      });
    });
  }

  private onData(chunk: Buffer): void {
    this.buffer += chunk.toString("utf8");
    if (!this.pending) return;
    const promptIndex = this.buffer.indexOf(PROMPT);
    if (promptIndex === -1) return;
    const response = this.buffer.slice(0, promptIndex);
    this.buffer = this.buffer.slice(promptIndex + 1);
    const { resolve, timer } = this.pending;
    clearTimeout(timer);
    this.pending = null;
    resolve(response);
  }

  private failPending(err: Error): void {
    if (!this.pending) return;
    clearTimeout(this.pending.timer);
    const { reject } = this.pending;
    this.pending = null;
    reject(err);
  }
}
