using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using Playlistic.Helpers;
using Playlistic.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Playlistic.Controllers
{
    public class YoutubePlaylistMetadata
    {
        public string title;
        public string description;
        public string coverImageInBase64;
    }

    public class Youtube2SpotifyController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _hostingEnvironment;
        public Youtube2SpotifyController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
            openAIAccessToken = configuration["OpenAIKey"];
            openAIAssistantSetupString = configuration["OpenAIPrompt"];
            googleAPIAccessToken = configuration["YoutubeAPIKey"];
        }

        private string YoutubePlaylistID { get; set; }
        private YoutubePlaylistMetadata youtubePlaylistMetadata;
        private SpotifyClient spotify;
        private readonly string openAIAccessToken;
        private readonly string openAIAssistantSetupString;
        private readonly string googleAPIAccessToken;

        public async Task<IActionResult> Index(string youtubePlaylistID)
        {
            ResultModel resultModel = new ResultModel();
            HomeModel homeModel = new HomeModel(false);
            string access_Token = HttpContext.Session.GetString("access_token");
            try
            {
                string expire_time_raw = HttpContext.Session.GetString("expire_time");
                DateTime expire_Time = DateTime.Parse(expire_time_raw);
                if (DateTime.Now > expire_Time)
                {
                    resultModel.faultTriggered = true;
                    resultModel.faultCode = faultCode.AuthExpired;
                    return Result(resultModel);
                }
            }
            catch
            {
                return Home(homeModel);
            }
            homeModel.SetAuthenticated(true);

            spotify = new SpotifyClient(access_Token);
            HttpContext.Session.SetString("user_Id", await GetUserId());

            if (!string.IsNullOrEmpty(youtubePlaylistID) || !string.IsNullOrWhiteSpace(youtubePlaylistID))
            {
                YoutubePlaylistID = youtubePlaylistID;
                resultModel = await ConvertYoutubePlaylist2SpotifyPlaylist(youtubePlaylistID);
            }
            else
            {
                return Home(homeModel);
            }

            return Result(resultModel);
        }

        public PartialViewResult Result(ResultModel result = null)
        {
            return PartialView("~/Views/Result/Index.cshtml", result);
        }

        public PartialViewResult Home(HomeModel home = null)
        {
            return PartialView("~/Views/Home/Index.cshtml", home);
        }

        private JArray YoutubePlaylistItemsFromHTML(string playlistId)
        {
            // youtube... in their infinite wisdom...
            // decided to not include certain songs within their playlist (basically videos provided by youtube music with "- topic"
            // in the channel name), rendering their playlistitem api unreliable

            // grab the playlist from music.youtube.com 

            string html;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://music.youtube.com/playlist?list={playlistId}");
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.5005.63 Safari/537.36 Edg/102.0.1245.33";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            HtmlParser parser = new HtmlParser();
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
            JArray playlist = initialData.contents.singleColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents[0].musicPlaylistShelfRenderer.contents;
            return playlist;
        }

        /// <summary>
        /// get title and artist name from youtube
        /// </summary>
        /// <param name="youtubePlaylistId"></param>
        /// <returns></returns>
        public async Task<ResultModel> ConvertYoutubePlaylist2SpotifyPlaylist(string youtubePlaylistId)
        {
            ResultModel resultModel = new ResultModel();
            List<PlaylistItem> PlaylistItems = new List<PlaylistItem>();

            try
            {
                youtubePlaylistMetadata = GenerateYoutubePlaylistMetadata(youtubePlaylistId);
            }
            catch
            {
                //something is weird with the youtubePlaylistId
                return new ResultModel() { faultTriggered = true, faultCode = faultCode.Unspported, YoutubeLink = $"https://music.youtube.com/playlist?list={youtubePlaylistId}" };
            }

            try
            {
                List<string> songNames = new List<string>();

                //collect the list of videos from Json
                JArray playlist = YoutubePlaylistItemsFromHTML(youtubePlaylistId);
                //cutting list down to 25 because of ChatGPT text limit
                PlaylistItems = GetPreliminaryPlaylistItems(playlist);


                if (PlaylistItems.Count > 12)
                {
                    //this would take too long for AI to handle, use traditional method
                    PlaylistItems = PlaylistItemFactory.CleanUpPlaylistItems(PlaylistItems);
                }
                else
                {
                    //throws the entire playlist at AI to go through and return a cleaned up search list
                    PlaylistItems = PlaylistItemFactory.CleanUpPlaylistItems_PoweredByAI(PlaylistItems, openAIAssistantSetupString, openAIAccessToken);
                }

                // add total number of song names
                // okay, we got the title, time to look it up on Spotify
                string newSpotifyPlaylistID = await CreateEmptyPlayListOnSpotify(youtubePlaylistMetadata);
                await UploadCoverToPlaylist(newSpotifyPlaylistID, youtubePlaylistMetadata);
                PlaylistItems = await SearchForSongsOnSpotify(PlaylistItems);
                bool success = AddTrackToSpotifyPlaylist(newSpotifyPlaylistID, PlaylistItems.Select(x => { return x.FoundSpotifyTrack; }).ToList());
                if (success)
                {
                    resultModel.PlaylistItems = PlaylistItems;
                    resultModel.SpotifyLink = $"https://open.spotify.com/playlist/{newSpotifyPlaylistID}";
                    resultModel.YoutubeLink = $"https://youtube.com/playlist?list={youtubePlaylistId}";
                    string resultModelString = JsonConvert.SerializeObject(resultModel);
                    return resultModel;
                }
                throw new Exception("Failed to add tracks to spotify");

            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
                return new ResultModel() { faultTriggered = true, faultCode = faultCode.ConversionFailed, YoutubeLink = $"https://music.youtube.com/playlist?list={youtubePlaylistId}" };
            }

        }

        /// <summary>
        /// Simulate a conversion result without having to execute the actual conversion process
        /// </summary>
        /// <returns></returns>
        public PartialViewResult FrontEndTest()
        {
            return PartialView("~/Views/Result/Index.cshtml", JsonConvert.DeserializeObject<ResultModel>(System.IO.File.ReadAllText($"{_hostingEnvironment.WebRootPath}\\SampleData.json")));
        }

        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(string playlistId)
        {
            string url = $"https://www.googleapis.com/youtube/v3/playlists?id={playlistId}&key={googleAPIAccessToken}&part=id,snippet&fields=items(id,snippet(title,channelId,channelTitle,description,thumbnails))";
            dynamic json = HttpHelpers.MakeYoutubeGetCalls(url);

            string title = json.items[0].snippet.title;
            string description = json.items[0].snippet.description;
            string coverArt = json.items[0].snippet.thumbnails.medium.url;

            string base64ImageString = string.Empty;

            if (title.Length > 200)
            {
                title = title.Substring(0, 200);
            }

            if (description.Length > 200)
            {
                description = description.Substring(0, 200);
            }

            using (Stream stream = GetStreamFromUrl(coverArt))
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byte[] fileBytes = ms.ToArray();
                    base64ImageString = Convert.ToBase64String(fileBytes);
                }
            }

            return new YoutubePlaylistMetadata()
            {
                title = title,
                description = description,
                coverImageInBase64 = base64ImageString
            };
        }

        private List<PlaylistItem> GetPreliminaryPlaylistItems(JArray incomingRawYoutubeMusicPlaylistData)
        {
            List<PlaylistItem> OriginalYoutubeData = new List<PlaylistItem>();
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

                    // The video id
                    string videoId = "";

                    // If a match is found, extract the video id
                    if (match.Success)
                    {
                        originalYoutubeVideoId = match.Groups[1].Value;
                    }


                }

                PlaylistItem playlistItem = new PlaylistItem();
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

        /// <summary>
        /// Creates an Empty Playlist
        /// </summary>
        /// <param name="youtubePlaylistItems"></param>
        public async Task<string> CreateEmptyPlayListOnSpotify(YoutubePlaylistMetadata youtubePlaylistMetadata)
        {
            //create a playlist using the currently authenticated profile
            string user_Id = HttpContext.Session.GetString("user_Id");

            PlaylistCreateRequest playlistCreateRequest = new PlaylistCreateRequest(youtubePlaylistMetadata.title);
            playlistCreateRequest.Description = youtubePlaylistMetadata.description;

            FullPlaylist fullPlaylist = await spotify.Playlists.Create(user_Id, playlistCreateRequest);
            return fullPlaylist.Id;
        }
        /// <summary>
        /// Uploads playlist cover to specified 
        /// </summary>
        /// <param name="SpotifyPlaylistId"></param>
        /// <returns></returns>
        public async Task<bool> UploadCoverToPlaylist(string SpotifyPlaylistId, YoutubePlaylistMetadata youtubePlaylistMetadata)
        {
            bool uploadCover = await spotify.Playlists.UploadCover(SpotifyPlaylistId, youtubePlaylistMetadata.coverImageInBase64);
            return uploadCover;
        }

        /// <summary>
        /// returns a list of corresponding track on spotify based on the list of youtube playlist items
        /// </summary>
        /// <param name="playlistItems">incoming list of youtube videos</param>
        /// <returns></returns>
        public async Task<List<PlaylistItem>> SearchForSongsOnSpotify(List<PlaylistItem> playlistItems)
        {

            foreach (PlaylistItem playlistItem in playlistItems)
            {
                SearchRequest searchRequest = new SearchRequest(SearchRequest.Types.Track, FormatSpotifySearchString(playlistItem));
                searchRequest.Limit = 1;

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
        public bool AddTrackToSpotifyPlaylist(string spotifyPlaylistId, List<FullTrack> tracksToAdd)
        {
            List<string> trackURI = new List<string>();

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
                HttpWebResponse httpWebResponse = AddTracksToPlaylist(spotifyPlaylistId, string.Join(",", trackURI));
                if (httpWebResponse.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

          
            return false;
        }

        private static Stream GetStreamFromUrl(string url)
        {
            byte[] imageData = null;

            using (var wc = new WebClient())
                imageData = wc.DownloadData(url);

            return new MemoryStream(imageData);
        }

        private string FormatResultString(string song, List<string> artists)
        {
            return $"{string.Join(", ", artists)} - {song}";
        }

        private string FormatSpotifySearchString(PlaylistItem playlistItem)
        {
            StringBuilder queryBuilder = new StringBuilder();

            queryBuilder.Append(string.Join(" ", playlistItem.SpotifySearchObject.Artists));
            queryBuilder.Append($" {playlistItem.SpotifySearchObject.Song}");

            return queryBuilder.ToString();
        }

        public HttpWebResponse AddTracksToPlaylist(string newSpotifyPlaylistID, string tracksToAdd)
        {
            string url = $"https://api.spotify.com/v1/playlists/{newSpotifyPlaylistID}/tracks";

            string postData = "{";
            postData += "\"uris\": " + $"[{tracksToAdd}]";
            postData += "}";

            return HttpHelpers.MakePostRequest(url, postData, HttpContext.Session.GetString("access_token"));
        }

        public async Task<string> GetUserId()
        {
            string user_Id = HttpContext.Session.GetString("user_id");
            // if we already stored the UserId in session, grab it and return it 
            if (!string.IsNullOrEmpty(user_Id))
            {
                return user_Id;
            }

            return await GetUserIdLive();
        }

        private async Task<string> GetUserIdLive()
        {
            PrivateUser privateUser = await spotify.UserProfile.Current();
            return privateUser.Id;
        }
    }
}
