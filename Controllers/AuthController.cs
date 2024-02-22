using System;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Playlistic.Helpers;
using SpotifyAPI.Web;


namespace Playlistic.Controllers
{
    public class AuthController : Controller
    {
        private readonly string spotifyAppId;
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
            spotifyAppId = _configuration["SpotifyId"];
        }

        [HttpPost]
        public IActionResult TriggerAuth()
        {
            // Generates a secure random verifier of length 100 and its challenge
            var (verifier, challenge) = PKCEUtil.GenerateCodes();
            HttpContext.Session.SetString("code_verifier", verifier);

            var loginRequest = new LoginRequest(new Uri($"{HttpUtility.UrlEncode(PlaylisticHttpContext.AppBaseUrl)}%2FAuth%2FAuthReturnCode"),
              spotifyAppId,
              LoginRequest.ResponseType.Code
            )
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = new[] { Scopes.PlaylistModifyPublic, Scopes.UgcImageUpload }
            };
            var uri = loginRequest.ToUri();
            return Redirect(uri.ToString());
        }

        public async Task<IActionResult> AuthReturnCode(string code)
        {
            if (code != null)
            {
                string code_verifier = HttpContext.Session.GetString("code_verifier");
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
