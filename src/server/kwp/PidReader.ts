import { ElmClient } from "../elm/ElmClient.js";
import type { PidDefinition, PidReading } from "../../shared/types.js";
import { buildReadByCidRequest, parseReadByCidResponse } from "./protocol.js";

const DEFAULT_TIMEOUT_MS = 1000;

/** Sends a known PID request, parses the response, and applies its formula. */
export class PidReader {
  constructor(
    private readonly elm: ElmClient,
    private readonly timeoutMs = DEFAULT_TIMEOUT_MS,
  ) {}

  async read(pid: PidDefinition): Promise<PidReading> {
    const timestamp = Date.now();
    let rawHex: string | null = null;

    try {
      rawHex = await this.elm.sendCommand(buildReadByCidRequest(pid.id), {
        timeoutMs: this.timeoutMs,
      });
      const parsed = parseReadByCidResponse(rawHex, pid.id);

      if (!parsed) {
        return this.failure(pid, rawHex, timestamp, `Unrecognized response: "${rawHex}"`);
      }
      if (!parsed.positive) {
        const nrc = parsed.nrc !== undefined ? `0x${parsed.nrc.toString(16)}` : "unknown";
        return this.failure(pid, rawHex, timestamp, `Negative response, NRC=${nrc}`);
      }
      if (parsed.dataBytes.length < pid.byteLength) {
        return this.failure(
          pid,
          rawHex,
          timestamp,
          `Expected ${pid.byteLength} data byte(s), got ${parsed.dataBytes.length}`,
        );
      }

      return {
        id: pid.id,
        name: pid.name,
        unit: pid.unit,
        value: pid.decode(parsed.dataBytes),
        rawHex,
        timestamp,
      };
    } catch (err) {
      return this.failure(pid, rawHex, timestamp, err instanceof Error ? err.message : String(err));
    }
  }

  async readAll(pids: PidDefinition[]): Promise<PidReading[]> {
    const readings: PidReading[] = [];
    for (const pid of pids) {
      readings.push(await this.read(pid));
    }
    return readings;
  }

  private failure(pid: PidDefinition, rawHex: string | null, timestamp: number, error: string): PidReading {
    return { id: pid.id, name: pid.name, unit: pid.unit, value: null, rawHex, timestamp, error };
  }
}
