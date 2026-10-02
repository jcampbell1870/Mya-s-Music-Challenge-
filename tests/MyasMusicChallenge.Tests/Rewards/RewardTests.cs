using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MyasMusicChallenge.Core.Rewards;
using MyasMusicChallenge.Tests.TestUtilities;

namespace MyasMusicChallenge.Tests.Rewards;

public class RewardTests
{
    private const string Wallet = "0x1111111111111111111111111111111111111111";
    private const string Vault = RewardTreasuryOptions.CryptoHockeyRewardVaultAddress;
    private static readonly string Signature = "0x" + new string('a', 64) + new string('b', 64) + "1b";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_UseTheCryptoHockeyRewardTreasury()
    {
        var options = new RewardTreasuryOptions();

        Assert.Equal("0x8eddD4edea39c5B5f77662453600F53A202EE47C", options.Arcade1870ContractAddress);
        Assert.Equal("0x1e4f6e4a382adbdb662733a19ae773d3ab8f497d", options.RewardVaultAddress);
        Assert.Equal("https://www.cryptohockey.org/api/reward-claim", options.RewardIssuerUrl);
        Assert.Equal("A1870", options.TokenSymbol);
        Assert.Equal(18, options.RewardTokenDecimals);
        Assert.Equal(1, options.DefaultNetworkChainId);
    }

    [Theory]
    [InlineData("0x1111111111111111111111111111111111111111", true)]
    [InlineData("0xAbCdEf1111111111111111111111111111111111", true)]
    [InlineData("1111111111111111111111111111111111111111", false)]
    [InlineData("0x111111111111111111111111111111111111111g", false)]
    [InlineData("0x11", false)]
    [InlineData(null, false)]
    public void IsValidAddress_ChecksFormat(string? address, bool expected)
    {
        Assert.Equal(expected, RewardClaimEncoder.IsValidAddress(address));
    }

    [Theory]
    [InlineData("00", "1b")]
    [InlineData("01", "1c")]
    [InlineData("1B", "1b")]
    [InlineData("1c", "1c")]
    public void NormalizeSignature_UsesEthereumRecoveryIds(string v, string expectedV)
    {
        var signature = "0x" + new string('A', 128) + v;

        Assert.True(RewardClaimEncoder.TryNormalizeSignature(signature, out var normalized));
        Assert.Equal(new string('a', 128) + expectedV, normalized);
        Assert.False(RewardClaimEncoder.TryNormalizeSignature("0x" + new string('a', 128) + "05", out _));
        Assert.False(RewardClaimEncoder.TryNormalizeSignature("0x1234", out _));
    }

    [Fact]
    public void EncodeClaimCall_MatchesVaultClaimAbi()
    {
        var payload = Payload(deadline: 1_900_000_000);

        var data = RewardClaimEncoder.EncodeClaimCall(payload);

        // keccak256("claim(uint256,uint256,uint256,bytes)")[0..4] == 0x6548b7ae
        Assert.StartsWith("0x6548b7ae", data);
        var words = Enumerable.Range(0, (data.Length - 10) / 64).Select(i => data.Substring(10 + (i * 64), 64)).ToList();
        Assert.Equal(BigWord(10_000_000_000_000_000_000m), words[0]);
        Assert.Equal(BigWord(42), words[1]);
        Assert.Equal(BigWord(1_900_000_000), words[2]);
        Assert.Equal(BigWord(0x80), words[3]); // offset of the dynamic bytes argument
        Assert.Equal(BigWord(65), words[4]); // signature length
        Assert.Equal(new string('a', 64), words[5]);
        Assert.Equal(new string('b', 64), words[6]);
        Assert.Equal("1b" + new string('0', 62), words[7]);
        Assert.Equal(8, words.Count);
    }

    [Theory]
    [InlineData("10000000000000000000", 18, "10")]
    [InlineData("1500000000000000000", 18, "1.5")]
    [InlineData("1", 18, "0.000000000000000001")]
    [InlineData("abc", 18, "abc")]
    public void FormatTokenAmount_UsesDecimals(string raw, int decimals, string expected)
    {
        Assert.Equal(expected, RewardClaimEncoder.FormatTokenAmount(raw, decimals));
    }

