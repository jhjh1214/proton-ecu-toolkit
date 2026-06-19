import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";

const container = document.querySelector("#app");
if (!container) throw new Error("missing #app root element");

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
