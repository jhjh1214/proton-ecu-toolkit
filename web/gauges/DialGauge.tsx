import type { PidReading } from "../../src/shared/types";
import { clampFraction, colorForZone, zoneFor, type GaugeRange } from "./gaugeMath";

interface DialGaugeProps {
  reading: PidReading;
  range: GaugeRange;
}

const CENTER_X = 60;
const CENTER_Y = 60;
const RADIUS = 48;
const NEEDLE_LENGTH = 42;

/** Upper semicircle from `startDeg` to `endDeg` (0deg = right, 90deg = top, 180deg = left). */
function arcPath(startDeg: number, endDeg: number, segments = 48): string {
  const points: string[] = [];
  for (let i = 0; i <= segments; i++) {
    const deg = startDeg + ((endDeg - startDeg) * i) / segments;
    const rad = (deg * Math.PI) / 180;
    const x = CENTER_X + RADIUS * Math.cos(rad);
    const y = CENTER_Y - RADIUS * Math.sin(rad);
    points.push(`${x.toFixed(2)},${y.toFixed(2)}`);
  }
  return `M${points.join(" L")}`;
}

export function DialGauge({ reading, range }: DialGaugeProps) {
  const displayValue = reading.value ?? range.min;
  const fraction = clampFraction(displayValue, range.min, range.max);
  const hasValue = reading.value !== null && reading.error === undefined;
  const zone = hasValue ? zoneFor(displayValue, range) : "ok";
  const color = hasValue ? colorForZone(zone) : "#555";

  const needleDeg = 180 - fraction * 180;
  const needleRad = (needleDeg * Math.PI) / 180;
  const needleX = CENTER_X + NEEDLE_LENGTH * Math.cos(needleRad);
  const needleY = CENTER_Y - NEEDLE_LENGTH * Math.sin(needleRad);

  return (
    <div className="gauge dial-gauge">
      <svg viewBox="0 0 120 68">
        <path d={arcPath(180, 0)} fill="none" stroke="#333" strokeWidth="10" strokeLinecap="round" />
        <path d={arcPath(180, needleDeg)} fill="none" stroke={color} strokeWidth="10" strokeLinecap="round" />
        <line x1={CENTER_X} y1={CENTER_Y} x2={needleX} y2={needleY} stroke="#eee" strokeWidth="2" />
        <circle cx={CENTER_X} cy={CENTER_Y} r={3} fill="#eee" />
      </svg>
      <div className="gauge-readout" style={{ color }}>
        {reading.error ? "ERR" : reading.value !== null ? reading.value.toFixed(1) : "--"}
        <span className="gauge-unit">{reading.unit}</span>
      </div>
      <div className="gauge-name">{reading.name}</div>
    </div>
  );
}
