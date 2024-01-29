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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Playlistic.Services
{
    public class Youtube2SpotifyServices : IYoutube2SpotifyService
    {
        private SpotifyClient spotify;
        private YoutubePlaylistMetadata youtubePlaylistMetadata;
        public SpotifyClient SpotifyClient { set { spotify = value; } }
        private dynamic InitialData;
        private string openAIAccessToken;
        private string openAIAssistantSetupString;
        private PrivateUser user;

        public string OpenAIAccessToken { set { openAIAccessToken = value; } }
        public string OpenAIAssistantSetupString { set { openAIAssistantSetupString = value; } }

        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(dynamic playlistData)
        {
            string title = playlistData.header.musicDetailHeaderRenderer.title.runs[0].text;
            string description = playlistData.header.musicDetailHeaderRenderer.description?.runs[0].text;
            string coverArt = playlistData.header.musicDetailHeaderRenderer.thumbnail.croppedSquareThumbnailRenderer.thumbnail.thumbnails[1].url;
            //apparently description can be null
            description = string.IsNullOrEmpty(description)? string.Empty : description;

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

        public async Task<bool> AddTrackToSpotifyPlaylist(string spotifyPlaylistId, List<FullTrack> tracksToAdd)
        {
            List<string> trackURI = new();

            foreach (FullTrack fullTrack in tracksToAdd)
            {
                if (fullTrack != null)
                {
                    List<SimpleArtist> artists = fullTrack.Artists;
                    List<string> artistNames = artists.Select(x => x.Name).ToList();
                    string songName = fullTrack.Name.ToString();
                    trackURI.Add(fullTrack.Uri);
                }
                continue;
            }

            try
            {
                PlaylistAddItemsRequest playlistAddItemsRequest = new(trackURI);

                SnapshotResponse snapShotResponse = await spotify.Playlists.AddItems(spotifyPlaylistId, playlistAddItemsRequest);
                if (snapShotResponse.SnapshotId != null)
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

        /// <summary>
        /// get title and artist name from youtube
        /// </summary>
        /// <param name="youtubePlaylistId"></param>
        /// <returns></returns>
        public async Task<ResultModel> ConvertYoutubePlaylist2SpotifyPlaylist(string youtubePlaylistId)
        {
            ResultModel resultModel = new();
            List<Playlistic_PlaylistItem> PlaylistItems = new();
            user = await spotify.UserProfile.Current();


            //youtube data api have now comletely failed for any playlist other than user created playlist
            //this is not acceptable, now getting metadata directly through youtube music
            try
            {
                //collect playlist data(including metadata and playlist items) from youtube music 
                InitialData = GetYoutubePlaylistDataFromHTML(youtubePlaylistId);
            }
            catch (Exception ex)
            {
                //something is weird with the youtubePlaylistId
                return new ResultModel(FaultCode.Unspported, $"https://youtube.com/playlist?list={youtubePlaylistId}");
            }

            try
            {
                youtubePlaylistMetadata = GenerateYoutubePlaylistMetadata(InitialData);

                List<string> songNames = new();

                JArray playlist = GetPlaylistItem(InitialData);

                if (playlist == null)
                {
                    //empty playlist, halt further processing
                    return new ResultModel(FaultCode.EmptyPlaylist, $"https://youtube.com/playlist?list={youtubePlaylistId}");
                }

                PlaylistItems = GetPreliminaryPlaylistItems(playlist);

                decimal d = (decimal)PlaylistItems.Count / (decimal)10;
                int numIterations = (int)Math.Ceiling(d);

                List<Playlistic_PlaylistItem> Results = new();
                // break input list into sublist of max 10 items
                for (int i = 0; i < numIterations; i++)
                {
                    var Sublist = PlaylistItems.Take(new Range(i * 10, i * 10 + 10));
                    var SubPlaylistItems = PlaylistItemFactory.CleanUpPlaylistItems_PoweredByAI(Sublist.ToList(), openAIAssistantSetupString, openAIAccessToken);
                    Results.AddRange(SubPlaylistItems);
                }

                PlaylistItems = Results;
                PlaylistItems = await SearchForSongsOnSpotify(PlaylistItems);

                if (PlaylistItems.Any(x => { return x.FoundSpotifyTrack != null; }))
                {
                    // break input list into sublist of max 10 items
                    // process each sublist with chatgpt
                    // merge the output of the sublist and output the list

                    // add total number of song names
                    // okay, we got the title, time to look it up on Spotify

                    //hol up, check if the user already have an existing playlist wit the same name/description
                    bool success = false;

                    string spotifyPlaylistID = await GetExistingPlaylistId(youtubePlaylistMetadata, user.Id);

                    if (string.IsNullOrEmpty(spotifyPlaylistID))
                    {
                        spotifyPlaylistID = await CreateEmptyPlayListOnSpotify(youtubePlaylistMetadata, user.Id);
                        await UploadCoverToPlaylist(spotifyPlaylistID, youtubePlaylistMetadata);
                        success = await AddTrackToSpotifyPlaylist(spotifyPlaylistID, PlaylistItems.Select(x => { return x.FoundSpotifyTrack; }).ToList());
                    }
                    else
                    {
                        //update the cover 
                        await UploadCoverToPlaylist(spotifyPlaylistID, youtubePlaylistMetadata);
                        //get existing tracks for current playlist
                        FullPlaylist fullPlaylist = await spotify.Playlists.Get(spotifyPlaylistID);
                        //get the trackIds
                        List<string> existingTracksIds = fullPlaylist.Tracks.Items.Select(x => { return ((FullTrack)x.Track).Id; }).ToList();
                        List<FullTrack> newTracks = PlaylistItems.Select(x => { return x.FoundSpotifyTrack; }).ToList();
                        //remove any existing tracks from the list we are intending to add to spotify
                        newTracks = newTracks.Where(x => !existingTracksIds.Contains(x.Id)).ToList();
                        success = await AddTrackToSpotifyPlaylist(spotifyPlaylistID, newTracks);
                    }

                    // used to generate test data for testing results page
                    //List<VerificationObject> verificationObjects = PlaylistItems.Select(x => { return new VerificationObject(PlaylistItems.IndexOf(x), x.SpotifySearchObject.Song, x.FoundSpotifyTrack.Name); }).ToList();
                    //string jsonString = JsonConvert.SerializeObject(verificationObjects);

                    if (success)
                    {                        
                        resultModel.PlaylistItems = PlaylistItems;
                        resultModel.SpotifyLink = $"https://open.spotify.com/playlist/{spotifyPlaylistID}";
                        resultModel.YoutubeLink = $"https://youtube.com/playlist?list={youtubePlaylistId}";
                        string resultModelString = JsonConvert.SerializeObject(resultModel);
                        return resultModel;
                    }
                    throw new Exception("Failed to add tracks to spotify");

                }
                else
                {
                    return new ResultModel(FaultCode.EmptySearchResult, $"https://youtube.com/playlist?list={youtubePlaylistId}");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
                return new ResultModel(FaultCode.ConversionFailed, $"https://youtube.com/playlist?list={youtubePlaylistId}");
            }
        }

        private async Task<string> GetExistingPlaylistId(YoutubePlaylistMetadata youtubePlaylistMetadata, string userId)
        {
            Paging<FullPlaylist> fullPlaylist = await spotify.Playlists.GetUsers(userId);
            FullPlaylist existingPlaylist = fullPlaylist.Items.FirstOrDefault(x =>
            {
                if (x.Name == youtubePlaylistMetadata.title && x.Description == youtubePlaylistMetadata.description)
                {
                    return true;
                }
                return false;
            });

            if (existingPlaylist != null)
            {
                return existingPlaylist.Id;
            }
            return null;

        }
        private static JArray GetPlaylistItem(dynamic initialData)
        {
            JArray playlist = initialData.contents.singleColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents[0].musicPlaylistShelfRenderer.contents;
            return playlist;
        }
    }
}
