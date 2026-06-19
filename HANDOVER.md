# Handover: Proton Waja CPS / Siemens EMS700 ECU Toolkit

Status: planning complete, **no code written yet**. This doc is meant to be the *only* context a fresh session needs to start implementing. Copy it into the new project's repo (e.g. as `README.md` or `docs/HANDOVER.md`) once that repo exists.

## 1. Goal

Build a desktop tool (Node.js + TypeScript, local web UI) that:
1. Connects to an ELM327 clone (PIC18F25K80) paired over Bluetooth Classic SPP, which Windows exposes as a virtual COM port.
2. Replicates the exact ECU handshake used by the decompiled "ProtonOBDFree" Torque plugin app to get a real, sustained KWP2000 session with the ECU (not just `BUS INIT: OK` — actual data flowing).
3. Reads the 5 PIDs the original app exposes, to prove the engine works end-to-end.
4. Then goes further than the original app: systematically probes the ECU's identifier space to discover **which other PIDs/identifiers this Siemens EMS700 ECU actually supports**, beyond the 5 hardcoded ones.
5. Has a clean, layered structure so DTC read/erase and the fan/injector actuator-test feature (also reverse-engineered, see §3.6) can be bolted on later without rework.

## 2. Hardware & vehicle context

- Vehicle: Proton Waja CPS, Campro CPS engine, **Siemens EMS700** ECU.
- Protocol: **KWP2000 / ISO 14230-4, Fast Init, 10.4 kbps** (K-line).
- Adapter: ELM327 clone (PIC18F25K80) over Bluetooth Classic SPP.
- **Important distinction**: the Bluetooth link between PC↔ELM327 is just a serial transport (whatever baud the OS/COM port negotiates — irrelevant to the protocol). The 10.4 kbps figure is the K-line speed between the ELM327 and the ECU, which the ELM327 handles internally once you set `ATSP5`. You never touch that speed directly.
- You already proved the adapter is reachable: `ATSH 81 10 F1` gave a stable `BUS INIT: OK` in a raw terminal, but standard PIDs returned `NO DATA`. Root cause found below (§3.2) — wrong header.
- Since raw terminal testing already worked, the adapter is already paired as a **Windows COM port**. The new tool talks to that same COM port like any serial device — no raw Android `BluetoothSocket` / Bluetooth stack code needed.

## 3. Reverse-engineered protocol (source of truth)

All of this was extracted from the decompiled "ProtonOBDFree" Torque plugin APK (`com.obd.saukintelli.protonobdpro2` / `obd.saukintelli.com.x10obd`). The app hid every AT/KWP string behind a trivial cipher (ROT13 on letters, ROT5 on digits — self-inverse) in `strings.xml`; all values below are already decoded.

### 3.1 ELM327 response framing — no manual checksum/header math needed

The app sets `ATH0` (headers off in responses). That means:
- **You send**: just the KWP2000 service+data bytes as an ASCII hex string (e.g. `22111F`). The ELM327 prepends the header (`81 01 F1`) and appends the checksum on the wire automatically, because you set it once via `ATSH`.
- **You receive**: just the response data bytes as ASCII hex (e.g. `62111F00`), no header, no checksum. The chip strips both before showing you the response.
- Conclusion: the toolkit's protocol layer never needs real ISO14230 checksum/header byte-math. Just build/parse plain hex strings. Don't over-engineer this.

### 3.2 Cold-start init sequence (`OBDadapterService.b()` in the decompile)

```
ATZ          x2   (reset; original app doesn't even check the response)
ATE0         retry until response contains "OK"   (echo off)
ATH0         retry until "OK"                       (headers off in responses)
ATSP5        retry until "OK"   -> protocol = ISO14230-4 KWP, FAST INIT, 10.4kbps
ATSH8101F1   retry until "OK"   -> header = 81 01 F1
[~2 second settle delay]
22111F                          -> test ping (Service 0x22 ReadDataByCommonIdentifier, CID 0x111F)
                                   success = response contains "62" (0x22 + 0x40 = positive response SID)
```

**This is almost certainly your bug**: you tested `ATSH 81 10 F1` (target `0x10`, the textbook default). The real ECU listens on target **`0x01`**, not `0x10`. `BUS INIT: OK` doesn't depend on header bytes (it's just a wake-up pulse timing pattern) — that's why init looked fine while every PID request silently failed.

