# Marsh PC Monitor v1 — Acceptance Smoke Test

**Date:** _(fill in when run)_
**Tester:** Dreux Marsh

---

## Target Machine

| Field | Value |
|---|---|
| CPU | Legion i9-14900HX |
| OS Build | _(fill in: `winver` or Settings → System → About)_ |
| .NET 8 SDK installed | _(yes/no)_ |
| LHM initialized cleanly | _(yes / no — did temp tiles show real values?)_ |
| Cold-start time (approx) | _(e.g., "< 1s", "~1.5s")_ |

---

## Install

```powershell
.\app\install\install.ps1 -Publish
```

- [ ] Completed without errors
- [ ] Printed install path: `%LocalAppData%\PcMonitor\PcMonitor.exe`
- [ ] Printed shortcut: `...\Start Menu\Programs\Marsh PC Monitor.lnk`
- [ ] Printed scripts path under `Documents\SysLogs\scripts\`

---

## Acceptance Checklist

- [ ] App builds and publishes from the documented command.
- [ ] Cold launch opens the cockpit in under 2 seconds.
- [ ] With `Documents\SysLogs\hourly\` absent (rename folder before launch), app still opens and live tiles show real-time data.
- [ ] Capture buttons are visually disabled while a capture is running and re-enabled afterward.
- [ ] Diagnostic capture produces a `diagnostic_*.txt` path and a suggested Claude prompt containing the correct `/mnt/c/...` WSL path.
- [ ] Live probe capture produces a `live_*.txt` path and a suggested Claude prompt containing the correct `/mnt/c/...` WSL path.
- [ ] Killing/cancelling a capture does not crash the app.
- [ ] Temp/throttle sensors: _(if temps visible, mark verified; otherwise: "degraded path tested via unit tests")_
- [ ] Single-instance: launching the exe a second time foregrounds the first window.
- [ ] Rule unit tests all pass: `dotnet test` → 63/63 green.

---

## "Copy Claude Prompt" Round-Trip

After a successful Diagnostic capture:

- [ ] Clicked "Copy Claude prompt"
- [ ] Pasted into Claude Code (WSL)
- [ ] Claude read the file and returned analysis successfully

---

## Quirks / Observations

_(Any threshold that fired immediately and felt wrong, visual glitches, unexpected behaviour — input for v1.1 settings work.)_

- 

---

## Verdict

- [ ] **PASS** — v1 shippable for own machine use.
- [ ] **FAIL** — blockers noted above.
