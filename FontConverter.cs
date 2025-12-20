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
            // Uppercase letters
            "upper_A", "upper_B", "upper_C", "upper_D", "upper_E", "upper_F", "upper_G", "upper_H",
            "upper_I", "upper_J", "upper_K", "upper_L", "upper_M", "upper_N", "upper_O", "upper_P",
            "upper_Q", "upper_R", "upper_S", "upper_T", "upper_U", "upper_V", "upper_W", "upper_X",
            "upper_Y", "upper_Z",
            // Lowercase letters
            "lower_a", "lower_b", "lower_c", "lower_d", "lower_e", "lower_f", "lower_g", "lower_h",
            "lower_i", "lower_j", "lower_k", "lower_l", "lower_m", "lower_n", "lower_o", "lower_p",
            "lower_q", "lower_r", "lower_s", "lower_t", "lower_u", "lower_v", "lower_w", "lower_x",
            "lower_y", "lower_z",
            // Numbers
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            // Symbols
            "space", "exclamation", "questionmark", "period", "comma", "colon", "semicolon",
            "hyphen", "plus", "equal", "at", "hash", "dollar", "percent", "caret",
            "ampersand", "asterisk", "leftparenthesis", "rightparenthesis",
            "underscore", "backtick", "tilde", "leftbracket", "rightbracket",
            "leftbrace", "rightbrace", "backslash", "forwardslash", "verticalbar",
            "lessthan", "greaterthan", "singlequote", "doublequote"
        };

        private readonly Dictionary<string, int> _symbolMap = new Dictionary<string, int>
        {
            {"space", 32}, {"exclamation", 33}, {"questionmark", 63}, {"period", 46}, {"comma", 44},
            {"colon", 58}, {"semicolon", 59}, {"hyphen", 45}, {"plus", 43},
            {"equal", 61}, {"at", 64}, {"hash", 35}, {"dollar", 36},
            {"percent", 37}, {"caret", 94}, {"ampersand", 38}, {"asterisk", 42},
            {"leftparenthesis", 40}, {"rightparenthesis", 41}, {"underscore", 95},
            {"backtick", 96}, {"tilde", 126}, {"leftbracket", 91},
            {"rightbracket", 93}, {"leftbrace", 123}, {"rightbrace", 125},
            {"backslash", 92}, {"forwardslash", 47}, {"verticalbar", 124},
            {"lessthan", 60}, {"greaterthan", 62}, {"singlequote", 39}, {"doublequote", 34}
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
import os
import sys
import traceback

def log_message(message):
    print(message)
    sys.stdout.flush()

def update_progress(progress, message):
    sys.stdout.write(f'PROGRESS:{{progress:.2f}}|{{message}}\n')
    sys.stdout.flush()

try:
    log_message('Starting font generation process...')

    font = fontforge.font()

    try:
        font.encoding = 'UnicodeFull'
        font.fontname = '{escapedFontName}'.replace(' ', '')
        font.familyname = '{escapedFontName}'
        font.fullname = '{escapedFontName}'
        font.version = '1.0'
        font.copyright = 'Custom Font'
        
        em_size = 1000
        font.em = em_size
        font.ascent = int(em_size * 0.2)
        font.descent = int(em_size * 0.2)
        
        symbol_map = {{
            'space': 32, 'exclamation': 33, 'questionmark': 63, 'period': 46, 'comma': 44,
            'colon': 58, 'semicolon': 59, 'hyphen': 45, 'plus': 43,
            'equal': 61, 'at': 64, 'hash': 35, 'dollar': 36,
            'percent': 37, 'caret': 94, 'ampersand': 38, 'asterisk': 42,
            'leftparenthesis': 40, 'rightparenthesis': 41, 'underscore': 95,
            'backtick': 96, 'tilde': 126, 'leftbracket': 91,
            'rightbracket': 93, 'leftbrace': 123, 'rightbrace': 125,
            'backslash': 92, 'forwardslash': 47, 'verticalbar': 124,
            'lessthan': 60, 'greaterthan': 62, 'singlequote': 39, 'doublequote': 34
        }}

        svg_files = [f for f in os.listdir('{escapedInputFolder}') if f.endswith('.svg')]
        total_files = len(svg_files)

        if total_files == 0:
            raise Exception('No SVG files found in the input folder')

        log_message(f'Found {{total_files}} SVG files to process')
        glyph_count = 0

        for index, file in enumerate(svg_files):
            try:
                file_path = os.path.join('{escapedInputFolder}', file)
                char_name = os.path.splitext(file)[0]

                unicode_value = None
                if char_name.startswith('upper_'):
                    unicode_value = ord(char_name.split('_')[1].upper())
                elif char_name.startswith('lower_'):
                    unicode_value = ord(char_name.split('_')[1].lower())
                elif char_name.isdigit():
                    unicode_value = ord(char_name)
                elif char_name in symbol_map:
                    unicode_value = symbol_map[char_name]

                if unicode_value is None:
                    log_message(f'Skipping {{file}} - couldn\'t determine Unicode value')
                    continue

                log_message(f'Processing {{file}} - Unicode value: {{unicode_value}} ({{chr(unicode_value) if 32 <= unicode_value < 127 else hex(unicode_value)}})')

                glyph = font.createChar(unicode_value)

                if char_name == 'space':
                    glyph.width = em_size // 8
                    glyph_count += 1
                    log_message(f'Space character created with width {{glyph.width}}')
                    continue

                # Import the SVG
                glyph.importOutlines(file_path)
                
                glyph.correctDirection()
                glyph.removeOverlap()
                glyph.simplify()
                glyph.left_side_bearing = glyph.right_side_bearing = em_size // 50

                glyph_count += 1
                progress = (index + 1) / total_files * 90
                update_progress(progress, f'Processed glyph: {{chr(unicode_value) if 32 <= unicode_value < 127 else hex(unicode_value)}}')

            except Exception as e:
                log_message(f'Error processing glyph {{file}}: {{str(e)}}')
                log_message(traceback.format_exc())
                continue

        if glyph_count == 0:
            raise Exception('No glyphs were successfully added to the font')

        log_message(f'Successfully added {{glyph_count}} glyphs to the font')

        # Set font metrics
        font.os2_winascent = font.ascent
        font.os2_windescent = font.descent
        font.os2_typoascent = font.ascent
        font.os2_typodescent = -font.descent
        font.os2_typolinegap = 0
        font.hhea_ascent = font.ascent
        font.hhea_descent = -font.descent
        font.hhea_linegap = 0

        update_progress(95, 'Generating font file...')

        font.generate('{escapedOutputPath}')

        if os.path.exists('{escapedOutputPath}'):
            file_size = os.path.getsize('{escapedOutputPath}')
            log_message(f'Font file created successfully. Size: {{file_size}} bytes')
        else:
            raise Exception('Font file was not created despite no errors')

    except Exception as e:
        log_message(f'Error during font generation: {{str(e)}}')
        log_message(traceback.format_exc())
        raise

    update_progress(100, f'Font generation completed! {{glyph_count}} glyphs added.')

except Exception as e:
    error_msg = f'An error occurred: {{str(e)}}'
    log_message(error_msg)
    traceback.print_exc()
    update_progress(0, f'ERROR: {{error_msg}}')
    sys.exit(1)

finally:
    if 'font' in locals():
        try:
            font.close()
        except Exception as e:
            log_message(f'Error closing font object: {{str(e)}}')
    log_message('FONTFORGE_SCRIPT_COMPLETED')
";
        }
    }
}