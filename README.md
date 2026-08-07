# fontloom

Cross-platform desktop **font manager & previewer** for **Windows 10/11 and macOS** — browse every font on your system (and loose font files on disk), preview them with your own sample text, compare typefaces side by side, tag/organize into collections, and get optional local-AI font‑pairing suggestions. Offline and privacy‑first: everything runs locally, no account and no cloud required.

## Overview

fontloom scans your installed fonts plus any folders of loose font files (`.ttf`, `.otf`, `.ttc`, `.woff`, `.woff2`) and gives you a fast, searchable gallery. Type a custom sample string once and see it rendered across your whole library at any size, weight, and style. Pin fonts into a comparison tray, organize favorites into named collections, and export a specimen sheet. An optional local‑AI mode (via Ollama / llama.cpp‑compatible tiny models) suggests complementary heading/body pairings — but the core experience works fully offline with no AI.

## Motivation

Designers, developers, and writers accumulate hundreds of fonts and have no good way to *see* them. The OS font pickers show a tiny one‑line preview, don't let you use your own sample text, and can't compare two faces at once. fontloom is a focused, native‑feeling desktop utility that makes choosing and organizing fonts fast — without uploading your font library to a web service.

## Use cases

- **Pick a font fast** — render your headline/paragraph in every installed font at once and scan visually.
- **Compare candidates** — pin 2–6 fonts into a side‑by‑side tray with synchronized sample text.
- **Organize a library** — tag fonts (serif, display, mono, "brand", "avoid") and group into collections.
- **Audit loose files** — point fontloom at a downloads/fonts folder and preview un‑installed font files without installing them.
- **Build a specimen sheet** — export a PNG/PDF specimen of a font or a collection to share.
- **Find a pairing** — (optional local‑AI) get heading/body pairing suggestions from fonts you already own.

## How to use

### Windows 10/11 quickstart

1. Download the portable `fontloom-win-x64.zip` from Releases (or the MSIX installer) and run `fontloom.exe`.
2. On first launch fontloom enumerates installed fonts via the system font directory and DirectWrite.
3. (Optional) Add a folder of loose font files: **File → Add font folder…**.
4. Type your sample text in the top bar; adjust size/weight with the sliders.

### macOS quickstart

1. Download `fontloom-macos.dmg` from Releases, drag **fontloom.app** to Applications, and launch it.
2. fontloom enumerates fonts from the system and user font directories (`~/Library/Fonts`, `/Library/Fonts`, `/System/Library/Fonts`) via Core Text.
3. (Optional) Add a folder of loose font files: **File → Add font folder…**.
4. Type your sample text and preview across your library.

> Cross‑platform note: the core logic lives in a UI‑free `Fontloom.Core` library so the same font parsing, indexing, tagging, and specimen rendering runs on both platforms; only the shell (window + system font enumeration) is platform‑specific.

## Example workflow

1. Launch fontloom → it indexes your installed fonts.
2. Set the sample text to `The quick brown fox — 0123456789` and size to 32pt.
3. Filter to **Sans‑serif** + weight **≥ 600** using the sidebar facets.
4. Pin three candidates into the comparison tray.
5. Tag the winner `brand/heading` and add it to the **Website 2026** collection.
6. **Export → Specimen (PDF)** for the collection to share with the team.

## Local‑AI integration (optional)

fontloom can talk to a local, OpenAI‑compatible endpoint (Ollama or llama.cpp `server`) to suggest font pairings and generate short, human‑readable descriptions/tags for a typeface based on its metadata and metrics.

- **Tiny‑model friendly:** Llama 3.2 3B / Qwen2.5 3B / Phi‑3‑mini class for text pairing suggestions; MiniCPM‑V class if visual specimen analysis is enabled.
- **Local only:** requests go to `http://localhost:11434` (or your configured endpoint). Nothing leaves your machine.
- **Graceful fallback:** a reachability probe runs first; if no model is available, AI features are hidden and rule‑based pairing heuristics (classification + contrast rules) are used instead.
- **Off by default:** enable it explicitly in **Settings → Local AI**.

## Current status / milestones

🚧 **Bootstrapping.** This repo currently contains the plan and backlog. Tracked milestones:

- **M1 — Core & indexing:** font file parsing + system enumeration + searchable index.
- **M2 — Gallery UI:** custom sample text, size/weight controls, virtualized grid.
- **M3 — Organize:** tags, collections, favorites, persisted store.
- **M4 — Compare & specimen export:** comparison tray + PNG/PDF specimen sheets.
- **M5 — Local‑AI pairing (optional):** endpoint probe + pairing suggestions with fallback.
- **M6 — Packaging:** Windows (portable zip + MSIX) and macOS (.app/.dmg) builds in CI.

See [PLAN.md](./PLAN.md) for scope, architecture, and non‑goals.
