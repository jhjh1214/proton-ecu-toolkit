import { describe, expect, it } from "vitest";
import { buildReadByCidRequest, containsPositiveResponseMarker, parseReadByCidResponse } from "./protocol.js";

describe("buildReadByCidRequest", () => {
  it("prefixes the CID with service 0x22", () => {
    expect(buildReadByCidRequest("1101")).toBe("221101");
    expect(buildReadByCidRequest("111f")).toBe("22111F");
  });
});

describe("parseReadByCidResponse", () => {
  it("parses a 1-byte positive response (coolant temp example from §3.4)", () => {
    const result = parseReadByCidResponse("6211013E", "1101");
    expect(result).toEqual({ positive: true, dataBytes: [0x3e] });
  });

  it("parses a 2-byte positive response (RPM)", () => {
    const result = parseReadByCidResponse("62110AB204", "110A");
    expect(result).toEqual({ positive: true, dataBytes: [0xb2, 0x04] });
  });

  it("tolerates whitespace/newlines from multi-line ELM327 output", () => {
    const result = parseReadByCidResponse("62 1101 3E\r\n", "1101");
    expect(result).toEqual({ positive: true, dataBytes: [0x3e] });
  });

  it("returns a negative response with its NRC", () => {
    const result = parseReadByCidResponse("7F2231", "1101");
    expect(result).toEqual({ positive: false, dataBytes: [], nrc: 0x31 });
  });

  it("returns null when the CID echo doesn't match what was requested", () => {
    const result = parseReadByCidResponse("6211043E", "1101");
    expect(result).toBeNull();
  });

  it("returns null for unrecognized/garbage responses", () => {
    expect(parseReadByCidResponse("NODATA", "1101")).toBeNull();
    expect(parseReadByCidResponse("", "1101")).toBeNull();
  });
});

describe("containsPositiveResponseMarker", () => {
  it("matches the test ping example from §3.2 (22111F -> contains 62)", () => {
    expect(containsPositiveResponseMarker("62111F00")).toBe(true);
  });

  it("rejects responses without a 62 byte", () => {
    expect(containsPositiveResponseMarker("NO DATA")).toBe(false);
    expect(containsPositiveResponseMarker("7F2211")).toBe(false);
  });
});
