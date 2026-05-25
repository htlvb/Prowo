using Npgsql;

namespace Prowo.WebAsm.Server.Data;

public class PgsqlEventStore : IEventStore, IDisposable
{
    private readonly NpgsqlDataSource dataSource;

    public PgsqlEventStore(string dbConnectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(dbConnectionString);
        dataSource = dataSourceBuilder.Build();
    }

    public async IAsyncEnumerable<Event> GetAll()
    {
        await using var dbConnection = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, title, \"start\", \"end\", visible_from, registration_from FROM event ORDER BY \"start\"",
            dbConnection);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            yield return ReadEvent(reader);
        }
    }

    public async Task<Event?> Get(string eventId)
    {
        if (!Guid.TryParse(eventId, out var guid)) return null;
        await using var dbConnection = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, title, \"start\", \"end\", visible_from, registration_from FROM event WHERE id = @id",
            dbConnection);
        cmd.Parameters.AddWithValue("id", guid);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return ReadEvent(reader);
    }

    public async Task Create(Event e)
    {
        await using var dbConnection = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO event (id, title, \"start\", \"end\", visible_from, registration_from) VALUES (@id, @title, @start, @end, @visible_from, @registration_from)",
            dbConnection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(e.Id));
        cmd.Parameters.AddWithValue("title", e.Title);
        cmd.Parameters.AddWithValue("start", e.Start.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("end", e.End.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("visible_from", e.VisibleFrom);
        cmd.Parameters.AddWithValue("registration_from", e.RegistrationFrom);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task Update(Event e)
    {
        await using var dbConnection = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE event SET title=@title, \"start\"=@start, \"end\"=@end, visible_from=@visible_from, registration_from=@registration_from WHERE id=@id",
            dbConnection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(e.Id));
        cmd.Parameters.AddWithValue("title", e.Title);
        cmd.Parameters.AddWithValue("start", e.Start.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("end", e.End.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("visible_from", e.VisibleFrom);
        cmd.Parameters.AddWithValue("registration_from", e.RegistrationFrom);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task Delete(string eventId)
    {
        if (!Guid.TryParse(eventId, out var guid)) return;
        await using var dbConnection = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM event WHERE id = @id", dbConnection);
        cmd.Parameters.AddWithValue("id", guid);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose() => dataSource.Dispose();

    private static Event ReadEvent(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0).ToString(),
            reader.GetString(1),
            DateOnly.FromDateTime(reader.GetDateTime(2)),
            DateOnly.FromDateTime(reader.GetDateTime(3)),
            reader.GetDateTime(4),
            reader.GetDateTime(5)
        );
}
