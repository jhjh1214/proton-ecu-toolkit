import { useEffect, useRef } from "react";

interface LogConsoleProps {
  lines: string[];
}

export function LogConsole({ lines }: LogConsoleProps) {
  const preRef = useRef<HTMLPreElement>(null);

  useEffect(() => {
    if (preRef.current) {
      preRef.current.scrollTop = preRef.current.scrollHeight;
    }
  }, [lines]);

  return <pre id="log" ref={preRef}>{lines.join("\n")}</pre>;
}