    [Fact]
    public void Validate_RejectsClaimsFromOtherTreasuries()
    {
        var options = new RewardTreasuryOptions();

        Assert.True(RewardClaimEncoder.TryValidate(Payload(), options, Now, out _));

        var otherVault = Payload();
        otherVault.VaultAddress = "0x2222222222222222222222222222222222222222";
        Assert.False(RewardClaimEncoder.TryValidate(otherVault, options, Now, out var vaultError));
        Assert.Contains("treasury", vaultError);

        var expired = Payload(deadline: Now.ToUnixTimeSeconds() - 1);
        Assert.False(RewardClaimEncoder.TryValidate(expired, options, Now, out var expiredError));
        Assert.Contains("expired", expiredError);

        var wrongChain = Payload();
        wrongChain.ChainId = 56;
        Assert.False(RewardClaimEncoder.TryValidate(wrongChain, options, Now, out _));

        var zeroAmount = Payload();
        zeroAmount.Amount = "0";
        Assert.False(RewardClaimEncoder.TryValidate(zeroAmount, options, Now, out _));

        Assert.False(RewardClaimEncoder.TryValidate(null, options, Now, out _));
    }

    [Theory]
    [InlineData("https://www.cryptohockey.org/api/reward-claim", new[] { "https://www.cryptohockey.org/api/reward-claim" })]
    [InlineData("https://www.cryptohockey.org", new[] { "https://www.cryptohockey.org/", "https://www.cryptohockey.org/api/reward-claim" })]
    [InlineData("www.cryptohockey.org", new[] { "https://www.cryptohockey.org/", "https://www.cryptohockey.org/api/reward-claim" })]
    public void CandidateUris_MatchCryptoHockeyResolution(string configured, string[] expected)
    {
        Assert.True(RewardIssuerClient.TryGetCandidateUris(configured, out var uris, out _));
        Assert.Equal(expected, uris.Select(u => u.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://cryptohockey.org")]
    public void CandidateUris_RejectInvalidConfiguration(string configured)
    {
        Assert.False(RewardIssuerClient.TryGetCandidateUris(configured, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task RequestClaim_PostsGameProofAndBuildsVaultTransaction()
    {
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(JsonSerializer.Serialize(Payload())));
        var client = new RewardIssuerClient(new HttpClient(handler), new RewardTreasuryOptions(), new FakeTimeProvider(Now));
        var proof = PlayReward.CreateProof("case-of-the-ex", 87, Now.UtcDateTime);

        var result = await client.RequestClaimAsync(Wallet, proof);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://www.cryptohockey.org/api/reward-claim", request.RequestUri!.ToString());

        using var json = JsonDocument.Parse(body!);
        Assert.Equal(Wallet, json.RootElement.GetProperty("recipient").GetString());
        var game = json.RootElement.GetProperty("game");
        Assert.StartsWith("mya-case-of-the-ex-", game.GetProperty("gameId").GetString());
        Assert.Equal("mya-karaoke", game.GetProperty("mode").GetString());
        Assert.Equal(87, game.GetProperty("playerScore").GetInt32());
        Assert.True(game.GetProperty("playerWon").GetBoolean());

        var transaction = result.Transaction!;
        Assert.Equal(Vault, transaction.VaultAddress);
        Assert.Equal(Wallet, transaction.Recipient);
        Assert.Equal("10", transaction.DisplayAmount);
        Assert.Equal("A1870", transaction.TokenSymbol);
        Assert.StartsWith("0x6548b7ae", transaction.Data);
    }

    [Fact]
    public async Task RequestClaim_SurfacesIssuerErrors()
    {
        var handler = new StubHttpHandler((_, _) =>
            StubHttpHandler.Json("""{"error":"This completed game has already been rewarded."}""", HttpStatusCode.BadRequest));
        var client = new RewardIssuerClient(new HttpClient(handler), new RewardTreasuryOptions(), new FakeTimeProvider(Now));

        var result = await client.RequestClaimAsync(Wallet, PlayReward.CreateProof("free", 50, Now.UtcDateTime));

        Assert.False(result.IsSuccessful);
        Assert.Equal("Reward issuer: This completed game has already been rewarded.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestClaim_FallsBackToRewardClaimPathForBareHost()
    {
        var handler = new StubHttpHandler((request, _) => request.RequestUri!.AbsolutePath == "/api/reward-claim"
            ? StubHttpHandler.Json(JsonSerializer.Serialize(Payload()))
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var options = new RewardTreasuryOptions { RewardIssuerUrl = "https://www.cryptohockey.org" };
        var client = new RewardIssuerClient(new HttpClient(handler), options, new FakeTimeProvider(Now));

        var result = await client.RequestClaimAsync(Wallet, PlayReward.CreateProof("free", 50, Now.UtcDateTime));

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task RequestClaim_ValidatesWalletAndEnabledFlagWithoutCallingIssuer()
    {
        var handler = new StubHttpHandler((_, _) => throw new InvalidOperationException("Should not be called."));
        var client = new RewardIssuerClient(new HttpClient(handler), new RewardTreasuryOptions(), new FakeTimeProvider(Now));
        var disabled = new RewardIssuerClient(new HttpClient(handler), new RewardTreasuryOptions { Enabled = false });

        Assert.False((await client.RequestClaimAsync("not-a-wallet", PlayReward.CreateProof("free", 1, Now.UtcDateTime))).IsSuccessful);
        Assert.False((await disabled.RequestClaimAsync(Wallet, PlayReward.CreateProof("free", 1, Now.UtcDateTime))).IsSuccessful);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void PlayReward_RequiresMinimumPlayTimeAndUniqueGameIds()
    {
        var options = new RewardTreasuryOptions { MinimumPlaySeconds = 30 };

        Assert.False(PlayReward.IsEligible(29.9, options));
        Assert.True(PlayReward.IsEligible(30, options));
        Assert.False(PlayReward.IsEligible(300, new RewardTreasuryOptions { Enabled = false }));

        var first = PlayReward.CreateProof("free", 150, Now.UtcDateTime);
        var second = PlayReward.CreateProof("free", -5, Now.UtcDateTime);
        Assert.NotEqual(first.GameId, second.GameId);
        Assert.Equal(100, first.PlayerScore);
        Assert.Equal(0, second.PlayerScore);
    }

    [Fact]
    public void ClaimPage_EmbedsTransactionAndEscapesText()
    {
        var html = ClaimPageBuilder.Build(Transaction(), "<script>alert(1)</script>");

        Assert.Contains("eth_sendTransaction", html);
        Assert.Contains("wallet_watchAsset", html);
        Assert.Contains("0x6548b7ae", html);
        Assert.Contains(Vault, html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public void ClaimPage_RejectsInvalidAddresses()
    {
        var transaction = Transaction();
        var bad = new RewardClaimTransaction
        {
            Recipient = "0x123",
            VaultAddress = transaction.VaultAddress,
            ChainId = transaction.ChainId,
            Data = transaction.Data,
            TokenAddress = transaction.TokenAddress,
            TokenSymbol = transaction.TokenSymbol,
            TokenDecimals = transaction.TokenDecimals,
            DisplayAmount = transaction.DisplayAmount,
            Deadline = transaction.Deadline,
        };

        Assert.Throws<ArgumentException>(() => ClaimPageBuilder.Build(bad, "Free"));
    }

    [Fact]
    public async Task ClaimPageServer_ServesPublishedPagesOnLoopbackOnly()
    {
        await using var server = new ClaimPageServer();
        var url = server.Publish("<html>claim me</html>");

        Assert.Equal("127.0.0.1", url.Host);
        using var http = new HttpClient();
        var page = await http.GetAsync(url);
        var missing = await http.GetAsync(new Uri(url, "/claim/" + new string('0', 32)));

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("<html>claim me</html>", await page.Content.ReadAsStringAsync());
        Assert.Equal("text/html", page.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static RewardClaimPayload Payload(long? deadline = null) => new()
    {
        Amount = "10000000000000000000",
        Nonce = "42",
        Deadline = deadline ?? Now.ToUnixTimeSeconds() + 600,
        Signature = Signature,
        VaultAddress = Vault,
        ChainId = 1,
    };

    private static RewardClaimTransaction Transaction() => new()
    {
        Recipient = Wallet,
        VaultAddress = Vault,
        ChainId = 1,
        Data = RewardClaimEncoder.EncodeClaimCall(Payload()),
        TokenAddress = RewardTreasuryOptions.CryptoHockeyArcade1870ContractAddress,
        TokenSymbol = "A1870",
        TokenDecimals = 18,
        DisplayAmount = "10",
        Deadline = Now.ToUnixTimeSeconds() + 600,
    };

    private static string BigWord(decimal value) =>
        System.Numerics.BigInteger.Parse(value.ToString("0")).ToString("x").TrimStart('0').PadLeft(64, '0');
}
