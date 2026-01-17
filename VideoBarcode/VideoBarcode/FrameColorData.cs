using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoBarcode;

internal class FrameColorData
{
    [JsonPropertyName("P")]
    public uint PackedBgra32 { get; set; }

    [JsonPropertyName("T")]
    public int TimestampMillisec { get; set; }

    [JsonIgnore]
    internal Bgra32 Bgra32 => new() { PackedValue = PackedBgra32 };

    public FrameColorData()
    {

    }

    internal FrameColorData(Color color, int timestampMsec)
    {
        PackedBgra32 = color.ToPixel<Bgra32>().PackedValue;
        TimestampMillisec = timestampMsec;
    }

    /// <summary>
    /// ImageSharp packs colors from LSB to MSB. Bgra32 is 0xARGB, Argb32 is 0xBGRA, Rgba32 is 0xABGR, etc.<br />
    /// Hence the file convention of Title.argb.uint.json for the packed format of Bgra32.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal static List<FrameColorData> Deserialize(string jsonFilePath)
        => JsonSerializer.Deserialize<List<FrameColorData>>(File.ReadAllText(jsonFilePath))
            ?? throw new InvalidOperationException("Failed to deserialize JSON file with color data");
}
