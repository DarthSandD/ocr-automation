# Changelog

## v2.2 (2026-09-10)

File → CAD-grade raster import.

- New **Import** button: PDF and image files (PDF/PNG/JPG/BMP/TIFF)
- PDFs rasterize at 150/300/600 DPI via Pdfium (PDFtoImage engine) — 300 DPI default, detail suitable for AutoCAD/MicroStation raster attach
- Page navigation (‹ Page X / N ›) appears when a PDF is loaded; changing DPI re-renders live
- Imported raster feeds everything: Run OCR, To Rows, Save Raster (lossless PNG), automation rules
- Regression test renders a real PDF at 300 DPI and asserts CAD-usable dimensions

Verified: tests 14 passed / 1 skipped; live UI shows Import + DPI 300 + nav slot.

## v2.1 (2026-09-10)

Sleek dark minimalist UI (navy/yellow), same features.

- ModernTheme.xaml: dark slate cards, amber primaries, ghost secondaries, dark ComboBox/DataGrid/log
- Header with status + confidence pills; CAPTURE / TEXT / RULES cards
- Target dropdown ToString fix (real titles, never type names)
- csproj version stamped 2.0.0+, single-file officially retired in code + docs

## v2.0.0 (2026-09-10)

Fixed the core complaint: the app captured pixels but returned no text.

- Screen-safe preprocessing by default (adaptive threshold + sharpening off for screenshots, small captures auto-upscaled for Tesseract)
- Dual-path OCR: runs raw + preprocessed, keeps the better result, logs the winner
- Tesseract PageSegMode Auto; broken Windows-Media-OCR stub no longer hijacks engine selection
- Empty-result hint in the log when nothing is recognized
- Engine init errors surfaced in the log with inner-cause chain (this is how the single-file issue was diagnosed)
- New **To Rows** button: OCR text cleaned into notepad rows
- New **Save Raster** button: capture saved as timestamped PNG in Pictures/OcrAutomation
- Single-file publishing disabled (Tesseract 5.2.0 native loader incompatible); `publish-loose/` is the official build
- Regression test: preprocessed screenshot must still recognize text
- UIA automation scripts under `tools/` (refresh / capture+OCR / log / rows+raster)

Verified live: Refresh 9 windows, Notepad capture, OCR 90% / 1581 chars, 20 rows, 44KB PNG. Build 0 warnings, tests 13 passed / 1 skipped.

## v1.1 (2026-09-09)

- Save-rules validation blocks bad saves with a clear message
- 5 new MappingServiceTests
- Build 0 warnings, tests 12 passed / 1 skipped
