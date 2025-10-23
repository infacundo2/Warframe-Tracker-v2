using System.Net.Http.Json;
using WarframeInventory.Models;

namespace WarframeInventory.Services
{
    public class WarframeApiService
    {
        private readonly HttpClient _http;

        public WarframeApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Warframe>> GetWarframesAsync()
        {
            var result = await _http.GetFromJsonAsync<List<Warframe>>("https://api.warframestat.us/warframes/?language=es");
            return result ?? new List<Warframe>();
        }
    }
}
