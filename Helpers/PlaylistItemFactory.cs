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
        public string song;
        public string artist;
        public string originalYoutubeVideoId;
        public FullTrack? foundSpotifyTrack;
        public string SpotifyArtists => string.Join(",", foundSpotifyTrack?.Artists.Select(y => y.Name).ToList());
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
                rawPlaylistItem.song = rawPlaylistItem.song.ToLower();
                rawPlaylistItem.song = rawPlaylistItem.song.Replace("- video edit", "");
                rawPlaylistItem.song = rawPlaylistItem.song.Replace("closed caption", "");

                string cleanedSongName = string.Join(' ', rawPlaylistItem.song.Split(' ').Select(x =>
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
                    rawPlaylistItem.artist = rawPlaylistItem.artist.ToLower();
                }

                rawPlaylistItem.song = cleanedSongName;
            }

            return rawPlaylistItems;
        }

        public static List<PlaylistItem> CleanUpPlaylistItems_PoweredByAI(List<PlaylistItem> rawPlaylistItems, string AIPromptString, string openAIAccessToken)
        {
            /*
            string OpenAIAssistantSetupString = AIPromptString;
            string AISystemPostRequestBody = $"{{" +
                                                    $"\"model\": \"gpt-3.5-turbo\"," +
                                                    $"\"temperature\": 0," +
                                                    $"\"top_p\": 0," +
                                                    $"\"max_tokens\": 2048," +
                                                    $"\"frequency_penalty\": 0," +
                                                    $"\"presence_penalty\": 0," +
                                                    $"\"messages\": [" +
                                                                        $"{{ \"role\": \"system\"," +
                                                                        $"   \"content\": \"{OpenAIAssistantSetupString}\"" +
                                                                        $"}}" +
                                                                        "," +
                                                                        $"{{ \"role\": \"user\"," +
                                                                        $"   \"content\": \"{OpenAIReadyInputListString}\"" +
                                                                        $"}}" +
                                                                  $"]" +
                                             $"}}";

            string AIPlaylistGenerationResponse = string.Empty;
            HttpWebResponse systemSetupResponse = HttpHelpers.MakePostRequest("https://api.openai.com/v1/chat/completions", AISystemPostRequestBody, openAIAccessToken);
            if (systemSetupResponse.StatusCode == HttpStatusCode.OK)
            {
                using (Stream stream = systemSetupResponse.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    AIPlaylistGenerationResponse = reader.ReadToEnd();
                    OpenAIResult openAIResult = JsonConvert.DeserializeObject<OpenAIResult>(AIPlaylistGenerationResponse);
                    string rawResult = openAIResult.choices[0].message.content;
                    rawResult = $"{{ youtubePlaylistItems:[{rawResult}]}}";
                    JsonAIResult jsonAIResult = JsonConvert.DeserializeObject<JsonAIResult>(rawResult);
                    return jsonAIResult.youtubePlaylistItems;
                }
            }*/
                
            return new List<PlaylistItem>();

        }
    }
}
