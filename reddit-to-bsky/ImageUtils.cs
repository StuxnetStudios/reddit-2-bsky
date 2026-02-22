using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using NLog;

public static class ImageUtils
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private static readonly HttpClient Client = new HttpClient();
    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "reddit-to-bsky");

    static ImageUtils()
    {
        Directory.CreateDirectory(TempFolder);
        Client.DefaultRequestHeaders.Add("User-Agent", "RedditToBlueskyBot/1.0");
        Client.Timeout = TimeSpan.FromSeconds(30);
    }

    public static async Task<string?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            var response = await Client.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            // Generate unique filename
            string fileName = $"img_{Guid.NewGuid()}.jpg";
            string filePath = Path.Combine(TempFolder, fileName);

            // Save image to disk
            byte[] imageData = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(filePath, imageData);

            Logger.Debug($"Downloaded image to: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error downloading image from {imageUrl}");
            return null;
        }
    }

    public static string ComputePerceptualHash(string imagePath)
    {
        try
        {
            using (var image = Image.Load<Rgb24>(imagePath))
            {
                // Resize to 8x8 for perceptual hash
                image.Mutate(x => x.Resize(8, 8));

                // Convert to grayscale and compute average
                double avg = 0;
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        var pixel = image[x, y];
                        avg += (pixel.R + pixel.G + pixel.B) / 3.0 / 255.0;
                    }
                }
                avg /= 64.0;

                // Build binary hash
                var hash = new System.Text.StringBuilder();
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        var pixel = image[x, y];
                        double gray = (pixel.R + pixel.G + pixel.B) / 3.0 / 255.0;
                        hash.Append(gray > avg ? '1' : '0');
                    }
                }

                string binaryHash = hash.ToString();
                // Convert binary to hex for storage
                string hexHash = Convert.ToHexString(BinaryToBytes(binaryHash));
                Logger.Debug($"Computed pHash: {hexHash}");
                return hexHash;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error computing perceptual hash for {imagePath}");
            throw;
        }
    }

    private static byte[] BinaryToBytes(string binary)
    {
        var result = new byte[(binary.Length + 7) / 8];
        for (int i = 0; i < binary.Length; i++)
        {
            if (binary[i] == '1')
            {
                int byteIndex = i / 8;
                int bitIndex = 7 - (i % 8);
                result[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
        return result;
    }

    public static void CleanupTempFolder()
    {
        try
        {
            if (Directory.Exists(TempFolder))
            {
                Directory.Delete(TempFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error cleaning up temp folder");
        }
    }
}
