using System.Globalization;

namespace Contracts;

public static class ApiRoutes
{
    public const string Base = "/api/v1";
    public const string Hub  = "/hubs/chat";

    public static class Auth
    {
        public const string Register = Base + "/auth/register";   // POST  RegisterRequest → 201 AuthResponse
        public const string Login    = Base + "/auth/login";      // POST  LoginRequest    → 200 AuthResponse
        public const string Refresh  = Base + "/auth/refresh";    // POST  RefreshRequest  → 200 AuthResponse
        public const string Logout   = Base + "/auth/logout";     // POST  LogoutRequest   → 204
        public const string Me       = Base + "/auth/me";         // GET                   → 200 UserDto
    }

    public static class Servers
    {
        public const string Collection = Base + "/servers";       // GET → ServerDto[] (mine); POST CreateServerRequest → 201 ServerDetailDto
        public static string Item(Guid id) => $"{Collection}/{id}";                 // GET → ServerDetailDto; PATCH UpdateServerRequest → ServerDto; DELETE → 204 (owner)
        public static string Members(Guid id) => $"{Item(id)}/members";             // GET → MemberDto[]
        public static string Member(Guid id, Guid userId) => $"{Members(id)}/{userId}";   // DELETE → 204 (kick)
        public static string Leave(Guid id) => $"{Members(id)}/me";                 // DELETE → 204 (leave)
        public static string MemberRole(Guid id, Guid userId, Guid roleId) => $"{Member(id, userId)}/roles/{roleId}"; // PUT → 204 assign; DELETE → 204 unassign
        public static string Roles(Guid id) => $"{Item(id)}/roles";                 // GET → RoleDto[]; POST CreateRoleRequest → 201 RoleDto
        public static string Role(Guid id, Guid roleId) => $"{Roles(id)}/{roleId}"; // PATCH UpdateRoleRequest → RoleDto; DELETE → 204
        public static string Channels(Guid id) => $"{Item(id)}/channels";           // GET → ChannelDto[]; POST CreateChannelRequest → 201 ChannelDto
        public static string ChannelOrder(Guid id) => $"{Channels(id)}/order";      // PUT ReorderChannelsRequest → 204
        public static string Invites(Guid id) => $"{Item(id)}/invites";             // POST CreateInviteRequest → 201 InviteDto
    }

    public static class Channels
    {
        public static string Item(Guid id) => $"{Base}/channels/{id}";              // PATCH UpdateChannelRequest → ChannelDto; DELETE → 204
        public static string Messages(Guid id) => $"{Item(id)}/messages";           // GET ?before&limit → MessagePage; POST SendMessageRequest → 201 MessageDto
        public static string Message(Guid id, Guid messageId) => $"{Messages(id)}/{messageId}"; // PATCH EditMessageRequest → MessageDto; DELETE → 204
    }

    public static class Dms
    {
        public const string Collection = Base + "/dms";           // GET → DmConversationDto[] (latest activity first); POST OpenDmRequest → 201 new / 200 existing
        public static string Messages(Guid id) => $"{Collection}/{id}/messages";    // same contract as channel messages
        public static string Message(Guid id, Guid messageId) => $"{Messages(id)}/{messageId}";
    }

    public static class Invites
    {
        public static string Item(string code) => $"{Base}/invites/{code}";         // GET (anonymous allowed) → InviteDto
        public static string Accept(string code) => $"{Item(code)}/accept";       // POST → 200 ServerDto
    }

    /// Helper for message paging query strings.
    public static string WithPaging(string messagesUrl, Guid? before, int? limit)
    {
        var query = new List<string>(2);
        if (before is { } b)
        {
            query.Add("before=" + b.ToString("D"));
        }

        if (limit is { } l)
        {
            query.Add("limit=" + l.ToString(CultureInfo.InvariantCulture));
        }

        return query.Count == 0 ? messagesUrl : messagesUrl + "?" + string.Join('&', query);
    }
}
