using Playlistic.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Playlistic.Helpers
{
    public static class PlaylistItemFactory
    {
        /// <summary>
        /// massages the song title to make it easier to search on spotify
        /// </summary>
        /// <param name="song">original title from youtube video, we will need to massage it a bit</param>
        /// <param name="artist">original channel title, we will need to massage it a bit, might or might not need it during actual search since many music MV include artist name in the title</param>
        /// <returns></returns>
        public static List<Playlistic_PlaylistItem> CleanUpPlaylistItems(List<Playlistic_PlaylistItem> rawPlaylistItems)
        {
            foreach (Playlistic_PlaylistItem rawPlaylistItem in rawPlaylistItems)
            {
                bool ignorebrackets = false;
                rawPlaylistItem.SpotifySearchObject.Song = rawPlaylistItem.SpotifySearchObject.Song.ToLower();
                rawPlaylistItem.SpotifySearchObject.Song = rawPlaylistItem.SpotifySearchObject.Song.Replace("- video edit", "");
                rawPlaylistItem.SpotifySearchObject.Song = rawPlaylistItem.SpotifySearchObject.Song.Replace("closed caption", "");

                string cleanedSongName = string.Join(' ', rawPlaylistItem.SpotifySearchObject.Song.Split(' ').Select(x =>
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
                    rawPlaylistItem.SpotifySearchObject.Artists = rawPlaylistItem.SpotifySearchObject.Artists.Select(x => x.ToLower()).ToList();
                }

                rawPlaylistItem.SpotifySearchObject.Song = cleanedSongName;
            }

            return rawPlaylistItems;
        }

        public static async Task<List<Playlistic_PlaylistItem>> CleanUpPlaylistItems_PoweredByAI(List<Playlistic_PlaylistItem> rawPlaylistItems, string OpenAIAssistantSetupString, string openAIAccessToken)
        {           
            string OpenAIReadyInputListString = "[" + string.Join(",", rawPlaylistItems.Select(x =>
              {
                  return 
                  $"{{" +
                  $"\\\"Input\\\":\\\"{x.OriginalYoutubeObject.VideoTitle}\\\""+
                  $"}}";

              })) + "]";

            List<SpotifySearchObject> spotifySearchObjects = await HttpHelpers.MakeOpenAIRequest(OpenAIAssistantSetupString, OpenAIReadyInputListString, openAIAccessToken);

            for (int i = 0; i < rawPlaylistItems.Count; i++)
            {
                rawPlaylistItems[i].SpotifySearchObject = spotifySearchObjects[i];
            }

            return rawPlaylistItems;
        }
    }
}
