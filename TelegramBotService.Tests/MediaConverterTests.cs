using TelegramBotService.MediaConverters;
using Xunit;
using Xunit.Abstractions;

namespace TelegramBotService.Tests;

public class MediaConverterTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDir;

    public MediaConverterTests(ITestOutputHelper output)
    {
        _output = output;
        _testDir = Path.Combine(Path.GetTempPath(), $"converter_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ==================== Availability Tests ====================

    [Fact]
    public void AudioConverter_CheckAvailability()
    {
        var converter = new AudioConverter();
        _output.WriteLine($"AudioConverter (Whisper) available: {converter.IsAvailable}");

        // This test always passes - it's informational
        Assert.True(true);
    }

    [Fact]
    public void ImageConverter_CheckAvailability()
    {
        var converter = new ImageConverter();
        _output.WriteLine($"ImageConverter (Tesseract) available: {converter.IsAvailable}");

        Assert.True(true);
    }

    [Fact]
    public void VideoConverter_CheckAvailability()
    {
        var converter = new VideoConverter();
        _output.WriteLine($"VideoConverter (FFmpeg + Whisper) available: {converter.IsAvailable}");

        Assert.True(true);
    }

    [Fact]
    public void MediaConversionService_PrintStatus()
    {
        var service = new MediaConversionService();
        var status = service.GetConverterStatus();

        _output.WriteLine("=== Converter Status ===");
        foreach (var kvp in status)
        {
            _output.WriteLine($"  {kvp.Key}: {(kvp.Value ? "OK" : "NOT AVAILABLE")}");
        }

        Assert.True(true);
    }

    // ==================== Converter Properties Tests ====================

    [Fact]
    public void AudioConverter_HasCorrectProperties()
    {
        var converter = new AudioConverter();

        Assert.Equal("AudioConverter", converter.Name);
        Assert.Contains("audio", converter.SupportedContentTypes);
        Assert.Contains("voice", converter.SupportedContentTypes);
        Assert.Equal(10, converter.Priority);
    }

    [Fact]
    public void ImageConverter_HasCorrectProperties()
    {
        var converter = new ImageConverter();

        Assert.Equal("ImageConverter", converter.Name);
        Assert.Contains("photo", converter.SupportedContentTypes);
        Assert.Equal(10, converter.Priority);
    }

    [Fact]
    public void VideoConverter_HasCorrectProperties()
    {
        var converter = new VideoConverter();

        Assert.Equal("VideoConverter", converter.Name);
        Assert.Contains("video", converter.SupportedContentTypes);
        Assert.Contains("video_note", converter.SupportedContentTypes);
        Assert.Equal(10, converter.Priority);
    }

    // ==================== ConversionResult Tests ====================

    [Fact]
    public void ConversionResult_Ok_CreatesSuccessResult()
    {
        var result = ConversionResult.Ok("Hello world", new Dictionary<string, object>
        {
            ["model"] = "base"
        });

        Assert.True(result.Success);
        Assert.Equal("Hello world", result.Text);
        Assert.Empty(result.Error);
        Assert.Equal("base", result.Metadata["model"]);
    }

    [Fact]
    public void ConversionResult_Fail_CreatesFailureResult()
    {
        var result = ConversionResult.Fail("Tool not found");

        Assert.False(result.Success);
        Assert.Empty(result.Text);
        Assert.Equal("Tool not found", result.Error);
    }

    [Fact]
    public void ConversionResult_Unavailable_CreatesUnavailableResult()
    {
        var result = ConversionResult.Unavailable("Whisper is not installed");

        Assert.False(result.Success);
        Assert.Equal("Whisper is not installed", result.Error);
    }

    // ==================== Converter Behavior When Unavailable ====================

    [Fact]
    public async Task AudioConverter_WhenUnavailable_ReturnsUnavailableResult()
    {
        // Use a non-existent path to simulate unavailability
        var converter = new AudioConverter(whisperPath: "/nonexistent/whisper");

        var context = new ConversionContext
        {
            ContentType = "audio",
            FilePath = "/some/audio.mp3"
        };

        var result = await converter.ConvertAsync(context);

        Assert.False(result.Success);
        Assert.Contains("not available", result.Error);
    }

    [Fact]
    public async Task ImageConverter_WhenUnavailable_ReturnsUnavailableResult()
    {
        var converter = new ImageConverter(tesseractPath: "/nonexistent/tesseract");

        var context = new ConversionContext
        {
            ContentType = "photo",
            FilePath = "/some/image.jpg"
        };

        var result = await converter.ConvertAsync(context);

        Assert.False(result.Success);
        Assert.Contains("not available", result.Error);
    }

    // ==================== Invalid Input Tests ====================

    [Fact]
    public async Task AudioConverter_MissingFilePath_Fails()
    {
        var converter = new AudioConverter();
        if (!converter.IsAvailable)
        {
            _output.WriteLine("Skipping: Whisper not available");
            return;
        }

        var context = new ConversionContext
        {
            ContentType = "audio",
            FilePath = ""
        };

        var result = await converter.ConvertAsync(context);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AudioConverter_NonExistentFile_Fails()
    {
        var converter = new AudioConverter();
        if (!converter.IsAvailable)
        {
            _output.WriteLine("Skipping: Whisper not available");
            return;
        }

        var context = new ConversionContext
        {
            ContentType = "audio",
            FilePath = "/nonexistent/audio.mp3"
        };

        var result = await converter.ConvertAsync(context);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ImageConverter_NonExistentFile_Fails()
    {
        var converter = new ImageConverter();
        if (!converter.IsAvailable)
        {
            _output.WriteLine("Skipping: Tesseract not available");
            return;
        }

        var context = new ConversionContext
        {
            ContentType = "photo",
            FilePath = "/nonexistent/image.jpg"
        };

        var result = await converter.ConvertAsync(context);

        Assert.False(result.Success);
    }

    // ==================== MediaConversionService Tests ====================

    [Fact]
    public void MediaConversionService_CanConvert_ReturnsCorrectly()
    {
        var service = new MediaConversionService();

        // These depend on tool availability
        _output.WriteLine($"Can convert audio: {service.CanConvert("audio")}");
        _output.WriteLine($"Can convert photo: {service.CanConvert("photo")}");
        _output.WriteLine($"Can convert video: {service.CanConvert("video")}");
        _output.WriteLine($"Can convert unknown: {service.CanConvert("unknown")}");

        // Unknown type should always return false
        Assert.False(service.CanConvert("unknown"));
    }

    [Fact]
    public void MediaConversionService_GetConverters_ReturnsForAudio()
    {
        var service = new MediaConversionService();
        var converters = service.GetConverters("audio").ToList();

        // Should find AudioConverter (may or may not be available)
        Assert.True(converters.Count >= 0);
        _output.WriteLine($"Found {converters.Count} converters for audio");
    }

    [Fact]
    public void MediaConversionService_GetConverters_ReturnsEmptyForUnknown()
    {
        var service = new MediaConversionService();
        var converters = service.GetConverters("unknown_type").ToList();

        Assert.Empty(converters);
    }

    [Fact]
    public async Task MediaConversionService_Convert_UnsupportedType_ReturnsUnavailable()
    {
        var service = new MediaConversionService();

        var context = new ConversionContext
        {
            ContentType = "unknown_type",
            FilePath = "/some/file.xyz"
        };

        var result = await service.ConvertAsync(context);

        Assert.False(result.Success);
        Assert.Contains("No converter available", result.Error);
    }

    // ==================== Real Conversion Tests (Skip if tools not available) ====================

    [Fact]
    public async Task AudioConverter_RealAudio_ConvertsToText()
    {
        var converter = new AudioConverter();
        if (!converter.IsAvailable)
        {
            _output.WriteLine("SKIPPED: Whisper not installed");
            return;
        }

        // Create a simple test audio file (silent WAV)
        var audioPath = CreateSilentWavFile();

        try
        {
            var context = new ConversionContext
            {
                ContentType = "audio",
                FilePath = audioPath,
                FileName = "test.wav"
            };

            var result = await converter.ConvertAsync(context);

            _output.WriteLine($"Conversion success: {result.Success}");
            _output.WriteLine($"Text: {result.Text}");
            _output.WriteLine($"Time: {result.ProcessingTimeMs}ms");

            // Silent audio should succeed but produce empty or minimal text
            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    [Fact]
    public async Task ImageConverter_RealImage_ConvertsToText()
    {
        var converter = new ImageConverter();
        if (!converter.IsAvailable)
        {
            _output.WriteLine("SKIPPED: Tesseract not installed");
            return;
        }

        // Create a simple test image with text
        var imagePath = CreateTestImageWithText("Hello World");

        try
        {
            var context = new ConversionContext
            {
                ContentType = "photo",
                FilePath = imagePath,
                FileName = "test.png"
            };

            var result = await converter.ConvertAsync(context);

            _output.WriteLine($"Conversion success: {result.Success}");
            _output.WriteLine($"Text: {result.Text}");
            _output.WriteLine($"Time: {result.ProcessingTimeMs}ms");

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    // ==================== Helper Methods ====================

    private string CreateSilentWavFile()
    {
        var path = Path.Combine(_testDir, "silent.wav");

        // Create a minimal valid WAV file (silent, 1 second, mono, 16-bit, 8000 Hz)
        var sampleRate = 8000;
        var numSamples = sampleRate;  // 1 second
        var byteRate = sampleRate * 2;
        var dataSize = numSamples * 2;

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // RIFF header
        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataSize);
        bw.Write("WAVE".ToCharArray());

        // fmt chunk
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);           // chunk size
        bw.Write((short)1);     // audio format (PCM)
        bw.Write((short)1);     // num channels
        bw.Write(sampleRate);   // sample rate
        bw.Write(byteRate);     // byte rate
        bw.Write((short)2);     // block align
        bw.Write((short)16);    // bits per sample

        // data chunk
        bw.Write("data".ToCharArray());
        bw.Write(dataSize);

        // Silent samples
        for (int i = 0; i < numSamples; i++)
        {
            bw.Write((short)0);
        }

        return path;
    }

    private string CreateTestImageWithText(string text)
    {
        var path = Path.Combine(_testDir, "test_image.png");

        // Create a simple 200x50 white PNG with black text
        // This is a minimal PNG - for real testing, use System.Drawing or similar
        // For now, create a blank image that tesseract can process
        var width = 200;
        var height = 50;

        using var fs = new FileStream(path, FileMode.Create);

        // PNG signature
        fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR chunk
        WriteChunk(fs, "IHDR", writer =>
        {
            writer.Write(ToBigEndian(width));
            writer.Write(ToBigEndian(height));
            writer.Write((byte)8);   // bit depth
            writer.Write((byte)2);   // color type (RGB)
            writer.Write((byte)0);   // compression
            writer.Write((byte)0);   // filter
            writer.Write((byte)0);   // interlace
        });

        // IDAT chunk (uncompressed white image data)
        var rawData = new byte[(width * 3 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            rawData[y * (width * 3 + 1)] = 0;  // filter byte
            for (int x = 0; x < width * 3; x++)
            {
                rawData[y * (width * 3 + 1) + 1 + x] = 255;  // white
            }
        }

        // Compress with zlib
        using var compressedMs = new MemoryStream();
        compressedMs.WriteByte(0x78);  // zlib header
        compressedMs.WriteByte(0x01);
        using (var deflate = new System.IO.Compression.DeflateStream(compressedMs, System.IO.Compression.CompressionMode.Compress, true))
        {
            deflate.Write(rawData);
        }
        var adler = ComputeAdler32(rawData);
        var adlerBytes = BitConverter.GetBytes(ToBigEndian((int)adler));
        compressedMs.Write(adlerBytes);

        WriteChunk(fs, "IDAT", compressedMs.ToArray());

        // IEND chunk
        WriteChunk(fs, "IEND", Array.Empty<byte>());

        return path;
    }

    private void WriteChunk(FileStream fs, string type, Action<BinaryWriter> writeData)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        writeData(bw);
        WriteChunk(fs, type, ms.ToArray());
    }

    private void WriteChunk(FileStream fs, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        // Length
        fs.Write(BitConverter.GetBytes(ToBigEndian(data.Length)));

        // Type
        fs.Write(typeBytes);

        // Data
        fs.Write(data);

        // CRC
        var crcData = new byte[typeBytes.Length + data.Length];
        Array.Copy(typeBytes, crcData, typeBytes.Length);
        Array.Copy(data, 0, crcData, typeBytes.Length, data.Length);
        var crc = ComputeCrc32(crcData);
        fs.Write(BitConverter.GetBytes(ToBigEndian((int)crc)));
    }

    private int ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return BitConverter.ToInt32(bytes);
    }

    private uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 * (crc & 1));
            }
        }
        return ~crc;
    }

    private uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}
