Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing

$code = @'
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class IconBuilder
{
    public static void GenerateAppIcon(string outputPath)
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] pngData = new byte[sizes.Length][];

        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // Background dark squircle
                double radius = size * 0.22;
                var bgBrush = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#181D26"),
                    (Color)ColorConverter.ConvertFromString("#0B0D11"),
                    new Point(0, 0), new Point(1, 1));

                var borderPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3140")), Math.Max(1.0, size * 0.035));
                dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(size * 0.02, size * 0.02, size * 0.96, size * 0.96), radius, radius);

                // Brand Logo Geometry
                var geom = Geometry.Parse("M4,2 A2,2 0 0,0 2,4 L2,20 A2,2 0 0,0 4,22 L11,22 A2,2 0 0,0 13,20 L13,15.5 L19.5,20.5 A2,2 0 0,0 22.5,18.8 L22.5,5.2 A2,2 0 0,0 19.5,3.5 L13,8.5 L13,4 A2,2 0 0,0 11,2 Z M5,5 L10,5 L10,19 L5,19 Z M13.5,11.8 L20,7.3 L20,16.7 L13.5,12.2 Z").Clone();

                double pad = size * 0.20;
                double targetW = size - (pad * 2);
                double scale = targetW / 24.0;

                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(scale, scale));
                transform.Children.Add(new TranslateTransform(pad, pad));
                geom.Transform = transform;

                var fgBrush = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#38BDF8"),
                    (Color)ColorConverter.ConvertFromString("#0284C7"),
                    new Point(0, 0), new Point(0.5, 1));

                dc.DrawGeometry(fgBrush, null, geom);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                pngData[i] = ms.ToArray();
            }
        }

        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0); // reserved
            bw.Write((short)1); // icon type
            bw.Write((short)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write((int)pngData[i].Length);
                bw.Write(offset);
                offset += pngData[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngData[i]);
            }
        }

        File.WriteAllBytes(Path.ChangeExtension(outputPath, ".png"), pngData[sizes.Length - 1]);
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies PresentationCore, PresentationFramework, WindowsBase, System.Drawing, System.Xaml

$outPath = Join-Path $PSScriptRoot "..\src\NuvioPlayer\Assets\app.ico"
$assetsDir = Split-Path $outPath
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
}

[IconBuilder]::GenerateAppIcon($outPath)
Write-Host "Generated app icon successfully: $outPath ($((Get-Item $outPath).Length) bytes)"
