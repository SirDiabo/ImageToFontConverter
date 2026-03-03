using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace ImageToFontConverter
{
    public class FontConverter
    {
        public static readonly string[] ExpectedGlyphs =
        {
            // Uppercase
            "upper_A", "upper_B", "upper_C", "upper_D", "upper_E", "upper_F", "upper_G", "upper_H",
            "upper_I", "upper_J", "upper_K", "upper_L", "upper_M", "upper_N", "upper_O", "upper_P",
            "upper_Q", "upper_R", "upper_S", "upper_T", "upper_U", "upper_V", "upper_W", "upper_X",
            "upper_Y", "upper_Z",
            // Lowercase
            "lower_a", "lower_b", "lower_c", "lower_d", "lower_e", "lower_f", "lower_g", "lower_h",
            "lower_i", "lower_j", "lower_k", "lower_l", "lower_m", "lower_n", "lower_o", "lower_p",
            "lower_q", "lower_r", "lower_s", "lower_t", "lower_u", "lower_v", "lower_w", "lower_x",
            "lower_y", "lower_z",
            // Numbers
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            // Punctuation
            "space", "exclamation", "questionmark", "period", "comma", "colon", "semicolon", 
            "hyphen", "endash", "emdash", "ellipsis", "degree", "bullet", "middot", 
            // Brackets & Quotes
            "leftparenthesis", "rightparenthesis", "leftbracket", "rightbracket", 
            "leftbrace", "rightbrace", "angleleft", "angleright", 
            "singlequote", "doublequote", "backtick", 
            // Math
            "plus", "equal", "caret", "percent", "asterisk", "divide", "multiply", "plusminus", 
            "lessthan", "greaterthan", 
            // Symbols
            "at", "hash", "ampersand", "underscore", "tilde", "backslash", "forwardslash", "verticalbar", 
            "copyright", "trademark", "registered", 
            // Currency
            "dollar", "euro", "pound", "yen", "cent", 
            // Accented Lowercase
            "lower_e_acute", "lower_e_grave", "lower_e_circ", "lower_e_uml", 
            "lower_a_acute", "lower_a_grave", "lower_a_circ", "lower_a_uml", "lower_a_ring", 
            "lower_o_acute", "lower_o_circ", "lower_o_uml", 
            "lower_u_acute", "lower_u_grave", "lower_u_uml", 
            "lower_i_acute", "lower_i_circ", 
            "lower_n_tilde", "lower_c_cedil", "lower_ss", "lower_ae", 
            // Accented Uppercase
            "upper_A_uml", "upper_O_uml", "upper_U_uml", 
            "upper_A_grave", "upper_A_ring", "upper_E_acute", 
            "upper_N_tilde", "upper_C_cedil", "upper_AE", 
        };

        // Static method that can be used by both FontConverter and MainWindow
        public static string FindFontForgeExecutable()
        {
            string[] possiblePaths =
            {
                @"C:\Program Files (x86)\FontForgeBuilds\bin\fontforge.exe",
                @"C:\Program Files\FontForgeBuilds\bin\fontforge.exe",
                @"C:\Program Files (x86)\FontForge\bin\fontforge.exe",
                @"C:\Program Files\FontForge\bin\fontforge.exe",
                @"C:\Program Files (x86)\FontForgeBuilds\bin\ffpython.exe",
                @"C:\Program Files\FontForgeBuilds\bin\ffpython.exe"
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Try to find using where command
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "fontforge",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Split('\n')[0].Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding fontforge with 'where' command: {ex.Message}");
            }

            return null;
        }

        public async Task ConvertToFontAsync(string inputFolder, string fontName, double simplification,
            Action<double, string> progressCallback, CancellationToken cancellationToken)
        {
            try
            {
                // Find FontForge executable
                string fontForgeExe = FindFontForgeExecutable();
                if (string.IsNullOrEmpty(fontForgeExe))
                {
                    throw new Exception("FontForge executable not found. Please ensure it's installed correctly.");
                }

                progressCallback(5, $"FontForge executable found at: {fontForgeExe}");

                // Create converted folder
                string convertedFolder = Path.Combine(inputFolder, "converted");
                Directory.CreateDirectory(convertedFolder);

                // Convert PNG files to SVG
                await ConvertPngToSvgAsync(inputFolder, convertedFolder, simplification, progressCallback, cancellationToken);

                // Generate font using FontForge
                string outputFontPath = Path.Combine(inputFolder, $"{fontName.Replace(" ", "_")}.ttf");
                await GenerateFontAsync(convertedFolder, outputFontPath, fontName, fontForgeExe, progressCallback, cancellationToken);

                if (File.Exists(outputFontPath))
                {
                    var fileInfo = new FileInfo(outputFontPath);
                    progressCallback(100, $"Custom font created: {fileInfo.Name} (Size: {fileInfo.Length / 1000.0:F2} KB)");
                }
                else
                {
                    throw new Exception("Font file was not created despite no errors.");
                }
            }
            catch (OperationCanceledException)
            {
                progressCallback(0, "Conversion cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                progressCallback(0, $"Error: {ex.Message}");
                throw;
            }
        }

        private async Task ConvertPngToSvgAsync(string inputFolder, string convertedFolder, double simplification,
            Action<double, string> progressCallback, CancellationToken cancellationToken)
        {
            string[] pngFiles = Directory.GetFiles(inputFolder, "*.png", SearchOption.TopDirectoryOnly);
            int totalFiles = pngFiles.Length;

            if (totalFiles == 0)
            {
                throw new Exception("No PNG files found in the input folder.");
            }

            for (int i = 0; i < totalFiles; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string pngFile = pngFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(pngFile);
                string svgPath = Path.Combine(convertedFolder, $"{fileName}.svg");

                await Task.Run(() => ConvertPngToSvg(pngFile, svgPath, simplification), cancellationToken);

                double progress = 20 + ((i + 1) / (double)totalFiles * 30);
                progressCallback(progress, $"Converting: {Path.GetFileName(pngFile)} to SVG");
            }
        }

        private void ConvertPngToSvg(string pngPath, string svgPath, double simplification)
        {
            if (File.Exists(svgPath))
            {
                Debug.WriteLine($"Skipping existing file: {svgPath}");
                return;
            }

            Mat img = null;
            Mat alpha = null;
            Mat binary = null;
            VectorOfVectorOfPoint contours = null;
            Mat hierarchy = null;

            try
            {
                img = CvInvoke.Imread(pngPath, ImreadModes.Unchanged);

                if (img.IsEmpty || img.NumberOfChannels < 4)
                {
                    Debug.WriteLine($"Failed to load image with alpha channel: {pngPath}");
                    return;
                }

                // Extract alpha channel
                alpha = new Mat();
                CvInvoke.ExtractChannel(img, alpha, 3);

                int height = alpha.Height;
                int width = alpha.Width;

                // Special handling for space character
                if (Path.GetFileName(pngPath).Equals("space.png", StringComparison.OrdinalIgnoreCase))
                {
                    CreateSpaceSvg(svgPath, width / 2, height);
                    return;
                }

                // Threshold to binary
                binary = new Mat();
                CvInvoke.Threshold(alpha, binary, 128, 255, ThresholdType.Binary);

                // Find contours
                contours = new VectorOfVectorOfPoint();
                hierarchy = new Mat();
                CvInvoke.FindContours(binary, contours, hierarchy, RetrType.Tree, ChainApproxMethod.ChainApproxTc89Kcos);

                if (contours.Size == 0)
                {
                    Debug.WriteLine($"No contours found in {pngPath}");
                    return;
                }

                // Create SVG
                CreateSvgFromContours(svgPath, contours, hierarchy, simplification, width, height);
                Debug.WriteLine($"Successfully converted {pngPath} to {svgPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting {pngPath} to SVG: {ex.Message}");
            }
            finally
            {
                img?.Dispose();
                alpha?.Dispose();
                binary?.Dispose();
                contours?.Dispose();
                hierarchy?.Dispose();
            }
        }

        private void CreateSpaceSvg(string svgPath, int width, int height)
        {
            var svg = new StringBuilder();
            svg.AppendLine($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
            svg.AppendLine("</svg>");
            File.WriteAllText(svgPath, svg.ToString());
        }

        private void CreateSvgFromContours(string svgPath, VectorOfVectorOfPoint contours, Mat hierarchy,
            double simplification, int width, int height)
        {
            var svg = new StringBuilder();
            svg.AppendLine($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
            svg.AppendLine("<g>");

            // Get hierarchy as array
            int[,] hierarchyArray = new int[hierarchy.Cols, 4];
            for (int i = 0; i < hierarchy.Cols; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    hierarchyArray[i, j] = (int)hierarchy.GetData().GetValue(0, i, j);
                }
            }

            for (int i = 0; i < contours.Size; i++)
            {
                // Check if this is an outer contour (no parent)
                if (hierarchyArray[i, 3] == -1)
                {
                    using (var contour = contours[i])
                    using (var approx = new VectorOfPoint())
                    {
                        double epsilon = simplification * CvInvoke.ArcLength(contour, true);
                        CvInvoke.ApproxPolyDP(contour, approx, epsilon, true);

                        if (approx.Size > 0)
                        {
                            var pathData = new StringBuilder();

                            // Add outer contour
                            var points = approx.ToArray();
                            for (int j = 0; j < points.Length; j++)
                            {
                                pathData.Append(j == 0 ? $"M {points[j].X},{points[j].Y}" : $" L {points[j].X},{points[j].Y}");
                            }
                            pathData.Append(" Z");

                            // Add holes (children)
                            int child = hierarchyArray[i, 2];
                            while (child != -1)
                            {
                                using (var holeContour = contours[child])
                                using (var holeApprox = new VectorOfPoint())
                                {
                                    double holeEpsilon = simplification * CvInvoke.ArcLength(holeContour, true);
                                    CvInvoke.ApproxPolyDP(holeContour, holeApprox, holeEpsilon, true);

                                    if (holeApprox.Size > 0)
                                    {
                                        var holePoints = holeApprox.ToArray();
                                        for (int k = 0; k < holePoints.Length; k++)
                                        {
                                            pathData.Append(k == 0 ? $" M {holePoints[k].X},{holePoints[k].Y}" : $" L {holePoints[k].X},{holePoints[k].Y}");
                                        }
                                        pathData.Append(" Z");
                                    }
                                }

                                child = hierarchyArray[child, 0]; // Next sibling
                            }

                            svg.AppendLine($"<path d=\"{pathData}\" fill=\"black\"/>");
                        }
                    }
                }
            }

            svg.AppendLine("</g>");
            svg.AppendLine("</svg>");

            File.WriteAllText(svgPath, svg.ToString());
        }

        private async Task GenerateFontAsync(string convertedFolder, string outputFontPath, string fontName,
            string fontForgeExe, Action<double, string> progressCallback, CancellationToken cancellationToken)
        {
            string scriptContent = CreateFontForgeScript(convertedFolder, outputFontPath, fontName);
            string tempScriptPath = Path.GetTempFileName() + ".py";

            try
            {
                await File.WriteAllTextAsync(tempScriptPath, scriptContent, cancellationToken);

                progressCallback(50, "Starting FontForge processing...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fontForgeExe,
                        Arguments = $"-script \"{tempScriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                var tcs = new TaskCompletionSource<int>();
                var outputLines = new List<string>();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputLines.Add(e.Data);
                        Debug.WriteLine($"FontForge output: {e.Data}");

                        if (e.Data.StartsWith("PROGRESS:"))
                        {
                            try
                            {
                                var parts = e.Data.Split('|');
                                if (parts.Length >= 2)
                                {
                                    var progressPart = parts[0].Substring(9);
                                    if (double.TryParse(progressPart, out double progress))
                                    {
                                        // Clamp progress to 0-100 range and scale to 50-100 for display
                                        progress = Math.Max(0, Math.Min(100, progress));
                                        double scaledProgress = 50 + (progress * 0.5);
                                        string message = string.Join("|", parts.Skip(1));
                                        progressCallback(scaledProgress, message);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error parsing progress: {ex.Message}");
                            }
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"FontForge error: {e.Data}");
                    }
                };

                process.Exited += (sender, e) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                process.EnableRaisingEvents = true;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for completion or cancellation
                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                        tcs.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error cancelling FontForge process: {ex.Message}");
                    }
                }))
                {
                    int exitCode = await tcs.Task;

                    if (exitCode == 0)
                    {
                        progressCallback(100, "Font generation completed successfully!");
                    }
                    else
                    {
                        var errorOutput = string.Join("\n", outputLines.TakeLast(10));
                        throw new Exception($"FontForge script failed with exit code {exitCode}. Last output:\n{errorOutput}");
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempScriptPath))
                    {
                        File.Delete(tempScriptPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting temp script: {ex.Message}");
                }
            }
        }

        private string CreateFontForgeScript(string inputFolder, string outputPath, string fontName)
        {
            // Escape backslashes for Python paths
            string escapedInputFolder = inputFolder.Replace("\\", "\\\\");
            string escapedOutputPath = outputPath.Replace("\\", "\\\\");
            string escapedFontName = fontName.Replace("'", "\\'");

            return $@"
import fontforge
import psMat
import os
import sys
import xml.etree.ElementTree as ET

def log(msg):
    print(msg); sys.stdout.flush()

def progress(p, msg):
    sys.stdout.write(f'PROGRESS:{{p:.2f}}|{{msg}}\n'); sys.stdout.flush()

try:
    font = fontforge.font()
    font.encoding = 'UnicodeFull'
    font.fontname = '{escapedFontName}'.replace(' ', '')
    font.familyname = '{escapedFontName}'
    font.fullname = '{escapedFontName}'
    font.version = '1.0'
    font.copyright = 'Custom Font'

    em_size = 1000
    font.em = em_size
    font.ascent = 800
    font.descent = 200

    symbol_map = {{
        'space': 32, 'exclamation': 33, 'questionmark': 63, 'period': 46, 'comma': 44,
        'colon': 58, 'semicolon': 59, 'hyphen': 45, 'endash': 8211, 'emdash': 8212,
        'ellipsis': 8230, 'degree': 176, 'bullet': 8226, 'middot': 183,
        'leftparenthesis': 40, 'rightparenthesis': 41, 'leftbracket': 91,
        'rightbracket': 93, 'leftbrace': 123, 'rightbrace': 125,
        'angleleft': 171, 'angleright': 187,
        'singlequote': 39, 'doublequote': 34, 'backtick': 96,
        'plus': 43, 'equal': 61, 'caret': 94, 'percent': 37, 'asterisk': 42,
        'divide': 247, 'multiply': 215, 'plusminus': 177,
        'lessthan': 60, 'greaterthan': 62,
        'at': 64, 'hash': 35, 'ampersand': 38, 'underscore': 95,
        'tilde': 126, 'backslash': 92, 'forwardslash': 47, 'verticalbar': 124,
        'copyright': 169, 'trademark': 8482, 'registered': 174,
        'dollar': 36, 'euro': 8364, 'pound': 163, 'yen': 165, 'cent': 162,
    }}
    accented_map = {{
        'lower_e_acute': 233, 'lower_e_grave': 232, 'lower_e_circ': 234, 'lower_e_uml': 235,
        'lower_a_acute': 225, 'lower_a_grave': 224, 'lower_a_circ': 226, 'lower_a_uml': 228, 'lower_a_ring': 229,
        'lower_o_acute': 243, 'lower_o_circ': 244, 'lower_o_uml': 246,
        'lower_u_acute': 250, 'lower_u_grave': 249, 'lower_u_uml': 252,
        'lower_i_acute': 237, 'lower_i_circ': 238,
        'lower_n_tilde': 241, 'lower_c_cedil': 231, 'lower_ss': 223, 'lower_ae': 230,
        'upper_A_uml': 196, 'upper_O_uml': 214, 'upper_U_uml': 220,
        'upper_A_grave': 192, 'upper_A_ring': 197, 'upper_E_acute': 201,
        'upper_N_tilde': 209, 'upper_C_cedil': 199, 'upper_AE': 198,
    }}

    svg_files = [f for f in os.listdir('{escapedInputFolder}') if f.endswith('.svg')]
    total = len(svg_files)

    # Uniform scale: map SVG image height to em. All images are the same square size.
    first_svg = ET.parse(os.path.join('{escapedInputFolder}', svg_files[0])).getroot()
    vb = first_svg.get('viewBox', '').split()
    svg_h = float(vb[3]) if len(vb) >= 4 else float(first_svg.get('height', em_size))
    reference_size = 512 # Somehow the right size for all image resolutions. Don't ask me why. I'm fucking done. Kill me. I beg you.
    scale = (em_size / reference_size) * (2/3)

    # After FontForge imports an SVG it flips Y, so SVG-bottom lands at font y=0 (baseline).
    # We shift everything down by descent so SVG-bottom = y=-descent and SVG-top = y=ascent.
    # This is a fixed coordinate-space correction, identical for every glyph.
    y_offset = -font.descent

    for idx, file in enumerate(svg_files):
        try:
            char_name = os.path.splitext(file)[0]

            unicode_value = None
            if char_name in accented_map:
                unicode_value = accented_map[char_name]
            elif char_name.startswith('upper_') and len(char_name) == 7:
                unicode_value = ord(char_name[6].upper())
            elif char_name.startswith('lower_') and len(char_name) == 7:
                unicode_value = ord(char_name[6].lower())
            elif char_name.isdigit():
                unicode_value = ord(char_name)
            elif char_name in symbol_map:
                unicode_value = symbol_map[char_name]

            if unicode_value is None:
                continue

            progress(50 + (idx / total * 45), f'Processing: {{char_name}}')

            glyph = font.createChar(unicode_value)

            if char_name == 'space':
                glyph.width = em_size // 4
                continue

            glyph.importOutlines(os.path.join('{escapedInputFolder}', file))
            glyph.correctDirection()
            glyph.removeOverlap()

            # Scale to em, then apply fixed coordinate-space offset. Nothing else.
            glyph.transform(psMat.compose(psMat.scale(scale), psMat.translate(0, y_offset)))

            # Trim horizontal whitespace only
            glyph.left_side_bearing = 50
            glyph.right_side_bearing = 50

        except Exception as e:
            log(f'Error processing {{file}}: {{str(e)}}')

    font.os2_winascent = font.ascent
    font.os2_windescent = font.descent
    font.hhea_ascent = font.ascent
    font.hhea_descent = -font.descent

    progress(95, 'Generating font file...')
    font.generate('{escapedOutputPath}')
    font.close()
    log('FONTFORGE_SCRIPT_COMPLETED')

except Exception as e:
    progress(0, f'ERROR: {{str(e)}}')
    sys.exit(1)
";
        }
    }
}