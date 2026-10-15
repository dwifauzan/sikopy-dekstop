# Architecture

This document explains **how SIKOPY is built** and **where your code should go**.
It's written for beginners. You don't need any Avalonia or MVVM experience to follow it.

---

## Words you'll see

You can come back to this list whenever you get stuck on a word.

| Word | Simple meaning |
|---|---|
| **Project** | A folder with a `.csproj` file. It builds into one program or library. |
| **Solution** | A group of projects that belong together (`SIKOPY.sln`). |
| **Avalonia** | The library we use to draw buttons, text, windows and so on, on every platform. |
| **`.axaml` file** | A file that describes what a screen **looks like** (written in XML, similar to HTML). |
| **`.axaml.cs` file** | The C# file that belongs to that `.axaml` file ("code-behind"). |
| **View** | A screen, or part of a screen, that the user **sees**. |
| **ViewModel** | A C# class that holds the **data and actions** for a View. |
| **Binding** | A "live connection" between something on screen and a property in the ViewModel. |
| **Package** | A ready-made library downloaded from the internet (NuGet), like Avalonia itself. |

---

## 1. The big picture: one app, four launchers

SIKOPY runs on **Desktop, Android, iOS and Browser**, but we write the app **only once**.

Think of it like a **game cartridge with four different consoles**:
- The **cartridge** (`SIKOPY/`) holds the whole game: all screens, logic and images.
- Each **console** (`SIKOPY.Desktop`, `SIKOPY.Android`, `SIKOPY.iOS`, `SIKOPY.Browser`) only knows how to **start** the cartridge on its own device.

```
                ┌──────────────────────────────┐
                │           SIKOPY             │  ← THE APP (screens, logic, images)
                │     you work here 99%        │
                └──────────────▲───────────────┘
                               │ all four use it
        ┌───────────────┬──────┴────────┬────────────────┐
 SIKOPY.Desktop   SIKOPY.Android   SIKOPY.iOS     SIKOPY.Browser
 (Windows/Mac/    (phones and      (iPhone/       (runs inside
  Linux)           tablets)          iPad)          a web page)
```

**Rule #1:** write features in **`SIKOPY/`**. The four launcher projects are tiny and you'll rarely touch them.

Inside `SIKOPY/`:

