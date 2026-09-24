using System.Net;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Articles;

// Guards against the Blazor form pages returning 500 when the antiforgery middleware is missing:
// a rendered EditForm carries antiforgery metadata that requires app.UseAntiforgery().
public sealed class ManualArticlePageTests(InterestApiFactory factory) : IClassFixture<InterestApiFactory>
{
    [Theory]
    [InlineData("/articles")]
    [InlineData("/sources")]
    [InlineData("/interests")]
    public async Task Form_pages_render_without_a_server_error(string path)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
