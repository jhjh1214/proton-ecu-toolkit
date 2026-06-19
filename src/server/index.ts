import express from "express";
import { createServer } from "node:http";
import { WebSocketServer } from "ws";
import { KwpSession } from "./kwp/KwpSession.js";
import { createRestRouter } from "./api/rest.js";
import { attachWebSocketServer } from "./api/ws.js";

const PORT = Number(process.env.PORT ?? 4000);

const session = new KwpSession();

const app = express();
app.use(express.json());
app.use("/api", createRestRouter(session));

const httpServer = createServer(app);
const wss = new WebSocketServer({ server: httpServer, path: "/ws" });
attachWebSocketServer(wss, session);

httpServer.listen(PORT, () => {
  console.log(`proton-ecu-toolkit server listening on http://localhost:${PORT}`);
});

process.on("SIGINT", () => {
  void session.disconnect().finally(() => process.exit(0));
});
