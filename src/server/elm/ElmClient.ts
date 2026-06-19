import { SerialTransport } from "../transport/SerialTransport.js";

export interface ElmCommandOptions {
  timeoutMs?: number;
}

export interface ElmRetryOptions extends ElmCommandOptions {
  maxAttempts?: number;
  retryDelayMs?: number;
}

const DEFAULT_TIMEOUT_MS = 2000;
const DEFAULT_MAX_ATTEMPTS = 5;
const DEFAULT_RETRY_DELAY_MS = 300;

/**
 * ELM327 AT command/response layer. Knows the chip's own text conventions
 * (command echo, the "OK" success marker) but nothing about what any given
 * command string means in KWP2000 terms - that's the kwp layer's job.
 */
export class ElmClient {
  constructor(private readonly transport: SerialTransport) {}

  /** Sends one line, returns the cleaned response text (command echo stripped if present). */
  async sendCommand(command: string, options: ElmCommandOptions = {}): Promise<string> {
    const raw = await this.transport.send(command, options.timeoutMs ?? DEFAULT_TIMEOUT_MS);
    return this.clean(raw, command);
  }

  /** Repeats a command until its response contains "OK", or gives up after maxAttempts. */
  async sendCommandUntilOk(command: string, options: ElmRetryOptions = {}): Promise<string> {
    const maxAttempts = options.maxAttempts ?? DEFAULT_MAX_ATTEMPTS;
    const retryDelayMs = options.retryDelayMs ?? DEFAULT_RETRY_DELAY_MS;
    let lastResponse = "";
    let lastError: unknown;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        lastResponse = await this.sendCommand(command, options);
        if (lastResponse.toUpperCase().includes("OK")) {
          return lastResponse;
        }
      } catch (err) {
        lastError = err;
      }
      if (attempt < maxAttempts) {
        await delay(retryDelayMs);
      }
    }

    const errSuffix = lastError ? `, last error: ${String(lastError)}` : "";
    throw new Error(
      `ElmClient: "${command}" did not return OK after ${maxAttempts} attempts ` +
        `(last response: ${JSON.stringify(lastResponse)}${errSuffix})`,
    );
  }

  private clean(raw: string, command: string): string {
    let text = raw.replace(/^\s+/, "");
    if (text.toUpperCase().startsWith(command.toUpperCase())) {
      text = text.slice(command.length);
    }
    return text
      .split(/[\r\n]+/)
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
      .join("\n");
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
