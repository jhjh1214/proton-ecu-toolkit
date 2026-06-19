import { useState } from "react";
import type { ConnectionState } from "../../src/shared/types";

interface ConnectionPanelProps {
  ports: string[];
  state: ConnectionState;
  detail?: string;
  onConnect: (port: string) => void;
  onDisconnect: () => void;
}

export function ConnectionPanel({ ports, state, detail, onConnect, onDisconnect }: ConnectionPanelProps) {
  const [selectedPort, setSelectedPort] = useState("");
  const effectivePort = ports.includes(selectedPort) ? selectedPort : ports[0] ?? "";
  const busy = state === "connecting" || state === "connected";

  return (
    <div className="panel">
      <select value={effectivePort} onChange={(e) => setSelectedPort(e.target.value)}>
        {ports.length === 0 && <option value="">No ports found</option>}
        {ports.map((p) => (
          <option key={p} value={p}>
            {p}
          </option>
        ))}
      </select>
      <button disabled={busy || !effectivePort} onClick={() => onConnect(effectivePort)}>
        Connect
      </button>
      <button disabled={state !== "connected"} onClick={onDisconnect}>
        Disconnect
      </button>
      <span>{detail ? `${state} (${detail})` : state}</span>
    </div>
  );
}
