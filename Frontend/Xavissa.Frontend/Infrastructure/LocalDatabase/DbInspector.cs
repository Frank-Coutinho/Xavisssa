using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public static class DbInspector
{
    public static async Task PrintSchema(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        Console.WriteLine("\n=== SQLITE SCHEMA DEBUG ===");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='table';";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"TABLE: {reader.GetString(0)}");
                Console.WriteLine(reader.GetString(1));
                Console.WriteLine();
            }
        }
    }
}
