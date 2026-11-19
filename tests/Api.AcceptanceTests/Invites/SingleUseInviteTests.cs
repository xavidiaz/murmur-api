namespace Api.AcceptanceTests.Invites;

/// <summary>
/// A single-use invite really is single-use — API-INV-05. As an owner, I want an invite for one person to let in
/// exactly one person.
/// </summary>
public sealed class SingleUseInviteTests(PostgresFixture db) : AcceptanceTest(db)
{
    private const int Rounds = 10;

    [Fact]
    [Trait("Story", "API-INV-05")]
    public async Task When_two_people_accept_a_single_use_invite_at_the_same_moment_exactly_one_gets_in()
    {
        var wins = new Dictionary<string, int> { ["Bob"] = 0, ["Carol"] = 0 };

        for (var round = 0; round < Rounds; round++)
        {
            // Given a fresh single-use invite and two fresh outsiders
            using var alice = await NewUserAsync();
            using var bob = await NewUserAsync();
            using var carol = await NewUserAsync();
            var server = await alice.CreateServerAsync();
            var invite = await alice.CreateInviteAsync(server.Id, maxUses: 1);

            // When both accept at once (a 0–2 ms stagger, alternating sides, so both orderings and true overlaps occur)
            var stagger = TimeSpan.FromMilliseconds(round % 3);
            var results = await RaceAsync(invite.Code,
                (bob, round % 2 == 0 ? TimeSpan.Zero : stagger),
                (carol, round % 2 == 0 ? stagger : TimeSpan.Zero));

            // Then exactly one gets in
            var winner = await AssertExactlyOneGotInAsync(server, invite.Code, results);
            wins[winner == bob ? "Bob" : "Carol"]++;
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{Rounds} rounds: Bob won {wins["Bob"]}, Carol won {wins["Carol"]}");
    }

    [Fact]
    [Trait("Story", "API-INV-05")]
    public async Task When_five_people_accept_a_single_use_invite_at_the_same_moment_exactly_one_gets_in()
    {
        // Given
        using var alice = await NewUserAsync();
        var server = await alice.CreateServerAsync();
        var invite = await alice.CreateInviteAsync(server.Id, maxUses: 1);
        var people = new List<ApiClient>();
        for (var i = 0; i < 5; i++)
            people.Add(await NewUserAsync());

        try
        {
            // When
            var results = await RaceAsync(invite.Code, people.Select(p => (p, TimeSpan.Zero)).ToArray());

            // Then
            await AssertExactlyOneGotInAsync(server, invite.Code, results);
        }
        finally
        {
            people.ForEach(p => p.Dispose());
        }
    }

    /// <summary>All accepts wait on one release signal, then fire together (each after its own optional stagger).</summary>
    private static async Task<(ApiClient Who, HttpResponseMessage Response)[]> RaceAsync(
        string code, params (ApiClient Who, TimeSpan Stagger)[] racers)
    {
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<(ApiClient, HttpResponseMessage)> AcceptWhenReleased(ApiClient who, TimeSpan stagger)
        {
            await go.Task;
            if (stagger > TimeSpan.Zero)
                await Task.Delay(stagger, TestContext.Current.CancellationToken);
            return (who, await who.PostAsync(ApiRoutes.Invites.Accept(code)));
        }

        var pending = racers.Select(r => AcceptWhenReleased(r.Who, r.Stagger)).ToArray();
        go.SetResult();
        return await Task.WhenAll(pending);
    }

    /// <summary>
    /// Exactly one 200, everyone else 410 invite_exhausted; only the winner has the server in their list; the invite
    /// is now used up (its preview is 410 too). Returns the winner.
    /// </summary>
    private static async Task<ApiClient> AssertExactlyOneGotInAsync(
        ServerDetailDto server, string code, (ApiClient Who, HttpResponseMessage Response)[] results)
    {
        var winners = results.Where(r => r.Response.StatusCode == HttpStatusCode.OK).ToList();
        Assert.True(winners.Count == 1,
            $"expected exactly one 200, got: {string.Join(", ", results.Select(r => (int)r.Response.StatusCode))}");
        var winner = winners[0].Who;
        var joined = await ApiClient.ReadAsync<ServerDto>(winners[0].Response);
        Assert.Equal(server.Id, joined.Id);

        foreach (var (loser, response) in results.Where(r => r.Who != winner))
        {
            await response.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExhausted);
            var losersServers = await loser.GetAsync<ServerDto[]>(ApiRoutes.Servers.Collection);
            Assert.DoesNotContain(losersServers, s => s.Id == server.Id);
        }

        var winnersServers = await winner.GetAsync<ServerDto[]>(ApiRoutes.Servers.Collection);
        Assert.Contains(winnersServers, s => s.Id == server.Id);

        // Uses can't be read once exhausted (preview of a dead link is 410), which itself shows Uses reached MaxUses.
        var preview = await winner.GetAsync(ApiRoutes.Invites.Item(code));
        await preview.AssertProblemAsync(HttpStatusCode.Gone, ErrorCodes.InviteExhausted);
        return winner;
    }
}
