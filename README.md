# Proton ECU Toolkit

A native Windows desktop diagnostic tool for a Proton Waja CPS, talking to its Siemens EMS700 ECU over a Bluetooth ELM327 adapter. Reads live engine data, scans/clears diagnostic trouble codes, and (eventually) brute-forces the ECU's full identifier space to find PIDs beyond the handful the factory diagnostic app exposes.

Built from scratch by reverse-engineering the OEM Android diagnostic app's decompiled source, then verifying every byte sequence and formula against the real car. This file is the single source of truth for the project - status, architecture, the full protocol reference, and the roadmap all live here.

## Vehicle & hardware

| | |
|---|---|
| Vehicle | Proton Waja CPS |
| Engine | Campro CPS |
| ECU | Siemens EMS700 |
| Protocol | KWP2000 / ISO 14230-4, Fast Init, 10.4 kbps (K-line) |
| Adapter | ELM327 clone (PIC18F25K80), Bluetooth Classic SPP |

Important distinction: the Bluetooth link between PC and ELM327 is just a serial transport (whatever baud the OS/COM port negotiates - irrelevant to the protocol). The 10.4 kbps figure is the K-line speed between the ELM327 and the ECU, handled entirely inside the adapter once it's told to use protocol 5 (`ATSP5`) - that speed is never touched directly.

The adapter pairs with Windows as a normal serial COM port - no Bluetooth-specific code needed anywhere in this project, it's just a COM port like any USB-serial device once paired. On the development machine this is **COM4**; pairing also creates a generic **COM3** "incoming" port (recognizable by an all-zero placeholder MAC in its `pnpId`) that isn't the one to use.

## Project status (as of 2026-06-20)

