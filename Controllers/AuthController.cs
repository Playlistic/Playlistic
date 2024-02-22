using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using System.Web;
using Playlistic.Helpers;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.Threading.Tasks;

namespace Playlistic.Controllers
{
    public class AuthController : Controller
    {
        string code_challenge;
        string code_verifier;
        private string spotifyAppId;
        private IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
            spotifyAppId = _configuration["SpotifyId"];
        }

        [HttpPost]
        public IActionResult TriggerAuth()
        {
            var rng = RandomNumberGenerator.Create();

            var bytes = new byte[32];
            rng.GetBytes(bytes);

            // It is recommended to use a URL-safe string as code_verifier.
            // See section 4 of RFC 7636 for more details.
            code_verifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');


            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(code_verifier));
                code_challenge = Convert.ToBase64String(challengeBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
            HttpContext.Session.SetString("code_verifier", code_verifier);



            return Redirect($"https://accounts.spotify.com/authorize?client_id={spotifyAppId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(PlaylisticHttpContext.AppBaseUrl)}%2FAuth%2FAuthReturnCode&scope=playlist-modify-public%20ugc-image-upload&code_challenge_method=S256&code_challenge=" + code_challenge);
        }

        public async Task<IActionResult> AuthReturnCode(string code)
        {
            if (code != null)
            {
                PKCETokenResponse spotifyToken = await new OAuthClient().RequestToken(new PKCETokenRequest(spotifyAppId, code, new Uri($"{HttpUtility.UrlEncode(PlaylisticHttpContext.AppBaseUrl)}%2FAuth%2FAuthReturnCode"), code_verifier));
                HttpContext.Session.SetString("access_token", spotifyToken.AccessToken);
                HttpContext.Session.SetString("expire_time", DateTime.Now.AddSeconds(spotifyToken.ExpiresIn).ToString());
            }

            return Redirect("~/");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
