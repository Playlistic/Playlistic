using System.Collections.Generic;
using System.Linq;

namespace Playlistic.Models
{
    public class ResultModel
    {
        public List<Playlistic_PlaylistItem> PlaylistItems;
        public string SpotifyLink;
        public string YoutubeLink;
        public int OriginalYoutubeVideoCount => PlaylistItems.Count;
        public int FoundSpotifyTracks => PlaylistItems.Select(x => x.FoundSpotifyTrack != null).Count();
        public bool FaultTriggered;
        public FaultCode FaultCode;
        public ResultModel()
        {
            PlaylistItems = new List<Playlistic_PlaylistItem>();
            SpotifyLink = string.Empty;
            YoutubeLink = string.Empty;
            FaultTriggered = false;
        }

        public ResultModel(FaultCode faultCode, string YoutubePlaylistURL)
        {
            PlaylistItems = new List<Playlistic_PlaylistItem>();
            SpotifyLink = string.Empty;
            YoutubeLink = YoutubePlaylistURL;
            FaultTriggered = true;
            FaultCode = faultCode;
        }
    }
    public enum FaultCode
    {
        Unspported,
        ConversionFailed,
        EmptyPlaylist,
        EmptySearchResult
    }
}
