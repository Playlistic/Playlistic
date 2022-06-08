using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Youtube2Spotify.Helpers;
using Youtube2Spotify.Models;
using System.IO;
namespace Youtube2Spotify.Controllers
{
    public class YoutubePlaylistTitleAndDescription
    {
        public string title;
        public string description;
    }
    public class Youtube2SpotifyController : Controller
    {
        private IWebHostEnvironment Environment;

        public Youtube2SpotifyController(IWebHostEnvironment _environment)
        {
            Environment = _environment;
        }

        public string YoutubePlaylistID { get; set; }
        public List<YoutubePlaylistItem> YoutubePlaylistItems = new List<YoutubePlaylistItem>();

        public IActionResult Index(string youtubePlaylistID)
        {
            if (!string.IsNullOrEmpty(youtubePlaylistID) || !string.IsNullOrWhiteSpace(youtubePlaylistID))
            {
                YoutubePlaylistID = youtubePlaylistID;
                HttpContext.Session.SetString("user_Id", GetUserId());

                return Result(GetYoutubeInfo(youtubePlaylistID));
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
            string ytInitialData = element.TextContent;
            List<string> scriptChunks = ytInitialData.Split(";").ToList();
            string rawPlaylistData = scriptChunks.First(x => x.Contains("\\/browse"));
            rawPlaylistData += ";";
            rawPlaylistData = rawPlaylistData.Replace("initialData.push(", "");
            rawPlaylistData.Replace(");", "");
            string realData = rawPlaylistData.Split("data: ").ToList().Last();
            realData = Regex.Unescape(@realData);
            realData = Regex.Unescape(@realData);
            realData = realData.Replace("'{", "{");
            realData = realData.Replace("}'", "");
            realData = realData.Replace(");", "");
            dynamic initialData = JsonConvert.DeserializeObject(realData);
            JArray playlist = initialData.contents.singleColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents[0].musicPlaylistShelfRenderer.contents;

            return playlist;
        }

        /// <summary>
        /// get title and artist name from youtube
        /// </summary>
        /// <param name="youtubePlaylistId"></param>
        /// <returns></returns>
        public ResultModel GetYoutubeInfo(string youtubePlaylistId)
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

                YoutubePlaylistItems.Add(info);
            }

            // add total number of song names
            ResultModel result = new ResultModel();
            result.TotalVideoNames.AddRange(songNames);

            // okay, we got the title, time to look it up on Spotify
            result.CompletedNames = GenerateSpotifyPlaylist(GenerateYoutubePlaylistTitleAndDescription(youtubePlaylistId), YoutubePlaylistItems);
            return result;
        }

