# Proton Waja CPS / Siemens EMS700 ECU - Phase 2 Scanning Research Summary

Context for whoever's reading this: this is a from-scratch reverse-engineering project for a Proton Waja CPS's ECU diagnostics, built into a native C#/WPF desktop app. The protocol layer was originally reverse-engineered from a decompiled Android diagnostic app ("ProtonOBDFree" Torque plugin), then verified against the real vehicle. This document summarizes the PID/CID discovery scanning work and asks for help interpreting the unidentified results.

## Vehicle & protocol

| | |
|---|---|
| Vehicle | Proton Waja CPS |
| Engine | Campro CPS (the "CPS" stands for Camshaft Profile Switching - a VTEC-like variable cam timing system) |
| ECU | Siemens EMS700 |
| Protocol | KWP2000 / ISO 14230-4, Fast Init, 10.4 kbps (K-line) |
| Adapter | ELM327 clone, Bluetooth Classic SPP, paired as a Windows COM port |

Init sequence: `ATZ` x2 -> `ATE0` -> `ATH0` -> `ATSP5` (protocol = ISO14230-4 KWP fast init) -> `ATSH8101F1` (header, **target ECU address 0x01, not the textbook default 0x10**) -> ~2s settle -> test ping `22111F` (Service 0x22, CID `0x111F`), success = response contains `62`.

With headers off (`ATH0`), the wire format is plain hex, no checksum/header math: send `22<CID>`, receive `62<CID><data bytes>` for a positive response, or `7F<SID><NRC>` for negative.

## The 5 known, fully-decoded PIDs (Service 0x22, ReadDataByCommonIdentifier)

These came from the OEM app's source code (a real scaling formula was hardcoded per PID), verified live against the car:

| Send | PID | Formula (byteA = 1st data byte, byteB = 2nd) | Confirmed live value |
|---|---|---|---|
| `221101` | Coolant temp | `byteA - 60` = °C | 69-85°C depending on warm-up state |
| `221104` | RPM | `byteB×255 + byteA` = rpm | ~800-850 at warm idle, ~1000-1050 cold idle |
| `22110A` | TPS | `byteA × 0.39216` = % | ~4-6% at closed throttle |
| `221110` | Battery voltage | `byteA × 0.078431` = V | ~13.0-14.4V running (alternator charging), 11.8V key-on-engine-off |
| `221113` | Vehicle speed | `byteA × 1.2` = km/h | 0 (stationary) |

Note: `1113`'s second byte (`byteB`) is never used by this formula and has been observed changing independently of vehicle speed - meaning is unknown.

## The 7 "known candidate" identifiers - bitfields, mostly decoded

These came from two CSV files bundled in the decompiled APK but never wired into any code path in the Free build (`diagstatus1.csv`, `signalstatus.csv`) - almost certainly Pro-tier leftovers. **All 7 confirmed positive under Service 0x22 on the real ECU.** Bit numbering is bit 0 = LSB. The bitfield lives in byte A (first data byte); byte B's meaning for these IDs is undocumented.

**`1147`** (from `signalstatus.csv`):

| bit | signal | 0 | 1 | status |
|---|---|---|---|---|
| 0 | LLK (idle contact) | opened | closed | named, plausible reading, not independently tested |
| 1 | Full Load | not active | active | named, plausible, not tested |
| 2 | Gear lever position | D | P/N | named, plausible, not tested |
| 4 | Air conditioning request | OFF | ON | **CONFIRMED both directions** - toggled A/C on/off twice, bit flipped exactly as predicted both times |
| 5 | VIM position | OFF | ON | named, not tested. "VIM" = Variable Intake Manifold (intake geometry switch). Owner confirms the activation threshold is ~4800 rpm. Planned test: stationary high-rev. |
| 6 | CPS position | OFF | ON | named, not tested. "CPS" = Camshaft Profile Switching (matches the engine name itself). Owner confirms the activation threshold is ~3800 rpm. Planned test: stationary high-rev. |
| 7 | Clutch Switch | not declutched | declutched | **TESTED AND DID NOT BEHAVE AS LABELED** - clutch pedal held down twice (once with extra timing buffer to rule out lag), bit never changed from 0. Hypothesis: this might actually be the A/C *compressor* clutch (the electromagnetic clutch engaging the compressor off the belt) rather than the transmission pedal switch - every capture so far is consistent with that (compressor was off or not cycling on in all 3 tests). Or it's simply a reserved bit not populated on this car. Unresolved. |

**`1148`** (from `signalstatus.csv`):

| bit | signal | 0 | 1 | status |
|---|---|---|---|---|
| 1 | Camshaft control active | OFF | active | named, not tested |
| 2 | Intake manifold control valve | OFF | active | named, not tested |
| 3 | Knock control status | forbidden | allowed | named, not tested |
| 5 | Tank purge valve | OFF | active | **CONFIRMED live** - two scans ~10 minutes apart while idling showed this bit turning on (0x40 -> 0x60), consistent with EVAP purge activating once the engine's been running long enough for closed-loop control |
| 6 | Lambda controller 1 | OFF | active | observed "active" in real readings, consistent with a warm engine in closed loop, not independently toggled |
| 7 | Lambda controller 2 | OFF | active | named, not tested |

