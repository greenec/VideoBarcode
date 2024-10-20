using System.Text.Json;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace VideoBarcode;

class Program
{
    static void Main(string[] args)
    {
        string directory = @"/Users/Connor/Desktop/Her";

        string videoFilePath = Path.Combine(directory, "Her (2013).mp4");
        string jsonFilePath = Path.Combine(directory, "Her.argb.uint.json");
        string gradientFilePath = Path.Combine(directory, "Her.jpg");

        // opens the video file (ffmpeg is probably needed)
        var capture = new VideoCapture(videoFilePath);

        Console.WriteLine($"Processing {Path.GetFileName(videoFilePath)}...");
        Console.WriteLine($"Duration: {TimeSpan.FromSeconds(capture.FrameCount / capture.Fps)}");

        List<Color> averageColors;

        // use the JSON file of average colors if it exists, otherwise compute a new list from the capture
        if (File.Exists(jsonFilePath))
        {
            // ImageSharp packs colors from LSB to MSB. Bgra32 is 0xARGB, Argb32 is 0xBGRA, Rgba32 is 0xABGR, etc.
            // Hence the file convention of Title.argb.uint.json for the packed format of Bgra32
            averageColors = JsonSerializer.Deserialize<List<uint>>(File.ReadAllText(jsonFilePath))
                ?.Select(u => new Color(new Bgra32 { PackedValue = u })).ToList()
                ?? throw new InvalidOperationException("Failed to deserialize JSON file with color data");
        }
        else
        {
            if (!File.Exists(videoFilePath))
            {
                throw new InvalidOperationException($"{videoFilePath} not found");
            }

            averageColors = AverageColorsOfCapture(capture);

            // Serialize the average colors list and write it to a file.
            // Note that this is the "packed" color format used by ImageSharp, different from the bit-shifted ARGB int that System.Drawing.Color uses.
            // From MSB to LSB, Bgra32 will be packed into the uint as 0xARGB
            File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(averageColors.Select(c => c.ToPixel<Bgra32>().PackedValue).ToArray()));
        }

        var summarizedColors = SummarizeColorsByTime(averageColors, (int)Math.Round(capture.Fps));

        WriteHistogram(gradientFilePath, summarizedColors, summarizedColors.Length / 4, summarizedColors.Length);

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
            Task.WaitAll([.. tasks]);

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
            return Color.FromRgb((byte)r, (byte)g, (byte)b);
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

        var colorGroups = new List<Bgra32>[numGroups].Select(s => new List<Bgra32>()).ToArray();

        for (int i = 0; i < totalFrames; i++)
        {
            colorGroups[i / fps].Add(colors[i].ToPixel<Bgra32>());
        }

        return colorGroups.Select(g =>
        {
            byte averageRed = (byte)g.Average(c => c.R);
            byte averageGreen = (byte)g.Average(c => c.G);
            byte averageBlue = (byte)g.Average(c => c.B);

            return Color.FromRgb(averageRed, averageGreen, averageBlue);
        }).ToArray();
    }

    private static void WriteHistogram(string gradientFilePath, Color[] colors, int height, int width)
    {
        using Image image = new Image<Bgra32>(width, height);

        for (int i = 0; i < colors.Length; i++)
        {
            image.Mutate(ctx => ctx.Fill(colors[i], new Rectangle(x: i, y: 0, width: 1, height: height)));
        }

        image.SaveAsJpeg(gradientFilePath);
    }
}
