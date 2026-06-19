import { Router } from "express";
import { SerialTransport } from "../transport/SerialTransport.js";
import type { KwpSession } from "../kwp/KwpSession.js";

export function createRestRouter(session: KwpSession): Router {
  const router = Router();

  router.get("/ports", async (_req, res) => {
    const ports = await SerialTransport.listPorts();
    res.json(ports.map((p) => ({ path: p.path, manufacturer: p.manufacturer, pnpId: p.pnpId })));
  });

  router.get("/status", (_req, res) => {
    res.json({ state: session.getState(), port: session.getPort() });
  });

  router.post("/connect", async (req, res) => {
    const port = req.body?.port;
    if (typeof port !== "string" || port.length === 0) {
      res.status(400).json({ error: "Expected { port: string } in body" });
      return;
    }
    try {
      await session.connect(port);
      res.json({ state: session.getState(), port: session.getPort() });
    } catch (err) {
      res.status(502).json({
        error: err instanceof Error ? err.message : String(err),
        state: session.getState(),
      });
    }
  });

  router.post("/disconnect", async (_req, res) => {
    await session.disconnect();
    res.json({ state: session.getState() });
  });

  router.post("/dtc/scan", async (_req, res) => {
    try {
      res.json(await session.scanDtcs());
    } catch (err) {
      res.status(409).json({ error: err instanceof Error ? err.message : String(err) });
    }
  });

  router.post("/dtc/clear", async (_req, res) => {
    try {
      res.json(await session.clearDtcs());
    } catch (err) {
      res.status(409).json({ error: err instanceof Error ? err.message : String(err) });
    }
  });

  return router;
}
