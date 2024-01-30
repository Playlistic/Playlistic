using System.Collections.Generic;

namespace Playlistic.Models
{
    public class SpotifySearchObject
    {
        public string Song { get; set; }
        public List<string> MainArtists { get; set; }
        public List<string> Producers { get; set; }
        public List<string> FeaturedArtists { get; set; }

        public SpotifySearchObject()
        {
            Song = string.Empty;
            MainArtists = new List<string>();
            Producers = new List<string>();
            FeaturedArtists = new List<string>();
        }
    }
}
