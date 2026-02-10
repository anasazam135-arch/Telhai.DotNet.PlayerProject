using System.Collections.Generic;

namespace Telhai.DotNet.PlayerProject.Models
{
    public class SongMetadata
    {
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        // Artwork URL from API (remote)
        public string ArtworkUrl { get; set; } = string.Empty;
        // Local images added by user (full file paths)
        public List<string> LocalImages { get; set; } = new List<string>();
        // Original file path for reference
        public string FilePath { get; set; } = string.Empty;
    }
}
