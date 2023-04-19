using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpotifyAPI.Web;
using Youtube2Spotify.Helpers;
using Youtube2Spotify.Models;

namespace Youtube2Spotify.Controllers
{
    public class YoutubePlaylistMetadata
    {
        public string title;
        public string description;
        public string coverImageInBase64;
    }
    public class Youtube2SpotifyController : Controller
    {
        private IWebHostEnvironment Environment;

        public Youtube2SpotifyController(IWebHostEnvironment _environment)
        {
            Environment = _environment;
        }

        private string YoutubePlaylistID { get; set; }
        private List<YoutubePlaylistItem> youtubePlaylistItems = new List<YoutubePlaylistItem>();
        private YoutubePlaylistMetadata youtubePlaylistMetadata;
        private SpotifyClient spotify;

        public async Task<IActionResult> Index(string youtubePlaylistID)
        {
            if (!string.IsNullOrEmpty(youtubePlaylistID) || !string.IsNullOrWhiteSpace(youtubePlaylistID))
            {
                YoutubePlaylistID = youtubePlaylistID;
                string access_Token = HttpContext.Session.GetString("access_token");
                spotify = new SpotifyClient(access_Token);
                HttpContext.Session.SetString("user_Id", await GetUserId());
                ResultModel resultModel = await GetYoutubeInfo(youtubePlaylistID);
                return Result(resultModel);
            }

            return Result();
        }

        public PartialViewResult Result(ResultModel result = null)
        {
            return PartialView("~/Views/Result/Index.cshtml", result);
        }

