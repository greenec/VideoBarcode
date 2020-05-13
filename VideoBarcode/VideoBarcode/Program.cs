using Accord.Math;
using Accord.Video.FFMPEG;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VideoBarcode
{
    class Program
    {
        static void Main(string[] args)
        {
            string sVideoFile = @"C:\Users\Connor\Desktop\1917.mp4";
            string sGradientFile = @"C:\Users\Connor\Desktop\1917_gradient.jpg";

            var colors = new List<Color>();

            using (var vFReader = new VideoFileReader())
            {
                vFReader.Open(sVideoFile);

                for (Rational f = 0; f < vFReader.FrameCount; f += vFReader.FrameRate * 300)
                {
                    Bitmap frame = vFReader.ReadVideoFrame((int)f);

                    var averageColor = AverageColorOfThumbnail(frame);
                    colors.Add(averageColor);

                    // display progress
                    double progress = Math.Round(100.0 * (double)f / vFReader.FrameCount, 2);
                    Console.WriteLine($"Progress: {progress}%");
                }

                vFReader.Close();
            }

            CreateGradient(sGradientFile, colors.ToArray(), 1080, 1920);
        }

        private static Color AverageColorOfThumbnail(Bitmap bitmap)
        {
            // accumulators used to sum pixel color channels
            long redSum = 0;
            long greenSum = 0;
            long blueSum = 0;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);

                    redSum += pixel.R;
                    greenSum += pixel.G;
                    blueSum += pixel.B;
                }
            }

            long numPixels = bitmap.Width * bitmap.Height;

            // average colors out by the total number of pixels
            int averageRed = (int)(redSum / numPixels);
            int averageGreen = (int)(greenSum / numPixels);
            int averageBlue = (int)(blueSum / numPixels);

            return Color.FromArgb(averageRed, averageGreen, averageBlue);
        }

        private static void CreateGradient(string sGradientFile, Color[] colors, int height, int width)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.Blue, Color.Red, LinearGradientMode.Horizontal))
            {
                var positions = new float[colors.Length];

                float position = 0;
                float step = 1f / (positions.Length - 1);

                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = position;
                    position += step;
                }

                brush.InterpolationColors = new ColorBlend(colors.Length)
                {
                    Colors = colors,
                    Positions = positions
                };

                brush.SetSigmaBellShape(0.5f);
                graphics.FillRectangle(brush, new Rectangle(0, 0, width, height));
                bitmap.Save(sGradientFile, ImageFormat.Jpeg);
            }
        }
    }
}
