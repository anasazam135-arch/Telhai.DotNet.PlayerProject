using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class MetadataCacheService
    {
        private readonly string _folder;

        public MetadataCacheService(string? folder = null)
        {
            _folder = folder ?? Path.Combine(Directory.GetCurrentDirectory(), "metadata");
            if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
        }

        private static string HashPath(string input)
        {
            using var sha = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string GetCacheFile(string filePath)
        {
            var name = HashPath(filePath) + ".json";
            return Path.Combine(_folder, name);
        }

        public async Task<SongMetadata?> LoadAsync(string filePath)
        {
            var f = GetCacheFile(filePath);
            if (!File.Exists(f)) return null;
            try
            {
                using var stream = File.OpenRead(f);
                var meta = await JsonSerializer.DeserializeAsync<SongMetadata>(stream);
                return meta;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveAsync(string filePath, SongMetadata meta)
        {
            var f = GetCacheFile(filePath);
            meta.FilePath = filePath; // ensure saved
            var options = new JsonSerializerOptions { WriteIndented = true };
            using var stream = File.Create(f);
            await JsonSerializer.SerializeAsync(stream, meta, options);
        }
    }
}
