using System.Text.Json.Serialization;
using SpaceAffinityApi.Domain;
using SpaceAffinityApi.Messages.Commands.SpaceNotes;

namespace SpaceAffinityApi.Context;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AddSpaceNote))]
[JsonSerializable(typeof(SpaceNote[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;