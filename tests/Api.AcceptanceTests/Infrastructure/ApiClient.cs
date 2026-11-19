using System.Net.Http.Headers;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>
/// An <see cref="HttpClient"/> on the in-memory API, optionally signed in as one user.
/// Raw methods return the <see cref="HttpResponseMessage"/> (assert status / problem yourself);
/// the generic <c>…Async&lt;T&gt;</c> overloads assert the expected status and deserialize.
/// </summary>
public sealed class ApiClient : IDisposable
{
    public const string DefaultPassword = "Passw0rd-123";

    private ApiClient(ApiFactory api, HttpClient http)
    {
        Api = api;
        Http = http;
    }

    public ApiFactory Api { get; }
    public HttpClient Http { get; }

    /// <summary>Last <see cref="AuthResponse"/> obtained by register/login/refresh; null for anonymous clients.</summary>
    public AuthResponse? Auth { get; private set; }
    public string? Password { get; private set; }

    public UserDto User => Auth?.User ?? throw new InvalidOperationException("Anonymous client has no user.");
    public Guid Id => User.Id;
    public string AccessToken => Auth?.AccessToken ?? throw new InvalidOperationException("Anonymous client has no token.");
    public string RefreshToken => Auth?.RefreshToken ?? throw new InvalidOperationException("Anonymous client has no token.");

    /// <summary>A client with no Authorization header.</summary>
    public static ApiClient Anonymous(ApiFactory api) => new(api, api.CreateClient());

    /// <summary>Registers a fresh user with random username/email (asserts 201) and returns a client signed in as them.</summary>
    public static async Task<ApiClient> RegisterAsync(ApiFactory api, string? displayName = null,
        string? userName = null, string? email = null, string password = DefaultPassword)
    {
        var client = Anonymous(api);
        var request = new RegisterRequest(userName ?? Unique.UserName(), email ?? Unique.Email(), password, displayName);
        var auth = await client.PostAsync<AuthResponse>(ApiRoutes.Auth.Register, request, HttpStatusCode.Created);
        client.Password = password;
        client.UseAuth(auth);
        return client;
    }

