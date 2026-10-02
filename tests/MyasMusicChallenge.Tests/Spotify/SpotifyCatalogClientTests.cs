using System.Net;
using MyasMusicChallenge.Core.Spotify;
using MyasMusicChallenge.Tests.TestUtilities;

namespace MyasMusicChallenge.Tests.Spotify;

public class SpotifyCatalogClientTests
{
    private const string ArtistId = "6lHL3ubAMgSasKjNqKb8HF";
    private const string AlbumId = "1AAAAAAAAAAAAAAAAAAAAA";
    private const string SingleId = "2BBBBBBBBBBBBBBBBBBBBB";

    private static readonly SpotifyOptions Options = new() { ClientId = "id", ClientSecret = "secret" };

    [Theory]
    [InlineData("Mýa", "mya")]
    [InlineData("MYA", "mya")]
    [InlineData("Mya Harrison", "myaharrison")]
    [InlineData(null, "")]
    public void NormalizeName_RemovesAccentsAndPunctuation(string? name, string expected)
    {
        Assert.Equal(expected, SpotifyCatalogClient.NormalizeName(name));
    }

    [Fact]
    public async Task GetDiscography_FindsMyaAndLoadsAlbumTracks()
    {
        var handler = new StubHttpHandler(Respond);
        var client = new SpotifyCatalogClient(new HttpClient(handler), Options);

        var songs = await client.GetDiscographyAsync();

        var token = handler.Requests[0];
        Assert.Equal(SpotifyCatalogClient.TokenEndpoint, token.Request.RequestUri!.ToString());
        Assert.Equal("Basic", token.Request.Headers.Authorization!.Scheme);
        Assert.Equal("grant_type=client_credentials", token.Body);
        Assert.All(handler.Requests.Skip(1), r => Assert.Equal("Bearer", r.Request.Headers.Authorization!.Scheme));

        Assert.Equal(new[] { "Case of the Ex", "Free", "Lock U Down" }, songs.Select(s => s.Title));
        var caseOfTheEx = songs[0];
        Assert.Equal("case-of-the-ex", caseOfTheEx.Id);
        Assert.Equal("Fear of Flying", caseOfTheEx.Album);
        Assert.Equal(2000, caseOfTheEx.Year);
        Assert.Equal(236.5, caseOfTheEx.DurationSeconds);
        Assert.Equal("spotify:track:3CCCCCCCCCCCCCCCCCCCCC", caseOfTheEx.SpotifyUri);
        Assert.Equal("feat. Lil Wayne", songs[2].Credit);

        // The "next" link pointing outside the Spotify API must never receive the bearer token.
        Assert.DoesNotContain(handler.Requests, r => r.Request.RequestUri!.Host == "evil.example.com");
    }

    [Fact]
    public async Task GetDiscography_UsesConfiguredArtistIdWithoutSearching()
    {
        var handler = new StubHttpHandler(Respond);
        var options = new SpotifyOptions { ClientId = "id", ClientSecret = "secret", ArtistId = ArtistId };

        await new SpotifyCatalogClient(new HttpClient(handler), options).GetDiscographyAsync();

        Assert.DoesNotContain(handler.Requests, r => r.Request.RequestUri!.AbsolutePath.EndsWith("/search"));
    }

    [Fact]
    public async Task GetDiscography_ExplainsPremiumRequirementOnForbidden()
    {
        var handler = new StubHttpHandler((request, _) => request.RequestUri!.Host == "accounts.spotify.com"
            ? StubHttpHandler.Json("""{"access_token":"tkn"}""")
            : StubHttpHandler.Json("""{"error":"forbidden"}""", HttpStatusCode.Forbidden));

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => new SpotifyCatalogClient(new HttpClient(handler), Options).GetDiscographyAsync());

        Assert.Contains("Premium", error.Message);
    }

    [Fact]
    public async Task GetDiscography_RequiresConfiguration()
    {
        var client = new SpotifyCatalogClient(new HttpClient(), new SpotifyOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetDiscographyAsync());
    }

    private static HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        var uri = request.RequestUri!;
        if (uri.Host == "accounts.spotify.com")
        {
            return StubHttpHandler.Json("""{"access_token":"tkn","token_type":"Bearer","expires_in":3600}""");
        }

        if (uri.AbsolutePath == "/v1/search")
        {
            return StubHttpHandler.Json($$"""
                {"artists":{"items":[
                  {"id":"9ZZZZZZZZZZZZZZZZZZZZZ","name":"Mya Tribute Band"},
                  {"id":"{{ArtistId}}","name":"Mýa"}
                ] } }
                """);
        }

        if (uri.AbsolutePath == $"/v1/artists/{ArtistId}/albums")
        {
            return StubHttpHandler.Json($$"""
                {"items":[
                  {"id":"{{SingleId}}","name":"Lock U Down","album_type":"single","release_date":"2007-02-01"},
                  {"id":"{{AlbumId}}","name":"Fear of Flying","album_type":"album","release_date":"2000-04-25"}
                ],"next":null}
                """);
        }

        if (uri.AbsolutePath == $"/v1/albums/{AlbumId}/tracks" && !uri.Query.Contains("offset"))
        {
            return StubHttpHandler.Json($$"""
                {"items":[
                  {"id":"3CCCCCCCCCCCCCCCCCCCCC","name":"Case of the Ex","duration_ms":236500,"artists":[{"id":"{{ArtistId}}","name":"Mýa"}]},
                  {"id":"short","name":"Bad Id","artists":[{"id":"{{ArtistId}}","name":"Mýa"}]}
                ],"next":"https://api.spotify.com/v1/albums/{{AlbumId}}/tracks?offset=10&limit=10"}
                """);
        }

        if (uri.AbsolutePath == $"/v1/albums/{AlbumId}/tracks")
        {
            return StubHttpHandler.Json($$"""
                {"items":[
                  {"id":"4DDDDDDDDDDDDDDDDDDDDD","name":"Free","duration_ms":220000,"artists":[{"id":"{{ArtistId}}","name":"Mýa"}]},
                  {"id":"5EEEEEEEEEEEEEEEEEEEEE","name":"Someone Else's Song","artists":[{"id":"7XXXXXXXXXXXXXXXXXXXXX","name":"Other"}]}
                ],"next":"https://evil.example.com/steal"}
                """);
        }

        if (uri.AbsolutePath == $"/v1/albums/{SingleId}/tracks")
        {
            return StubHttpHandler.Json($$"""
                {"items":[
                  {"id":"6FFFFFFFFFFFFFFFFFFFFF","name":"Lock U Down","artists":[{"id":"{{ArtistId}}","name":"Mýa"},{"id":"8YYYYYYYYYYYYYYYYYYYYY","name":"Lil Wayne"}]},
                  {"id":"7GGGGGGGGGGGGGGGGGGGGG","name":"Case of the Ex","artists":[{"id":"{{ArtistId}}","name":"Mýa"}]}
                ]}
                """);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
