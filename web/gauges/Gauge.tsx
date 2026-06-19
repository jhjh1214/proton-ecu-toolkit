import type { PidReading } from "../../src/shared/types";
import { getGaugeRange } from "./gaugeConfig";
import { DialGauge } from "./DialGauge";
import { DigitalGauge } from "./DigitalGauge";

export type GaugeTheme = "dial" | "digital";

interface GaugeProps {
  reading: PidReading;
  history: number[];
  theme: GaugeTheme;
}

export function Gauge({ reading, history, theme }: GaugeProps) {
  const range = getGaugeRange(reading.id);
  return theme === "dial" ? (
    <DialGauge reading={reading} range={range} />
  ) : (
    <DigitalGauge reading={reading} history={history} range={range} />
  );
}
