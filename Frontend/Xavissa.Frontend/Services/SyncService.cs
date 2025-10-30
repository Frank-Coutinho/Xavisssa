using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;
using System.Text.Json;
using System;
using System.Threading.Tasks;
using System.Net.Http;


namespace Xavissa.Frontend.Services
{
    public class SyncService
    {
        private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("http://localhost:5000/") };

        public async Task SyncAsync()
        {
            using var db = new LocalDbContext();

            // Push local changes to server
            var pending = await db.SyncLogs.Where(s => !s.Synced).ToListAsync();
            if (pending.Any())
            {
                var response = await _httpClient.PostAsJsonAsync("api/sync/upload", pending);
                if (response.IsSuccessStatusCode)
                {
                    foreach (var log in pending)
                        log.Synced = true;
                    await db.SaveChangesAsync();
                }
            }

            // Pull latest from server
            var updates = await _httpClient.GetFromJsonAsync<List<Product>>("api/sync/download");
            if (updates != null)
            {
                foreach (var product in updates)
                {
                    var local = await db.Products.FindAsync(product.Id);
                    if (local == null || local.LastModified < product.LastModified)
                    {
                        db.Products.Update(product);
                    }
                }
                await db.SaveChangesAsync();
            }
        }
    }
}
