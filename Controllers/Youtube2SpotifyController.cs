using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playlistic.Helpers;
using Playlistic.Models;
using Microsoft.AspNetCore.Hosting;
using Playlistic.Interfaces;

namespace Playlistic.Controllers
{
    public class Youtube2SpotifyController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IYoutube2SpotifyService _youtube2SpotifyService;
        private string _access_Token { get { return HttpContext.Session.GetString("access_token"); } }
        private string _user_Id { get { return HttpContext.Session.GetString("user_Id"); } }      
        private string YoutubePlaylistID { get; set; }
        private YoutubePlaylistMetadata youtubePlaylistMetadata;

        private dynamic InitialData;
        private readonly string openAIAccessToken;
        private readonly string openAIAssistantSetupString;

        public Youtube2SpotifyController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment, IYoutube2SpotifyService youtube2SpotifyService)
        {
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
            openAIAccessToken = configuration["OpenAIKey"];
            openAIAssistantSetupString = configuration["OpenAIPrompt"];
            _youtube2SpotifyService = youtube2SpotifyService;
        }


        public async Task<IActionResult> Index(string youtubePlaylistID)
        {
            HomeModel homeModel = new(false);

            try
            {
                string expire_time_raw = HttpContext.Session.GetString("expire_time");
                DateTime expire_Time = DateTime.Parse(expire_time_raw);
                if (DateTime.Now > expire_Time)
                {
                    return Home(homeModel);
                }
            }
            catch
            {
                return Home(homeModel);
            }

            homeModel.SetAuthenticated(true);

            _youtube2SpotifyService.SpotifyClient = new SpotifyClient(_access_Token);
            HttpContext.Session.SetString("user_Id", await _youtube2SpotifyService.GetUserIdLive());

            if (!string.IsNullOrEmpty(youtubePlaylistID) || !string.IsNullOrWhiteSpace(youtubePlaylistID))
            {
                YoutubePlaylistID = youtubePlaylistID;
                return Result(await ConvertYoutubePlaylist2SpotifyPlaylist(youtubePlaylistID));
            }
            else
            {
                return Home(homeModel);
            }
        }

        public PartialViewResult Result(ResultModel result = null)
        {
            return PartialView("~/Views/Result/Index.cshtml", result);
        }

        public PartialViewResult Home(HomeModel home = null)
        {
            return PartialView("~/Views/Home/Index.cshtml", home);
        }

        private static JArray GetPlaylistItem(dynamic initialData)
        {
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
            ResultModel resultModel = new();
            List<Playlistic_PlaylistItem> PlaylistItems = new();

            //youtube data api have now comletely failed for any playlist other than user created playlist
            //this is not acceptable, now getting metadata directly through youtube music
            try
            {
                //collect playlist data(including metadata and playlist items) from youtube music 
                InitialData = _youtube2SpotifyService.GetYoutubePlaylistDataFromHTML(youtubePlaylistId);
            }
            catch (Exception ex)
            {
                //something is weird with the youtubePlaylistId
                return new ResultModel(FaultCode.Unspported, $"https://youtube.com/playlist?list={youtubePlaylistId}");
            }

            try
            {
                youtubePlaylistMetadata = _youtube2SpotifyService.GenerateYoutubePlaylistMetadata(InitialData);

                List<string> songNames = new();

                JArray playlist = GetPlaylistItem(InitialData);

                if (playlist == null)
                {
                    //empty playlist, halt further processing
                    return new ResultModel(FaultCode.EmptyPlaylist, $"https://youtube.com/playlist?list={youtubePlaylistId}");
                }

                PlaylistItems = _youtube2SpotifyService.GetPreliminaryPlaylistItems(playlist);

                int numIterations = PlaylistItems.Count / 10;
                List<Playlistic_PlaylistItem> Results = new();
                // break input list into sublist of max 10 items
                for (int i = 0; i < numIterations; i++)
                {
                    var Sublist = PlaylistItems.Take(new Range(i * 10, i * 10 + 10));
                    var SubPlaylistItems = PlaylistItemFactory.CleanUpPlaylistItems_PoweredByAI(Sublist.ToList(), openAIAssistantSetupString, openAIAccessToken);
                    Results.AddRange(SubPlaylistItems);
                }

                PlaylistItems = Results;
                PlaylistItems = await _youtube2SpotifyService.SearchForSongsOnSpotify(PlaylistItems);

                if (PlaylistItems.Any(x => { return x.FoundSpotifyTrack != null; }))
                {
                    // break input list into sublist of max 10 items
                    // process each sublist with chatgpt
                    // merge the output of the sublist and output the list

                    // add total number of song names
                    // okay, we got the title, time to look it up on Spotify
                    string newSpotifyPlaylistID = await _youtube2SpotifyService.CreateEmptyPlayListOnSpotify(youtubePlaylistMetadata, _user_Id);

                    await _youtube2SpotifyService.UploadCoverToPlaylist(newSpotifyPlaylistID, youtubePlaylistMetadata);


                    List<VerificationObject> verificationObjects = PlaylistItems.Select(x => { return new VerificationObject(PlaylistItems.IndexOf(x), x.SpotifySearchObject.Song, x.FoundSpotifyTrack.Name); }).ToList();
                    string jsonString = JsonConvert.SerializeObject(verificationObjects);

                    bool success = _youtube2SpotifyService.AddTrackToSpotifyPlaylist(newSpotifyPlaylistID, PlaylistItems.Select(x => { return x.FoundSpotifyTrack; }).ToList(), _access_Token);

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

        /// <summary>
        /// Simulate a conversion result without having to execute the actual conversion process
        /// </summary>
        /// <returns></returns>
        public PartialViewResult FrontEndTest()
        {
            return PartialView("~/Views/Result/Index.cshtml", JsonConvert.DeserializeObject<ResultModel>(System.IO.File.ReadAllText($"{_hostingEnvironment.WebRootPath}\\SampleData.json")));
        }
    }
}
