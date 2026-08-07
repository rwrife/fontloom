# fontloom — Project Plan

## Scope

fontloom is a **cross-platform desktop font manager & previewer** for Windows 10/11 and macOS. It is a small, focused, single‑user productivity utility. In scope:

- Enumerate installed system fonts and index loose font files from user‑chosen folders.
- Parse font metadata (family, subfamily/style, weight, width, italic flag, format, glyph coverage summary) from `.ttf`/`.otf`/`.ttc`/`.woff`/`.woff2`.
- A fast, virtualized gallery that renders **user‑supplied sample text** across the whole library at adjustable size/weight/style.
- Faceted filtering & search (family name, classification, weight, monospace, has‑glyphs‑for‑text).
- Organization: favorites, freeform tags, and named collections — persisted locally.
- Side‑by‑side comparison tray (2–6 fonts, synchronized sample text).
- Specimen export (PNG and PDF) for a single font or a collection.
- Optional local‑AI font‑pairing suggestions and auto‑descriptions via a local OpenAI‑compatible endpoint, with graceful non‑AI fallback.

## Architecture / tech approach

- **Language/runtime:** .NET 8. UI‑free **`Fontloom.Core`** class library holds all portable logic (font parsing, indexing, tag/collection store, specimen layout, AI client + heuristics). Platform shells consume Core.
- **Font parsing:** a managed OpenType/TrueType table reader (`name`, `head`, `OS/2`, `cmap`) behind an `IFontFileReader` interface; candidate backing libs (e.g. SixLabors.Fonts / Typography.OpenFont) kept swappable behind the interface.
- **System font enumeration:** platform adapters behind `ISystemFontSource` — DirectWrite / system font dir on Windows; Core Text / font directories on macOS.
- **UI shells:**
  - Primary target: a cross‑platform .NET UI (Avalonia UI) so a single MVVM codebase renders on both Windows and macOS, with platform adapters only for font enumeration and native dialogs.
  - Fallback/alternative evaluated in M2: WPF (Windows) + a thin macOS shell if Avalonia rendering fidelity is insufficient.
- **Rendering previews:** render sample text per‑font using the UI toolkit's text stack; specimen export composites to a bitmap (PNG) and a vector/paginated PDF via a document library behind `ISpecimenExporter`.
- **Persistence:** local store (SQLite, or JSON documents) under the platform app‑data dir (`%APPDATA%\fontloom` on Windows, `~/Library/Application Support/fontloom` on macOS) for tags, collections, favorites, settings, and an index cache.
- **Local‑AI:** `IFontAiService` → OpenAI‑compatible `/v1/chat/completions` (Ollama/llama.cpp). Reachability probe + timeout + graceful fallback to rule‑based pairing (classification + weight/contrast heuristics). Sends only font metadata (names, metrics, classification) — never file contents. Off by default.
- **Testing:** xUnit against `Fontloom.Core` (parsing fixtures, index/query, tag/collection store, pairing heuristics, AI client fallback).

## Milestones

- **M1 — Core & indexing:** `Fontloom.Core` with `IFontFileReader`, metadata model, `ISystemFontSource` (Win + mac adapters), and a searchable in‑memory + cached index. Unit tests on parsing fixtures.
- **M2 — Gallery UI:** Avalonia MVVM shell, virtualized font grid, custom sample text, size/weight/style controls, faceted search sidebar.
- **M3 — Organize:** favorites, tags, collections; local persistence + index cache; incremental refresh when font folders change.
- **M4 — Compare & specimen export:** comparison tray (2–6 fonts, synced text) + PNG/PDF specimen export for font/collection.
- **M5 — Local‑AI pairing (optional):** `IFontAiService`, endpoint probe, pairing suggestions + auto‑descriptions, rule‑based fallback, Settings → Local AI.
- **M6 — Packaging & CI:** Windows portable self‑contained zip + MSIX; macOS `.app` + `.dmg`; GitHub Actions matrix build (windows-latest, macos-latest) + test.

## Non-goals

- **No font installation/uninstallation into the OS** in v1 (preview un‑installed files without installing). May be revisited later behind explicit confirmation.
- **No font editing / glyph editing** — fontloom views and organizes, it does not modify font files.
- **No cloud sync, accounts, or font marketplace** — strictly local.
- **No web/mobile app** — desktop (Windows + macOS) only.
- **No mandatory AI** — AI is strictly optional and off by default.
- **No Linux packaging target in v1** (Core stays portable, but only Win/mac are shipped/tested).

## Packaging / distribution target

- **Windows 10/11:** portable self‑contained `win-x64` zip **and** an MSIX installer.
- **macOS:** notarizable `.app` bundle distributed via `.dmg` (Apple Silicon + Intel where feasible via a universal or per‑arch build).
- **CI:** GitHub Actions matrix (`windows-latest`, `macos-latest`) builds artifacts and runs `Fontloom.Core` tests on every push/PR.
