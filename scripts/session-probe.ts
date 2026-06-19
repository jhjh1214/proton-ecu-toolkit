import { KwpSession } from "../src/server/kwp/KwpSession.js";

const path = process.argv[2] ?? process.env.ELM_PORT;
if (!path) {
  console.error("Usage: npx tsx scripts/session-probe.ts <COM_PORT>");
  process.exit(1);
}

const MAX_CYCLES = 5;

const session = new KwpSession();

session.on("state", (state, detail) => {
  console.log(`[state] ${state}${detail ? ` (${detail})` : ""}`);
});

let cycles = 0;
session.on("pids", (readings) => {
  cycles++;
  console.log(`\n[pids cycle ${cycles}/${MAX_CYCLES}]`);
  for (const r of readings) {
    if (r.error) {
      console.log(`  ${r.name.padEnd(16)} ERROR: ${r.error}  (raw=${r.rawHex})`);
    } else {
      console.log(`  ${r.name.padEnd(16)} ${r.value?.toFixed(2)} ${r.unit}  (raw=${r.rawHex})`);
    }
  }
  if (cycles >= MAX_CYCLES) {
    void session.disconnect().then(() => process.exit(0));
  }
});

await session.connect(path);
