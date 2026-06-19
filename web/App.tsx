import { useServerConnection } from "./hooks/useServerConnection";
import { ConnectionPanel } from "./components/ConnectionPanel";
import { PidTable } from "./components/PidTable";
import { LogConsole } from "./components/LogConsole";

export function App() {
  const { ports, state, detail, readings, logLines, connect, disconnect } = useServerConnection();

  return (
    <>
      <h1>Proton ECU Toolkit</h1>
      <ConnectionPanel ports={ports} state={state} detail={detail} onConnect={connect} onDisconnect={disconnect} />
      <PidTable readings={readings} />
      <LogConsole lines={logLines} />
    </>
  );
}
