// Generates src/DropMorph.App/Assets/Icons/dropmorph.ico and dropmorph.png from dropmorph.svg (the drop logo).
// Run from the repository root:  dotnet run build/tools/make-icon.cs
#:package Magick.NET-Q8-AnyCPU

using System.Text;
using ImageMagick;

var outDir = Path.Combine("src", "DropMorph.App", "Assets", "Icons");
var svg = File.ReadAllText(Path.Combine(outDir, "dropmorph.svg"));

var settings = new MagickReadSettings { BackgroundColor = MagickColors.Transparent, Format = MagickFormat.Svg };
using var master = new MagickImage(Encoding.UTF8.GetBytes(svg), settings);
master.Write(Path.Combine(outDir, "dropmorph.png"), MagickFormat.Png);

// One image + "auto-resize" makes ImageMagick embed every size into a single .ico file.
master.Settings.SetDefine(MagickFormat.Icon, "auto-resize", "256,64,48,32,24,16");
master.Write(Path.Combine(outDir, "dropmorph.ico"), MagickFormat.Icon);
Console.WriteLine($"Icons written to {outDir}");
