using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VideoBarcode
{
    class Program
    {
        static void Main(string[] args)
        {
            string sDirectory = @"C:\Users\Connor\Desktop\Video Barcode\Revenge of the Sith";

            string sVideoFile = Path.Combine(sDirectory, "Star Wars Episode 3 - Revenge of The Sith (2005).mp4");
            string sJsonFile = Path.Combine(sDirectory, "Revenge of the Sith.json");
            string sGradientFile = Path.Combine(sDirectory, "Revenge of the Sith.jpg");

            // opens the video file (ffmpeg is probably needed)
            var capture = new VideoCapture(sVideoFile);

            Console.WriteLine($"Processing {Path.GetFileName(sVideoFile)}...");
            Console.WriteLine($"Duration: {TimeSpan.FromSeconds(capture.FrameCount / capture.Fps)}");

            List<Color> averageColors;

            // use the JSON file of average colors if it exists, otherwise compute a new list from the capture
            if (File.Exists(sJsonFile))
            {
                averageColors = JsonConvert.DeserializeObject<List<Color>>(File.ReadAllText(sJsonFile));
            }
            else
            {
                averageColors = AverageColorsOfCapture(capture);
            }

            // serialize the average colors list and write it to a file
            File.WriteAllText(sJsonFile, JsonConvert.SerializeObject(averageColors));

            var summarizedColors = SummarizeColorsByTime(averageColors, (int)Math.Round(capture.Fps));

            WriteHistogram(sGradientFile, summarizedColors, summarizedColors.Length / 4, summarizedColors.Length);

            Console.WriteLine("\nFinished.");
        }

        private static List<Color> AverageColorsOfCapture(VideoCapture capture)
        {
            // this list represents the average color of every frame of the video
            var averageColors = new List<Color>();

            // frame image buffers
            var images = new Mat<Vec3b>[Environment.ProcessorCount].Select(i => new Mat<Vec3b>()).ToArray();

            int frameIdx = 0;
            bool finished = false;

            // the list of tasks to find the average color of a frame, one for each logical processor
            var tasks = new List<Task<Color>>();

            while (true)
            {
                // read one frame for each thread
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    capture.Read(images[i]);

                    if (images[i].Empty())
                    {
                        finished = true;
                        break;
                    }

                    tasks.Add(AverageFrameColorHSV(images[i]));

                    frameIdx++;
                }

                // schedule all of the average frame color tasks
                Task.WaitAll(tasks.ToArray());

                // add the results to the running list of average colors
                averageColors.AddRange(tasks.Select(t => t.Result));

                if (finished)
                {
                    break;
                }

                // display progress, formatted to two decimal places
                float progress = (float)frameIdx / capture.FrameCount;
                Console.Write($"\rProgress: {string.Format("{0:0.00}", progress * 100.0)}%");

                tasks.Clear();
            }

            return averageColors;
        }

        private static Task<Color> AverageFrameColorHSV(Mat<Vec3b> image)
        {
            return Task.Run(() =>
            {
                // accumulators for hue, saturation, and value
                var hueCounts = new int[360];
                var saturationAccum = new float[360];
                var valueAccum = new float[360];

                int hue;
                float saturation, value;

                var indexer = image.GetIndexer();

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Vec3b pixel = indexer[y, x];

                        // var color = Color.FromArgb(pixel.Item2, pixel.Item1, pixel.Item0);
                        // ColorHelp.ColorToHSV(color, out double dHue, out saturation, out value);

                        ColorHelp.RGBtoHSV(pixel.Item2, pixel.Item1, pixel.Item0, out float h, out float s, out float v);

                        hue = (int)h;

                        hueCounts[hue] += 1;
                        saturationAccum[hue] += s;
                        valueAccum[hue] += v;
                    }
                }

                // smooth out the hue array by adding neigboring hues
                var smoothHueCounts = SmoothArray(hueCounts, 10);

                // compute the average hue, saturation, and value
                hue = Array.IndexOf(smoothHueCounts, smoothHueCounts.Max());
                saturation = saturationAccum[hue] / hueCounts[hue];
                value = valueAccum[hue] / hueCounts[hue];

                if (double.IsNaN(saturation))
                {
                    saturation = 0;
                }

                if (double.IsNaN(value))
                {
                    value = 0;
                }

                ColorHelp.HSVtoRGB(out float r, out float g, out float b, hue, saturation, value);
                return Color.FromArgb((int)r, (int)g, (int)b);
            });
        }

        private static int[] SmoothArray(int[] inputArr, int factor)
        {
            var outputArr = new int[inputArr.Length];

            for (int i = 0; i < inputArr.Length; i++)
            {
                int start = Math.Max(0, i - factor);
                int end = Math.Min(inputArr.Length - 1, i + factor);

                outputArr[i] = inputArr.Skip(start).Take(end - start + 1).Sum();
            }

            return outputArr;
        }

        private static Color[] SummarizeColorsByTime(List<Color> colors, int fps)
        {
            int totalFrames = colors.Count;
            int numGroups = (int)Math.Ceiling((double)totalFrames / fps);

            var colorGroups = new List<Color>[numGroups].Select(s => new List<Color>()).ToArray();

            for (int i = 0; i < totalFrames; i++)
            {
                colorGroups[i / fps].Add(colors[i]);
            }

            return colorGroups.Select(g =>
            {
                int averageRed = (int)g.Average(c => c.R);
                int averageGreen = (int)g.Average(c => c.G);
                int averageBlue = (int)g.Average(c => c.B);

                return Color.FromArgb(averageRed, averageGreen, averageBlue);
            }).ToArray();
        }

        private static void WriteHistogram(string sGradientFile, Color[] colors, int height, int width)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    var color = colors[i];
                    var pen = new Pen(color, 1);

                    PointF start = new PointF(i, 0);
                    PointF end = new PointF(i, height);

                    graphics.DrawLine(pen, start, end);
                }

                bitmap.Save(sGradientFile, ImageFormat.Jpeg);
            }
        }
    }
}