**`1149`** (from `signalstatus.csv`) - all named, all readings plausible, none individually tested:
bit0 End of line mode, bit1 Cat heating idle (ignition), bit2 Poststart (injection), bit3 Fuel cut off (SAS), bit4 Safety fuel shut off, bit5 TBA adaptation (not finished/finished).

**`11CC`-`11CF`** (from `diagstatus1.csv`) - onboard diagnostic self-test status bits (catalyst, cat heater, tank evap, secondary air, A/C system, lambda probe + heater, EGR, clutch switch [diagnostic context], transmission interface, misfire diagnostics, lambda adaptation x3, lambda integrator, knock sensor check, IPA active test, engine temp sensor check, MAP sensor check, vehicle speed signal check) - each pair is "Diagnosis performed" vs "Diagnosis in progress." All bits are named from the CSV, but **the 0-vs-1 polarity (which value means "performed" vs "in progress") has not been independently verified** the way the OFF/ON pairs above were - treat as a hypothesis, not confirmed.

## Nearby-range scan: `0x1000`-`0x12FF` (768 candidates) under Service 0x22

This range was chosen because every known-real ID clusters there. **Run 3 times** (2026-06-20 twice ~30 min apart, 2026-06-21 once more ~8 hours later): **768/768 positive every time** (occasionally 767/768 with one transient timeout).

This is the most important structural finding: **this ECU does not validate the 2-byte CID for this range at all** - every single offset in `0x1000`-`0x12FF` returns *some* data, whether or not it's backed by a real signal. The working theory is that the ECU treats this whole block as a literal memory-mapped read window (i.e. the CID is functioning as a raw memory/table offset, not a validated signal identifier), not a sparse list of defined PIDs the way the 5-PID + 7-candidate sets are.

### Confirmed: the CID is a sliding 2-byte window into contiguous memory (found via re-analysis, no new hardware access)

**Byte B of CID `N` equals byte A of CID `N+1`** within a real memory block - adjacent CIDs overlap by one byte rather than being independent 2-byte readings. Verified two ways, both lining up exactly with the original decompile's CSV groupings:

- `1147`-`1148`-`1149`: chains perfectly across all 3 captures taken so far, breaks cleanly on both sides (`1146` doesn't connect, `114A` doesn't connect). True underlying data is a 4-byte block (`05 40 20 72` most recently), not 6 redundant bytes.
- `11CC`-`11CF`: same pattern, `00 02 0F 80` as one block, clean break before `11CC`. **New finding**: the block actually extends one more byte than documented - `11D0` (never in the original CSV) genuinely continues the chain, making the real block 5 bytes (`00 02 0F 80 00`).

Scanning the rest of the nearby range found 24 more non-trivial chains (3-12 IDs each) scattered through the range, each presumably marking a distinct real sub-structure - full list in `data/scan-results/sliding-window-chains-2026-06-21.json`. Two chains contain the identical byte sequence (`08 E0 80 01`) at two addresses 26 bytes apart - more aliasing, same phenomenon as the `5532`/`5512` value. A few short sequences also repeat at exactly 41 (`0x29`) bytes apart from each other, but a systematic check across the whole range only matched ~20.8% of non-zero candidates at that offset - a real but localized pattern, not a confirmed global repeating period. Don't extrapolate the 41-byte period without checking it first.

**Validation pass** (`data/scan-results/memory-mapping-validation-2026-06-21.json`): re-ran chain detection across all 3 independent captures and kept only chains with byte-for-byte identical boundaries every time - 52 stable chains, strong evidence of real structural edges rather than a snapshot artifact. Also checked whether the 5 known sensor PIDs connect to either neighbor the same way - **none do** (the apparent match for `1113` is a trivial zero-coincidence). The sensor PIDs appear to be individually-addressed standalone cells, while the bitfield groups are packed into contiguous memory - likely two different internal table layouts.

**9 of the 50 still-unexplained stable chains contain a value that changed** between the first and third capture (hours apart) - mostly a single live byte followed by zero padding, with small bounded deltas (never a big jump), more consistent with a real sensor than a counter. Priority list for future decode: `1088`, `10A0`, `1164`, `11EF`, `11F8`, `1248`, `1278`, `12A8`, `12B8`.

