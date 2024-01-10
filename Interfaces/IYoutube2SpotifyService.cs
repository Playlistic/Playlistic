using Newtonsoft.Json.Linq;
using Playlistic.Models;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Playlistic.Interfaces
{
    public interface IYoutube2SpotifyService
    {
        public SpotifyClient SpotifyClient { set; }
        public dynamic GetYoutubePlaylistDataFromHTML(string playlistId);
        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(dynamic playlistData);
        public List<Playlistic_PlaylistItem> GetPreliminaryPlaylistItems(JArray incomingRawYoutubeMusicPlaylistData);
        public bool AddTrackToSpotifyPlaylist(string spotifyPlaylistId, List<FullTrack> tracksToAdd, string accessToken);
        public Task<bool> UploadCoverToPlaylist(string SpotifyPlaylistId, YoutubePlaylistMetadata youtubePlaylistMetadata);
        public Task<string> CreateEmptyPlayListOnSpotify(YoutubePlaylistMetadata youtubePlaylistMetadata, string user_Id);
        public Task<List<Playlistic_PlaylistItem>> SearchForSongsOnSpotify(List<Playlistic_PlaylistItem> playlistItems);
        public Task<string> GetUserIdLive();
    }
}