- **Phase 0 - proof of life**: done and hardware-validated. Opening the COM port and sending a bare `ATZ` returns `ELM327 v1.5`.
- **Phase 1 - replicate the OEM app**: done and hardware-validated, live, with the engine running. The full KWP2000 init sequence, the keep-alive/recovery pattern, and all 5 known PIDs were confirmed against the real ECU - decoded values were physically sane (coolant temp rising as the engine warmed, RPM at a believable idle, battery voltage jumping from 11.8V to 14V+ once the alternator started charging, matching the ELM327's own `ATRV` voltage sense as an independent cross-check).
- **Native desktop rewrite (C#/.NET WPF)**: code-complete. Every protocol fact proven in Phase 0/1 was ported faithfully from the original TypeScript prototype to C#, byte-for-byte. Verified via 25/25 passing unit tests (ported 1:1 from the original suite, same inputs/outputs) and the app launches cleanly with zero binding/runtime errors, but **the WPF app itself has not yet been run against the real ECU** - only its TypeScript predecessor has.
- **Custom dashboard**: each gauge tile is fully user-configurable - a gear icon opens a small editor to pick which PID it shows, its min/max, and an optional redline (e.g. RPM redline, a coolant overheat threshold). Gauges can be freely added and removed, each with its own dial/digital theme, and the layout persists across launches (`%AppData%\ProtonEcuToolkit\dashboard.json`).
- **DTC scan/clear**: wired up end-to-end, but only shows raw, undecoded hex. The OEM app's decompile documents the request bytes and the positive-response marker, never the actual fault-code byte layout - that needs a real ECU response with a stored code to reverse-engineer.
- **PID/CID scanner (Phase 2)**: not started. This is the actual long-term goal - discovering which other identifiers this ECU supports beyond the 5 hardcoded ones.
- **Actuator test panel (Phase 4)**: not started (fan/injector toggle via IO control).

## Architecture

**Current: C#/.NET 8, WPF, MVVM** (`desktop/`). Chosen because the requirement is a genuinely native desktop app - no web rendering technology anywhere, not even invisibly packaged (which rules out Electron/Tauri, since both still render the UI with HTML/CSS under the hood), usable fully offline, distributed as a single self-contained `.exe`. Mobile was considered and explicitly deferred; if it happens later, .NET MAUI can share the C# `Core` class library's business logic with this desktop app.

```
proton-ecu-toolkit/
├── README.md                                 # this file
├── desktop/                                   # current development - C#/.NET WPF app
│   ├── ProtonEcuToolkit.sln
│   ├── ProtonEcuToolkit.Core/                 # protocol engine, no UI dependency
│   │   ├── Transport/SerialTransport.cs       # raw COM port framing, buffers to the ELM327 '>' prompt
│   │   ├── Elm/ElmClient.cs                   # AT command layer (echo stripping, retry-until-OK)
│   │   ├── Models/                            # ConnectionState, PidReading, DtcActionResult
│   │   └── Kwp/
│   │       ├── Protocol.cs                    # Service 0x22 request/response hex encode-decode
│   │       ├── Dtc.cs                         # DTC scan/clear hex encode-decode
│   │       ├── KnownPids.cs, PidDefinition.cs # the 5 known PIDs: id, name, unit, formula
│   │       ├── PidReader.cs                   # send + parse + decode a known PID
│   │       └── KwpSession.cs                  # connection state machine, init sequence, keep-alive, poll loop
│   ├── ProtonEcuToolkit.Core.Tests/           # xUnit, ports the original vitest suite 1:1
│   ├── ProtonEcuToolkit.HardwareProbe/        # console harness for manual hardware checks
│   └── ProtonEcuToolkit.App/                  # WPF UI (MVVM via CommunityToolkit.Mvvm)
│       ├── ViewModels/                        # MainViewModel owns one KwpSession in-process - no server, no IPC
│       ├── Gauges/                            # custom dashboard: per-gauge PID/min/max/redline/theme,
│       │                                       #   persisted to %AppData%\ProtonEcuToolkit\dashboard.json
│       └── Views/                             # connection panel, gauge panel, DTC panel
├── src/server/, web/                          # original Node/TS + React prototype - reference only, see below
└── data/scan-results/                          # CSV/JSONL output from scanner runs (Phase 2, not yet built)
```

The layering is what made the rewrite low-risk: `Transport` knows nothing about AT commands, `Elm` knows nothing about KWP2000, and `Kwp` (including `KwpSession`) knows nothing about WPF or any UI framework - it only raises plain C# events. Each layer only talks to the one below it. This is also what will make DTC code-decoding and the Phase 2/4 features pure additions later, not rewrites.

**Legacy (kept as a validated reference, not deleted, not under active development): Node.js/TypeScript + Express/WebSocket backend + Vite/React frontend**, in `src/` and `web/`. This is where Phase 0 and Phase 1 were originally built and proven against real hardware, before the native-desktop requirement became explicit and the engine was ported to C#. It used the same `serialport`-talks-to-the-paired-COM-port approach and the same transport/elm/kwp layering.

## The reverse-engineered protocol

Everything below was extracted from the decompiled "ProtonOBDFree" Torque plugin APK (`com.obd.saukintelli.protonobdpro2` / `obd.saukintelli.com.x10obd`). The app hid every AT/KWP string behind a trivial cipher (ROT13 on letters, ROT5 on digits - self-inverse) in `strings.xml`; everything here is already decoded. See [Source / provenance](#source--provenance) for the exact decompiled files each fact came from. Every fact in this section has since been independently confirmed against the real ECU (not just decompile theory) - see [Project status](#project-status-as-of-2026-06-20).

### Response framing - no manual checksum/header math needed

The app sets `ATH0` (headers off in responses). That means:
- **You send**: just the KWP2000 service+data bytes as an ASCII hex string (e.g. `22111F`). The ELM327 prepends the header (`81 01 F1`) and appends the checksum on the wire automatically, because the header was set once via `ATSH`.
- **You receive**: just the response data bytes as ASCII hex (e.g. `62111F00`), no header, no checksum. The chip strips both before showing the response.
- Conclusion: the protocol layer never needs real ISO14230 checksum/header byte-math. Just build/parse plain hex strings.

### Cold-start init sequence

```
ATZ          x2   (reset; the original app doesn't even check the response)
ATE0         retry until response contains "OK"   (echo off)
ATH0         retry until "OK"                       (headers off in responses)
ATSP5        retry until "OK"   -> protocol = ISO14230-4 KWP, FAST INIT, 10.4kbps
ATSH8101F1   retry until "OK"   -> header = 81 01 F1
[~2 second settle delay]
22111F                          -> test ping (Service 0x22 ReadDataByCommonIdentifier, CID 0x111F)
                                   success = response contains "62" (0x22 + 0x40 = positive response SID)
```

The critical, easy-to-get-wrong detail: the real ECU listens on target **`0x01`**, not the textbook default `0x10`. `BUS INIT: OK` doesn't depend on header bytes (it's just a wake-up pulse timing pattern), which is why init can look fine while every PID request silently fails if the header is wrong - confirmed live: with the wrong target, the test ping comes back `BUS INIT: ERROR`; with `0x01`, it comes back `BUS INIT: OK` followed by real data.

### Keep-alive / re-sync

KWP2000 sessions time out if the bus goes quiet too long (P3 timeout). The app's pattern:
- Every time it wants fresh PID data, it first sends the test ping `22111F` again.
- If that fails, it sends `ATFI` (**Force Fast Init** - re-triggers the wake-up pulse without re-sending `ATSP`/`ATSH`) and tries again.
- If that still fails, it fully restarts: `ATSP5` -> `ATSH8101F1` -> ping again.

This toolkit keeps a background "tester present" cadence by polling at 1-second intervals while connected (well under the 2-4 second ceiling implied by the app's behavior) - if a poll cycle gets no response from any of the 5 PIDs, the recovery ladder above kicks in automatically.

### The 5 known-working PIDs - and a second hidden cipher

`pidlist2b.csv` (bundled in the APK) stores Mode+PID as `2210AA`, `2210AD`, `2210B3`, `2210B9`, `2210BC` - **these are NOT the real wire values**. The actual polling engine adds **0x57 (87 decimal)** to the 2-byte identifier portion before sending. Verified real requests:

| Send (Service 0x22) | PID | Formula (byteA = 1st data byte, byteB = 2nd) | Unit |
|---|---|---|---|
| `221101` | Coolant temp | `byteA − 60` | °C |
| `221104` | RPM | `byteB×255 + byteA` | rpm |
| `22110A` | TPS | `byteA × 0.39216` (≈ byteA/2.55) | % |
| `221110` | Battery voltage | `byteA × 0.078431` (≈ byteA/12.75) | V |
| `221113` | Vehicle speed | `byteA × 1.2` | km/h |

Response shape: `62 <2-byte CID echo> <byteA> [byteB]`. E.g. requesting `221101` gives back something like `6211013E` → CID echoed as `1101`, byteA = `0x3E` → 0x3E (62 decimal) − 60 = 2°C.

The +0x57 offset is corroborated by the resulting real IDs (`0x1101`, `0x1104`, `0x110A`, `0x1110`, `0x1113`) clustering tightly with two other confirmed-real identifiers: the test ping `0x111F`, and the candidate groups below - **and** by every decoded value matching physical reality live (coolant warming up, RPM at a sane idle, battery voltage tracking the alternator's charging state).

### Likely-real extra identifiers (high-value scan targets for Phase 2)

Two CSVs ship in the APK (`diagstatus1.csv`, `signalstatus.csv`) but are **not wired into any code path** in this Free build - almost certainly Pro-only leftovers, but they tell us real identifiers the ECU likely supports:

- `1147`, `1148`, `1149` - digital I/O / signal status bytes (idle switch, full load, gear position, A/C request, VIM/CPS position, clutch switch, cam control, knock control, tank purge valve, lambda controllers, cat heating, fuel cutoff, etc. - each is a bitfield, one bit per row in the CSV)
- `11CC`, `11CD`, `11CE`, `11CF` - onboard diagnostic test status bitfields (catalyst, lambda probe, EGR, misfire, knock sensor check, etc.)

Hypothesis to test first when the scanner is built (cheap, high value, before any brute force): these are very likely **also Service 0x22 CIDs**, same family as the 5 known PIDs (e.g. send `2211CC`, `221147`, etc.) - everything else in the app exclusively uses SID `0x22` for reads; SID `0x21` (ReadDataByLocalIdentifier) was never observed anywhere in the decompiled code. Still worth verifying both `0x22` and `0x21` against these 7 specific candidates before trusting the hypothesis - **this hasn't been tested yet**.

### Diagnostic session + actuator test commands (Phase 4 material)

From the decompile - a "kill a cylinder / toggle the fan" feature:

```
1083   -> StartDiagnosticSession, sub-function 0x83 (must be sent before IO control works)
            response "7F" = negative (retry up to 5x), "50" = positive (success)
20     -> StopDiagnosticSession (sent automatically when the original app's screen closes)
```

IO control commands (Service `0x30` = InputOutputControlByLocalIdentifier, control parameter `0x07` = **Short Term Adjustment** - i.e. these are session-bound, non-permanent overrides that revert on session end/ECU reset, not EEPROM writes):

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

The original app never reads back the response to these (fire-and-forget) - this toolkit should check for `70` (positive, 0x30+0x40) vs `7F` (negative) instead, once this feature gets built. See [Safety notes](#safety-notes) before ever sending these to the real car.

### DTC scan/erase

```
18020000   -> ReadDiagnosticTroubleCodesByStatus  (expect positive resp containing "58")
140000     -> ClearDiagnosticInformation            (expect positive resp containing "54")
```

Only the request bytes and the positive-response marker are documented from the decompile - the actual fault-code byte layout (how many bytes per code, status byte format) is unknown, so codes are surfaced as raw hex pending a real stored-code capture to reverse-engineer against.

## PID/CID discovery scanner - design (Phase 2)

This is the actual long-term goal of the project: systematically discovering which identifiers this ECU supports beyond the 5 hardcoded ones, beyond what the OEM app ever exposed.

The OEM app's approach is too slow for this directly - its read loop waits up to 1000ms before declaring "no data." Scanning a wide identifier range at 1s/candidate is impractical (65536 candidates × 1s ≈ 18 hours). Plan:

1. **Tune the timeout down.** A KWP2000 ECU normally returns a negative response (`7F`) for an unsupported identifier quickly (tens of ms) - it doesn't usually stay silent for a full second. Start with a ~200-300ms read timeout for scanning; only fall back to longer timeouts if false "no response" results start appearing on IDs already known to be real (the 5 known PIDs + the candidates above), as a calibration check.
2. **Scan order, cheapest/highest-value first:**
   - Step A: directly request the 7 known-but-unverified candidates (`1147,1148,1149,11CC,11CD,11CE,11CF`) under both SID `0x22` and SID `0x21`. 14 requests, seconds to run, immediately confirms/refutes the "everything is SID 0x22" hypothesis.
   - Step B: scan `0x1000`-`0x12FF` (768 candidates) under SID `0x22` - the neighborhood where every confirmed-real ID lives. At ~250ms/candidate that's ~3-4 minutes.
   - Step C: widen to `0x0000`-`0x1FFF` if B didn't already start looking sparse/done, interleaving a keep-alive request every ~2-3 seconds of scanning so the session doesn't drop mid-scan.
   - Step D (optional/stretch): also brute-force SID `0x21` (1-byte LID, only 256 candidates, cheap).
3. **Classify every response**: positive (`62` + CID echo + N data bytes - record N, the raw bytes, and a guessed scaling later), negative (`7F` + service + NRC byte - record the NRC, since NRCs like "request out of range" vs "conditions not correct" mean different things), no-data/timeout, or malformed.
4. **Log everything**, not just hits - a JSONL file with `{timestamp, sid, id, requestHex, responseHex, classification, latencyMs}` per attempt. This is the raw evidence trail; the "PID database" is a filtered view of it (positive responses only), but the full log stays useful for re-analysis later (e.g. re-classifying NRCs once the ECU's NRC dialect is better understood).
5. **UI**: a simple panel - start/stop scan, range picker, live progress bar + running count of hits, and a results table (ID, byte count, raw hex, latency) exportable to CSV. Nothing fancy needed for v1.

Once a candidate is confirmed positive, the actual formula behind its bytes still needs manual reverse-engineering: watch the raw bytes while changing something physically (rev the engine, open the throttle, switch on A/C) and see which newly-discovered ID's bytes move in response.

## Building and running

Requires the .NET 8 SDK.

```
cd desktop
dotnet build                                    # build everything
dotnet test ProtonEcuToolkit.Core.Tests         # run the unit tests
dotnet run --project ProtonEcuToolkit.App       # run the WPF app
```

To produce a standalone executable (no .NET install required on the target machine):

```
dotnet publish ProtonEcuToolkit.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

For ad-hoc hardware checks without the full UI:

```
dotnet run --project ProtonEcuToolkit.HardwareProbe -- COM4
```

Find the COM port via Device Manager → Ports (COM & LPT) (look for the Bluetooth SPP/outgoing port for the ELM327's paired name), or enumerate via `System.IO.Ports.SerialPort.GetPortNames()` in code.

## Roadmap

1. ~~Phase 0 - proof of life~~ - done, hardware-validated.
2. ~~Phase 1 - replicate the OEM app~~ (init sequence, keep-alive, 5 known PIDs, live dashboard) - done, hardware-validated.
3. Native desktop rewrite (C#/.NET WPF) - code complete, pending its own hardware validation pass.
4. **Phase 2 - PID/CID scanner**: the actual goal. Brute-force the ECU's identifier space, classify every response, log everything to CSV/JSONL as the raw evidence trail for manually reverse-engineering new PID formulas.
5. Phase 3 - dashboard polish (gauges already built ahead of schedule; CSV export of live sessions still to do).
6. Phase 4 - decode the DTC byte format once a real stored code is captured; actuator test panel (fan/injector toggle via IO control), with proper response-checking added (the original app doesn't check, this one should).

## What's still unverified

- Whether the §3.5-equivalent 7 leftover identifiers respond under SID `0x22`, SID `0x21`, both, or neither - untested, first thing to check once the scanner exists.
- How short the scan timeout can be pushed before false negatives appear - needs calibration against the known-good IDs once scanning starts.
- The ECU's actual P3 timeout ceiling - the 1-second poll interval is comfortably under the app's implied 2-4 second cadence, but the real ceiling hasn't been measured empirically (increase the gap between requests until the session drops - that's the ceiling, stay well under it).
- The native C# port's behavior against real hardware - logic has been verified to match the proven TypeScript version exactly via unit tests, but the WPF app itself hasn't had its own hardware test yet.

Already resolved (kept here for the record): `ATSH8101F1` does get real PID data back (not just `OK`) - confirmed live. The `+0x57` offset hypothesis is confirmed correct - decoded values are physically sane and track real engine behavior in real time.

## Safety notes

This is a personal vehicle used for diagnostic/reverse-engineering purposes - no concerns there. The one practical risk worth flagging for later: the actuator IO-control commands (cutting an injector, toggling the fan, §"Diagnostic session + actuator test commands" above) are session-bound and revert automatically, but cutting an injector live **will** cause a real misfire/rough running while active. Bench/idle testing only, expect the MIL to light, never while driving.

## Quick-reference command table

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
| Unverified, test these | `221147`/`221148`/`221149`/`2211CC`/`2211CD`/`2211CE`/`2211CF` (and SID `0x21` variants) | TBD |

## Source / provenance

All protocol facts above were extracted from the decompiled "ProtonOBDFree" Torque plugin APK (`com.obd.saukintelli.protonobdpro2` / `obd.saukintelli.com.x10obd`). Key decompiled files referenced: `OBDadapterService.java` (init sequence), `PluginActivity.java`, `PluginReceiver1.java` / `PluginService` (keep-alive), `J/a.java` (the +0x57 PID offset), `extraFunction.java` (actuator commands), `pidlist2b.csv`, `diagstatus1.csv`, `signalstatus.csv`, `strings.xml` (the ROT13/ROT5 cipher hiding every AT/KWP string).
