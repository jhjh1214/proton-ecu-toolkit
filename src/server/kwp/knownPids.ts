import type { PidDefinition } from "../../shared/types.js";

/**
 * The 5 PIDs the original ProtonOBDFree app reads, per HANDOVER.md §3.4.
 * All are Service 0x22 (ReadDataByCommonIdentifier) requests; `id` is the
 * already-offset (+0x57) 2-byte CID actually sent on the wire.
 */
export const KNOWN_PIDS: PidDefinition[] = [
  {
    id: "1101",
    name: "Coolant temp",
    unit: "°C",
    byteLength: 1,
    decode: ([byteA]) => byteA - 60,
  },
  {
    id: "1104",
    name: "RPM",
    unit: "rpm",
    byteLength: 2,
    decode: ([byteA, byteB]) => byteB * 255 + byteA,
  },
  {
    id: "110A",
    name: "TPS",
    unit: "%",
    byteLength: 1,
    decode: ([byteA]) => byteA * 0.39216,
  },
  {
    id: "1110",
    name: "Battery voltage",
    unit: "V",
    byteLength: 1,
    decode: ([byteA]) => byteA * 0.078431,
  },
  {
    id: "1113",
    name: "Vehicle speed",
    unit: "km/h",
    byteLength: 1,
    decode: ([byteA]) => byteA * 1.2,
  },
];

/** Service 0x22 CID used as a connectivity/keep-alive ping (§3.2, §3.3). Not a telemetry PID. */
export const TEST_PING_CID = "111F";
