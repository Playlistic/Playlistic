using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Youtube2Spotify.Models
{
    public class ResultModel
    {
        public List<string> TotalVideoNames;
        public List<string> CompletedNames;
        public string SpotifyLink;

        public ResultModel()
        {
            TotalVideoNames = new List<string>();
            CompletedNames = new List<string>();
            SpotifyLink = string.Empty;
        }
    }
}
