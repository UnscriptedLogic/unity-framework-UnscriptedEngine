# LogService

Import `UnscriptedEngine`. No component or initialization is required.

```csharp
this.Log("Jump started", category: "Movement");
this.Warning("Save delayed", category: "Save");
this.Error("Save failed", category: "Save", allowInBuild: true);

LogService.Enabled = false; // Suppresses every service severity, even with build opt-in.
LogService.Enabled = true;
LogService.SetCategoryFilter("Movement", "Save");
LogService.ClearCategoryFilter();
```

Output: `[Movement] [PlayerController] Jump started`. The source's runtime type is used,
including for inherited methods. Unity objects are passed to the Console as context.
Null sources display `Unknown`. Static methods can use `LogService.Log<PlayerController>(...)`;
static classes can use `LogService.Log(typeof(MyStaticClass), ...)`.

Categories are trimmed and matched case-insensitively. Null, empty, or whitespace categories
become `General`. Setting a filter replaces it; no categories or null clears it. The filter
suppresses emission; it does not hide or restore messages already in the Unity Console.

All player builds, including Development Builds, suppress calls unless `allowInBuild: true`.
That argument bypasses only the build gate. Global and category controls still apply.
Calls remain compiled into players so explicit opt-in works. Direct `Debug.Log` calls are unaffected.

Arguments still evaluate when logging is disabled. Guard expensive messages explicitly:

```csharp
if (LogService.IsEnabled("Movement"))
    this.Log($"State: {BuildExpensiveDescription()}", "Movement");
```

Pass the same category and build opt-in to the guard and log call. Use the API on the Unity main
thread. Controls reset to enabled/unfiltered at each play session, including when domain reload
is disabled; configure session-specific filters after subsystem registration.
