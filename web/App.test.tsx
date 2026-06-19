import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { App } from "./App";

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  private listeners: Record<string, ((ev: unknown) => void)[]> = {};

  constructor(public url: string) {
    FakeWebSocket.instances.push(this);
  }

  addEventListener(type: string, callback: (ev: unknown) => void): void {
    (this.listeners[type] ??= []).push(callback);
  }

  close(): void {}
}

beforeEach(() => {
  FakeWebSocket.instances = [];
  vi.stubGlobal("WebSocket", FakeWebSocket);
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({ ok: true, json: async () => [] }),
  );
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("App", () => {
  it("renders the dashboard shell and starts disconnected, with no hardware", async () => {
    render(<App />);
    screen.getByText("Proton ECU Toolkit");
    await waitFor(() => screen.getByText("disconnected"));
  });

  it("opens exactly one WebSocket to /ws on mount", () => {
    render(<App />);
    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(FakeWebSocket.instances[0].url).toContain("/ws");
  });
});
