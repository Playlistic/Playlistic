namespace Playlistic.Models
{
    public class VerificationObject
    {
        public int Index;
        public string OriginalYoutubeName;
        public string FoundSpotifyName;

        public VerificationObject(int index, string originalYoutubeName, string foundSpotifyName)
        {
            Index = index;
            OriginalYoutubeName = originalYoutubeName;
            FoundSpotifyName = foundSpotifyName;
        }
    }
}
