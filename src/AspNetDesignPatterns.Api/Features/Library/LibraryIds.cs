using System.Globalization;

namespace AspNetDesignPatterns.Api.Features.Library;

// Strongly typed ids. On the wire a book id and a loan id are both plain integers, and stay
// that way: routes take /books/7, responses and cursors carry "id": 7. What changes is
// everything between the edge and the database. The route value is parsed into a BookId or
// LoanId once, by TryParse, as the request record is bound; from then on the endpoint,
// handler, service, repository and row types all say which id they mean, so passing a loan id
// where a book id belongs is a compile error instead of a 404 (or, worse, the wrong row). The
// Library README's "Strongly typed ids" section has the whole picture.
//
// In the OpenAPI document they're plain integers too (IdOpenApiSchemaTransformer).
//
// readonly record structs: no allocation, value equality, and nothing to null-check. Both
// override ToString to print the bare number, so a log line or an interpolated route says "7",
// not "BookId { Value = 7 }".

/// <summary>A book's id.</summary>
internal readonly record struct BookId(int Value)
{
    /// <summary>Minimal API binding hook: parses the <c>{bookId}</c> route value.</summary>
    public static bool TryParse(string? value, out BookId id)
    {
        bool parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number);
        id = new BookId(number);
        return parsed;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A loan's id.</summary>
internal readonly record struct LoanId(int Value)
{
    /// <summary>Minimal API binding hook: parses the <c>{loanId}</c> route value.</summary>
    public static bool TryParse(string? value, out LoanId id)
    {
        bool parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number);
        id = new LoanId(number);
        return parsed;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
