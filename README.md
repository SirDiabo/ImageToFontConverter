# ImageToFontConverter

ImageToFontConverter is a small WPF tool (C# / .NET 9) that converts a folder of PNG glyph images into a TrueType font (TTF). It vectorizes PNG alpha masks into SVGs using Emgu.CV (OpenCV) and automates FontForge with a generated Python script to produce the final font. The app includes a simple GUI with progress reporting and cancellation.

## Highlights
- PNG → SVG vectorization using Emgu.CV
- Automated FontForge script generation and execution
- Produces a `.ttf` font and keeps intermediate SVGs for inspection
- Simple WPF GUI with progress and cancellation support

## Requirements
- .NET 9 SDK
- Visual Studio 2026 (recommended) with the __.NET desktop development__ workload
- FontForge installed and available on PATH or in one of the common install locations
- Emgu.CV NuGet packages referenced by the solution and the native OpenCV/Emgu runtime (platform native dependencies)
- (Windows) Visual C++ Redistributable may be required by native Emgu/OpenCV binaries

## Quick start (build & run)
1. Clone the repository:
   - `git clone https://github.com/SirDiabo/ImageToFontConverter.git`
2. Open the solution in Visual Studio: __File > Open > Project/Solution__ and select the `.sln`.
3. Restore NuGet packages (__Project > Restore NuGet Packages__ if needed).
4. Confirm target framework is `.NET 9` in __Right-click Project > Properties > Target Framework__.
5. Build the solution: __Build > Build Solution__.
6. Run the app: __Debug > Start Debugging__ (F5) or __Debug > Start Without Debugging__ (Ctrl+F5).

## Preparing input images
- Input must be PNG files with an alpha channel (RGBA). The converter uses the alpha channel to detect glyph contours.
- Place all glyph PNGs in a single folder (top-level). The app does not recurse subfolders.
- Filenames define the mapping to Unicode codepoints. Conventions supported by `FontConverter`:
  - `upper_A.png`, `upper_B.png`, ... → `A`..`Z`
  - `lower_a.png`, `lower_b.png`, ... → `a`..`z`
  - `0.png` .. `9.png` → digits
  - Symbol names: `space.png`, `exclamation.png`, `questionmark.png`, `period.png`, `comma.png`, `colon.png`, `semicolon.png`, `hyphen.png`, `plus.png`, `equal.png`, `at.png`, `hash.png`, `dollar.png`, `percent.png`, `caret.png`, `ampersand.png`, `asterisk.png`, `leftparenthesis.png`, `rightparenthesis.png`, `underscore.png`, `backtick.png`, `tilde.png`, `leftbracket.png`, `rightbracket.png`, `leftbrace.png`, `rightbrace.png`, `backslash.png`, `forwardslash.png`, `verticalbar.png`, `lessthan.png`, `greaterthan.png`, `singlequote.png`, `doublequote.png`
- `space.png` is treated specially (creates an empty glyph with a default width).

Tip: use clean, high-contrast alpha masks (opaque glyph area, transparent background) for best tracing results.

## Using the app
1. Launch the app and use the Browse button to select the input folder.
2. The UI reports how many expected glyph files were found (based on `FontConverter.ExpectedGlyphs`).
3. Adjust the Simplification slider to reduce or increase contour detail (higher = fewer points).
4. Enter a Font Name. The output filename will be `<FontName>.ttf` and will be the font's internal install name.
5. Click Convert. Progress is shown in the UI. Conversion can be cancelled.
6. On success you will find:
   - `<FontName>.ttf` in the input folder
   - `converted/` subfolder with generated `.svg` files

## How it works (technical summary)
- `MainWindow.xaml.cs` drives the UI: folder selection, progress updates, and cancellation.
- `FontConverter.cs` does the heavy lifting:
  - Finds FontForge executable (`FindFontForgeExecutable()` checks common paths and `where`).
  - Loads PNGs with Emgu.CV and extracts the alpha channel.
  - Thresholds the alpha channel and finds contours (`CvInvoke.FindContours`).
  - Approximates contours and writes SVG path data (preserving holes via contour hierarchy).
  - Generates a FontForge Python script that maps filenames to Unicode values, imports SVGs, sets metrics, and writes a TTF.
  - Launches FontForge with redirected stdout/stderr and parses `PROGRESS:` lines for UI updates.
- Temporary Python script is created and deleted automatically (if you need it for debugging, check the code and add logging before deletion).

## Outputs
- `<FontName>.ttf` — final TrueType font (written to the input folder)
- `converted/*.svg` — intermediate SVGs (kept for inspection)
- Progress messages and errors are shown in the app UI; the app surfaces FontForge output to help debugging.

## Troubleshooting
- "FontForge not detected" — install FontForge and make sure it is on PATH or in a common location scanned by the app.
- "No PNG files found" — ensure `.png` files exist at the top level of the selected folder.
- "No contours found" — check the PNG alpha channel; if the glyph is drawn on an opaque background, contour detection will fail.
- Complex glyphs can produce very complex SVGs; increase simplification to reduce path complexity.
- If FontForge fails, inspect the app output and the (temporary) generated Python script for the cause.

## Project structure (key files)
- `MainWindow.xaml` / `MainWindow.xaml.cs` — WPF UI and interaction logic
- `FontConverter.cs` — PNG → SVG logic, FontForge script creation and process orchestration
- `GlyphsWindow` (if present) — UI helper to inspect found glyphs
- `README.md` — this file