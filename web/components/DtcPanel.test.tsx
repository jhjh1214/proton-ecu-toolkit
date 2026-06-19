import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DtcPanel } from "./DtcPanel";

beforeEach(() => {
  vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("DtcPanel", () => {
  it("disables scan/clear when not connected", () => {
    render(<DtcPanel connected={false} />);
    expect((screen.getByText("Scan for codes") as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByText("Clear codes") as HTMLButtonElement).disabled).toBe(true);
  });

  it("enables scan/clear when connected", () => {
    render(<DtcPanel connected={true} />);
    expect((screen.getByText("Scan for codes") as HTMLButtonElement).disabled).toBe(false);
    expect((screen.getByText("Clear codes") as HTMLButtonElement).disabled).toBe(false);
  });

  it("scans and shows the raw, undecoded response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        json: async () => ({ positive: true, rawHex: "024391", timestamp: Date.now() }),
      }),
    );
    render(<DtcPanel connected={true} />);
    fireEvent.click(screen.getByText("Scan for codes"));
    await waitFor(() => screen.getByText(/undecoded/));
    expect(fetch).toHaveBeenCalledWith("/api/dtc/scan", { method: "POST" });
  });

  it("asks for confirmation before clearing, and skips the request if declined", () => {
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(false));
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<DtcPanel connected={true} />);
    fireEvent.click(screen.getByText("Clear codes"));
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("clears codes when confirmed", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        json: async () => ({ positive: true, rawHex: "", timestamp: Date.now() }),
      }),
    );
    render(<DtcPanel connected={true} />);
    fireEvent.click(screen.getByText("Clear codes"));
    await waitFor(() => screen.getByText(/codes erased/));
    expect(fetch).toHaveBeenCalledWith("/api/dtc/clear", { method: "POST" });
  });
});
