using System.Collections.Generic;

namespace Youtube2Spotify.Models
{
    public class ResultModel
    {
        public List<string> YoutubeVideoNames;
        public List<string> SpotifyTrackNames;
        public string SpotifyLink;
        public string YoutubeLink;

        public bool faultTriggered;
        public faultCode faultCode;
        public ResultModel()
        {
            YoutubeVideoNames = new List<string>();
            SpotifyTrackNames = new List<string>();
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
