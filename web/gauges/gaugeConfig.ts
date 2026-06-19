import type { GaugeRange } from "./gaugeMath";

/**
 * Display ranges/zones per known PID, for gauge scaling only. HANDOVER.md
 * doesn't specify these - they're reasonable defaults, not reverse-engineered
 * facts, and can be retuned once real readings are seen. An id with no entry
 * here (e.g. a Phase 2 scanner discovery) falls back to a plain 0-100 range.
 */
const RANGES: Record<string, GaugeRange> = {
  "1101": { min: -40, max: 120, warnHigh: 110, dangerHigh: 118 }, // coolant temp, °C
  "1104": { min: 0, max: 7000, warnHigh: 6000, dangerHigh: 6500 }, // RPM
  "110A": { min: 0, max: 100 }, // TPS, %
  "1110": { min: 0, max: 16, dangerLow: 11, warnLow: 12, warnHigh: 14.5, dangerHigh: 15 }, // battery, V
  "1113": { min: 0, max: 200 }, // vehicle speed, km/h
};

const DEFAULT_RANGE: GaugeRange = { min: 0, max: 100 };

export function getGaugeRange(pidId: string): GaugeRange {
  return RANGES[pidId] ?? DEFAULT_RANGE;
}
