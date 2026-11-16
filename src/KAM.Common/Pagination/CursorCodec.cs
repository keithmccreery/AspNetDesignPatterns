using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace KAM.Common.Pagination;

/// <summary>
/// Turns a page's position — the sort key of its last row, as a small record such as
/// <c>BookCursor(string Title, int Id)</c> — into an opaque, URL-safe cursor string and back:
/// JSON, then base64url.
/// </summary>
/// <remarks>
/// Opaque, not secret or signed: a cursor holds only a position, so a client that edits one
/// just starts somewhere else in a list it can already read. If a cursor ever needs to carry
/// something a client mustn't forge (a tenant, a permission, a snapshot id), sign it with an
/// HMAC before encoding.
/// </remarks>
public static class CursorCodec
{
    /// <summary>The longest cursor <see cref="TryDecode{TKey}"/> will attempt to decode.</summary>
    public const int MaxLength = 512;

    // Strict on the way back in: a missing or null member is a malformed cursor, not a
    // half-populated key whose nulls would silently change the query.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static string Encode<TKey>(TKey key)
        where TKey : notnull =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(key, SerializerOptions));

    /// <summary>Decodes a cursor produced by <see cref="Encode{TKey}"/>; <see langword="false"/> for anything else.</summary>
    public static bool TryDecode<TKey>(string? cursor, [NotNullWhen(true)] out TKey? key)
        where TKey : notnull
    {
        key = default;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > MaxLength)
        {
            return false;
        }

        try
        {
            key = JsonSerializer.Deserialize<TKey>(Base64Url.DecodeFromChars(cursor), SerializerOptions);
            return key is not null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}
