import type { WebSocket, WebSocketServer } from "ws";
import type { KwpSession } from "../kwp/KwpSession.js";
import type { ServerMessage } from "../../shared/types.js";

/** Pushes live connection state + PID readings to every connected browser tab. */
export function attachWebSocketServer(wss: WebSocketServer, session: KwpSession): void {
  const broadcast = (message: ServerMessage): void => {
    const payload = JSON.stringify(message);
    for (const client of wss.clients) {
      if (client.readyState === client.OPEN) {
        client.send(payload);
      }
    }
  };

  session.on("state", (state, detail) => {
    broadcast({ type: "status", state, port: session.getPort(), detail });
  });
  session.on("pids", (readings) => {
    broadcast({ type: "pids", readings });
  });

  wss.on("connection", (socket: WebSocket) => {
    const message: ServerMessage = {
      type: "status",
      state: session.getState(),
      port: session.getPort(),
    };
    socket.send(JSON.stringify(message));
  });
}
