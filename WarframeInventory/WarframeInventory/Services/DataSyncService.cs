using WarframeInventory.Data;
using WarframeInventory.Models;
using Microsoft.EntityFrameworkCore;

namespace WarframeInventory.Services
{
    public class DataSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly WarframeApiService _api;

        public DataSyncService(ApplicationDbContext context, WarframeApiService api)
        {
            _context = context;
            _api = api;
        }

        public async Task SyncWarframesAsync()
        {
            var apiWarframes = await _api.GetWarframesAsync();
            var existing = await _context.Warframes.AsNoTracking().ToListAsync();

            foreach (var wf in apiWarframes)
            {
                wf.Description ??= "(Sin descripción)";
                _context.Warframes.Add(wf);
                if (!existing.Any(e => e.UniqueName == wf.UniqueName))
                {
                    _context.Warframes.Add(wf);
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}
