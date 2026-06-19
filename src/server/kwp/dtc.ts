/**
 * Plain hex string encode/decode for the DTC scan/erase services from
 * HANDOVER.md §3.7 (non-CFE / Siemens branch):
 *   18020000 -> ReadDiagnosticTroubleCodesByStatus, positive resp contains "58"
 *   140000   -> ClearDiagnosticInformation, positive resp contains "54"
 *
 * §3.7 only documents the request bytes and the positive-response SID - it
 * does NOT document how the returned DTC bytes are laid out (how many bytes
 * per code, status byte format, etc.). So scan responses are surfaced as a
 * raw, undecoded hex blob rather than split into individual fault codes
 * until that's reverse-engineered against a real ECU response.
 */

export interface DtcResponse {
  positive: boolean;
  /** Raw data hex after the positive response SID. Undecoded - see module docblock. */
  dataHex: string;
  /** Negative response code, only set when positive is false. */
  nrc?: number;
}

const SCAN_REQUEST = "18020000";
const CLEAR_REQUEST = "140000";
const SCAN_POSITIVE_SID = "58"; // 0x18 + 0x40
const CLEAR_POSITIVE_SID = "54"; // 0x14 + 0x40
const NEGATIVE_SID = "7F";

export function buildDtcScanRequest(): string {
  return SCAN_REQUEST;
}

export function buildDtcClearRequest(): string {
  return CLEAR_REQUEST;
}

function parseDtcResponse(raw: string, positiveSid: string): DtcResponse | null {
  const hex = raw.replace(/[^0-9a-fA-F]/g, "").toUpperCase();
  if (hex.length < 2) return null;

  const sid = hex.slice(0, 2);

  if (sid === NEGATIVE_SID) {
    const nrc = parseInt(hex.slice(4, 6), 16);
    return { positive: false, dataHex: "", nrc: Number.isNaN(nrc) ? undefined : nrc };
  }

  if (sid !== positiveSid) return null;

  return { positive: true, dataHex: hex.slice(2) };
}

export function parseDtcScanResponse(raw: string): DtcResponse | null {
  return parseDtcResponse(raw, SCAN_POSITIVE_SID);
}

export function parseDtcClearResponse(raw: string): DtcResponse | null {
  return parseDtcResponse(raw, CLEAR_POSITIVE_SID);
}
