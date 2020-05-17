using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            string sVideoFile = @"C:\Users\Connor\Desktop\1917.mp4";
            string sJsonFile = @"C:\Users\Connor\Desktop\1917.json";
            string sGradientFile = @"C:\Users\Connor\Desktop\1917.jpg";

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

            CreateGradient(sGradientFile, averageColors.ToArray(), 2000, 8000);
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

                    tasks.Add(AverageColorOfThumbnail(images[i]));

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
                Console.SetCursorPosition(0, 2);
                Console.WriteLine($"Progress: {string.Format("{0:0.00}", progress * 100.0)}%");

                tasks.Clear();
            }

            return averageColors;
        }

        private static Task<Color> AverageColorOfThumbnail(Mat<Vec3b> image)
        {
            return Task.Run(() =>
            {
                // accumulators used to sum pixel color channels
                long redSum = 0;
                long greenSum = 0;
                long blueSum = 0;

                var indexer = image.GetIndexer();

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Vec3b color = indexer[y, x];

                        blueSum += color.Item0;
                        greenSum += color.Item1;
                        redSum += color.Item2;
                    }
                }

                long numPixels = image.Width * image.Height;

                // average colors out by the total number of pixels
                int averageRed = (int)(redSum / numPixels);
                int averageGreen = (int)(greenSum / numPixels);
                int averageBlue = (int)(blueSum / numPixels);

                return Color.FromArgb(averageRed, averageGreen, averageBlue);
            });
        }

        private static void CreateGradient(string sGradientFile, Color[] colors, int height, int width)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), colors.First(), colors.Last(), LinearGradientMode.Horizontal))
            {
                var positions = new float[colors.Length];

                float position = 0;
                float step = 1f / (positions.Length - 1);

                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = Math.Min(position, 1f);
                    position += step;
                }

                brush.InterpolationColors = new ColorBlend(colors.Length)
                {
                    Colors = colors,
                    Positions = positions
                };

                graphics.FillRectangle(brush, new Rectangle(0, 0, width, height));
                bitmap.Save(sGradientFile, ImageFormat.Jpeg);
            }
        }
    }
}
