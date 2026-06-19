/**
 * Plain hex string encode/decode for KWP2000 Service 0x22
 * (ReadDataByCommonIdentifier). Assumes ATH0 (headers off) and ATE0 (echo
 * off) are already in effect, so there's no header/checksum byte-math to
 * do here (HANDOVER.md §3.1) - just ASCII hex.
 */

export interface ParsedReadByCidResponse {
  positive: boolean;
  /** Data bytes after the CID echo. Empty for negative responses. */
  dataBytes: number[];
  /** Negative response code, only set when positive is false. */
  nrc?: number;
}

const POSITIVE_RESPONSE_SID = 0x62; // 0x22 + 0x40
const NEGATIVE_RESPONSE_SID = 0x7f;

export function buildReadByCidRequest(cid: string): string {
  return `22${cid.toUpperCase()}`;
}

export function parseReadByCidResponse(raw: string, cid: string): ParsedReadByCidResponse | null {
  const hex = raw.replace(/[^0-9a-fA-F]/g, "").toUpperCase();
  if (hex.length < 2) return null;

  const sid = parseInt(hex.slice(0, 2), 16);

  if (sid === NEGATIVE_RESPONSE_SID) {
    const nrc = parseInt(hex.slice(4, 6), 16);
    return { positive: false, dataBytes: [], nrc: Number.isNaN(nrc) ? undefined : nrc };
  }

  if (sid !== POSITIVE_RESPONSE_SID) return null;

  const echoedCid = hex.slice(2, 6);
  if (echoedCid !== cid.toUpperCase()) return null;

  const dataHex = hex.slice(6);
  const dataBytes: number[] = [];
  for (let i = 0; i + 1 < dataHex.length; i += 2) {
    dataBytes.push(parseInt(dataHex.slice(i, i + 2), 16));
  }
  return { positive: true, dataBytes };
}

/** Cheap check used for the test ping (§3.2): "success = response contains 62". */
export function containsPositiveResponseMarker(raw: string): boolean {
  return raw.replace(/[^0-9a-fA-F]/g, "").toUpperCase().includes("62");
}
