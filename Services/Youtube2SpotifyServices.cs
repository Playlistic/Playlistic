using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playlistic.Helpers;
using Playlistic.Interfaces;
using Playlistic.Models;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Playlistic.Services
{
    public class Youtube2SpotifyServices : IYoutube2SpotifyService
    {
        private SpotifyClient spotify;

        public SpotifyClient SpotifyClient { set { spotify = value; } }

        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(dynamic playlistData)
        {
            string title = playlistData.header.musicDetailHeaderRenderer.title.runs[0].text;
            string description = playlistData.header.musicDetailHeaderRenderer.description.runs[0].text;
            string coverArt = playlistData.header.musicDetailHeaderRenderer.thumbnail.croppedSquareThumbnailRenderer.thumbnail.thumbnails[1].url;

            string base64ImageString = string.Empty;

            //spotify have a title and description limit of 200 characters, capping that
            if (title.Length > 200)
            {
                title = title[..200];
            }

            if (description.Length > 200)
            {
                description = description[..200];
            }

            try
            {
                using Stream stream = GetStreamFromUrl(coverArt);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] fileBytes = ms.ToArray();
                base64ImageString = Convert.ToBase64String(fileBytes);
            }
            catch
            {
                base64ImageString = string.Empty;
            }

            return new YoutubePlaylistMetadata()
            {
                title = title,
                description = description,
                coverImageInBase64 = base64ImageString
            };
        }

        public dynamic GetYoutubePlaylistDataFromHTML(string playlistId)
        {
            // youtube... in their infinite wisdom...
            // decided to not include certain songs within their playlist (basically videos provided by youtube music with "- topic"
            // in the channel name), rendering their playlistitem api unreliable

            // grab the playlist from music.youtube.com 

            string html;
            HttpWebRequest request = WebRequest.Create($"https://music.youtube.com/playlist?list={playlistId}") as HttpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.5005.63 Safari/537.36 Edg/102.0.1245.33";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new(stream))
            {
                html = reader.ReadToEnd();
            }

            HtmlParser parser = new();
            IHtmlDocument document = parser.ParseDocument(html);
            List<IElement> listOfScript = document.Body.GetElementsByTagName("script").ToList();
            IElement element = listOfScript.First(x => x.TextContent.Contains("initialData.push"));
            string ytInitialData = element.InnerHtml;

            // people put all kinds of characters in metadata, gross
            ytInitialData = Regex.Unescape(ytInitialData);
            ytInitialData = HttpUtility.HtmlDecode(ytInitialData);
            string rawPlaylistData = ytInitialData.Split("data: '").Last();
            rawPlaylistData = rawPlaylistData.Replace("'});ytcfg.set({'YTMUSIC_INITIAL_DATA': initialData});} catch (e) {}", "");

            dynamic initialData = JsonConvert.DeserializeObject(rawPlaylistData);
            return initialData;
        }

        private static Stream GetStreamFromUrl(string url)
        {
            byte[] imageData = null;

            using (var wc = new WebClient())
                imageData = wc.DownloadData(url);

            return new MemoryStream(imageData);
        }

        public bool AddTrackToSpotifyPlaylist(string spotifyPlaylistId, List<FullTrack> tracksToAdd, string accessToken)
        {
            List<string> trackURI = new();

            foreach (FullTrack fullTrack in tracksToAdd)
            {
                if (fullTrack != null)
                {
                    List<SimpleArtist> artists = fullTrack.Artists;
                    List<string> artistNames = artists.Select(x => x.Name).ToList();
                    string songName = fullTrack.Name.ToString();
                    trackURI.Add($"\"{fullTrack.Uri}\"");
                }
                continue;
            }

            try
            {
                HttpWebResponse httpWebResponse = AddTracksToPlaylist(spotifyPlaylistId, string.Join(",", trackURI), accessToken);
                if (httpWebResponse.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return false;
        }

        private static HttpWebResponse AddTracksToPlaylist(string newSpotifyPlaylistID, string tracksToAdd, string accessToken)
        {
            string url = $"https://api.spotify.com/v1/playlists/{newSpotifyPlaylistID}/tracks";

            string postData = "{";
            postData += "\"uris\": " + $"[{tracksToAdd}]";
            postData += "}";

            return HttpHelpers.MakePostRequest(url, postData, accessToken);
        }

        /// <summary>
        /// Uploads playlist cover to specified 
        /// </summary>
        /// <param name="SpotifyPlaylistId"></param>
        /// <returns></returns>
        public async Task<bool> UploadCoverToPlaylist(string SpotifyPlaylistId, YoutubePlaylistMetadata youtubePlaylistMetadata)
        {
            bool uploadCover = false;
            if (!string.IsNullOrEmpty(youtubePlaylistMetadata.coverImageInBase64))
            {
                uploadCover = await spotify.Playlists.UploadCover(SpotifyPlaylistId, youtubePlaylistMetadata.coverImageInBase64);
            }

            return uploadCover;
        }

        /// <summary>
        /// Creates an Empty Playlist
        /// </summary>
        /// <param name="youtubePlaylistItems"></param>
        public async Task<string> CreateEmptyPlayListOnSpotify(YoutubePlaylistMetadata youtubePlaylistMetadata, string user_Id)
        {
            //create a playlist using the currently authenticated profile
            PlaylistCreateRequest playlistCreateRequest = new(youtubePlaylistMetadata.title)
            {
                Description = youtubePlaylistMetadata.description
            };

            FullPlaylist fullPlaylist = await spotify.Playlists.Create(user_Id, playlistCreateRequest);
            return fullPlaylist.Id;
        }

        /// <summary>
        /// returns a list of corresponding track on spotify based on the list of youtube playlist items
        /// </summary>
        /// <param name="playlistItems">incoming list of youtube videos</param>
        /// <returns></returns>
        public async Task<List<Playlistic_PlaylistItem>> SearchForSongsOnSpotify(List<Playlistic_PlaylistItem> playlistItems)
        {
            foreach (Playlistic_PlaylistItem playlistItem in playlistItems)
            {
                SearchRequest searchRequest = new(SearchRequest.Types.Track, FormatSpotifySearchString(playlistItem))
                {
                    Limit = 1
                };

                SearchResponse searchResponse = await spotify.Search.Item(searchRequest);

                if (searchResponse.Tracks.Items != null)
                {
                    if (searchResponse.Tracks.Items.Count > 0)
                    {
                        FullTrack fullTrack = searchResponse.Tracks.Items[0];
                        playlistItem.FoundSpotifyTrack = fullTrack;
                    }
                }
            }
            return playlistItems;
        }

        private static string FormatSpotifySearchString(Playlistic_PlaylistItem playlistItem)
        {
            StringBuilder queryBuilder = new();

            queryBuilder.Append(string.Join(" ", playlistItem.SpotifySearchObject.Artists));
            queryBuilder.Append($" {playlistItem.SpotifySearchObject.Song}");

            return queryBuilder.ToString();
        }

        public List<Playlistic_PlaylistItem> GetPreliminaryPlaylistItems(JArray incomingRawYoutubeMusicPlaylistData)
        {
            List<Playlistic_PlaylistItem> OriginalYoutubeData = new();
            foreach (dynamic musicResponsiveListItemRenderer in incomingRawYoutubeMusicPlaylistData)
            {
                string songName = musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.flexColumns[0].musicResponsiveListItemFlexColumnRenderer.text.runs[0].text.Value.ToString();
                string songArtists = musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.flexColumns[1].musicResponsiveListItemFlexColumnRenderer.text.runs[0].text.Value.ToString();
                string originalYoutubeThumbnailSmall = musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.thumbnail.musicThumbnailRenderer.thumbnail.thumbnails[0].url.Value;

                string originalYoutubeVideoId = string.Empty;

                try
                {
                    originalYoutubeVideoId = musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.playlistItemData.videoId.Value;
                }
                catch
                {

                    string pattern = @"/([a-zA-Z0-9_-]+)\.[a-z]+$";

                    // The match object
                    Match match = Regex.Match(musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.thumbnail.musicThumbnailRenderer.thumbnail.thumbnails[0].url.Value, pattern);

                    // If a match is found, extract the video id
                    if (match.Success)
                    {
                        originalYoutubeVideoId = match.Groups[1].Value;
                    }


                }

                Playlistic_PlaylistItem playlistItem = new();
                songName = songName.Replace("\"", "");
                playlistItem.SpotifySearchObject.Song = songName;
                playlistItem.OriginalYoutubeObject.VideoId = originalYoutubeVideoId;
                playlistItem.OriginalYoutubeObject.ThumbnailURL = originalYoutubeThumbnailSmall;
                playlistItem.OriginalYoutubeObject.VideoChannelTitle = songArtists;
                playlistItem.OriginalYoutubeObject.VideoTitle = songName;
                playlistItem.SpotifySearchObject.Artists.Add(songArtists.ToLower());
                OriginalYoutubeData.Add(playlistItem);
            }
            return OriginalYoutubeData;
        }

        public async Task<string> GetUserIdLive()
        {
            PrivateUser privateUser = await spotify.UserProfile.Current();
            return privateUser.Id;
        }
    }
}
