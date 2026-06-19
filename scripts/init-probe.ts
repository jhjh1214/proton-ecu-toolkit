import { SerialTransport } from "../src/server/transport/SerialTransport.js";
import { ElmClient } from "../src/server/elm/ElmClient.js";
import { buildReadByCidRequest, containsPositiveResponseMarker } from "../src/server/kwp/protocol.js";
import { TEST_PING_CID } from "../src/server/kwp/knownPids.js";

const path = process.argv[2] ?? process.env.ELM_PORT;
if (!path) {
  console.error("Usage: npx tsx scripts/init-probe.ts <COM_PORT>");
  process.exit(1);
}

function describe(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

const transport = new SerialTransport({ path });
const elm = new ElmClient(transport);

console.log(`Opening ${path}...`);
await transport.open();

try {
  console.log("ATZ       :", await elm.sendCommand("ATZ", { timeoutMs: 3000 }).catch(describe));
  console.log("ATZ (x2)  :", await elm.sendCommand("ATZ", { timeoutMs: 3000 }).catch(describe));
  console.log("ATE0      :", await elm.sendCommandUntilOk("ATE0").catch(describe));
  console.log("ATH0      :", await elm.sendCommandUntilOk("ATH0").catch(describe));
  console.log("ATSP5     :", await elm.sendCommandUntilOk("ATSP5").catch(describe));
  console.log("ATSH8101F1:", await elm.sendCommandUntilOk("ATSH8101F1").catch(describe));

  console.log("\nAll ELM327-only config commands done. The rest needs the ECU powered (ignition on).");
  console.log("Settling 2s, then trying the test ping 22111F (expected to fail/timeout with ignition off)...");
  await new Promise((resolve) => setTimeout(resolve, 2000));

  try {
    const raw = await elm.sendCommand(buildReadByCidRequest(TEST_PING_CID), { timeoutMs: 1500 });
    console.log("22111F    :", JSON.stringify(raw), "- positive:", containsPositiveResponseMarker(raw));
  } catch (err) {
    console.log("22111F    : failed as expected without ignition on -", describe(err));
  }

  console.log(
    "\nATRV (OBD port voltage, doesn't need the ECU - pin 16 is usually hot off the battery):",
    await elm.sendCommand("ATRV", { timeoutMs: 2000 }).catch(describe),
  );
} finally {
  await transport.close();
  console.log("\nPort closed.");
}
