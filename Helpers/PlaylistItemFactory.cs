using Newtonsoft.Json;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Youtube2Spotify.Models;

namespace Youtube2Spotify.Helpers
{
    public class PlaylistItem
    {
        public string searchSongName;
        public List<string> searchArtistName;
        public string originalYoutubeVideoTitle;
        public string originalYoutubeVideoChannelTitle;
        public string originalYoutubeVideoId;
        public string originalYoutubeThumbnailURL;
        public FullTrack? foundSpotifyTrack;
        public string SpotifyArtists => string.Join(",", foundSpotifyTrack?.Artists.Select(y => y.Name).ToList());
        public PlaylistItem()
        {
            searchArtistName = new List<string>();
        }
    }

    public static class PlaylistItemFactory
    {
        /// <summary>
        /// massages the song title to make it easier to search on spotify
        /// </summary>
        /// <param name="song">original title from youtube video, we will need to massage it a bit</param>
        /// <param name="artist">original channel title, we will need to massage it a bit, might or might not need it during actual search since many music MV include artist name in the title</param>
        /// <returns></returns>
        public static List<PlaylistItem> CleanUpPlaylistItems(List<PlaylistItem> rawPlaylistItems)
        {
            foreach (PlaylistItem rawPlaylistItem in rawPlaylistItems)
            {
                bool ignorebrackets = false;
                rawPlaylistItem.searchSongName = rawPlaylistItem.searchSongName.ToLower();
                rawPlaylistItem.searchSongName = rawPlaylistItem.searchSongName.Replace("- video edit", "");
                rawPlaylistItem.searchSongName = rawPlaylistItem.searchSongName.Replace("closed caption", "");

                string cleanedSongName = string.Join(' ', rawPlaylistItem.searchSongName.Split(' ').Select(x =>
                {
                    // I suck with regex so this the alternative
                    // the goal is to ignore everything that's between "(official" and "audio)"
                    if (x.Contains("[official") || x.Contains("(official") || x.Contains("(lyric") || x.Contains("[lyric"))
                    {
                        ignorebrackets = true;
                        return string.Empty;
                    }

                    if (x.Contains("(video)") || x.Contains("(visualizer)"))
                    {
                        return string.Empty;
                    }

                    if (x.Contains("audio)") || x.Contains("video)") || x.Contains("audio]") || x.Contains("video]"))
                    {
                        ignorebrackets = false;
                        return string.Empty;
                    }

                    if (x.Contains("[clean]") || x.Contains("(clean)"))
                    {
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
                    rawPlaylistItem.searchArtistName = rawPlaylistItem.searchArtistName.Select(x => x.ToLower()).ToList();
                }

                rawPlaylistItem.searchSongName = cleanedSongName;
            }

            return rawPlaylistItems;
        }

        public static List<PlaylistItem> CleanUpPlaylistItems_PoweredByAI(List<PlaylistItem> rawPlaylistItems, string openAISongString, string openAIArtistString, string openAIAccessToken)
        {
            foreach (PlaylistItem rawPlayListItem in rawPlaylistItems)
            {
                string OpenAIReadyInputListString = openAISongString + rawPlayListItem.searchSongName;
                rawPlayListItem.searchSongName = HttpHelpers.MakeOpenAIRequest(OpenAIReadyInputListString, openAIAccessToken);
                OpenAIReadyInputListString = openAIArtistString + rawPlayListItem.searchSongName;
                string foundArtist = HttpHelpers.MakeOpenAIRequest(OpenAIReadyInputListString, openAIAccessToken);
                foundArtist = foundArtist.ToLower();

                if (!rawPlayListItem.searchArtistName.Contains(foundArtist))
                {
                    rawPlayListItem.searchArtistName.Add(foundArtist);
                }
            }

            return rawPlaylistItems;
        }
    }
}
