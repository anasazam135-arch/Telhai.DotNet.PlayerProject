using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class ItunesService
    {
        private readonly HttpClient _http;

        public ItunesService(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }

        public async Task<SongMetadata?> SearchAsync(string query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            // build request for iTunes Search API
            string url = $"https://itunes.apple.com/search?term={System.Uri.EscapeDataString(query)}&limit=1&entity=song";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("results", out var results)) return null;
            if (results.GetArrayLength() == 0) return null;

            var first = results[0];
            var meta = new SongMetadata();
            if (first.TryGetProperty("trackName", out var t)) meta.TrackName = t.GetString() ?? string.Empty;
            if (first.TryGetProperty("artistName", out var a)) meta.ArtistName = a.GetString() ?? string.Empty;
            if (first.TryGetProperty("collectionName", out var c)) meta.AlbumName = c.GetString() ?? string.Empty;
            if (first.TryGetProperty("artworkUrl100", out var art)) meta.ArtworkUrl = art.GetString() ?? string.Empty;

            return meta;
        }
    }
}
