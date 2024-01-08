using System.Collections.Generic;

namespace Playlistic.Models
{
    public class SpotifySearchObject
    {
        public string Song { get; set; }
        public List<string> Artists { get; set; }
        public List<string> Producers { get; set; }
        public List<string> Featured_Artists { get; set; }

        public SpotifySearchObject()
        {
            Song = string.Empty;
            Artists = new List<string>();
            Producers = new List<string>();
            Featured_Artists = new List<string>();
        }
    }
}
