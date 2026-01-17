using System.Text.Json;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VideoBarcode;

int sampleMsec = 1000;

string directory = @"/Users/Connor/Desktop/Video Barcode/Her";

string videoFilePath = Path.Combine(directory, "Her (2013).mp4");
string jsonFilePath = Path.Combine(directory, "Her.argb.uint.json");
string gradientFilePath = Path.Combine(directory, "Her.png");

if (!File.Exists(videoFilePath))
{
    throw new FileNotFoundException($"{videoFilePath} not found");
}

// opens the video file (ffmpeg may not be needed as long as the native bindings are available for the platform)
var capture = new VideoCapture(videoFilePath);

Console.WriteLine($"Processing {Path.GetFileName(videoFilePath)}...");
Console.WriteLine($"Frame Rate: {capture.Fps} FPS, Sampling every {sampleMsec} ms");
Console.WriteLine($"Duration: {TimeSpan.FromSeconds(capture.FrameCount / capture.Fps)}");

IReadOnlyList<FrameColorData> averageColors;

// use the JSON file of average colors if it exists, otherwise compute a new list from the capture
if (File.Exists(jsonFilePath))
{
    averageColors = FrameColorData.Deserialize(jsonFilePath);
}
else
{
    averageColors = AverageColorsOfCapture(capture, sampleMsec);

    // Serialize the average colors list and write it to a file.
    // Note that this is the "packed" color format used by ImageSharp, different from the bit-shifted ARGB int that System.Drawing.Color uses.
    // From MSB to LSB, Bgra32 will be packed into the uint as 0xARGB. LSB to MSB is 0xBGRA, hence the type name.
    File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(averageColors));
}

var summarizedColors = SummarizeColorsByTime(averageColors);

WriteHistogram(gradientFilePath, summarizedColors, height: summarizedColors.Count / 4, width: summarizedColors.Count);

Console.WriteLine("\nFinished.");

static IReadOnlyList<FrameColorData> AverageColorsOfCapture(VideoCapture capture, int sampleMsec)
{
    int captureLengthSeconds = Convert.ToInt32(capture.FrameCount / capture.Fps);

    // this list represents the average color of every frame of the video
    var averageColors = new List<FrameColorData>(capacity: captureLengthSeconds + 1);

    // Mat is IDisposable
    using var image = new Mat<Vec3b>();
    while (true)
    {
        capture.Read(image);
        if (image.Empty())
        {
            break;
        }

        // read PosMsec here, because advancing the time seems to cause the value to decrease until the next capture.Read() call
        int timestampMillis = capture.PosMsec;

        averageColors.Add(new FrameColorData(AverageFrameColorHSV(image), timestampMillis));

        // advance the time to the next sample
        capture.PosMsec += sampleMsec;

        // display progress, formatted to two decimal places. \r is a carriage return without a newline to overwrite the previous line
        float progress = (float)Math.Min(capture.PosFrames, capture.FrameCount) / capture.FrameCount;
        Console.Write($"\rProgress: {string.Format("{0:0.00}", progress * 100.0)}% {TimeSpan.FromMilliseconds(timestampMillis)}");
    }

    return averageColors;
}

static Color AverageFrameColorHSV(Mat<Vec3b> image)
{
    // accumulators for hue, saturation, and value
    int[] hueCounts = new int[360];
    float[] saturationAccum = new float[360];
    float[] valueAccum = new float[360];

    int hue;
    float saturation, value;

    var indexer = image.GetIndexer();

    for (int y = 0; y < image.Height; y++)
    {
        for (int x = 0; x < image.Width; x++)
        {
            Vec3b pixel = indexer[y, x];
            var (h, s, v) = ColorHelp.RGBtoHSV(pixel.Item2, pixel.Item1, pixel.Item0);

            // Use cast instead of Convert because Convert will round up to 360 (out of bounds)
            hue = (int)h;

            hueCounts[hue] += 1;
            saturationAccum[hue] += s;
            valueAccum[hue] += v;
        }
    }

    // smooth out the hue array by adding neigboring hues
    var smoothHueCounts = SmoothArray(hueCounts, factor: 10);

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

    var (r, g, b) = ColorHelp.HSVtoRGB(hue, saturation, value);
    return Color.FromRgb((byte)r, (byte)g, (byte)b);
}

static int[] SmoothArray(int[] inputArr, int factor)
{
    var outputArr = new int[inputArr.Length];

    for (int i = 0; i < inputArr.Length; i++)
    {
        int startIdx = Math.Max(0, i - factor);
        int end = Math.Min(inputArr.Length - 1, i + factor);

        outputArr[i] = inputArr.Skip(startIdx).Take(end - startIdx + 1).Sum();
    }

    return outputArr;
}

static IReadOnlyList<Color> SummarizeColorsByTime(IReadOnlyList<FrameColorData> frameData)
{
    int totalFrames = frameData.Count;
    int numGroups = (frameData.Last().TimestampMillisec / 1_000) + 1;

    var colorGroups = Enumerable.Range(0, numGroups).Select(_ => new List<Bgra32>()).ToList();

    foreach (FrameColorData entry in frameData)
    {
        int second = entry.TimestampMillisec / 1_000;
        colorGroups[second].Add(entry.Bgra32);
    }

    return colorGroups.Select(g =>
    {
        if (g.Count == 0)
        {
            return Color.Black;
        }

        byte averageRed = Convert.ToByte(g.Average(c => c.R));
        byte averageGreen = Convert.ToByte(g.Average(c => c.G));
        byte averageBlue = Convert.ToByte(g.Average(c => c.B));

        return Color.FromRgb(averageRed, averageGreen, averageBlue);
    }).ToList();
}

static void WriteHistogram(string gradientFilePath, IReadOnlyList<Color> colors, int height, int width)
{
    using Image image = new Image<Bgra32>(width, height);

    for (int i = 0; i < colors.Count; i++)
    {
        image.Mutate(ctx => ctx.Fill(colors[i], new Rectangle(x: i, y: 0, width: 1, height: height)));
    }

    image.SaveAsPng(gradientFilePath);
}