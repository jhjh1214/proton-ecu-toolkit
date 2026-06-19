import { describe, expect, it } from "vitest";
import { KNOWN_PIDS } from "./knownPids.js";

function pid(id: string) {
  const found = KNOWN_PIDS.find((p) => p.id === id);
  if (!found) throw new Error(`no known PID with id ${id}`);
  return found;
}

describe("coolant temp (1101): byteA - 60 = °C", () => {
  it("matches the §3.4 worked example (0x3E -> 2°C, a cold-engine reading)", () => {
    expect(pid("1101").decode([0x3e])).toBe(2);
  });

  it("handles the zero crossing", () => {
    expect(pid("1101").decode([60])).toBe(0);
    expect(pid("1101").decode([0])).toBe(-60);
  });
});

describe("RPM (1104): byteB*255 + byteA = rpm", () => {
  it("decodes a representative idle reading", () => {
    expect(pid("1104").decode([0xf0, 0x02])).toBe(750);
  });

  it("decodes zero", () => {
    expect(pid("1104").decode([0, 0])).toBe(0);
  });
});

describe("TPS (110A): byteA * 0.39216 = %", () => {
  it("decodes full scale to ~100%", () => {
    expect(pid("110A").decode([0xff])).toBeCloseTo(100, 0);
  });

  it("decodes closed throttle to 0%", () => {
    expect(pid("110A").decode([0])).toBe(0);
  });
});

describe("battery voltage (1110): byteA * 0.078431 = V", () => {
  it("decodes a plausible running-voltage reading", () => {
    expect(pid("1110").decode([163])).toBeCloseTo(12.78, 1);
  });
});

describe("vehicle speed (1113): byteA * 1.2 = km/h", () => {
  it("decodes a representative reading", () => {
    expect(pid("1113").decode([50])).toBe(60);
  });

  it("decodes standstill to 0", () => {
    expect(pid("1113").decode([0])).toBe(0);
  });
});
