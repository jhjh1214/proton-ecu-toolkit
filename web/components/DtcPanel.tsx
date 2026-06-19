import { useState } from "react";
import type { DtcActionResult } from "../../src/shared/types";

interface DtcPanelProps {
  connected: boolean;
}

async function postJson(url: string): Promise<DtcActionResult> {
  const res = await fetch(url, { method: "POST" });
  return res.json();
}

export function DtcPanel({ connected }: DtcPanelProps) {
  const [scanResult, setScanResult] = useState<DtcActionResult | null>(null);
  const [clearResult, setClearResult] = useState<DtcActionResult | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleScan() {
    setBusy(true);
    try {
      setScanResult(await postJson("/api/dtc/scan"));
    } finally {
      setBusy(false);
    }
  }

  async function handleClear() {
    if (!window.confirm("Erase all stored diagnostic trouble codes? This cannot be undone.")) return;
    setBusy(true);
    try {
      setClearResult(await postJson("/api/dtc/clear"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="dtc-panel">
      <h2>Diagnostic Trouble Codes</h2>
      <div className="panel">
        <button disabled={!connected || busy} onClick={handleScan}>
          Scan for codes
        </button>
        <button disabled={!connected || busy} onClick={handleClear}>
          Clear codes
        </button>
      </div>
      {scanResult && (
        <p className={scanResult.positive ? undefined : "error"}>
          Scan:{" "}
          {scanResult.positive
            ? `raw response 58${scanResult.rawHex || "(empty)"} (undecoded - format not yet reverse-engineered)`
            : scanResult.error}
        </p>
      )}
      {clearResult && (
        <p className={clearResult.positive ? undefined : "error"}>
          Clear: {clearResult.positive ? "codes erased" : clearResult.error}
        </p>
      )}
    </div>
  );
}