**Filler classification - revised, this is important:**
- In the first session, ~331/768 returned `0000` and ~145/768 returned the exact value `5532`, in long contiguous runs of consecutive addresses. Both were classified as "filler/padding" (unmapped memory returning a default value).
- **The `5532` classification turned out to be wrong.** In the third capture (~8 hours after the first two), several of those exact identifiers (`1008`, `1016`, `102D`, `103F`, `1056`, `1071`, `1072`, and others) all shifted from `5532` to `5512` - the *same* byte delta, across many different addresses, simultaneously. That's not 145 independent sensors coincidentally matching, and it's not random noise either (random noise wouldn't move every instance by the exact same amount at once) - it looks like one real, slow-changing or counter-like underlying value that's aliased/mirrored across many different CID addresses in this block.
- The `0000` pattern has not been similarly falsified yet, but given the above, **do not assume `0000` is conclusively filler either** - it just hasn't been seen to change in 3 captures so far.

**Liveness across captures:** comparing all 3 captures pairwise, **253 of 768 candidates (33%) show a value that differs from every previous capture** - i.e., not just two-point noise, a third genuinely distinct value. This includes the already-known PIDs in this range (`1104` RPM, `110A` TPS, `1110` battery voltage) showing exactly the expected small idle-noise-magnitude changes between captures, which is good evidence the diffing methodology itself is sound, not an artifact.

**Classification of the 98 candidates that changed between the first two captures** (before the third capture/5532 correction):
- 18 single-bit-flip (exactly one bit differs) - the cheapest, strongest signal type, found by the same method that confirmed `1147` bit4 and `1148` bit5. **Caveat: this is a heuristic, not proof** - `110A` (TPS, a known *analog* PID) also produced a single-bit-looking delta purely by coincidence (its noise happened to step by exactly 4, i.e. one bit's worth). The other 17 are priority candidates for direct on/off correlation testing, not confirmed flags.
- 37 small multi-bit drift (changed by a small amount in both directions) - consistent with continuous analog sensor noise. RPM/TPS/battery land here.
- 43 large jump - counters, timers, multi-byte values, or unclear.

**A dead end worth knowing about so it isn't retried:** applying the 5 known PIDs' formulas (e.g. `byteA - 60` for temperature) to every *other* candidate and checking whether the result lands in a physically plausible range matched 276 of 768 candidates. That's just because the plausible-value ranges for temperature/voltage/etc. cover most of a single byte's range (0-255) - it's not a meaningful signal on its own, only useful combined with an independent confirmation like the diff-over-time approach above.

## What hasn't been tried yet

- **Wide range scan** (`0x0000`-`0x1FFF`, 8192 candidates) - not run.
- **SID 0x21** (ReadDataByLocalIdentifier) - never observed in the original decompile, never tested at all. Everything above is SID `0x22` only.
- **Stationary high-RPM test** for `1147` bits 5/6 (VIM/CPS) - rev the engine hard while parked (handbrake on, in neutral/park) past the confirmed thresholds (CPS ~3800 rpm, VIM ~4800 rpm - confirmed by the owner from direct experience with this car) to see if these RPM-threshold mechanisms trigger without needing to actually drive. Expect bit 6 (CPS) to flip first, bit 5 (VIM) second.
- **Direct on/off correlation testing** of the 17 unconfirmed single-bit-flip candidates from the classification above, the same way A/C and the purge valve got confirmed.
- Figuring out what's actually behind the `5532`/`5512`-shifting aliased value (confirmed to span exactly 145 of the 768 nearby-range identifiers, full list in `nearby-range-classification-2026-06-20.json`/`nearby-range-third-capture-diff-2026-06-21.json`).
- Byte B's meaning is now mostly explained (it's the next CID's byte A, see the sliding-window finding above) - what's still open is finding the boundaries and meaning of the other 22 non-trivial chains in `sliding-window-chains-2026-06-21.json` that aren't part of an already-decoded group.

## Questions this summary is meant to prompt help with

1. Given a Siemens EMS700 ECU on a Campro CPS engine treats `0x1000`-`0x12FF` as a memory-mapped read window with no per-identifier validation, is there a known/documented internal memory layout for this ECU family that would explain what's actually at these offsets?
2. Any informed guess at what a value that's aliased identically across exactly 145 different addresses, and shifts by the same delta after ~8 hours, might represent? (Candidate guesses so far: a slow-moving counter, an elapsed-time/uptime value, a checksum, a calibration revision marker - none confirmed.)
3. Is there a more systematic way to disambiguate "real changing signal" from "counter/timer incrementing for unrelated reasons" purely from captured data, without more physical correlation tests?

**Note on external review:** an earlier version of this summary was shared with another AI assistant. Its general engineering reasoning (memory-mapped diagnostic windows being typical of this ECU era, methodology suggestions like treating byte B as part of a 16-bit value) was useful, but its specific cited claims (exact EMS700 memory-map references, footnoted sources) could not be independently verified and should be treated as unconfirmed speculation dressed up as sourced fact - a known failure mode worth watching for when getting outside opinions on this. Where it actually computed something directly from the raw data file (e.g. the exact count of `5532`-aliased addresses), it was correct and verifiable.
