using Playlistic.Helpers;
using SpotifyAPI.Web;
using System.Linq;

namespace Playlistic.Models
{
    public class Playlistic_PlaylistItem
    {
        public SpotifySearchObject SpotifySearchObject { get; set; }
        public OriginalYoutubeObject OriginalYoutubeObject { get; set; }
        public FullTrack? FoundSpotifyTrack { get; set; }
        public string SpotifyArtists => FoundSpotifyTrack != null ? string.Join(", ", FoundSpotifyTrack.Artists.Select(y => y.Name).ToList()) : string.Empty;
        public string YoutubeArtists => OriginalYoutubeObject.VideoChannelTitle;
        public Playlistic_PlaylistItem()
        {
            OriginalYoutubeObject = new OriginalYoutubeObject();
            SpotifySearchObject = new SpotifySearchObject();
        }
    }
}
