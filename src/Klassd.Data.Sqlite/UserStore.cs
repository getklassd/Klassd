using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>User store over raw Microsoft.Data.Sqlite.</summary>
public sealed class UserStore(SqliteContext context) : IUserStore
{
    private const string Columns = "id, username, password_hash, email, provider, external_id, disabled, roles";

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM users WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task<UserRecord?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM users WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM users";

        var list = new List<UserRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task InsertAsync(UserRecord user, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO users ({Columns}) VALUES (@id, @u, @p, @em, @pr, @e, @d, @roles)";
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@u", user.Username);
        cmd.Parameters.AddWithValue("@p", user.PasswordHash);
        cmd.Parameters.AddWithValue("@em", (object?)user.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pr", user.Provider);
        cmd.Parameters.AddWithValue("@e", (object?)user.ExternalId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@d", user.Disabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@roles", JsonSerializer.Serialize(user.Roles));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<UserRecord?> FindByExternalAsync(string provider, string externalId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM users WHERE provider = @p AND external_id = @e";
        cmd.Parameters.AddWithValue("@p", provider);
        cmd.Parameters.AddWithValue("@e", externalId);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM users WHERE email = @e";
        cmd.Parameters.AddWithValue("@e", email);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task UpdateAsync(UserRecord user, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE users SET
              username = @u, email = @em, password_hash = @p, provider = @pr,
              external_id = @e, disabled = @d, roles = @roles
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@u", user.Username);
        cmd.Parameters.AddWithValue("@p", user.PasswordHash);
        cmd.Parameters.AddWithValue("@em", (object?)user.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pr", user.Provider);
        cmd.Parameters.AddWithValue("@e", (object?)user.ExternalId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@d", user.Disabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@roles", JsonSerializer.Serialize(user.Roles));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<UserRecord?> ReadOneAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    private static UserRecord Map(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        Username = r.GetString(1),
        PasswordHash = r.GetString(2),
        Email = r.IsDBNull(3) ? null : r.GetString(3),
        Provider = r.GetString(4),
        ExternalId = r.IsDBNull(5) ? null : r.GetString(5),
        Disabled = r.GetBoolean(6),
        Roles = r.IsDBNull(7) ? new() : (JsonSerializer.Deserialize<List<string>>(r.GetString(7)) ?? new()),
    };
}
