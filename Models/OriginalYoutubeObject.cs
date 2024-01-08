namespace Playlistic.Models
{    public class OriginalYoutubeObject
    {
        public string VideoTitle
        {
            get; set;
        }
        public string VideoChannelTitle
        {
            get; set;
        }
        public string VideoId
        {
            get; set;
        }
        public string ThumbnailURL
        {
            get; set;
        }
        public OriginalYoutubeObject()
        {
            VideoTitle = string.Empty;
            VideoChannelTitle = string.Empty;
            VideoId = string.Empty;
            ThumbnailURL = string.Empty;
        }
    }
}
