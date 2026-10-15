### Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add login screen
fix: crash when list is empty on Android
docs: explain ViewLocator naming rule
refactor: extract navigation into service
chore: bump Avalonia to 12.1.5
```

## Coding conventions

### C#
- Follow standard .NET naming: `PascalCase` for types, methods and properties, `_camelCase` for private fields, `camelCase` for locals and parameters.
- One public type per file, with the file named after the type.
- Use file-scoped namespaces (`namespace SIKOPY.ViewModels;`).
- Nullable reference types are **enabled**. Don't silence warnings with `!` unless you're certain.
- Use `async`/`await` for I/O and never block with `.Result` / `.Wait()` on the UI thread.

### Where code goes
- Features, UI and logic go in **`SIKOPY/`** (shared).
- Native platform APIs go in the platform project, behind an interface defined in `SIKOPY/`.
- Don't put feature code in `SIKOPY.Desktop`, `.Android`, `.iOS` or `.Browser`.

## Dependencies

- Add a package version to **`Directory.Packages.props`** first, then reference it in the `.csproj` **without** `Version`.
- Keep all `Avalonia.*` packages on the **same version**.
- Explain in the PR why the new dependency is needed.

## Never commit

- `bin/`, `obj/`, `.idea/`, `*.user` (already covered by `.gitignore`)
- API keys, passwords, signing keys (`*.keystore`, `*.p12`, provisioning profiles)
- Machine-specific paths in `.csproj` files (for example your local JDK path). Configure those in your IDE instead.

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `XA0030: Building with JDK version 26... is not supported` | Wrong JDK | Use JDK 21 (see [Android](#android-any-os)) |
| `XA5207: Could not find android.jar for API level 36` | Missing SDK platform | Run the `InstallAndroidDependencies` command above |
| `Could not find a valid Xcode developer path` | `xcode-select` points at the Command Line Tools | `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer` |
| `You have not agreed to the Xcode license agreements` | License not accepted | `sudo xcodebuild -license accept` |
| `SupportedOSPlatformVersion ... lower than the minimum` | iOS min version too low for the SDK | Keep `SupportedOSPlatformVersion` in `SIKOPY.iOS.csproj` and `MinimumOSVersion` in `Info.plist` **equal** and ≥ 15.0 |
| Browser build warning: `NativeFileReference is not empty...` | SkiaSharp native libs not relinked | Harmless, so you can ignore it |
| IDE shows red errors in Android/iOS projects but you haven't changed code | The IDE couldn't restore/build that target (usually one of the setup problems above) | Fix the setup, then **Reload All Projects** or **File → Invalidate Caches** |
