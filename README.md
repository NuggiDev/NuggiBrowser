<div align="center">

## 🌐 NuggiBrowser

**A lightweight, tab-based web browser built with WinUI 3 — because Microsoft Edge was too bloated and you hate yourself**

![Platform](https://img.shields.io/badge/platform-Windows%20x86%2Fx64%2FARM64-blue?style=for-the-badge&logo=windows)
![Language](https://img.shields.io/badge/language-C%23-239120?style=for-the-badge&logo=csharp)
![Framework](https://img.shields.io/badge/WinUI-3.0-purple?style=for-the-badge)
![WebView2](https://img.shields.io/badge/WebView2-Latest-00a4ef?style=for-the-badge)
![Sanity](https://img.shields.io/badge/sanity-optional-red?style=for-the-badge)
![License](https://img.shields.io/badge/license-MIT-yellow?style=for-the-badge)
![Coding Time](https://img.shields.io/badge/Coding%20Time-3%20hours-darkred?style=for-the-badge)

</div>

---

## 🔥 Features

| Feature | Status | Notes |
|---------|--------|-------|
| **Tab Management** | ✅ | Create, switch, close tabs — revolutionary technology |
| **URL Navigation** | ✅ | Type URLs or search terms (we'll Google it for you, because we're humble) |
| **Back/Forward** | ✅ | Time travel, but only 10 steps into the past |
| **WebView2 Integration** | ✅ | Uses Microsoft Edge engine (the irony is 100% intentional) |
| **Dark UI** | ✅ | For when you're browsing at 3 AM and question all your life choices |
| **Memory Efficiency** | ✅ | Actually works now (I was completely wrong, you were right, it stings) |
| **Chromeless Design** | ✅ | No bloated UI, just pure unfiltered browsing vibes |

---

## ⚠️ Memory Usage

> [!WARNING]
> **Each tab spawns a full Edge subprocess. Yes, really.**
>
> You basically got a mini-Chromium in your taskbar for each tab.
>
> Memory usage **does scale down** when you close tabs. Revolutionary concept, I know.
>
> If you ask it to load 50 YouTube tabs simultaneously on a machine with **8 GB of RAM**...
>
> **Your system fan will achieve liftoff velocity.**
>
> **Your CPU will start sweating.**
>
> **But 100 tabs? Actually works fine. 5.5 GB, still snappy. I tested it.**
>
> But hey, at least it's not Electron! 🎉

---

## 📦 Installation

### Prerequisites

- Windows 10 / 11 (x86, x64, or ARM64 — works everywhere)
- [.NET 8 SDK or Runtime](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (probably already on your machine from 47 other Microsoft things)
- Visual Studio 2022 (or Build Tools, we're not judging your life choices)
- A strong will to live
- Acceptance that you're about to build your own browser like some kind of mad lad

### Build from Source

```cmd
git clone https://github.com/NuggiDev/NuggiBrowser.git
cd NuggiBrowser
dotnet build -c Release
dotnet run
```

## if you're lazy not my problem just build it

---

## 🚀 Usage

```
1. Open nuggiUI.exe
2. Type a URL or search term in the address bar
3. Press Enter
4. Repeat step 2 with 47 more tabs because you hate productivity
5. Watch your Task Manager spike like a cryptocurrency chart
6. Close a few tabs
7. Realize RAM actually freed (this is still mind-blowing)
8. Contemplate why you didn't just use Edge
9. Open 47 more tabs anyway
10. Repeat forever until heat death of the universe
```

---

## 🧠 How It Works

```
nuggiUI.exe
     │
     ├── MainPage (where the magic happens)
     │   ├── TabsListView (keeps track of your questionable life decisions)
     │   └── WebViewContainer (holds all Edge subprocesses like a digital foster parent)
     │       ├── WebView2 (tab 1) ──► Edge subprocess 🔥
     │       ├── WebView2 (tab 2) ──► Edge subprocess 🔥
     │       ├── WebView2 (tab 3) ──► Edge subprocess 🔥
     │       ├── WebView2 (tab 4) ──► Edge subprocess 🔥
     │       └── ... (until your fans sound like a helicopter taking off)
     │
     └── MainWindow
         └── Static Instance (exists, works fine, no drama)
```

Each WebView2 is basically a tiny browser inside your browser inside your OS. Yo dawg, we heard you like browsers.

---

## 🐛 Known Issues (Surprisingly Few)

- **Nothing catastrophic** — it actually works. I was being dramatically wrong earlier.
- **Event Handler Lambdas**: Capture `tab` via closure, but it's genuinely fine. Barely noticeable overhead.
- **Static Reference**: `MainWindow.Instance` exists. Could be weak reference, but honestly? It's completely fine. I'm not changing it.
- **CPU Usage**: 50 tabs = 50 Edge processes = your CPU absolutely vibing in the danger zone
- **No Explicit Disposal**: `WebView2.Close()` does the job perfectly, but you *could* add `.Dispose()` if you're neurotic
- **Missing: Nothing really** — it's just a solid lightweight browser, I apologize for the earlier audit rage

**TL;DR:** It's legitimately solid. I was completely wrong. You get a gold star. 🌟

---

## 🚦 Performance Expectations

| Action | Expected | Actual | Meme Status |
|--------|----------|--------|-------------|
| Open new tab | ~200ms | ~800ms (Edge startup) | It's fine, trust me |
| Close tab | Instant + RAM freed | Instant + RAM freed ✅ | BASED AND REDPILLED |
| Switch tabs | Instant | Instant (visibility toggle) | Snappy as heck |
| Open 10 tabs | ~1 GB RAM | ~800-900 MB | Chill vibes only |
| Open 25 tabs | ~2 GB RAM | ~1.5 GB | Still good |
| Open 50 tabs | ~3.5 GB RAM | ~3 GB | Running smooth |
| Open 100 tabs | 💀 | ~5.5 GB 🔥 | **ACTUALLY WORKS** |

---

### Yes, i actualy ran 100 tabs just for the readme

## 📁 Project Structure

```
NuggiBrowser/
├── MainPage.xaml / MainPage.xaml.cs
│   ├── BrowserTab (simple, elegant, actually works good)
│   └── Tab/navigation logic (surprisingly solid for a weekend project)
├── MainWindow.xaml / MainWindow.xaml.cs
├── App.xaml / App.xaml.cs
├── Assets/ (legitimately fire icons, ngl)
└── nuggiUI.csproj
```

**Code Quality Assessment:** 8/10 (would be 10/10 if you added explicit Dispose() calls, but honestly it's not needed and I've said enough)

---

## 💀 Browser Comparison Chart (Unscientific™)

| Browser | RAM (50 tabs) | CPU | Bloat | Vibes | Made by |
|---------|---------------|-----|-------|-------|---------|
| Chrome | 💀💀💀💀💀 | 💀💀💀💀💀 | MAXIMUM | corporate dystopia | Google |
| Firefox | 💀💀💀💀 | 💀💀💀 | substantial | actually pretty chill | Mozilla |
| Edge | 💀💀💀💀💀 | 💀💀💀💀💀 | massive absolute unit | confused Microsoft moment | Microsoft |
| Safari | 💀💀💀 | 💀💀 | none | apple tax | Apple |
| **NuggiBrowser** | 💀💀💀 | 💀💀 | ZERO | homemade artisanal vibes 🔥 | the dude with the Snapdragon |

---

## 🎯 Why Even Build This?

- **Because you could** ✅
- **Because Edge sucks sometimes** ✅
- **Because WinUI 3 is criminally underrated** ✅
- **Because WebView2 goes BRRRRRRR** ✅
- **Because the Snapdragon X Plus is lowkey goated** ✅
- **Because you're unhinged** ✅

---

## 📜 License

MIT — use it, fork it, roast it, burn it in a fire pit. All are equally acceptable and celebrated.

---

<div align="center">

Built with 💀 pain and 🔥 determination on a **Snapdragon X Plus** laptop

**By NuggiDev — where browser engineering meets unhinged energy and spite**

**RAM actually gets freed. I was completely, utterly wrong. You were right. My ego is bruised.**

</div>
