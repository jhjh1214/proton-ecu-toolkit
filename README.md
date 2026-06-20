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

## Project status (as of 2026-06-21)

- **Phase 0 - proof of life**: done and hardware-validated. Opening the COM port and sending a bare `ATZ` returns `ELM327 v1.5`.
- **Phase 1 - replicate the OEM app**: done and hardware-validated, live, with the engine running. The full KWP2000 init sequence, the keep-alive/recovery pattern, and all 5 known PIDs were confirmed against the real ECU - decoded values were physically sane (coolant temp rising as the engine warmed, RPM at a believable idle, battery voltage jumping from 11.8V to 14V+ once the alternator started charging, matching the ELM327's own `ATRV` voltage sense as an independent cross-check).
- **Native desktop rewrite (C#/.NET WPF)**: done and hardware-validated, live, with the engine running - same as the TypeScript predecessor was. The WPF app connects to the real ECU (COM4), completes the full init sequence, and decodes all 5 PIDs correctly (coolant 69°C, RPM 808 at warm idle - lower than the 1000-1050 cold idle seen earlier, exactly the expected warm-up behavior, battery 13.7V charging, speed 0). DTC scan also confirmed working against the real ECU (`58` positive response). 25/25 ported unit tests pass, and the app launches with zero binding/runtime errors.
- **Custom dashboard**: each gauge tile is fully user-configurable - a gear icon opens a small editor to pick which PID it shows, its min/max, and an optional redline (e.g. RPM redline, a coolant overheat threshold). Gauges can be freely added and removed, each with its own dial/digital theme, and the layout persists across launches (`%AppData%\ProtonEcuToolkit\dashboard.json`).
- **Gauge visuals**: proper analog instrument faces (270-degree sweep, major/minor tick marks with numbers, a tapered needle, a red danger arc near the redline) and a digital-cluster-style display (segmented bar that lights up and turns red past the redline, bold numeral readout).
- **DTC scan/clear**: wired up end-to-end and confirmed working against the real ECU, but only shows raw, undecoded hex. The OEM app's decompile documents the request bytes and the positive-response marker, never the actual fault-code byte layout - that needs a real stored DTC to reverse-engineer (the car currently has none).
- **PID/CID scanner (Phase 2)**: built and run against real hardware across 3 sessions (2026-06-20 and 2026-06-21) - this is the actual long-term goal. Known candidates: 7/7 positive every time. Nearby range `0x1000`-`0x12FF`: 768/768 positive every time - this ECU treats that whole block as a memory-mapped read window, not a validated identifier list. 253 of 768 candidates (33%) show a value that changed across the 3 captures, including a correction to the first session's filler classification - see "PID/CID discovery scanner" below. Two new signals fully decoded and verified against the real car: `1147` bit 4 (A/C request, confirmed both ON and OFF) and `1148` bit 5 (tank purge valve, confirmed live/changing over time). The "Wide range" tier (`0x0000`-`0x1FFF`, 8192 candidates) hasn't been run yet.
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
│   │   └── Scanning/                          # PidScanner, ResponseClassifier, ScanPlans - Service 0x22 only
│   ├── ProtonEcuToolkit.Core.Tests/           # xUnit, ports the original vitest suite 1:1
│   ├── ProtonEcuToolkit.HardwareProbe/        # console harness for manual hardware checks
│   └── ProtonEcuToolkit.App/                  # WPF UI (MVVM via CommunityToolkit.Mvvm)
│       ├── ViewModels/                        # MainViewModel owns one KwpSession in-process - no server, no IPC
│       ├── Gauges/                            # custom dashboard: per-gauge PID/min/max/redline/theme,
│       │                                       #   persisted to %AppData%\ProtonEcuToolkit\dashboard.json
│       ├── Scanning/                          # ScannerViewModel, JSONL log writer
│       └── Views/                             # connection panel, gauge panel, DTC panel, scanner panel
├── src/server/, web/                          # original Node/TS + React prototype - reference only, see below
└── data/scan-results/                          # superseded - scan output now lives in %AppData%\ProtonEcuToolkit\scan-results\
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

### Extra identifiers from leftover CSVs - confirmed real, partly decoded (Phase 2 result)

Two CSVs ship in the APK (`diagstatus1.csv`, `signalstatus.csv`) but are **not wired into any code path** in this Free build - almost certainly Pro-only leftovers. **Confirmed on real hardware, 2026-06-20: all 7 respond positively under Service 0x22, exactly like the 5 known PIDs** (no +0x57 offset needed - these are already the wire-format CIDs). Each is a 2-byte bitfield, one bit per row in its CSV, decoded directly from the original CSV content (`resources/res/raw/diagstatus1.csv` and `signalstatus.csv` in the decompile):

| CID | Bit | Signal | 0 | 1 |
|---|---|---|---|---|
| `1147` | 0 | LLK (idle contact) | opened | closed |
| `1147` | 1 | Full Load | not active | active |
| `1147` | 2 | Gear lever position | D | P/N |
| `1147` | 4 | **Air conditioning request** | OFF | **ON** |
| `1147` | 5 | VIM position (Variable Intake Manifold, ~4800 rpm switch point) | OFF | ON |
| `1147` | 6 | CPS position (Camshaft Profile Switching, ~3800 rpm switch point) | OFF | ON |
| `1147` | 7 | Clutch Switch | not declutched | declutched |
| `1148` | 1 | Camshaft control active | OFF | active |
| `1148` | 2 | Intake manifold control valve | OFF | active |
| `1148` | 3 | Knock control status | forbidden | allowed |
| `1148` | 5 | **Tank purge valve** | OFF | **active** |
| `1148` | 6 | Lambda controller 1 | OFF | active |
| `1148` | 7 | Lambda controller 2 | OFF | active |
| `1149` | 0 | End of line mode | OFF | ON |
| `1149` | 1 | Cat heating idle (ignition) | OFF | active |
| `1149` | 2 | Poststart (injection) | OFF | active |
| `1149` | 3 | Fuel cut off (SAS) | OFF | active |
| `1149` | 4 | Safety fuel shut off | OFF | active |
| `1149` | 5 | TBA adaptation | not finished | finished |
| `11CC`-`11CF` | various | onboard diagnostic test status (catalyst, lambda probe, EGR, misfire, knock check, etc.) | "Diagnosis performed"/"in progress" pair per bit - **bit polarity not yet confirmed**, see below | |

The bitfield lives in byte A (the first data byte after the CID echo). Byte B isn't an independent unknown signal - see the memory-mapping discovery below, it's the *next* identifier's byte A.

**Two bits independently verified against the real car, not just read from the CSV:**
- **`1147` bit 4 (A/C request)** - confirmed in both directions. Captured `0x15` (bit 4 set) while A/C was on; asked the user to turn it off, re-scanned, got `0x05` (bit 4 cleared). Flipped exactly as predicted, twice.
- **`1148` bit 5 (Tank purge valve)** - confirmed live/changing. Two scans ~10 minutes apart while idling: `0x40` -> `0x60`, exactly bit 5 turning on - consistent with EVAP purge typically activating once the engine's been running long enough to enter closed-loop control (Lambda controller 1, bit 6, was already active in both readings).

**`1147` bit 7 ("Clutch Switch") is probably not the transmission clutch pedal**, despite the CSV's label. Tested twice (clutch pedal held down, including a retry with extra buffer time to rule out timing lag) - the bit never moved. Current best hypothesis: it's actually the **A/C compressor clutch** (the electromagnetic clutch that engages the compressor), not the transmission clutch - the original CSV label is ambiguous about which "clutch" it means, and the data is at least as consistent with that reading: every capture so far had the compressor either off or not yet engaged. Testing this directly (A/C genuinely requesting cooling, compressor prevented from physically engaging) is a planned next step.

**Planned next test: `1147` bits 5/6 (VIM/CPS) via a stationary high-RPM rev**, not yet performed. Target thresholds confirmed by the user from direct experience with this car: **CPS switches at ~3800 rpm, VIM at ~4800 rpm**. Plan: car in Park/Neutral, handbrake on, foot on the brake, rev smoothly past both thresholds while watching `1147` byte A - expect bit 6 to flip first (~3800 rpm) and bit 5 second (~4800 rpm). If the bits don't flip even at a clean stationary high rev, that points to a load-dependent trigger (needs actual driving, not just RPM) - a harder problem since live-scanning while driving isn't safe to coordinate the way the stationary tests have been.

### Memory-mapping discovery: the CID is a sliding 2-byte window, not an independent value

Found by re-analyzing already-collected scan data, no new hardware access needed. **Byte B of CID `N` equals byte A of CID `N+1`** within a real memory block - meaning two adjacent CIDs aren't two separate readings, they're an overlapping view into the same underlying bytes. Confirmed two ways:

- **`1147`-`1148`-`1149`** (the signalstatus.csv group): `1147`'s byte B always equals `1148`'s byte A, and `1148`'s byte B always equals `1149`'s byte A, across all 3 captures taken so far (different sessions, different car states) - and the chain breaks cleanly on both sides (`1146` doesn't connect in, `114A` doesn't connect out). The true underlying data is a **4-byte block** (`05 40 20 72` in the most recent capture), not 6 redundant bytes from 3 separate 2-byte reads.
- **`11CC`-`11CF`** (the diagstatus1.csv group): same pattern, `00 02 0F 80` as one continuous 4-byte block, chain breaks cleanly before `11CC`. **Bonus finding: the block actually extends one byte further than the CSV documented** - `11D0` (never mentioned in the original decompile) genuinely continues the same chain, making the real block 5 bytes (`00 02 0F 80 00`), not 4.

Both block boundaries land exactly where the original leftover CSVs grouped identifiers together - strong, independent confirmation that the CSV's groupings reflect real, physically contiguous memory, and that the CID literally addresses into that memory rather than being a validated, independent signal ID.

**Scanning the rest of the nearby range for the same pattern found 24 more non-trivial chains** (3-12 IDs long) scattered through `0x1000`-`0x12FF`, each presumably marking a real, distinct sub-structure with its own boundary - e.g. `1059`-`1064` (`01 01 35 67 00 00 00`), `1088`-`1092` (a single byte `95` followed by 11 bytes of zero), `1164`-`1166` (`11 22 33 B7` - the `11,22,33` looks like it could be a firmware test/canary pattern, unconfirmed). Full list in `data/scan-results/sliding-window-chains-2026-06-21.json`.

**Two of those chains contain the exact same byte sequence at two different addresses 26 bytes apart** (`08 E0 80 01` at both `1059`-`1064`'s start and `1073`-`1076`) - the same kind of aliasing already seen with the `5532`/`5512` value, now confirmed in unrelated content too. A few other short sequences (`93 00 04`, `80 00 40`, `80 80 80`) also repeat at exactly **0x29 (41) bytes apart** from each other, hinting at a possible periodic/tiled sub-structure - but checking this systematically across the *entire* range only matched 20.8% of non-zero candidates at that exact offset, well above random chance but far short of the ~100% a clean global repeating period would produce. Treat the 41-byte period as a real but localized pattern in that one region, not a universal law - don't extrapolate it across the whole range without checking.

The `11CC`-`11CF` 0-vs-1 polarity (which value means "performed" vs "in progress") is read directly off the CSV but hasn't been independently falsified the way the `1147`/`1148` bits have, so treat it as a working hypothesis, not confirmed.

Per HANDOVER.md, everything in the app exclusively uses SID `0x22` for reads; SID `0x21` (ReadDataByLocalIdentifier) was never observed anywhere in the decompiled code and hasn't been tested.

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

## PID/CID discovery scanner (Phase 2)

This is the actual long-term goal of the project: systematically discovering which identifiers this ECU supports beyond the 5 hardcoded ones, beyond what the OEM app ever exposed. **Built and run against real hardware as of 2026-06-20** - `desktop/ProtonEcuToolkit.Core/Scanning/` (`PidScanner`, `ResponseClassifier`, `ScanPlans`) plus a "PID Scanner" tab in the app (range picker, start/stop, live progress, a results table of hits, CSV export).

Scoped deliberately to **Service 0x22 (ReadDataByCommonIdentifier) only** - the same read-only service the 5 known PIDs already use. No write services, no security access, no routine control, no IO control, no reflash services - reading an unsupported identifier just gets a negative response, it doesn't change ECU state, which is what keeps this safe to brute-force. SID `0x21` (ReadDataByLocalIdentifier) was never observed in the decompile and has been deliberately left out of scope for now.

The OEM app's own read loop waits up to 1000ms before declaring "no data," which is too slow to scan widely - the scanner uses a 250ms timeout instead, since the ECU returns a response (positive or negative) quickly regardless of timeout length; only genuinely no-response candidates eat the full timeout. A keep-alive check runs every ~2.5 seconds of scanning, escalating through the same §3.3 recovery ladder used elsewhere, so a long scan doesn't let the session drop.

**Results so far (3 scanning sessions, 2026-06-20 and 2026-06-21):**
- **Known candidates (7)**: 7/7 positive every time - confirms the §3.5 hypothesis outright. See "Extra identifiers" above for the full decode.
- **Nearby range (768, `0x1000`-`0x12FF`)**: 768/768 (occasionally 767/768 with one transient timeout) positive every time it's been run. This isn't a sparse validated identifier list the way the OEM app's 5 PIDs suggested - this ECU appears to treat the whole block as a literal memory-mapped read window, returning *something* for every offset regardless of whether it's backed by a real signal.
  - **Correction from the first session**: the `0000` and `5532` patterns were originally classified as filler/padding because `5532` showed up 145 times in long contiguous runs. A third capture taken ~8 hours later showed several of those exact `5532` entries (`1008`, `1016`, `102D`, `103F`, `1056`, `1071`, `1072`, etc.) shift identically to `5512` - the same byte delta across many different addresses at once. That rules out "static padding": it's a real, almost certainly slow-changing or counter-like value that gets aliased across many addresses, not unmapped memory. Don't trust the original "filler vs real" split without re-checking it.
  - Across the 3 captures, **253 of 768 candidates (33%) show a value that differs from all previous captures** - strong evidence most of this range is live, not static.
- **Wide range (8192, `0x0000`-`0x1FFF`)**: not run yet.

Every attempt (positive, negative, no-response, or malformed - not just hits) is logged to `%AppData%\ProtonEcuToolkit\scan-results\scan-<timestamp>.jsonl` as the raw evidence trail; the live results table only shows positive hits, exportable to CSV via the same panel.

**Confirming what a positive candidate's bytes actually mean still requires the manual step the original plan anticipated**: watch the raw bytes while changing something physically and see what moves. This is how `1147` bit 4 (A/C request) and `1148` bit 5 (tank purge valve) got confirmed - turning the A/C on/off flipped bit 4 in both directions; idling for ~10 minutes flipped `1148`'s purge-valve bit. No amount of further scanning substitutes for this.

### Triaging candidates without hardware access

Re-running the same scan some time later and diffing the two captures is a cheap way to separate live signals from static constants even without a deliberate physical test - this is how the 98-candidate list (`data/scan-results/nearby-range-live-candidates-2026-06-20.json`) was found. Classifying *how* each one changed narrows the list further (`data/scan-results/nearby-range-classification-2026-06-20.json`):

- **Single-bit flip (18)** - the strongest, cheapest-to-verify signal, the same pattern that found `1147`/`1148`'s bits. Caveat: this is a heuristic, not proof - `110A` (TPS, a known *analog* PID) produced a single-bit-looking delta purely by coincidence (its noise happened to step by exactly 4). Treat hits as priority candidates for direct on/off correlation testing next time, not as confirmed flags.
- **Small multi-bit drift (37)** - consistent with continuous analog sensor noise; this is where the already-known RPM/TPS/battery PIDs land in this same diff, which is reassuring evidence the classification approach works.
- **Large jump (43)** - counters, timers, multi-byte values, or just unclear without more data.

One dead end worth recording so it doesn't get retried: applying the 5 known PIDs' formulas (e.g. `byteA-60` for coolant) to every *other* candidate and checking if the result lands in a physically plausible range matched 276 of 768 candidates - the value ranges just overlap too much for that check to mean anything on its own, without an independent signal like the diff-over-time approach above.

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
3. ~~Native desktop rewrite (C#/.NET WPF)~~ - done, hardware-validated.
4. **Phase 2 - PID/CID scanner**: the actual goal, and underway. Known candidates and the nearby range are scanned (see results above); the wide range (`0x0000`-`0x1FFF`) is still to run. The bigger remaining task is the manual correlation work - watching candidates while changing something physically - to decode the ~292 still-unidentified nearby-range hits the same way `1147`/`1148` got decoded.
5. Phase 3 - dashboard polish (gauges already built ahead of schedule; CSV export of live sessions still to do).
6. Phase 4 - decode the DTC byte format once a real stored code is captured; actuator test panel (fan/injector toggle via IO control), with proper response-checking added (the original app doesn't check, this one should).

## What's still unverified

- Whether the §3.5-equivalent 7 leftover identifiers respond under SID `0x22`, SID `0x21`, both, or neither - untested, first thing to check once the scanner exists.
- How short the scan timeout can be pushed before false negatives appear - needs calibration against the known-good IDs once scanning starts.
- The ECU's actual P3 timeout ceiling - the 1-second poll interval is comfortably under the app's implied 2-4 second cadence, but the real ceiling hasn't been measured empirically (increase the gap between requests until the session drops - that's the ceiling, stay well under it).

Already resolved (kept here for the record): `ATSH8101F1` does get real PID data back (not just `OK`) - confirmed live. The `+0x57` offset hypothesis is confirmed correct - decoded values are physically sane and track real engine behavior in real time. The native C# WPF port behaves identically to the proven TypeScript version against the real ECU - connects, completes the init sequence, decodes all 5 PIDs correctly, and DTC scan returns a clean positive response.

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
