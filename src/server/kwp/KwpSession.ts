import { EventEmitter } from "node:events";
import { SerialTransport } from "../transport/SerialTransport.js";
import { ElmClient } from "../elm/ElmClient.js";
import { PidReader } from "./PidReader.js";
import { KNOWN_PIDS, TEST_PING_CID } from "./knownPids.js";
import { buildReadByCidRequest, containsPositiveResponseMarker } from "./protocol.js";
import type { ConnectionState, PidReading } from "../../shared/types.js";

const SETTLE_DELAY_MS = 2000;
/** Well under the 2-4s "tester present" ceiling from HANDOVER.md §3.3. */
const POLL_INTERVAL_MS = 1000;

/**
 * Owns the cold-start init sequence (§3.2), the keep-alive/recovery pattern
 * (§3.3), and polling the 5 known PIDs (§3.4). Knows what KWP2000 services
 * and CIDs mean; talks to the ECU only through ElmClient, which has no idea
 * what any of this means in protocol terms.
 */
export class KwpSession extends EventEmitter {
  private state: ConnectionState = "disconnected";
  private transport: SerialTransport | null = null;
  private elm: ElmClient | null = null;
  private pidReader: PidReader | null = null;
  private pollTimer: NodeJS.Timeout | null = null;
  private polling = false;
  private connectedPath: string | null = null;

  getState(): ConnectionState {
    return this.state;
  }

  getPort(): string | null {
    return this.connectedPath;
  }

  async connect(path: string): Promise<void> {
    if (this.state === "connecting" || this.state === "connected") {
      throw new Error(`KwpSession: cannot connect while state is "${this.state}"`);
    }

    this.setState("connecting", `Opening ${path}`);
    const transport = new SerialTransport({ path });
    this.transport = transport;
    this.elm = new ElmClient(transport);
    this.pidReader = new PidReader(this.elm);

    try {
      await transport.open();
      await this.runInitSequence();
      this.connectedPath = path;
      this.setState("connected");
      this.startPolling();
    } catch (err) {
      await transport.close().catch(() => undefined);
      this.transport = null;
      this.elm = null;
      this.pidReader = null;
      this.setState("error", err instanceof Error ? err.message : String(err));
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    this.stopPolling();
    await this.transport?.close().catch(() => undefined);
    this.transport = null;
    this.elm = null;
    this.pidReader = null;
    this.connectedPath = null;
    this.setState("disconnected");
  }

  /** Cold-start init sequence per HANDOVER.md §3.2. */
  private async runInitSequence(): Promise<void> {
    const elm = this.requireElm();

    // The original app sends ATZ twice and doesn't check the response.
    await elm.sendCommand("ATZ", { timeoutMs: 3000 }).catch(() => undefined);
    await elm.sendCommand("ATZ", { timeoutMs: 3000 }).catch(() => undefined);

    await elm.sendCommandUntilOk("ATE0");
    await elm.sendCommandUntilOk("ATH0");
    await elm.sendCommandUntilOk("ATSP5");
    await elm.sendCommandUntilOk("ATSH8101F1");

    await delay(SETTLE_DELAY_MS);

    const ok = await this.sendTestPing();
    if (!ok) {
      throw new Error('KwpSession: init sequence finished but test ping "22111F" got no positive response');
    }
  }

  /** §3.2/§3.3 connectivity check: success = response contains "62". */
  private async sendTestPing(): Promise<boolean> {
    try {
      const raw = await this.requireElm().sendCommand(buildReadByCidRequest(TEST_PING_CID), {
        timeoutMs: 1000,
      });
      return containsPositiveResponseMarker(raw);
    } catch {
      return false;
    }
  }

  /** Keep-alive recovery ladder per §3.3: re-ping -> force fast init -> full restart. */
  private async recover(): Promise<boolean> {
    if (await this.sendTestPing()) return true;

    try {
      await this.requireElm().sendCommand("ATFI", { timeoutMs: 3000 });
    } catch {
      // fall through to the full restart below
    }
    if (await this.sendTestPing()) return true;

    try {
      const elm = this.requireElm();
      await elm.sendCommandUntilOk("ATSP5");
      await elm.sendCommandUntilOk("ATSH8101F1");
    } catch {
      return false;
    }
    return this.sendTestPing();
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollTimer = setInterval(() => {
      void this.pollOnce();
    }, POLL_INTERVAL_MS);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private async pollOnce(): Promise<void> {
    if (this.polling || this.state !== "connected" || !this.pidReader) return;
    this.polling = true;
    try {
      const readings = await this.pidReader.readAll(KNOWN_PIDS);
      this.emit("pids", readings satisfies PidReading[]);

      const sessionDropped = readings.every((reading) => reading.error !== undefined);
      if (sessionDropped) {
        const recovered = await this.recover();
        if (!recovered) {
          this.stopPolling();
          this.setState("error", "Lost KWP session and recovery (§3.3) failed");
        }
      }
    } finally {
      this.polling = false;
    }
  }

  private requireElm(): ElmClient {
    if (!this.elm) throw new Error("KwpSession: not connected");
    return this.elm;
  }

  private setState(state: ConnectionState, detail?: string): void {
    this.state = state;
    this.emit("state", state, detail);
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export declare interface KwpSession {
  on(event: "state", listener: (state: ConnectionState, detail?: string) => void): this;
  on(event: "pids", listener: (readings: PidReading[]) => void): this;
  emit(event: "state", state: ConnectionState, detail?: string): boolean;
  emit(event: "pids", readings: PidReading[]): boolean;
}
