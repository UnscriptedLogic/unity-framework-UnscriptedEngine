# Generate the API reference

Run this from the framework repository root with the .NET 10 SDK.

```shell
dotnet run --project Documentation~/ApiGenerator/ApiGenerator.csproj -- framework .
```

Open `Documentation~/API/api-index.md` to browse the generated Markdown. The sandbox handbook invokes the same generator with `project` and the sandbox root to write `Documentation/API/`. The generator uses Roslyn from the .NET SDK and can run without Unity or a NuGet package feed.

The reference combines partial declarations and records directly declared public and protected members, explicit interface implementations, enum values, attributed Unity serialization members on visible types, and menu commands. It preserves overloads and includes source links and existing XML documentation. The package reference reads framework runtime, integrations, editor tools, and samples. The project reference reads first-party scripts, editor tools, and prototypes. Tests are excluded.

The generator parses the source for the editor, standalone, WebGL and other player branches used in this checkout, with and without the new Input System and the Unity 6000.4 symbol. Platform-specific declarations appear together. Check the source or Unity assembly settings for the conditions on an individual member. The reference covers declared members. Inherited members and compiler-generated APIs are available through their declaring types and the C# compiler.
