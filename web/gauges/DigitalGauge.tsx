import type { PidReading } from "../../src/shared/types";
import { clampFraction, colorForZone, zoneFor, type GaugeRange } from "./gaugeMath";

interface DigitalGaugeProps {
  reading: PidReading;
  history: number[];
  range: GaugeRange;
}

export function DigitalGauge({ reading, history, range }: DigitalGaugeProps) {
  const hasValue = reading.value !== null && reading.error === undefined;
  const zone = hasValue && reading.value !== null ? zoneFor(reading.value, range) : "ok";
  const color = hasValue ? colorForZone(zone) : "#555";

  const points =
    history.length > 1
      ? history
          .map((v, i) => {
            const x = (i / (history.length - 1)) * 100;
            const y = 30 - clampFraction(v, range.min, range.max) * 28;
            return `${x.toFixed(1)},${y.toFixed(1)}`;
          })
          .join(" ")
      : "";

  return (
    <div className="gauge digital-gauge">
      <div className="gauge-name">{reading.name}</div>
      <div className="gauge-readout" style={{ color }}>
        {reading.error ? "ERR" : reading.value !== null ? reading.value.toFixed(1) : "--"}
        <span className="gauge-unit">{reading.unit}</span>
      </div>
      <svg viewBox="0 0 100 32" preserveAspectRatio="none" className="sparkline">
        {points && <polyline points={points} fill="none" stroke={color} strokeWidth="2" />}
      </svg>
    </div>
  );
}
