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
        public string OpenAIAccessToken { set; }
        public string OpenAIAssistantSetupString { set; }
        public Task<dynamic> GetYoutubePlaylistDataFromHTML(string playlistId);
        public YoutubePlaylistMetadata GenerateYoutubePlaylistMetadata(dynamic playlistData);
        public List<Playlistic_PlaylistItem> GetPreliminaryPlaylistItems(JArray incomingRawYoutubeMusicPlaylistData);
        public Task<bool> AddTrackToSpotifyPlaylist(string spotifyPlaylistId, List<FullTrack> tracksToAdd);
        public Task<bool> UploadCoverToPlaylist(string SpotifyPlaylistId, YoutubePlaylistMetadata youtubePlaylistMetadata);
        public Task<string> CreateEmptyPlayListOnSpotify(YoutubePlaylistMetadata youtubePlaylistMetadata, string user_Id);
        public Task<List<Playlistic_PlaylistItem>> SearchForSongsOnSpotify(List<Playlistic_PlaylistItem> playlistItems);
        public Task<ResultModel> ConvertYoutubePlaylist2SpotifyPlaylist(string youtubePlaylistId);
    }
}
