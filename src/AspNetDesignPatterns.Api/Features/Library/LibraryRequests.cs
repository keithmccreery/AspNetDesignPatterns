namespace AspNetDesignPatterns.Api.Features.Library;

// The single-resource requests. Route values bind by name ({bookId} → BookId); MemberId binds
// from the access token. No validators: an id that matches nothing is a 404, which the handler
// already returns, so there's no rule a validator could add.

/// <summary><c>GET /library/books/{bookId}</c>.</summary>
internal sealed record GetBookRequest(int BookId);

/// <summary><c>POST /library/books/{bookId}/loans</c>: borrow one copy.</summary>
internal sealed record BorrowBookRequest(int BookId, MemberId Member);

/// <summary><c>GET /library/loans/{loanId}</c>: one of the caller's loans.</summary>
internal sealed record GetLoanRequest(int LoanId, MemberId Member);

/// <summary><c>POST /library/loans/{loanId}/return</c>.</summary>
internal sealed record ReturnLoanRequest(int LoanId, MemberId Member);
