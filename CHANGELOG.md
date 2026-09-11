# Changelog

All notable changes to diode are recorded here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.1] - 2026-09-10

First public release.

### Added

- Tray app that drives every monitor attached to the machine. Left-click drops all of them
  to minimum brightness, left-click again brings them back to maximum.
- Three fallback paths per monitor, tried in order on every click: DDC/CI (`dxva2`),
  WMI (`root\WMI`), and a Gamma ramp fallback when neither answers. Monitors are re-enumerated
  per click, so hot-plugging a display needs no restart.
- Left-click reads current brightness before choosing a direction instead of flipping a flag.
- Right-click menu: darkest, brightest, run at startup, edit `config.ini`, diagnostics report, quit.
- `Ctrl+Alt+B` global hotkey, configurable.
- Per-Monitor V2 DPI awareness (embedded manifest): tray icon and menu render sharply at
  125% / 150% / 200%, including mixed-DPI multi-monitor setups.
- `config.ini` written next to the exe on first launch, hot-reloaded on save.
- Command line: `--diag`, `--set <0-100>`, `--version`.
- `diode.exe --diag` writes a report to the Desktop listing which control path each monitor took.

### Known limitations

- The Gamma fallback dims the picture, not the backlight. On a monitor without DDC/CI,
  "darkest" still glows faintly.
- DDC/CI depends on the monitor, cable, and any dock or KVM in between. When the channel is
  blocked, diode falls back to WMI or Gamma without complaining.
- `config.ini` is written next to the exe, so keep diode out of read-only directories
  such as `Program Files`. A failed write is silent.