    /// <summary>Logs in (asserts 200) and returns a new client signed in with that session's tokens.</summary>
    public static async Task<ApiClient> LoginAsync(ApiFactory api, string userNameOrEmail, string password)
    {
        var client = Anonymous(api);
        var auth = await client.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest(userNameOrEmail, password));
        client.Password = password;
        client.UseAuth(auth);
        return client;
    }

    /// <summary>A second, independent login (own refresh token) for the same user.</summary>
    public Task<ApiClient> LoginAgainAsync() => LoginAsync(Api, User.UserName, Password!);

    /// <summary>Switches this client to the given tokens.</summary>
    public void UseAuth(AuthResponse auth)
    {
        Auth = auth;
        UseBearer(auth.AccessToken);
    }

    /// <summary>Sends an arbitrary bearer token (e.g. malformed or forged); null removes the header.</summary>
    public void UseBearer(string? token) =>
        Http.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    // ---- raw ----
    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url, Ct);
    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => Http.PostAsJsonAsync(url, body, Json.Options, Ct);
    public Task<HttpResponseMessage> PutAsync(string url, object? body = null) => Http.PutAsJsonAsync(url, body, Json.Options, Ct);
    public Task<HttpResponseMessage> PatchAsync(string url, object body) => Http.PatchAsJsonAsync(url, body, Json.Options, Ct);
    public Task<HttpResponseMessage> DeleteAsync(string url) => Http.DeleteAsync(url, Ct);

    // ---- typed: assert status, then deserialize ----
    public async Task<T> GetAsync<T>(string url, HttpStatusCode expected = HttpStatusCode.OK) =>
        await ReadAsync<T>(await GetAsync(url), expected);

    public async Task<T> PostAsync<T>(string url, object? body, HttpStatusCode expected = HttpStatusCode.OK) =>
        await ReadAsync<T>(await PostAsync(url, body), expected);

    public async Task<T> PutAsync<T>(string url, object? body, HttpStatusCode expected = HttpStatusCode.OK) =>
        await ReadAsync<T>(await PutAsync(url, body), expected);

    public async Task<T> PatchAsync<T>(string url, object body, HttpStatusCode expected = HttpStatusCode.OK) =>
        await ReadAsync<T>(await PatchAsync(url, body), expected);

    /// <summary>Asserts the status (showing the body on mismatch) and returns the deserialized payload.</summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await AssertStatusAsync(response, expected);
        var value = await response.Content.ReadFromJsonAsync<T>(Json.Options, Ct);
        Assert.NotNull(value);
        return value;
    }

    /// <summary>Asserts the status; on mismatch the failure shows method, URL and the response body.</summary>
    public static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
            return;
        var body = response.Content is null ? "" : await response.Content.ReadAsStringAsync(Ct);
        Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: " +
                    $"expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}. Body: {Truncate(body)}");
    }

    // ---- shortcuts used by many stories ----

    /// <summary>Creates a server owned by this user (asserts 201).</summary>
    public Task<ServerDetailDto> CreateServerAsync(string? name = null, string? iconUrl = null) =>
        PostAsync<ServerDetailDto>(ApiRoutes.Servers.Collection, new CreateServerRequest(name ?? Unique.Name("server"), iconUrl), HttpStatusCode.Created);

    /// <summary>Creates an invite for <paramref name="serverId"/> (asserts 201).</summary>
    public Task<InviteDto> CreateInviteAsync(Guid serverId, int? maxUses = null, int? expiresInSeconds = null) =>
        PostAsync<InviteDto>(ApiRoutes.Servers.Invites(serverId), new CreateInviteRequest(maxUses, expiresInSeconds), HttpStatusCode.Created);

    /// <summary>Accepts an invite as this user (asserts 200).</summary>
    public Task<ServerDto> AcceptInviteAsync(string code) =>
        PostAsync<ServerDto>(ApiRoutes.Invites.Accept(code), null);

    /// <summary>Has <paramref name="owner"/> invite this user into <paramref name="serverId"/> and accepts it.</summary>
    public async Task JoinAsync(ApiClient owner, Guid serverId)
    {
        var invite = await owner.CreateInviteAsync(serverId);
        await AcceptInviteAsync(invite.Code);
    }

    /// <summary>Creates a role on <paramref name="serverId"/> (asserts 201).</summary>
    public Task<RoleDto> CreateRoleAsync(Guid serverId, string? name = null, int? color = null, Permissions permissions = Permissions.None) =>
        PostAsync<RoleDto>(ApiRoutes.Servers.Roles(serverId), new CreateRoleRequest(name ?? Unique.Name("role"), color, permissions), HttpStatusCode.Created);

    /// <summary>Gives <paramref name="userId"/> the role (asserts 204).</summary>
    public async Task AssignRoleAsync(Guid serverId, Guid userId, Guid roleId) =>
        await AssertStatusAsync(await PutAsync(ApiRoutes.Servers.MemberRole(serverId, userId, roleId)), HttpStatusCode.NoContent);

    /// <summary>The <see cref="MemberDto"/> of <paramref name="userId"/> from the server's members list (asserts it is there).</summary>
    public async Task<MemberDto> GetMemberAsync(Guid serverId, Guid userId)
    {
        var members = await GetAsync<MemberDto[]>(ApiRoutes.Servers.Members(serverId));
        return Assert.Single(members, m => m.User.Id == userId);
    }

    /// <summary>Posts a channel message (asserts 201).</summary>
    public Task<MessageDto> SendAsync(Guid channelId, string? content = null, Guid? nonce = null) =>
        PostAsync<MessageDto>(ApiRoutes.Channels.Messages(channelId), new SendMessageRequest(content ?? Unique.Name("msg"), nonce), HttpStatusCode.Created);

    public void Dispose() => Http.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..2000] + "…";
}