| Folder / file | What's inside |
|---|---|
| `Views/` | The screens (what the user sees) |
| `ViewModels/` | The data and actions behind each screen |
| `Assets/` | Images and icons |
| `App.axaml` + `App.axaml.cs` | App-wide settings (theme) and the code that opens the first screen |
| `ViewLocator.cs` | A helper that finds the right screen for a ViewModel (explained in [section 4](#4-viewlocator-how-the-app-finds-the-right-screen)) |

---

## 2. What happens when the app starts

This is the order of events when someone opens SIKOPY:

```
 1. Launcher starts     →   2. App loads settings   →   3. First screen is shown
 (Desktop/Android/...)       (App.axaml)                  (MainView)
```

**Step 1: the launcher starts the app.** Each platform has its own "start" file:

| Platform | Start file | What it does |
|---|---|---|
| Desktop | `SIKOPY.Desktop/Program.cs` | Opens a normal window |
| Android | `SIKOPY.Android/MainActivity.cs` | Opens the app on the phone |
| iOS | `SIKOPY.iOS/AppDelegate.cs` | Opens the app on the iPhone |
| Browser | `SIKOPY.Browser/Program.cs` | Draws the app inside `wwwroot/index.html` |

**Step 2: `App.axaml` loads the app-wide settings.**
This is where the theme (Fluent, which follows the light/dark mode of the device) is set.
In Debug builds it also turns on **DevTools**: press **F12** while the app is running to inspect the screen, a bit like "Inspect Element" in a web browser.

**Step 3: `App.axaml.cs` shows the first screen.**
Desktop and mobile show screens differently:

| Platform | What gets shown |
|---|---|
| Desktop | `MainWindow` (a window), with `MainView` inside it |
| Android, iOS, Browser | `MainView` directly (phones and browsers don't have "windows") |

**Rule #2:** `MainView` is the real first screen on **every** platform. `MainWindow` is just a frame around it on desktop.
So put your UI in **`MainView`** (or screens inside it), **not** in `MainWindow`. Otherwise it won't appear on phones.

---

## 3. MVVM: how a screen is organized

SIKOPY uses a pattern called **MVVM** (Model, View, ViewModel). It sounds complicated, but the idea is simple:
**keep "what it looks like" separate from "what it does."**

Think of a **restaurant**:

| MVVM part | Restaurant | In SIKOPY |
|---|---|---|
| **View** | The dining room and menu: what the customer **sees** | `.axaml` files in `Views/` |
| **ViewModel** | The waiter: takes requests, brings back results | C# classes in `ViewModels/` |
| **Model** | The kitchen and ingredients: the real data | Your data classes and services (add these as the app grows) |

The customer never walks into the kitchen, and the kitchen doesn't care what the dining room looks like. Each part does one job.

### Every screen comes as a pair

```
Views/ProfileView.axaml        ← what the screen looks like
ViewModels/ProfileViewModel.cs ← the data and buttons' actions for that screen
```

### A ViewModel, explained line by line

```csharp
public partial class ProfileViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Name { get; set; } = "";

    [RelayCommand]
    private async Task SaveAsync() { /* save the profile */ }
}
```

| Code | What it means |
|---|---|
| `: ViewModelBase` | Every ViewModel must inherit from `ViewModelBase`. |
| `partial` | Required. It lets the toolkit generate extra code for you automatically. |
| `[ObservableProperty]` | "When `Name` changes, update the screen automatically." |
| `[RelayCommand]` on `SaveAsync` | Creates a `SaveCommand` that a button can use (the `Async` ending is dropped from the name). |

### The View connects to the ViewModel with "binding"

```xml
<UserControl ... x:DataType="vm:ProfileViewModel">
    <StackPanel>
        <TextBox Text="{Binding Name}" />
        <Button Content="Save" Command="{Binding SaveCommand}" />
    </StackPanel>
</UserControl>
```

- `{Binding Name}` is a **live wire**: when the user types, `Name` updates, and when the code changes `Name`, the textbox updates.
- `{Binding SaveCommand}` means that clicking the button runs `SaveAsync()`.
- `x:DataType="vm:ProfileViewModel"` tells the compiler which ViewModel this screen uses. **Always include it.** If you misspell a binding (for example `{Binding Nmae}`), the build fails and tells you right away, instead of the screen silently showing nothing.

### Rules to remember

- ✅ Screen layout goes in `.axaml`.
- ✅ Data and logic go in the **ViewModel**.
- ❌ Don't put logic (saving, loading, calculations) in the `.axaml.cs` code-behind file. Keep it nearly empty.

---

## 4. ViewLocator: how the app finds the right screen

`ViewLocator.cs` is a small helper that answers one question:
**"I have this ViewModel. Which screen should I show for it?"**

It answers **by looking at the name**. It swaps the word `ViewModel` for `View`:

```
SIKOPY.ViewModels.ProfileViewModel   →   SIKOPY.Views.ProfileView
         ^^^^^^^^^        ^^^^^^^^^               ^^^^^        ^^^^
```

It's like a **name tag system**: if the names match, it finds the screen; if not, it can't.

That means names **must** follow these rules:

| ✅ Works | ❌ Breaks |
|---|---|
| `ProfileViewModel` + `ProfileView` | `ProfileViewModel` + `ProfilePage` |
| `ViewModels/Settings/AccountViewModel` + `Views/Settings/AccountView` | Putting them in different subfolders |

**If you see the text `Not Found: SIKOPY.Views.SomethingView` in the app**, the names or folders don't match. Check the spelling.

> ⚠️ **Note for later (release builds):** ViewLocator finds screens by name while the app is running. When building a final release for iOS, Android or Browser, the build tool may remove code it thinks is unused, and it might remove your screens. If a release build shows `Not Found` but Debug works, this is why. The fix is to list each ViewModel → View pair in `ViewLocator.cs` by hand instead of matching names.

---

## 5. Packages (dependencies)

Packages are ready-made libraries (like Avalonia) that the app downloads.

**All package versions are written in one place: `Directory.Packages.props`** (at the root of the repo).
This keeps every project on the same version.

### Rules

- All `Avalonia.*` packages **must have the same version number**. When you upgrade one, upgrade all of them together, or the app may fail to build or behave strangely.
- `AvaloniaUI.DiagnosticsSupport` (the F12 DevTools) only gets included in **Debug** builds, so users never see it.

---

## Quick summary

1. Write your code in **`SIKOPY/`**, not in the four launcher projects.
2. Put your UI in **`MainView`** (or screens inside it), not in `MainWindow`.
3. Every screen is a pair: **`XxxView.axaml`** + **`XxxViewModel.cs`**, with matching names.
4. Looks go in `.axaml`, logic goes in the ViewModel, and code-behind stays nearly empty.
5. Always add `x:DataType` to a View so binding typos are caught when you build.
6. Package versions live only in **`Directory.Packages.props`**.
