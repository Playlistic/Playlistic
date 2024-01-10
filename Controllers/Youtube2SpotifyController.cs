using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using System;
using System.Text;
using System.Threading.Tasks;
using Playlistic.Models;
using Microsoft.AspNetCore.Hosting;
using Playlistic.Interfaces;

namespace Playlistic.Controllers
{
    public class Youtube2SpotifyController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IYoutube2SpotifyService _youtube2SpotifyService;
        private string _access_Token { get { return HttpContext.Session.GetString("access_token"); } }


        public Youtube2SpotifyController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment, IYoutube2SpotifyService youtube2SpotifyService)
        {
            _hostingEnvironment = hostingEnvironment;
            _youtube2SpotifyService = youtube2SpotifyService;
            _youtube2SpotifyService.OpenAIAccessToken = configuration["OpenAIKey"];
            _youtube2SpotifyService.OpenAIAssistantSetupString = configuration["OpenAIPrompt"];
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

            if (!string.IsNullOrEmpty(youtubePlaylistID) || !string.IsNullOrWhiteSpace(youtubePlaylistID))
            {
                return Result(await _youtube2SpotifyService.ConvertYoutubePlaylist2SpotifyPlaylist(youtubePlaylistID));
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
