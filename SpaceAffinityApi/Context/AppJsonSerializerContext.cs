using SpaceAffinityApi.Domain;
using SpaceAffinityApi.Messages.Commands.Security;
using SpaceAffinityApi.Messages.Commands.SpaceNotes;
using System.Text.Json.Serialization;

namespace SpaceAffinityApi.Context;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AddSpaceNote))]
[JsonSerializable(typeof(SpaceNote[]))]
[JsonSerializable(typeof(LoginCommand))]
[JsonSerializable(typeof(LoginResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;