import { SerialTransport } from "../src/server/transport/SerialTransport.js";
import { ElmClient } from "../src/server/elm/ElmClient.js";

const path = process.argv[2] ?? process.env.ELM_PORT;
const command = process.argv[3];
if (!path || !command) {
  console.error("Usage: npx tsx scripts/at-cmd.ts <COM_PORT> <COMMAND>   (e.g. npx tsx scripts/at-cmd.ts COM4 ATRV)");
  process.exit(1);
}

const transport = new SerialTransport({ path });
const elm = new ElmClient(transport);

await transport.open();
try {
  const response = await elm.sendCommand(command, { timeoutMs: 3000 });
  console.log(`${command}:`, response);
} finally {
  await transport.close();
}
