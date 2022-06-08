using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Youtube2Spotify.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace Youtube2Spotify.Controllers
{
    public class AuthController : Controller
    {
        string code_challenge;
        string code_verifier;
        private string spotifyAppId;
        private IWebHostEnvironment Environment;

        public AuthController(IWebHostEnvironment _environment)
        {
            Environment = _environment;
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

            spotifyAppId = System.IO.File.ReadAllLines($"{Environment.WebRootPath}\\Secret.txt")[1];

            return Redirect($"https://accounts.spotify.com/authorize?client_id={spotifyAppId}&response_type=code&redirect_uri=https%3A%2F%2Flocalhost%3a44320%2FAuth%2FAuthReturnCode&scope=playlist-modify-public%20playlist-modify-private%20playlist-read-private%20playlist-read-collaborative&code_challenge_method=S256&code_challenge=" + code_challenge);
        }

        public IActionResult AuthReturnCode(string code)
        {
            if (code != null)
            {
                string url = "https://accounts.spotify.com/api/token";
                string html = string.Empty;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                spotifyAppId = System.IO.File.ReadAllLines($"{Environment.WebRootPath}\\Secret.txt")[1];
                string postData = "client_id=" + spotifyAppId;
                postData += "&grant_type=" + "authorization_code";
                postData += "&code=" + code;
                postData += "&redirect_uri=" + "https%3A%2F%2Flocalhost%3a44320%2FAuth%2FAuthReturnCode";               
                code_verifier = HttpContext.Session.GetString("code_verifier");
                postData += "&code_verifier=" + code_verifier;
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    html = reader.ReadToEnd();
                }
                SpotifyToken spotifyToken = JsonConvert.DeserializeObject<SpotifyToken>(html);
                HttpContext.Session.SetString("access_token", spotifyToken.access_token);
            }

            return Redirect("~/");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
