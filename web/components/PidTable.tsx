import type { PidReading } from "../../src/shared/types";

interface PidTableProps {
  readings: PidReading[];
}

export function PidTable({ readings }: PidTableProps) {
  return (
    <table>
      <tbody>
        {readings.map((r) => (
          <tr key={r.id} className={r.error ? "error" : undefined}>
            <td>{r.name}</td>
            <td>{r.value !== null ? r.value.toFixed(2) : "--"}</td>
            <td>{r.unit}</td>
            <td>{r.error ?? ""}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
