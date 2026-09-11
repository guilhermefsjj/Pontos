using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Pontos.Models;

namespace Pontos.Services
{
    /// <summary>
    /// Handles downloading random images for points.
    /// Uses public APIs to fetch random images and associates them with points.
    /// </summary>
    public class ImageService
    {
        private readonly PersistenceService _persistence;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Raised when an image is successfully downloaded and associated with a point.
        /// </summary>
        public event EventHandler<ImageDownloadedEventArgs>? ImageDownloaded;

        /// <summary>
        /// Raised when image download fails (point is still awarded).
        /// </summary>
        public event EventHandler<string>? ImageDownloadFailed;

        public ImageService(PersistenceService persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Pontos-App/1.0");
        }

        /// <summary>
        /// Downloads a random image and saves it for a point.
        /// If download fails, returns null but doesn't prevent point from being awarded.
        /// </summary>
        public async Task<string?> DownloadImageForPointAsync(Point point)
        {
            if (point == null)
                return null;

            try
            {
                // Try downloading from Picsum Photos (free, no auth required)
                // Each image is 200x200, random from their collection
                string imageUrl = $"https://picsum.photos/200/200?random={Guid.NewGuid()}";
                
                byte[] imageData = await _httpClient.GetByteArrayAsync(imageUrl);

                if (imageData == null || imageData.Length == 0)
                {
                    ImageDownloadFailed?.Invoke(this, "Downloaded image is empty");
                    return null;
                }

                // Save to disk
                string imagePath = _persistence.GetPointImagePath(point.UniqueId);
                string directory = Path.GetDirectoryName(imagePath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(imagePath, imageData);

                // Update point with image path
                point.ImagePath = imagePath;

                ImageDownloaded?.Invoke(this, new ImageDownloadedEventArgs
                {
                    Point = point,
                    ImagePath = imagePath
                });

                System.Diagnostics.Debug.WriteLine($"[INFO] Image downloaded for point {point.UniqueId}");
                return imagePath;
            }
            catch (HttpRequestException ex)
            {
                // Network/connectivity error
                string error = $"Network error downloading image: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[WARNING] {error}");
                ImageDownloadFailed?.Invoke(this, error);
                return null;
            }
            catch (OperationCanceledException)
            {
                // Timeout
                string error = "Image download timed out";
                System.Diagnostics.Debug.WriteLine($"[WARNING] {error}");
                ImageDownloadFailed?.Invoke(this, error);
                return null;
            }
            catch (Exception ex)
            {
                string error = $"Unexpected error downloading image: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ERROR] {error}");
                ImageDownloadFailed?.Invoke(this, error);
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class ImageDownloadedEventArgs : EventArgs
    {
        public Point? Point { get; set; }
        public string? ImagePath { get; set; }
    }
}