        public YoutubePlaylistTitleAndDescription GenerateYoutubePlaylistTitleAndDescription(string playlistId)
        {
            string key = File.ReadAllLines($"{Environment.WebRootPath}\\Secret.txt")[0];

            string url = $"https://www.googleapis.com/youtube/v3/playlists?id={playlistId}&key={key}&part=id,snippet&fields=items(id,snippet(title,channelId,channelTitle,description))";
            dynamic json = MakeYoutubeGetCalls(url);
            return new YoutubePlaylistTitleAndDescription()
            {
                title = json.items[0].snippet.title,
                description = json.items[0].snippet.description + $"\n Pulled from:https://music.youtube.com/playlist?list={playlistId}"
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
        public List<string> GenerateSpotifyPlaylist(YoutubePlaylistTitleAndDescription youtubePlaylistTitleAndDescription, List<YoutubePlaylistItem> youtubePlaylistItems)
        {
            List<string> foundTracks = new List<string>();
            //create a playlist using the currently authenticated profile
            string newSpotifyPlaylistID = string.Empty;
            string user_Id = HttpContext.Session.GetString("user_Id");
            List<string> trackString = new List<string>();
            string url = $"https://api.spotify.com/v1/users/{user_Id}/playlists";

            string postData = "{";
            postData += "\"name\": " + $"\"{youtubePlaylistTitleAndDescription.title}\",";
            postData += "\"description\":" + $"\"{youtubePlaylistTitleAndDescription.description}\",";
            postData += "\"public\": true";
            postData += "}";

            using (HttpWebResponse response = MakeSpotifyPostRequest(url, postData))
            {
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    using Stream stream = response.GetResponseStream();
                    using StreamReader reader = new StreamReader(stream);
                    string spotifyResponse = reader.ReadToEnd();
                    dynamic json = JsonConvert.DeserializeObject(spotifyResponse);
                    newSpotifyPlaylistID = json.uri;
                    newSpotifyPlaylistID = newSpotifyPlaylistID.Replace("spotify:playlist:", "");
                }
            }

            if (!string.IsNullOrEmpty(newSpotifyPlaylistID))
            {
                foreach (YoutubePlaylistItem youtubePlaylistItem in youtubePlaylistItems)
                {
                    dynamic rightTrack = FindRightTrack(youtubePlaylistItem);

                    if (rightTrack == null)
                    {
                        //still add blank entry to make the list look nice
                        foundTracks.Add("");
                        continue;
                    }

                    JArray artists = rightTrack.artists;

                    List<string> artistNames = new List<string>();

                    foreach (dynamic artist in artists)
                    {
                        artistNames.Add(artist.name.ToString());
                    }

                    string songName = rightTrack.name.ToString();

                    trackString.Add($"\"{rightTrack.uri.ToString()}\"");
                    foundTracks.Add(FormatResultString(songName, artistNames));
                }
            }

            AddTracksToPlaylist(newSpotifyPlaylistID, string.Join(",", trackString));
            return foundTracks;
        }


        private dynamic FindRightTrack(YoutubePlaylistItem youtubePlaylistItem)
        {
            string queryString = FormatSpotifySearchString(youtubePlaylistItem);
            dynamic searchResult = GetTracks(queryString);

            if (searchResult == null)
            {
                return null;
            }

            dynamic rightTrack = searchResult.tracks.items[0];
            return rightTrack;
        }


        private string FormatResultString(string song, List<string> artists)
        {
            string songName = string.Empty;

            songName += $"{string.Join(", ", artists)} - {song}";

            return songName;
        }

        private string FormatSpotifySearchString(YoutubePlaylistItem youtubePlaylistItem)
        {
            StringBuilder queryBuilder = new StringBuilder();
            queryBuilder.Append("https://api.spotify.com/v1/search?query=");
            if (!string.IsNullOrEmpty(youtubePlaylistItem.song) && !string.IsNullOrWhiteSpace(youtubePlaylistItem.song))
            {
                string songName = youtubePlaylistItem.song;
                queryBuilder.Append(HttpUtility.UrlEncode(string.Join(" ", youtubePlaylistItem.artists)));
                queryBuilder.Append(HttpUtility.UrlEncode($" {songName}"));
            }
            queryBuilder.Append("&type=track&offset=0&limit=1");

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

        public dynamic GetTracks(string requestString)
        {
            dynamic search_result;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestString);
            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", "Bearer " + HttpContext.Session.GetString("access_token"));

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                search_result = reader.ReadToEnd();
            }

            dynamic json = JsonConvert.DeserializeObject(search_result);
            if (json.tracks.items.Count > 0)
            {
                return json;
            }
            return null;

        }

        public string GetUserId()
        {
            dynamic user_Object;
            string user_Id = HttpContext.Session.GetString("user_id");
            // if we already stored the UserId in session, grab it and return it 
            if (!string.IsNullOrEmpty(user_Id))
            {
                return user_Id;
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.spotify.com/v1/me");
            request.Method = "GET";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            string token = HttpContext.Session.GetString("access_token");
            request.Headers.Add("Authorization", "Bearer " + token);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                user_Object = reader.ReadToEnd();
            }

            dynamic json = JsonConvert.DeserializeObject(user_Object);
            user_Id = json.id;

            return user_Id;
        }
    }
}
