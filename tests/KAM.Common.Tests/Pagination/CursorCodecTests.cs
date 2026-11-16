using System.Buffers.Text;
using System.Text;

using KAM.Common.Pagination;

namespace KAM.Common.Tests.Pagination;

[TestFixture]
public class CursorCodecTests
{
    private sealed record TitleCursor(string Title, int Id);

    private static string Base64UrlOf(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

    [Test]
    public void A_key_round_trips_through_an_opaque_url_safe_string()
    {
        // Arrange
        TitleCursor key = new("Poems / + = ?", 26);

        // Act
        string cursor = CursorCodec.Encode(key);
        bool decoded = CursorCodec.TryDecode(cursor, out TitleCursor? roundTripped);

        // Assert
        cursor.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        decoded.Should().BeTrue();
        roundTripped.Should().Be(key);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not base64url!")]
    public void Rejects_a_missing_or_undecodable_cursor(string? cursor)
    {
        // Act & Assert
        CursorCodec.TryDecode(cursor, out TitleCursor? _).Should().BeFalse();
    }

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("{\"id\":3}")]
    [TestCase("{\"title\":null,\"id\":3}")]
    [TestCase("{\"title\":\"x\",\"id\":\"three\"}")]
    public void Rejects_valid_base64url_that_is_not_a_complete_key(string json)
    {
        // Act & Assert — a missing or null member would otherwise quietly change the query
        CursorCodec.TryDecode(Base64UrlOf(json), out TitleCursor? _).Should().BeFalse();
    }

    [Test]
    public void Rejects_a_cursor_longer_than_the_maximum_without_decoding_it()
    {
        // Arrange
        string cursor = CursorCodec.Encode(new TitleCursor(new string('x', CursorCodec.MaxLength), 1));

        // Act & Assert
        CursorCodec.TryDecode(cursor, out TitleCursor? _).Should().BeFalse();
    }
}
