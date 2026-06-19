import { describe, expect, it } from "vitest";
import {
  buildDtcClearRequest,
  buildDtcScanRequest,
  parseDtcClearResponse,
  parseDtcScanResponse,
} from "./dtc.js";

describe("buildDtcScanRequest / buildDtcClearRequest", () => {
  it("matches the request bytes from §3.7", () => {
    expect(buildDtcScanRequest()).toBe("18020000");
    expect(buildDtcClearRequest()).toBe("140000");
  });
});

describe("parseDtcScanResponse", () => {
  it("parses a positive response and keeps the rest as an undecoded hex blob", () => {
    expect(parseDtcScanResponse("580243910071")).toEqual({
      positive: true,
      dataHex: "0243910071",
    });
  });

  it("tolerates whitespace from multi-line ELM327 output", () => {
    expect(parseDtcScanResponse("58 02 43 91\r\n")).toEqual({
      positive: true,
      dataHex: "024391",
    });
  });

  it("parses a negative response with its NRC", () => {
    expect(parseDtcScanResponse("7F1822")).toEqual({
      positive: false,
      dataHex: "",
      nrc: 0x22,
    });
  });

  it("returns null for unrecognized/garbage responses", () => {
    expect(parseDtcScanResponse("NO DATA")).toBeNull();
    expect(parseDtcScanResponse("")).toBeNull();
    expect(parseDtcScanResponse("621101")).toBeNull(); // a PID response, not a DTC one
  });
});

describe("parseDtcClearResponse", () => {
  it("parses a positive clear response", () => {
    expect(parseDtcClearResponse("54")).toEqual({ positive: true, dataHex: "" });
  });

  it("parses a negative clear response with its NRC", () => {
    expect(parseDtcClearResponse("7F1431")).toEqual({
      positive: false,
      dataHex: "",
      nrc: 0x31,
    });
  });
});
