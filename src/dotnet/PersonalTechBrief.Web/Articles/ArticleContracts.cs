namespace PersonalTechBrief.Web.Articles;

public sealed record SubmitArticleRequest(string? Url);

public sealed record SubmitArticleResponse(Guid? ArticleId, string Status);
