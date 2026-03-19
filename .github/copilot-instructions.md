# Copilot Instructions

You must always strictly adhere to these instructions!

## DEFINITION OF DONE - MANDATORY

Before writing any code, read this section. After writing code, execute every step below in order before responding.

Before marking any task as complete, you MUST perform ALL of the following steps in order:

1. Read the `./.editorconfig` file at the repo root (if it exists) and verify all code changes adhere to its rules and the existing coding style conventions.
2. Run the build and verify it succeeds with no errors.
3. Check the build output for warnings. Compare against pre-existing warnings and ensure your changes introduced zero new warnings. If new warnings are found, fix them before proceeding.
4. If a unit test project exist, add new tests or update existing tests to cover your changes as appropriate. Ensure all tests pass locally.
5. Run all unit tests and verify none are broken. If any tests fail due to your changes, fix them before proceeding.

Do NOT consider the task done until all five steps pass. Keep iterating until they do.

## General Guidelines

- This is a Visual Studio extension project.
- It uses the latest version of C# that is supported on .NET Framework 4.8.
- Don't use emojis in .md files and elsewhere.
- Don't use the em-dash character (-), use single hyphens (-) surrounded by space characters instead.

## Exception Handling

ALWAYS log caught exceptions using `ex.LogAsync()`. Never swallow an exception silently or use any other logging mechanism.

## File Management

`source.extension.cs` and `VSCommandTable.cs` are generated files, but you can't generate them, so just edit them directly.

## JSON Parsing

ALWAYS use `System.Text.Json` for JSON parsing. NEVER use `Newtonsoft.Json` or any other legacy serializer.