### 3.3 Keep-alive / re-sync (`PluginReceiver1.StartCommService` / `PluginService`)

KWP2000 sessions time out if the bus goes quiet too long (P3 timeout). The app's pattern:
- Every time it wants fresh PID data, it first sends the test ping `22111F` again.
- If that fails, it sends `ATFI` (**Force Fast Init** — an ELM327 AT command that re-triggers the wake-up pulse without re-sending `ATSP`/`ATSH`) and tries again.
- If that still fails, it fully restarts: `ATSP5` → `ATSH8101F1` → ping again.

For your tool: keep a background "tester present" cadence — re-issue some request (the ping, or whatever PID you're polling) at least every 2–4 seconds while connected, or the ECU may drop the session.

### 3.4 The 5 known-working PIDs — and a second hidden cipher

`pidlist2b.csv` (bundled in the APK) stores Mode+PID as `2210AA`, `2210AD`, `2210B3`, `2210B9`, `2210BC` — **these are NOT the real wire values**. The actual polling engine (`J/a.java` in the decompile) adds **0x57 (87 decimal)** to the 2-byte identifier portion before sending. Verified real requests:

| Send (Service 0x22) | PID | Formula (byteA = 1st data byte, byteB = 2nd) |
|---|---|---|
| `221101` | Coolant temp | `byteA − 60` = °C |
| `221104` | RPM | `byteB×255 + byteA` = rpm |
| `22110A` | TPS | `byteA × 0.39216` (≈ byteA/2.55) = % |
| `221110` | Battery voltage | `byteA × 0.078431` (≈ byteA/12.75) = V |
| `221113` | Vehicle speed | `byteA × 1.2` = km/h |

Response shape: `62 <2-byte CID echo> <byteA> [byteB]`. E.g. requesting `221101` gives back something like `6211013E` → CID echoed as `1101`, byteA = `0x3E`.

This +0x57 offset is corroborated by the fact that the resulting real IDs (`0x1101`, `0x1104`, `0x110A`, `0x1110`, `0x1113`) cluster tightly with two other confirmed-real identifiers: the test ping `0x111F`, and the LID groups below.

### 3.5 Unverified-but-likely-real extra identifiers (high-value scan targets)

Two CSVs ship in the APK (`diagstatus1.csv`, `signalstatus.csv`) but are **not wired into any code path** in this Free build — almost certainly Pro-only leftovers, but they tell us real identifiers the ECU likely supports:

- `1147`, `1148`, `1149` — digital I/O / signal status bytes (idle switch, full load, gear position, A/C request, VIM/CPS position, clutch switch, cam control, knock control, tank purge valve, lambda controllers, cat heating, fuel cutoff, etc. — each is a bitfield, one bit per row in the CSV)
- `11CC`, `11CD`, `11CE`, `11CF` — onboard diagnostic test status bitfields (catalyst, lambda probe, EGR, misfire, knock sensor check, etc.)

Hypothesis to test first (cheap, high value, do this before any brute force): these are very likely **also Service 0x22 CIDs**, same family as the 5 known PIDs (e.g. send `2211CC`, `221147`, etc.) — everything else in this app exclusively uses SID `0x22` for reads; SID `0x21` (ReadDataByLocalIdentifier) was never observed anywhere in the decompiled code. But verify both `0x22` and `0x21` against these specific 7 known candidates before trusting the hypothesis.

### 3.6 Diagnostic session + actuator test feature (bonus — Phase 4 material)

From `extraFunction.java` in the decompile — a "kill a cylinder / toggle the fan" feature:

```
1083   -> StartDiagnosticSession, sub-function 0x83 (must be sent before IO control works)
            response "7F" = negative (retry up to 5x), "50" = positive (success)
20     -> StopDiagnosticSession (sent automatically when the original app's screen closes)
```

IO control commands (Service `0x30` = InputOutputControlByLocalIdentifier, control parameter `0x07` = **Short Term Adjustment** — i.e. these are session-bound, non-permanent overrides that revert on session end/ECU reset, not EEPROM writes):

| Command | Effect |
|---|---|
| `30420700` | All injectors ON (normal/default) |
| `30420701` | All injectors OFF |
| `30420702` | Injector 1 OFF (bit 0x02) |
| `30420704` | Injector 2 OFF (bit 0x04) |
| `30420708` | Injector 3 OFF (bit 0x08) |
| `30420710` | Injector 4 OFF (bit 0x10) |
| `304A0700` | Radiator fan OFF |
| `304A0701` | Radiator fan ON |

The original app never reads back the response to these (fire-and-forget) — your tool should actually check for `70` (positive, 0x30+0x40) vs `7F` (negative) instead, that's a straightforward improvement.

### 3.7 DTC scan/erase (non-CFE / Siemens branch)

```
18020000   -> ReadDiagnosticTroubleCodesByStatus  (expect positive resp containing "58")
140000     -> ClearDiagnosticInformation            (expect positive resp containing "54")
```

## 4. Chosen architecture

**Revised 2026-06-20.** The project started as Node.js + TypeScript with a local web UI (Express/WebSocket backend, React frontend in a browser tab) - see §4.1 below for why, and it's the reason `src/` and `web/` still exist in this repo. Phase 0 and Phase 1 were fully built and validated against real hardware in that stack (engine running, both the init sequence and all 5 known PIDs confirmed correct). But the deliverable needed to be a genuinely native desktop app - no web rendering technology at all, not even packaged invisibly (i.e. not Electron/Tauri) - so the protocol engine was ported to C#/.NET, faithfully, byte-for-byte, now that every formula and timing assumption was already a hardware-confirmed fact rather than decompile theory. The TS/web code is left in place as a validated reference, not deleted; it's not under active development.

- **Language/runtime**: C#/.NET 8, WPF for the UI (MVVM via `CommunityToolkit.Mvvm`).
- **Transport**: `System.IO.Ports.SerialPort` talking directly to the Windows COM port the ELM327 is paired as (no Bluetooth-specific API needed - Windows already exposes the paired SPP connection as a COM port, same as the original Node version's `serialport` package did). On this hardware that's **COM4** (the "outgoing" port bound to the adapter's MAC address; COM3 is the generic Bluetooth "incoming" port Windows also creates and isn't the one to use).
- **App shell**: no server, no IPC, no serialization. The WPF `MainViewModel` owns one `KwpSession` instance in-process and subscribes to its events directly. Packaged via `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` - a single standalone `.exe`, no .NET runtime install required on the target machine, no internet connection needed ever.
- **Why not Electron/Tauri**: both still render the UI with HTML/CSS under the hood, just invisibly packaged - the requirement was no web tech at all, not just no visible browser tab.
- **Why not mobile (yet)**: explicitly deferred by design - if it happens later, .NET MAUI can share the C# `Core` class library's business logic with this desktop app.

### 4.1 Original (superseded) reasoning for Node/TS + web

Kept for history: Node + TypeScript end-to-end gave strong typing for hex/byte protocol parsing and a mature `serialport` package; a local Express/WebSocket backend + Vite/React frontend in a browser tab was fast to build and avoided Electron packaging up front. This reasoning was sound for what it optimized for (iteration speed while reverse-engineering was still unverified) - it just stopped matching the actual deliverable once Phase 0/1 were proven and the desktop-native requirement became explicit.

## 5. Project structure

```
proton-ecu-toolkit/
├── HANDOVER.md
├── desktop/                                  # current development - C#/.NET WPF app
│   ├── ProtonEcuToolkit.sln
│   ├── ProtonEcuToolkit.Core/                # class library, no UI dependency
│   │   ├── Transport/SerialTransport.cs      # open/close/write COM port, buffers reads until the '>' prompt
│   │   ├── Elm/ElmClient.cs                  # AT command layer: SendCommandAsync/SendCommandUntilOkAsync
│   │   ├── Models/                           # ConnectionState, PidReading, DtcActionResult
│   │   └── Kwp/
│   │       ├── Protocol.cs                   # Service 0x22 request/response hex encode-decode
│   │       ├── Dtc.cs                        # §3.7 DTC scan/clear request/response hex encode-decode
│   │       ├── KnownPids.cs, PidDefinition.cs # the §3.4 table: id, name, unit, formula
│   │       ├── PidReader.cs                  # send a known PID request, parse response, apply formula
│   │       └── KwpSession.cs                 # connection state machine, init sequence (§3.2),
│   │                                         #   keep-alive/recovery (§3.3), poll loop, DTC scan/clear
│   ├── ProtonEcuToolkit.Core.Tests/          # xUnit - ports the original vitest suite 1:1
│   ├── ProtonEcuToolkit.HardwareProbe/       # console smoke-test harness for manual hardware checks
│   └── ProtonEcuToolkit.App/                 # WPF, MVVM
│       ├── ViewModels/                       # MainViewModel (owns the KwpSession), PidGaugeViewModel
│       ├── Gauges/                           # GaugeMath/GaugeConfig (ported) + DialGaugeControl/DigitalGaugeControl
│       └── Views/                            # ConnectionPanelView, GaugePanelView, DtcPanelView
├── src/server/, web/                          # original Node/TS + React prototype (§4.1) - reference only
└── data/
    └── scan-results/                          # CSV/JSONL output from scanner runs (Phase 2, not yet built)
```

Layering rule, carried over unchanged from the TS version: `Transport` knows nothing about AT commands; `Elm` knows nothing about KWP2000; `Kwp` (including `KwpSession`) knows nothing about WPF or any other UI framework - it only raises plain C# events. Each layer only talks to the one below it. This is what made the rewrite itself low-risk (the Core layers ported almost mechanically) and is what will make DTC code-decoding and Phase 2/4 features pure additions later, not rewrites.

## 6. PID/CID discovery scanner — design

**Why the original app's approach is too slow for this**: its read loop waits up to 1000ms before declaring "no data." Scanning a wide identifier range at 1s/candidate is impractical (65536 candidates × 1s ≈ 18 hours).

Plan:
1. **Tune the timeout down.** A KWP2000 ECU normally returns a negative response (`7F`) for an unsupported identifier quickly (tens of ms), it doesn't usually stay silent for a full second. Start with a ~200–300ms read timeout for scanning; only fall back to longer timeouts if you're seeing too many false "no response" results on IDs you already know are real (the 5 known PIDs + the §3.5 candidates) as a calibration check.
2. **Scan order, cheapest/highest-value first:**
   - Step A: directly request the 7 known-but-unverified candidates from §3.5 (`1147,1148,1149,11CC,11CD,11CE,11CF`) under both SID `0x22` and SID `0x21`. 14 requests, seconds to run, immediately confirms/refutes the "everything is SID 0x22" hypothesis.
   - Step B: scan `0x1000`–`0x12FF` (768 candidates) under SID `0x22` — this is the neighborhood where every confirmed-real ID lives. At ~250ms/candidate that's ~3-4 minutes.
   - Step C: widen to `0x0000`–`0x1FFF` if B didn't already start looking sparse/done, interleaving a keep-alive request every ~2-3 seconds of scanning (see §3.3 — don't let the session drop mid-scan).
   - Step D (optional/stretch): also brute-force SID `0x21` (1-byte LID, only 256 candidates, cheap) across the same idea.
3. **Classify every response** (`ResponseClassifier.ts`): positive (`62` + CID echo + N data bytes — record N, the raw bytes, and a guessed scaling later), negative (`7F` + service + NRC byte — record the NRC, useful since NRCs like "request out of range" vs "conditions not correct" mean different things), no-data/timeout, or malformed.
4. **Log everything**, not just hits — a JSONL file with `{timestamp, sid, id, requestHex, responseHex, classification, latencyMs}` per attempt. This is your raw evidence trail; the "PID database" is a filtered view of it (positive responses only), but keep the full log for re-analysis later (e.g. you might want to re-classify NRCs once you understand the ECU's NRC dialect better).
5. **UI**: a simple panel — start/stop scan, range picker, live progress bar + running count of hits, and a results table (ID, byte count, raw hex, latency) you can export to CSV. Nothing fancy needed for v1.

## 7. Phased roadmap

- **Phase 0 — proof of life**: open the right COM port with `serialport`, send `ATZ`, confirm you get something back. This just proves the transport layer works before any protocol logic.
- **Phase 1 — replicate the app**: full init sequence (§3.2) + keep-alive (§3.3) + read the 5 known PIDs (§3.4) on a timer, shown in a basic live dashboard. Acceptance: you can watch RPM/coolant/etc. update in the browser while the engine runs (or with key on, engine off, for sensors that work without cranking).
- **Phase 2 — the actual goal: PID/CID scanner** (§6). Acceptance: a CSV/JSONL of every identifier the ECU responded positively to, with raw bytes, ready for manual formula-reverse-engineering (watch a value while changing something physically — e.g. rev the engine, open throttle, switch on A/C — and see which newly-discovered ID's bytes move in response).
- **Phase 3 — polish the dashboard**: gauges for confirmed PIDs, a simple time-series graph/log, CSV export of live sessions.
- **Phase 4 — stretch**: DTC read/erase UI (§3.7), and the fan/injector actuator-test panel (§3.6) — with proper response-checking added (the original app doesn't check, you should).

## 8. Things to verify first in the new session (don't assume, test)

- [ ] Confirm `ATSH8101F1` actually gets real PID data back on your hardware (not just `OK` on the AT command) — this is the fix for your original `NO DATA` problem.
- [ ] Confirm the `+0x57` offset hypothesis for the 5 known PIDs by checking the byte values make physical sense (e.g. `221101` while engine is cold should read a low coolant temp after applying `byteA - 60`).
- [ ] Confirm whether §3.5's 7 leftover identifiers respond under SID `0x22`, SID `0x21`, both, or neither.
- [ ] Confirm how short you can push the scan timeout before false negatives start appearing (calibrate against the known-good IDs).
- [ ] Confirm the keep-alive cadence needed in practice — the app re-pings on every Torque PID_QUERY cycle, but we don't know the ECU's actual P3 timeout value; find it empirically (increase the gap between requests until the session drops, that's your ceiling — stay well under it).

## 9. Setup notes

- Find the COM port: Device Manager → Ports (COM & LPT) (look for the Bluetooth SPP/outgoing port for your ELM327's paired name), or enumerate via `System.IO.Ports.SerialPort.GetPortNames()`. On this hardware that's COM4 - COM3 also shows up but is the generic Bluetooth "incoming" port, not the one bound to the adapter.
- Scaffold (current, C#): `dotnet new sln/classlib/xunit/console/wpf`, `System.IO.Ports` and `CommunityToolkit.Mvvm` NuGet packages. See `desktop/`.
- No checksum/header math needed anywhere in the code (§3.1) — keep the protocol layer to plain ASCII hex string building/parsing.
- Superseded (original TS prototype, kept for reference): `npm init`, `typescript`, `@types/node`, `serialport`, `express`, `ws`, `vite`, `vitest`.

## 10. Safety notes

- The fan/injector actuator commands (§3.6) are session-bound (revert automatically), but cutting an injector live **will** cause a real misfire/rough running while active — bench/idle testing only, expect the MIL to light, don't do this while driving.
- This is your own vehicle for personal diagnostic/RE purposes — no concerns there, just listing the practical risk of the actuator feature so it's not a surprise.

## 11. Quick-reference command table (everything decoded, all in one place)

| Purpose | Command | Expect |
|---|---|---|
| Reset | `ATZ` | (anything) |
| Echo off | `ATE0` | `OK` |
| Headers off in responses | `ATH0` | `OK` |
| Protocol = KWP fast init | `ATSP5` | `OK` |
| Set header (target=0x01!) | `ATSH8101F1` | `OK` |
| Force fast init (re-wake) | `ATFI` | `OK` |
| Connectivity test ping | `22111F` | contains `62` |
| Coolant temp | `221101` | `62 1101 <A>` → A−60 = °C |
| RPM | `221104` | `62 1104 <A><B>` → B×255+A = rpm |
| TPS | `22110A` | `62 110A <A>` → A×0.39216 = % |
| Battery voltage | `221110` | `62 1110 <A>` → A×0.078431 = V |
| Vehicle speed | `221113` | `62 1113 <A>` → A×1.2 = km/h |
| Start extended session (for IO control) | `1083` | `50` (or `7F` = retry) |
| Stop diagnostic session | `20` | — |
| All injectors ON | `30420700` | `70` ideally (app doesn't check) |
| All injectors OFF | `30420701` | `70` |
| Injector 1/2/3/4 OFF | `30420702` / `04` / `08` / `10` | `70` |
| Fan OFF / ON | `304A0700` / `304A0701` | `70` |
| Scan DTCs | `18020000` | contains `58` |
| Erase DTCs | `140000` | contains `54` |
| Unverified, test these (§3.5) | `221147`/`221148`/`221149`/`2211CC`/`2211CD`/`2211CE`/`2211CF` (and SID `0x21` variants) | TBD |

---
*Source: decompiled `ProtonOBDFree` Torque plugin APK at `c:\Users\User\Downloads\ProtonOBDFree.apk_Decompiler.com\`. Key files referenced: `OBDadapterService.java`, `PluginActivity.java`, `PluginReceiver1.java`, `J/a.java`, `extraFunction.java`, `pidlist2b.csv`, `diagstatus1.csv`, `signalstatus.csv`, `strings.xml`.*
