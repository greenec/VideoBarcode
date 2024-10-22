using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace VideoBarcode;

class FrameColorData
{
    [JsonPropertyName("P")]
    public uint PackedBgra32 { get; set; }

    [JsonPropertyName("T")]
    public int TimestampMsec { get; set; }

    [JsonIgnore]
    internal Bgra32 Bgra32 => new() { PackedValue = PackedBgra32 };

    public FrameColorData()
    {

    }

    internal FrameColorData(Color color, int timestampMsec)
    {
        PackedBgra32 = color.ToPixel<Bgra32>().PackedValue;
        TimestampMsec = timestampMsec;
    }
}

class Program
{
    static void Main(string[] args)
    {
        int sampleMsec = 1000;

        string directory = @"/Users/Connor/Desktop/Her";

        string videoFilePath = Path.Combine(directory, "Her (2013).mp4");
        string jsonFilePath = Path.Combine(directory, "Her.argb.uint.json");
        string gradientFilePath = Path.Combine(directory, "Her.jpg");

        // opens the video file (ffmpeg may not be needed as long as the native bindings are available for the platform)
        var capture = new VideoCapture(videoFilePath);

        Console.WriteLine($"Processing {Path.GetFileName(videoFilePath)}...");
        Console.WriteLine($"Frame Rate: {capture.Fps} FPS, Sampling every {sampleMsec} ms");
        Console.WriteLine($"Duration: {TimeSpan.FromSeconds(capture.FrameCount / capture.Fps)}");

        List<FrameColorData> averageColors;

        // use the JSON file of average colors if it exists, otherwise compute a new list from the capture
        if (File.Exists(jsonFilePath))
        {
            // ImageSharp packs colors from LSB to MSB. Bgra32 is 0xARGB, Argb32 is 0xBGRA, Rgba32 is 0xABGR, etc.
            // Hence the file convention of Title.argb.uint.json for the packed format of Bgra32
            averageColors = JsonSerializer.Deserialize<List<FrameColorData>>(File.ReadAllText(jsonFilePath))
                ?? throw new InvalidOperationException("Failed to deserialize JSON file with color data");
        }
        else
        {
            if (!File.Exists(videoFilePath))
            {
                throw new InvalidOperationException($"{videoFilePath} not found");
            }

            averageColors = AverageColorsOfCapture(capture, sampleMsec);

            // Serialize the average colors list and write it to a file.
            // Note that this is the "packed" color format used by ImageSharp, different from the bit-shifted ARGB int that System.Drawing.Color uses.
            // From MSB to LSB, Bgra32 will be packed into the uint as 0xARGB. LSB to MSB is 0xBGRA, hence the type name.
            File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(averageColors));
        }

        var summarizedColors = SummarizeColorsByTime(averageColors);

        WriteHistogram(gradientFilePath, summarizedColors, summarizedColors.Length / 4, summarizedColors.Length);

        Console.WriteLine("\nFinished.");
    }

    private static List<FrameColorData> AverageColorsOfCapture(VideoCapture capture, int sampleMsec)
    {
        // this list represents the average color of every frame of the video
        var averageColors = new List<FrameColorData>();

        var image = new Mat<Vec3b>();
        while (true)
        {
            // read one frame for each thread
            capture.Read(image);

            if (image.Empty())
            {
                break;
            }

            averageColors.Add(new FrameColorData(AverageFrameColorHSV(image), capture.PosMsec));

            capture.PosMsec += sampleMsec;

            // display progress, formatted to two decimal places
            float progress = (float)Math.Min(capture.PosFrames, capture.FrameCount) / capture.FrameCount;
            Console.Write($"\rProgress: {string.Format("{0:0.00}", progress * 100.0)}% {TimeSpan.FromMilliseconds(capture.PosMsec)}");
        }

        return averageColors;
    }

    private static Color AverageFrameColorHSV(Mat<Vec3b> image)
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

                // Use cast instead of Convert because Convert will round up to 360 (out of bounds)
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

    private static Color[] SummarizeColorsByTime(List<FrameColorData> frameData)
    {
        int totalFrames = frameData.Count;
        int numGroups = (frameData.Last().TimestampMsec / 1_000) + 1;

        var colorGroups = Enumerable.Range(0, numGroups).Select(_ => new List<Bgra32>()).ToArray();

        foreach (FrameColorData entry in frameData)
        {
            int second = entry.TimestampMsec / 1_000;
            colorGroups[second].Add(entry.Bgra32);
        }

        return colorGroups.Select(g =>
        {
            byte averageRed = Convert.ToByte(g.Average(c => c.R));
            byte averageGreen = Convert.ToByte(g.Average(c => c.G));
            byte averageBlue = Convert.ToByte(g.Average(c => c.B));

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
