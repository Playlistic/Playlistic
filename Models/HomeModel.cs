namespace Playlistic.Models
{
    public class HomeModel
    {
        private bool _authenticated;
        public bool Authenticated => _authenticated;

        public HomeModel(bool authenticated)
        {
            _authenticated = authenticated;
        }
        public void SetAuthenticated(bool status)
        {
            _authenticated = status;
        }
    }
}
