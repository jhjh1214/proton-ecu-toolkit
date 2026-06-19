export interface GaugeRange {
  min: number;
  max: number;
  warnLow?: number;
  warnHigh?: number;
  dangerLow?: number;
  dangerHigh?: number;
}

export type GaugeZone = "ok" | "warn" | "danger";

/** Where `value` sits between `min` and `max`, clamped to [0, 1]. */
export function clampFraction(value: number, min: number, max: number): number {
  if (max <= min) return 0;
  return Math.min(1, Math.max(0, (value - min) / (max - min)));
}

export function zoneFor(value: number, range: GaugeRange): GaugeZone {
  const { warnLow, warnHigh, dangerLow, dangerHigh } = range;
  if ((dangerLow !== undefined && value <= dangerLow) || (dangerHigh !== undefined && value >= dangerHigh)) {
    return "danger";
  }
  if ((warnLow !== undefined && value <= warnLow) || (warnHigh !== undefined && value >= warnHigh)) {
    return "warn";
  }
  return "ok";
}

const ZONE_COLORS: Record<GaugeZone, string> = {
  ok: "#4caf50",
  warn: "#e0c34c",
  danger: "#e55353",
};

export function colorForZone(zone: GaugeZone): string {
  return ZONE_COLORS[zone];
}
