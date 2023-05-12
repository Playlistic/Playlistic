using SpotifyAPI.Web;
using System.Collections.Generic;
using Youtube2Spotify.Helpers;

namespace Youtube2Spotify.Models
{
    public class ResultModel
    {
        public List<YoutubePlaylistItem> YoutubeVideos;
        public List<FullTrack> SpotifyTracks;
        public string SpotifyLink;
        public string YoutubeLink;

        public bool faultTriggered;
        public faultCode faultCode;
        public ResultModel()
        {
            YoutubeVideos = new List<YoutubePlaylistItem>();
            SpotifyTracks = new List<FullTrack>();
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
