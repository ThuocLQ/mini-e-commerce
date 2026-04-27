using Microsoft.Data.Sqlite;

namespace CatalogService.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    
    public DatabaseInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = """
              CREATE TABLE IF NOT EXISTS Products (
                  Id TEXT PRIMARY KEY NOT NULL,
                  Name TEXT NOT NULL,
                  Price REAL NOT NULL DEFAULT 0
              );
              """;
        await command.ExecuteNonQueryAsync();
    }
}