import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { GaugePanel } from "./GaugePanel";
import type { PidReading } from "../../src/shared/types";

afterEach(() => {
  cleanup();
});

const sampleReading: PidReading = {
  id: "1101",
  name: "Coolant temp",
  unit: "°C",
  value: 2,
  rawHex: "6211013E",
  timestamp: Date.now(),
};

describe("GaugePanel", () => {
  it("shows an empty-state message with no readings", () => {
    render(<GaugePanel readings={[]} history={{}} />);
    screen.getByText(/No live data yet/);
  });

  it("renders a gauge readout for each reading, defaulting to the dial theme", () => {
    render(<GaugePanel readings={[sampleReading]} history={{ "1101": [1, 1.5, 2] }} />);
    screen.getByText("Coolant temp");
    screen.getByText("°C");
  });

  it("switches to the digital theme on click", () => {
    render(<GaugePanel readings={[sampleReading]} history={{ "1101": [1, 1.5, 2] }} />);
    const digitalButton = screen.getByText("Digital");
    fireEvent.click(digitalButton);
    expect(digitalButton.className).toContain("active");
  });

  it("shows ERR instead of a value when the reading has an error", () => {
    const errored: PidReading = { ...sampleReading, value: null, error: "timed out" };
    render(<GaugePanel readings={[errored]} history={{}} />);
    screen.getByText("ERR");
  });
});
