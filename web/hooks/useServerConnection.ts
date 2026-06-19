import { useCallback, useEffect, useState } from "react";
import type { ConnectionState, PidReading, ServerMessage } from "../../src/shared/types";

const MAX_LOG_LINES = 50;
const MAX_HISTORY_POINTS = 30;
const RECONNECT_DELAY_MS = 2000;

export interface ServerConnection {
  ports: string[];
  state: ConnectionState;
  detail?: string;
  port: string | null;
  readings: PidReading[];
  /** Recent values per PID id, oldest first, capped at MAX_HISTORY_POINTS. For sparklines. */
  history: Record<string, number[]>;
  logLines: string[];
  connect: (port: string) => Promise<void>;
  disconnect: () => Promise<void>;
}

export function useServerConnection(): ServerConnection {
  const [ports, setPorts] = useState<string[]>([]);
  const [state, setState] = useState<ConnectionState>("disconnected");
  const [detail, setDetail] = useState<string | undefined>(undefined);
  const [port, setPort] = useState<string | null>(null);
  const [readings, setReadings] = useState<PidReading[]>([]);
  const [history, setHistory] = useState<Record<string, number[]>>({});
  const [logLines, setLogLines] = useState<string[]>([]);

  const log = useCallback((line: string) => {
    const timestamp = new Date().toLocaleTimeString();
    setLogLines((prev) => {
      const next = [...prev, `[${timestamp}] ${line}`];
      return next.length > MAX_LOG_LINES ? next.slice(next.length - MAX_LOG_LINES) : next;
    });
  }, []);

  useEffect(() => {
    let cancelled = false;
    fetch("/api/ports")
      .then((res) => res.json())
      .then((list: { path: string }[]) => {
        if (!cancelled) setPorts(list.map((p) => p.path));
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let socket: WebSocket | undefined;
    let reconnectTimer: ReturnType<typeof setTimeout> | undefined;
    let closedByUs = false;

    const connectSocket = () => {
      const url = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws`;
      socket = new WebSocket(url);

      socket.addEventListener("message", (event) => {
        const message = JSON.parse(event.data as string) as ServerMessage;
        if (message.type === "status") {
          setState(message.state);
          setDetail(message.detail);
          setPort(message.port);
          log(`status: ${message.state}${message.detail ? ` (${message.detail})` : ""}`);
        } else if (message.type === "pids") {
          setReadings(message.readings);
          setHistory((prev) => {
            const next = { ...prev };
            for (const reading of message.readings) {
              if (reading.value === null) continue;
              const points = [...(next[reading.id] ?? []), reading.value];
              next[reading.id] =
                points.length > MAX_HISTORY_POINTS ? points.slice(points.length - MAX_HISTORY_POINTS) : points;
            }
            return next;
          });
        }
      });

      socket.addEventListener("close", () => {
        if (closedByUs) return;
        log(`WebSocket disconnected, retrying in ${RECONNECT_DELAY_MS / 1000}s...`);
        reconnectTimer = setTimeout(connectSocket, RECONNECT_DELAY_MS);
      });
    };

    connectSocket();

    return () => {
      closedByUs = true;
      clearTimeout(reconnectTimer);
      socket?.close();
    };
  }, [log]);

  const connect = useCallback(
    async (selectedPort: string) => {
      log(`Connecting to ${selectedPort}...`);
      const res = await fetch("/api/connect", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ port: selectedPort }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        log(`Connect failed: ${body.error ?? res.statusText}`);
      }
    },
    [log],
  );

  const disconnect = useCallback(async () => {
    await fetch("/api/disconnect", { method: "POST" });
  }, []);

  return { ports, state, detail, port, readings, history, logLines, connect, disconnect };
}
