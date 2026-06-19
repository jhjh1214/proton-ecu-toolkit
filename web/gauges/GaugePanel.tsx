import { useState } from "react";
import type { PidReading } from "../../src/shared/types";
import { Gauge, type GaugeTheme } from "./Gauge";

interface GaugePanelProps {
  readings: PidReading[];
  history: Record<string, number[]>;
}

export function GaugePanel({ readings, history }: GaugePanelProps) {
  const [theme, setTheme] = useState<GaugeTheme>("dial");

  return (
    <div className="gauge-panel">
      <div className="theme-toggle">
        <button className={theme === "dial" ? "active" : undefined} onClick={() => setTheme("dial")}>
          Dial
        </button>
        <button className={theme === "digital" ? "active" : undefined} onClick={() => setTheme("digital")}>
          Digital
        </button>
      </div>
      <div className="gauge-grid">
        {readings.map((reading) => (
          <Gauge key={reading.id} reading={reading} history={history[reading.id] ?? []} theme={theme} />
        ))}
        {readings.length === 0 && <p className="gauge-empty">No live data yet - connect to the adapter.</p>}
      </div>
    </div>
  );
}
