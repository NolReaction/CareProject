# Package installer regressions

`dotnet run --project Validation/PackageInstallerTests` executes the real external
C# transaction core with temporary directories. It covers payload/base corruption,
unknown edited retired sources, known duplicate GUID aliases, backups and exact
restore, repeated use, post-install edits, wrong roots, a held Unity lock,
case-insensitive duplicate inventory paths and directory/junction escapes. V18
and V19 installs retain their actual version in backups/receipts and can both be
restored; unsupported versions fail before changing files.

The helper stays outside `Assets`, so it is not imported into Unity or a mobile
build. Windows PowerShell 5.1, FolderBrowserDialog and a real Unity process are
separate manual checks. The C# helper is also compiled with language version 5.
