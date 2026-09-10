# OcrAutomation v1.1 Continuation Report

Date: 2026-09-09 · SDK: .NET 8.0.424 · Config: Release · Solution: `OcrAutomation.sln`

## 1. Build status

| Step | Command | Result |
|------|---------|--------|
| Restore | `dotnet restore OcrAutomation.sln` | ✅ OK — both projects restored |
| Build | `dotnet build OcrAutomation.sln -c Release` | ✅ **Succeeded — 0 warnings, 0 errors** (before and after change) |
| Test | `dotnet test OcrAutomation.sln -c Release` | ✅ See §2 |

No build fixes were needed. Baseline build was already clean (0 warnings).

## 2. Test results

Before: **8 total — 7 passed, 0 failed, 1 skipped**.
After: **13 total — 12 passed, 0 failed, 1 skipped**.

| Test | Result |
|------|--------|
| UnitTest1.Test1 | ✅ Pass |
| KeySequenceParser ×3 | ✅ Pass |
| CaptureOcrPipeline ×3 (incl. live Tesseract OCR) | ✅ Pass |
| **MappingServiceTests ×5 (new)** | ✅ Pass |
| NotepadIntegration_TypeCaptureOcrCopy | ⏭️ Skip (by design — interactive desktop test, manual only) |

Full console outputs from restore/build/test were captured during the run; exit code 0 throughout.

## 3. What was fixed

**Nothing was broken — no failing tests, no build warnings.** No fixes required. The only test-output anomaly is the Tesseract native `ObjectCache ... WARNING! LEAK!` lines (5× `eng.traineddata*`); these come from the unmanaged Leptonica/Tesseract layer at process teardown in the test host (the `TesseractOcr_RecognizesText` test never disposes its engine), not from app code paths — logged as backlog item #1 instead of "fixed".

## 4. The one improvement (user-visible)

**Validate mapping rules on Save — block bad saves with a clear message.**

- Problem: `MappingService.ValidateRules()` existed but was never called anywhere. "Save Rules" silently persisted invalid regex patterns; the user only discovered it later when automation quietly ignored the rule (`MatchText` swallows bad patterns). No feedback at the point of error.
- Change (`src/OcrAutomation/ViewModels/MainViewModel.cs`, `SaveMappingsAsync`): validation now runs before saving. If any rule fails, each error is written to the activity log as `Rule validation error: …`, followed by `Save blocked: N rule validation error(s). Fix the rules and try again.`, the status bar shows **"Rule validation failed"**, and the file is not written. Valid saves behave exactly as before.
- Tests (`tests/OcrAutomation.Tests/MappingServiceTests.cs`, 5 new xUnit tests covering the validation logic the feature relies on): valid rules pass; invalid regex flagged with rule name; missing pattern/name flagged; `MatchText` still ignores broken rules safely; no-match returns empty. All 5 pass (see §2).
- Proof: post-change `dotnet test` run — `MappingServiceTests.*` 5/5 Passed, full suite 12 passed / 0 failed / 1 skipped, build 0 warnings / 0 errors.

## 5. Backlog top-5 (not done — out of scope: no arch refactoring)

1. **Tesseract engine disposal / native ObjectCache leak warnings** — test host prints 5 `WARNING! LEAK!` lines on teardown; `TesseractOcrEngine` has `Dispose()` but tests and possibly the app lifetime never call it. Small: dispose engine in test / verify app disposes on exit.
2. **Validate rules on load, not just on save** — `LoadMappingsAsync` loads `sample_mappings.json` without validation, so a hand-edited file with a bad pattern still fails silently at automation time. One-line reuse of `ValidateRules()` + log line.
3. **No log export** — activity log lives only in the UI list; users can't copy/save diagnostics. Small: "Export log" button writing `LogEntries` to a timestamped text file.
4. **Empty-OCR result has no distinct message** — `RunOcrAsync` reports "OCR done" even when recognized text is empty; add an explicit "No text recognized — try a larger region / different window" hint.
5. **Hardcoded tuning + packaging weight** — `LowConfidenceThreshold` (0.6) and tessdata path handling are hardcoded; single-file exe is ~129 MB. Consider user-facing confidence setting, trimmable publish, and CI (no pipeline currently runs build/tests automatically).
