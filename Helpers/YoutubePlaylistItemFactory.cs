using System.Collections.Generic;
using System.Linq;

namespace Youtube2Spotify.Helpers
{
    public class YoutubePlaylistItem
    {
        public string song;
        public List<string> artists;
    }

    public static class YoutubePlaylistItemFactory
    {
        /// <summary>
        /// massages the song title to make it easier to search on spotify
        /// </summary>
        /// <param name="song">original title from youtube video, we will need to massage it a bit</param>
        /// <param name="artist">original channel title, we will need to massage it a bit, might or might not need it during actual search since many music MV include artist name in the title</param>
        /// <returns></returns>
        public static YoutubePlaylistItem GetYoutubePlaylistItem(string song, List<string> artists)
        {
            YoutubePlaylistItem youtubePlaylistItem = new YoutubePlaylistItem();
            youtubePlaylistItem.artists = new List<string>();
            bool ignorebrackets = false;
            string cleanedSongName = string.Join(' ', song.ToLower().Split(' ').Select(x =>
            {
                // I suck with regex so this the alternative
                // the goal is to ignore everything that's between "(official" and "audio)"
                if (x.Contains("[official") || x.Contains("(official"))
                {
                    ignorebrackets = true;
                    return string.Empty;
                }

                if (x.Contains("audio)") || x.Contains("video)") || x.Contains("audio]") || x.Contains("video]"))
                {
                    ignorebrackets = false;
                    return string.Empty;
                }

                if (ignorebrackets)
                {
                    return string.Empty;
                }

                return x;
            }).ToArray());

            cleanedSongName = cleanedSongName.Replace("   ", "  ");
            cleanedSongName = cleanedSongName.Replace("  ", " ");
            cleanedSongName = cleanedSongName.Replace("\"", "");

            if (!cleanedSongName.Contains(" - ") && !cleanedSongName.Contains(" – "))
            {
                foreach (string artist in artists)
                {
                    string artistNameLowered = artist.ToLower();
                    youtubePlaylistItem.artists.Add(artistNameLowered);
                }
            }          
            
            youtubePlaylistItem.song = cleanedSongName;
            return youtubePlaylistItem;
        }
    }
}
