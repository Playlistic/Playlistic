using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Youtube2Spotify.Models
{
    public class SpotifyToken
    {
        public string access_token;
        public string token_type;
        public int expires_in;
        public string refresh_token;
        public string scope;
    }
}
