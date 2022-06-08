using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Youtube2Spotify.Models;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Globalization;

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
            string cleanedSongName = string.Join(' ', song.ToLower().Split(' ').Select(x =>
            {
                if (x.Contains("[official") || x.Contains("(official") || x.Contains("audio)") || x.Contains("video)") || x.Contains("audio]") || x.Contains("video]"))
                {
                    return string.Empty;
                }
                return x;
            }).ToArray());

            cleanedSongName = cleanedSongName.Replace("   ", "  ");
            cleanedSongName = cleanedSongName.Replace("  ", " ");
            cleanedSongName = cleanedSongName.Replace("\"", "");

            foreach (string artist in artists)
            {
                string artistNameLowered = artist.ToLower();
                //youtube music still sometimes include artist name in the title, which messes with spotify searches
                cleanedSongName = cleanedSongName.Replace(artistNameLowered, "");
                youtubePlaylistItem.artists.Add(artistNameLowered);
            }

            youtubePlaylistItem.song = cleanedSongName;

            return youtubePlaylistItem;
        }
    }
}
