# OCR Automation — Usage Record (v2.2.0)

What this tool does, end to end. Portable, offline, no admin, no install.

## 1. Capture
- Pick Target window → Capture Window, or Capture Region for anything on screen
- Import PDF or image file → renders at 150 / 300 / 600 DPI
- Page with ‹ › when a PDF is loaded

Proof: screenshots/v2.2-import-ui.png (dark 3-card UI), screenshots/v2-main-window-90pct.png (live 90% OCR read)

## 2. Read
- Run OCR (Tesseract, bundled, no download)
- TEXT card fills with real text + confidence pill in header
- Copy button pastes anywhere
- Verified: 90% confidence, 1581 chars, 20 clean rows (Notepad live test)

## 3. Rasterize (CAD-grade)
- Save Raster → PNG at selected DPI
- 600 DPI = CAD-seat detail for AutoCAD / MicroStation handoff
- PDF → raster path uses same DPI selector

## 4. Rules (automate after read)
- RULES card: Name + regex Pattern + On toggle
- Run Automation (Automate button): each enabled rule tests OCR text, fires keystrokes / clicks on match
- Load / Save mappings JSON for reuse
- Ships with empty sample — add your own patterns (e.g. invoice no → type into next app + Enter)

## Run it
1. Download https://github.com/DarthSandD/ocr-automation/releases/tag/v2.2.0
2. Unzip, run OcrAutomation.exe (loose publish, no admin)
3. Refresh → pick window → Capture → Run OCR → To Rows / Save Raster

## Stack
C# WPF, Tesseract 5, PDFtoImage, SkiaSharp. Offline-first. MIT with fork credit.