        private object MakeYoutubeGetCalls(string url)
        {
            string html = string.Empty;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }
            // you better know what this is beforehand
            dynamic json = JsonConvert.DeserializeObject(html);
            return json;
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
        public async Task<ResultModel> GetYoutubeInfo(string youtubePlaylistId)
        {
            try
            {
                youtubePlaylistMetadata = GenerateYoutubePlaylistMetadata(youtubePlaylistId);
            }
            catch
            {
                //something is weird with the youtubePlaylistId
                return new ResultModel() { Unsupported = true, YoutubeLink = $"https://music.youtube.com/playlist?list={youtubePlaylistId}" };
            }

            try
            {
                List<string> songNames = new List<string>();

                //collect the list of videos from Json
                JArray playlist = YoutubePlaylistItemsFromHTML(youtubePlaylistId);

                foreach (dynamic musicResponsiveListItemRenderer in playlist)
                {
                    string name = musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.flexColumns[0].musicResponsiveListItemFlexColumnRenderer.text.runs[0].text.Value.ToString();
                    List<string> artists = new List<string>();

                    foreach (dynamic artistInfo in musicResponsiveListItemRenderer.musicResponsiveListItemRenderer.flexColumns[1].musicResponsiveListItemFlexColumnRenderer.text.runs)
                    {
                        string artistName = artistInfo.text.Value.ToString();
                        if (((JObject)artistInfo).Count > 1 && artistName.Length > 1)
                        {
                            artists.Add(artistName);
                        }
                    }

                    YoutubePlaylistItem info = YoutubePlaylistItemFactory.GetYoutubePlaylistItem(name, artists);
                    string allArtistInfo = info.artists.Count() > 0 ? string.Join(", ", info.artists) + " - " : string.Empty;
                    songNames.Add($"{allArtistInfo}{info.song}");

                    youtubePlaylistItems.Add(info);
                }

                // add total number of song names
                // okay, we got the title, time to look it up on Spotify
                return await GenerateSpotifyPlaylist(youtubePlaylistMetadata, youtubePlaylistItems, songNames, youtubePlaylistId);
            }
            catch
            {
                return new ResultModel() { ConversionFailed = true, YoutubeLink = $"https://music.youtube.com/playlist?list={youtubePlaylistId}" };
            }

        }

        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(string playlistId)
        {
            string key = System.IO.File.ReadAllLines($"{Environment.WebRootPath}\\Secret.txt")[0];

            string url = $"https://www.googleapis.com/youtube/v3/playlists?id={playlistId}&key={key}&part=id,snippet&fields=items(id,snippet(title,channelId,channelTitle,description,thumbnails))";
            dynamic json = MakeYoutubeGetCalls(url);

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
                    byte [] fileBytes = ms.ToArray();
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

        private HttpWebResponse MakeSpotifyPostRequest(string url, string postData)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            var data = Encoding.ASCII.GetBytes(postData);

            request.ContentLength = data.Length;
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", "Bearer " + HttpContext.Session.GetString("access_token"));

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>
        /// Generates a spotify playlist based on crawled music info from youtube
        /// </summary>
        /// <param name="youtubePlaylistItems"></param>
        public async Task<ResultModel> GenerateSpotifyPlaylist(YoutubePlaylistMetadata youtubePlaylistMetadata, List<YoutubePlaylistItem> youtubePlaylistItems, List<string> songNames, string youtubePlaylistId)
        {
            ResultModel resultModel = new ResultModel();
            resultModel.YoutubeVideoNames = songNames;
            List<string> foundTracks = new List<string>();
            List<string> trackString = new List<string>();
            //create a playlist using the currently authenticated profile
            string newSpotifyPlaylistID = string.Empty;
            string user_Id = HttpContext.Session.GetString("user_Id");          

            PlaylistCreateRequest playlistCreateRequest = new PlaylistCreateRequest(youtubePlaylistMetadata.title);
            playlistCreateRequest.Description = youtubePlaylistMetadata.description;

            FullPlaylist fullPlaylist = await spotify.Playlists.Create(user_Id, playlistCreateRequest);
            bool uploadCover = await spotify.Playlists.UploadCover(fullPlaylist.Id, youtubePlaylistMetadata.coverImageInBase64);


            newSpotifyPlaylistID = fullPlaylist.Id;

            if (!string.IsNullOrEmpty(newSpotifyPlaylistID))
            {
                foreach (YoutubePlaylistItem youtubePlaylistItem in youtubePlaylistItems)
                {
                    SearchRequest searchRequest = new SearchRequest(SearchRequest.Types.Track, FormatSpotifySearchString(youtubePlaylistItem));
                    searchRequest.Limit = 1;

                    SearchResponse searchResponse = await spotify.Search.Item(searchRequest);

                    if (!searchResponse.Tracks.Total.HasValue)
                    {
                        //still add blank entry to make the list look nice
                        foundTracks.Add("");
                        continue;
                    }

                    if (!(searchResponse.Tracks.Total > 0))
                    {
                        //still add blank entry to make the list look nice
                        foundTracks.Add("");
                        continue;
                    }

                    FullTrack fullTrack = searchResponse.Tracks.Items[0];

                    List<SimpleArtist> artists = fullTrack.Artists;

                    List<string> artistNames = artists.Select(x => x.Name).ToList();


                    string songName = fullTrack.Name.ToString();

                    trackString.Add($"\"{fullTrack.Uri}\"");
                    foundTracks.Add(FormatResultString(songName, artistNames));
                }
            }

            AddTracksToPlaylist(newSpotifyPlaylistID, string.Join(",", trackString));
            resultModel.SpotifyTrackNames = foundTracks;
            resultModel.SpotifyLink = $"https://open.spotify.com/playlist/{newSpotifyPlaylistID}";
            resultModel.YoutubeLink = $"https://music.youtube.com/playlist?list={youtubePlaylistId}";
            return resultModel;
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

        private string FormatSpotifySearchString(YoutubePlaylistItem youtubePlaylistItem)
        {
            StringBuilder queryBuilder = new StringBuilder();

            queryBuilder.Append(string.Join(" ", youtubePlaylistItem.artists));
            queryBuilder.Append($" {youtubePlaylistItem.song}");

            return queryBuilder.ToString();
        }

        public void AddTracksToPlaylist(string newSpotifyPlaylistID, string tracksToAdd)
        {
            string url = $"https://api.spotify.com/v1/playlists/{newSpotifyPlaylistID}/tracks";

            string postData = "{";
            postData += "\"uris\": " + $"[{tracksToAdd}]";
            postData += "}";

            MakeSpotifyPostRequest(url, postData);
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
