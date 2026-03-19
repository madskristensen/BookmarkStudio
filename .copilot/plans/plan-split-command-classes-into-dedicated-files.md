# 🎯 Split command classes into dedicated files

## Understanding
The user wants command-related classes split so each command class lives in its own `.cs` file, and they want the legacy `MyCommand.cs` removed, including any stale command-table references if applicable.
## Assumptions
- The command classes currently live in `Commands\MyCommand.cs` and `Commands\BookmarkCommands.cs`.
- This is a non-SDK .NET Framework project, so file includes must be updated explicitly in `BookmarkStudio.csproj`.
- The VSCT should only be changed if it still references removed commands.
## Approach
I will extract each command class into a dedicated file under `Commands\`, preserving namespaces and behavior exactly. Shared helper classes currently co-located with commands will also be split to keep one class per file and avoid behavior changes. Then I will remove `MyCommand.cs` and the old aggregate command file entry from the project file, add compile includes for the new files, and verify whether `VSCommandTable.vsct` needs edits.
## Key Files
- BookmarkStudio\Commands\MyCommand.cs - old location of `OpenBookmarkManagerCommand` to remove.
- BookmarkStudio\Commands\BookmarkCommands.cs - current multi-class command container to split.
- BookmarkStudio\BookmarkStudio.csproj - explicit compile include list to update.
- BookmarkStudio\VSCommandTable.vsct - verify command ids and symbols are still valid.
## Risks & Open Questions
- Removing a file from a non-SDK project without updating compile includes will break build.
- If any tooling expects a specific class-to-file layout, rename/removal order must remain consistent.

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 22:13:54

## 📝 Plan Steps
- ✅ **Create dedicated command files**
- ✅ **Move shared command helper classes into separate files**
- ✅ **Remove legacy command source files from project**
- ✅ **Update project compile includes for new command files**
- ✅ **Verify VSCT references remain correct**
- ✅ **Build and run tests to validate no regressions**

