import { describe, expect, it } from "vitest";
import { clampFraction, colorForZone, zoneFor } from "./gaugeMath";

describe("clampFraction", () => {
  it("maps a value linearly between min and max", () => {
    expect(clampFraction(50, 0, 100)).toBe(0.5);
    expect(clampFraction(0, 0, 100)).toBe(0);
    expect(clampFraction(100, 0, 100)).toBe(1);
  });

  it("clamps out-of-range values to [0, 1]", () => {
    expect(clampFraction(-10, 0, 100)).toBe(0);
    expect(clampFraction(150, 0, 100)).toBe(1);
  });

  it("returns 0 for a degenerate range instead of dividing by zero", () => {
    expect(clampFraction(5, 10, 10)).toBe(0);
    expect(clampFraction(5, 10, 0)).toBe(0);
  });
});

describe("zoneFor", () => {
  it("is 'ok' when no thresholds are configured", () => {
    expect(zoneFor(9999, { min: 0, max: 100 })).toBe("ok");
  });

  it("flags high-side warn/danger thresholds", () => {
    const range = { min: 0, max: 100, warnHigh: 80, dangerHigh: 95 };
    expect(zoneFor(50, range)).toBe("ok");
    expect(zoneFor(80, range)).toBe("warn");
    expect(zoneFor(95, range)).toBe("danger");
  });

  it("flags low-side warn/danger thresholds", () => {
    const range = { min: 0, max: 100, warnLow: 20, dangerLow: 5 };
    expect(zoneFor(50, range)).toBe("ok");
    expect(zoneFor(20, range)).toBe("warn");
    expect(zoneFor(5, range)).toBe("danger");
  });

  it("prefers danger over warn when both thresholds are crossed", () => {
    const range = { min: 0, max: 100, warnHigh: 80, dangerHigh: 80 };
    expect(zoneFor(80, range)).toBe("danger");
  });
});

describe("colorForZone", () => {
  it("returns a distinct color per zone", () => {
    const colors = new Set([colorForZone("ok"), colorForZone("warn"), colorForZone("danger")]);
    expect(colors.size).toBe(3);
  });
});
