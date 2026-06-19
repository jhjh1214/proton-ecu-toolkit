import { SerialTransport } from "../src/server/transport/SerialTransport.js";

const ports = await SerialTransport.listPorts();

if (ports.length === 0) {
  console.log("No serial ports found.");
} else {
  for (const port of ports) {
    console.log(
      `${port.path}  ${port.manufacturer ?? ""}  ${port.pnpId ?? ""}`.trimEnd(),
    );
  }
}
