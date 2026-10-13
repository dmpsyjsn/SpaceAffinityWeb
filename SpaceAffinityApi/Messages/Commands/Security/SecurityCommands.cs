namespace SpaceAffinityApi.Messages.Commands.Security;

public record LoginCommand(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt);