import { useServerConnection } from "./hooks/useServerConnection";
import { ConnectionPanel } from "./components/ConnectionPanel";
import { DtcPanel } from "./components/DtcPanel";
import { LogConsole } from "./components/LogConsole";
import { GaugePanel } from "./gauges/GaugePanel";

export function App() {
  const { ports, state, detail, readings, history, logLines, connect, disconnect } = useServerConnection();

  return (
    <>
      <h1>Proton ECU Toolkit</h1>
      <ConnectionPanel ports={ports} state={state} detail={detail} onConnect={connect} onDisconnect={disconnect} />
      <GaugePanel readings={readings} history={history} />
      <DtcPanel connected={state === "connected"} />
      <LogConsole lines={logLines} />
    </>
  );
}
