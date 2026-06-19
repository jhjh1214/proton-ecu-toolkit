import { SerialTransport } from "../src/server/transport/SerialTransport.js";
import { ElmClient } from "../src/server/elm/ElmClient.js";

const path = process.argv[2] ?? process.env.ELM_PORT;
if (!path) {
  console.error("Usage: npm run atz -- <COM_PORT>   (e.g. npm run atz -- COM5)");
  process.exit(1);
}

const transport = new SerialTransport({ path });
const elm = new ElmClient(transport);

console.log(`Opening ${path}...`);
await transport.open();

try {
  console.log("Sending ATZ...");
  const response = await elm.sendCommand("ATZ", { timeoutMs: 5000 });
  console.log("Response:", JSON.stringify(response));
} finally {
  await transport.close();
  console.log("Port closed.");
}
