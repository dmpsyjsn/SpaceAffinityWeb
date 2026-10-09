namespace SpaceAffinityApi.Messages.Commands.SpaceNotes;

// Id 0 (or omitted) adds a new note; any other Id edits that note.
public record AddSpaceNote(string Description, string PictureUrl, int Id = 0);
