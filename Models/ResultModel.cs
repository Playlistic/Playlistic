using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Linq;
using Youtube2Spotify.Helpers;

namespace Youtube2Spotify.Models
{
    public class ResultModel
    {
        public List<PlaylistItem> PlaylistItems;
        public string SpotifyLink;
        public string YoutubeLink;
        public int OriginalYoutubeVideoCount => PlaylistItems.Count;
        public int FoundSpotifyTracks => PlaylistItems.Select(x => x.FoundSpotifyTrack != null).Count();
        public bool faultTriggered;
        public faultCode faultCode;
        public ResultModel()
        {
            PlaylistItems = new List<PlaylistItem>();
            SpotifyLink = string.Empty;
            YoutubeLink = string.Empty;
            faultTriggered = false;
        }
    }
    public enum faultCode
    {
        Unspported,
        ConversionFailed,
        AuthExpired
    }
}
