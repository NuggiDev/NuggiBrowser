using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Windows.System;
using Microsoft.UI.Xaml.Media.Imaging;

namespace nuggiUI
{
    public class BrowserTab : INotifyPropertyChanged
    {
        private string _title = "New Tab";
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value) { _title = value; OnPropertyChanged(); }
            }
        }

        public string Url { get; set; } = "https://www.google.com";
        public WebView2 WebView { get; set; } = null!;
        public bool IsPinned { get; set; } = false;

        private BitmapImage? _favicon = null;
        public BitmapImage? Favicon
        {
            get => _favicon;
            set
            {
                if (_favicon != value) 
                { 
                    _favicon = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(HasFavicon));
                    OnPropertyChanged(nameof(FaviconVisibility));
                    OnPropertyChanged(nameof(FallbackVisibility));
                }
            }
        }

        public bool HasFavicon => _favicon != null;
        public Visibility FaviconVisibility => HasFavicon ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FallbackVisibility => HasFavicon ? Visibility.Collapsed : Visibility.Visible;

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class MainPage : Page
    {
        public ObservableCollection<BrowserTab> Tabs { get; } = new();
        public ObservableCollection<BrowserTab> PinnedTabs { get; } = new();
        private BrowserTab? _activeTab;

        public MainPage()
        {
            this.InitializeComponent();
            TabsListView.ItemsSource = Tabs;
            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MainPage_Loaded;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var initialTheme = ElementTheme.Dark;
            if (localSettings.Values["Theme"] is string theme)
            {
                initialTheme = theme == "Light" ? ElementTheme.Light : ElementTheme.Dark;
            }

            // Register the top nav grid as the window drag/title bar region
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.SetTitleBar(TopNavGrid);
                MainWindow.Instance.UpdateTheme(initialTheme);
            }

            if (localSettings.Values["PinSidebar"] is bool pinned && pinned)
            {
                SidebarSplitView.IsPaneOpen = true;
            }

            CreateNewTab();
        }

        private async void CreateNewTab(string url = "nuggi://newtab")
        {
            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            var tab = new BrowserTab { Title = "New Tab", Url = url, WebView = webView };

            webView.NavigationStarting += (s, e) =>
            {
                if (e.Uri != "about:blank" && !e.Uri.StartsWith("data:"))
                {
                    tab.Url = e.Uri;
                }
                tab.IsLoading = true;
                if (tab == _activeTab) 
                {
                    UrlTextBox.Text = tab.Url;
                    MainProgressBar.Visibility = Visibility.Visible;
                }
            };

            webView.NavigationCompleted += (s, e) =>
            {
                tab.IsLoading = false;
                if (tab == _activeTab) MainProgressBar.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrWhiteSpace(webView.CoreWebView2.DocumentTitle))
                {
                    tab.Title = webView.CoreWebView2.DocumentTitle;
                }

                if (tab.Url != "nuggi://newtab" && !string.IsNullOrEmpty(tab.Url))
                {
                    try 
                    { 
                        var uri = new Uri(tab.Url);
                        tab.Favicon = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32"));
                    } catch { }

                    _ = HistoryManager.AddEntryAsync(tab.Title, tab.Url);
                }
            };

            Tabs.Add(tab);
            WebViewContainer.Children.Add(webView);
            TabsListView.SelectedItem = tab;

            await webView.EnsureCoreWebView2Async();
            try
            {
                var scheme = this.ActualTheme == ElementTheme.Dark 
                    ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark 
                    : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;
                webView.CoreWebView2.Profile.PreferredColorScheme = scheme;
            }
            catch { }

            if (url == "nuggi://newtab")
            {
                webView.NavigateToString(GetNewTabHtml());
                tab.Url = "";
            }
            else
            {
                webView.CoreWebView2.Navigate(url);
            }
        }

        private void SwitchToTab(BrowserTab tab)
        {
            if (_activeTab != null) _activeTab.WebView.Visibility = Visibility.Collapsed;
            _activeTab = tab;
            _activeTab.WebView.Visibility = Visibility.Visible;
            UrlTextBox.Text = _activeTab.Url;
            TabsListView.SelectedItem = _activeTab;
            MainProgressBar.Visibility = _activeTab.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TabsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsListView.SelectedItem is BrowserTab tab) SwitchToTab(tab);
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
            => CreateNewTab();

        private void CloseTab(BrowserTab tab)
        {
            WebViewContainer.Children.Remove(tab.WebView);
            tab.WebView.Close();
            if (Tabs.Contains(tab)) Tabs.Remove(tab);
            if (PinnedTabs.Contains(tab)) PinnedTabs.Remove(tab);

            if (Tabs.Count == 0 && PinnedTabs.Count == 0) CreateNewTab();
            else if (tab == _activeTab) 
            {
                if (Tabs.Count > 0) SwitchToTab(Tabs[0]);
                else if (PinnedTabs.Count > 0) SwitchToTab(PinnedTabs[0]);
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is BrowserTab tab)
            {
                CloseTab(tab);
            }
        }

        private void PinTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BrowserTab tab)
            {
                if (Tabs.Contains(tab)) Tabs.Remove(tab);
                tab.IsPinned = true;
                if (!PinnedTabs.Contains(tab)) PinnedTabs.Add(tab);
                RefreshNewTabs();
            }
        }

        private void UnpinTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BrowserTab tab)
            {
                if (PinnedTabs.Contains(tab)) PinnedTabs.Remove(tab);
                tab.IsPinned = false;
                if (!Tabs.Contains(tab)) Tabs.Add(tab);
                RefreshNewTabs();
            }
        }

        private void PinnedTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BrowserTab tab)
            {
                SwitchToTab(tab);
            }
        }

        private void NewTab_Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            CreateNewTab();
        }

        private void CloseTab_Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (_activeTab != null) CloseTab(_activeTab);
        }

        private void FocusUrl_Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            UrlTextBox.Focus(FocusState.Programmatic);
            UrlTextBox.SelectAll();
        }

        private void History_Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            Settings_Click(null, new RoutedEventArgs());
        }

        private void HoverTrigger_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            SidebarSplitView.IsPaneOpen = true;
            SidebarDismissOverlay.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void Content_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["PinSidebar"] is bool pinned && pinned) return;
            
            SidebarSplitView.IsPaneOpen = false;
            SidebarDismissOverlay.Visibility = Visibility.Collapsed;
        }

        private void Sidebar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["PinSidebar"] is bool pinned && pinned) return;

            var ptr = e.GetCurrentPoint(SidebarRoot).Position;
            if (ptr.X > SidebarRoot.ActualWidth - 2 || ptr.X < 2 || ptr.Y < 2 || ptr.Y > SidebarRoot.ActualHeight - 2)
            {
                SidebarSplitView.IsPaneOpen = false;
                SidebarDismissOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Refresh_Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            _activeTab?.WebView.Reload();
        }

        private void NavigateTo(string url)
        {
            if (_activeTab != null)
            {
                _activeTab.WebView.CoreWebView2.Navigate(url);
            }
        }

        private async void NavigateActiveTab(string text)
        {
            if (_activeTab == null) return;
            
            if (text == "nuggi://newtab" || string.IsNullOrWhiteSpace(text))
            {
                await _activeTab.WebView.EnsureCoreWebView2Async();
                _activeTab.WebView.NavigateToString(GetNewTabHtml());
                _activeTab.Url = "";
                UrlTextBox.Text = "";
                return;
            }

            if (text.StartsWith("http://") || text.StartsWith("https://") || text.Contains("."))
            {
                string dest = text.StartsWith("http") ? text : "https://" + text;
                try { _activeTab.WebView.Source = new Uri(dest); } catch (UriFormatException) { }
            }
            else 
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                string engine = localSettings.Values["SearchEngine"] as string ?? "Google";
                string searchUrl = engine switch
                {
                    "Bing" => $"https://www.bing.com/search?q={Uri.EscapeDataString(text)}",
                    "DuckDuckGo" => $"https://duckduckgo.com/?q={Uri.EscapeDataString(text)}",
                    _ => $"https://www.google.com/search?q={Uri.EscapeDataString(text)}"
                };
                NavigateTo(searchUrl);
            }
        }

        private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        { if (e.Key == VirtualKey.Enter) NavigateActiveTab(UrlTextBox.Text); }

        private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UrlTextBox.SelectAll();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        { if (_activeTab?.WebView.CanGoBack == true) _activeTab.WebView.GoBack(); }

        private void Forward_Click(object sender, RoutedEventArgs e)
        { if (_activeTab?.WebView.CanGoForward == true) _activeTab.WebView.GoForward(); }

        private void Refresh_Click(object sender, RoutedEventArgs e)
            => _activeTab?.WebView.Reload();
            
        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Nuggi Settings",
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };

            var stack = new StackPanel { Spacing = 24, Margin = new Thickness(0, 12, 0, 0) };
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            // 1. Theme Setting
            var themeStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            themeStack.Children.Add(new FontIcon { Glyph = "\uE793", VerticalAlignment = VerticalAlignment.Center });
            var themeToggle = new ToggleSwitch
            {
                Header = "Dark Mode",
                IsOn = this.ActualTheme == ElementTheme.Dark
            };
            themeToggle.Toggled += (s, args) =>
            {
                var newTheme = themeToggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
                if (MainWindow.Instance != null) MainWindow.Instance.UpdateTheme(newTheme);
                else this.RequestedTheme = newTheme;
                dialog.RequestedTheme = newTheme;
                localSettings.Values["Theme"] = themeToggle.IsOn ? "Dark" : "Light";
                
                var wvTheme = themeToggle.IsOn 
                    ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark 
                    : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;
                
                foreach (var tab in Tabs)
                {
                    try { tab.WebView.CoreWebView2.Profile.PreferredColorScheme = wvTheme; } catch { }
                }
                
                RefreshNewTabs();
            };
            themeStack.Children.Add(themeToggle);
            stack.Children.Add(themeStack);

            // 2. Pin Sidebar Setting
            var pinStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            pinStack.Children.Add(new FontIcon { Glyph = "\uE840", VerticalAlignment = VerticalAlignment.Center });
            var pinToggle = new ToggleSwitch
            {
                Header = "Pin Sidebar",
                IsOn = localSettings.Values["PinSidebar"] is bool pinned && pinned,
                OffContent = "Auto-hide",
                OnContent = "Pinned"
            };
            pinToggle.Toggled += (s, args) =>
            {
                localSettings.Values["PinSidebar"] = pinToggle.IsOn;
                SidebarSplitView.IsPaneOpen = pinToggle.IsOn;
                SidebarDismissOverlay.Visibility = Visibility.Collapsed;
            };
            pinStack.Children.Add(pinToggle);
            stack.Children.Add(pinStack);

            // 3. Search Engine Setting
            var searchStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            searchStack.Children.Add(new FontIcon { Glyph = "\uE721", VerticalAlignment = VerticalAlignment.Center });
            var searchCombo = new ComboBox
            {
                Header = "Default Search Engine",
                Width = 200,
                ItemsSource = new[] { "Google", "Bing", "DuckDuckGo" },
                SelectedItem = localSettings.Values["SearchEngine"] as string ?? "Google"
            };
            searchCombo.SelectionChanged += (s, args) =>
            {
                localSettings.Values["SearchEngine"] = searchCombo.SelectedItem as string;
                RefreshNewTabs();
            };
            searchStack.Children.Add(searchCombo);
            stack.Children.Add(searchStack);
            
            // 4. Clear Tabs Action
            var clearStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            clearStack.Children.Add(new FontIcon { Glyph = "\uE74D", VerticalAlignment = VerticalAlignment.Center });
            var clearBtn = new Button { Content = "Close all tabs", Width = 200 };
            clearBtn.Click += (s, args) => 
            {
                var unpinned = Tabs.Where(t => !t.IsPinned).ToList();
                foreach (var t in unpinned) { Tabs.Remove(t); WebViewContainer.Children.Remove(t.WebView); t.WebView.Close(); }
                if (!Tabs.Any() && !PinnedTabs.Any()) CreateNewTab();
            };
            clearStack.Children.Add(clearBtn);
            stack.Children.Add(clearStack);

            dialog.Content = stack;
            await dialog.ShowAsync();
        }

        private void RefreshNewTabs()
        {
            foreach (var tab in Tabs)
            {
                if (tab.Url == "") 
                {
                    tab.WebView.NavigateToString(GetNewTabHtml());
                }
            }
        }
            
        private string GetNewTabHtml()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string engine = localSettings.Values["SearchEngine"] as string ?? "Google";
            string searchAction = engine switch
            {
                "Bing" => "https://www.bing.com/search?q=",
                "DuckDuckGo" => "https://duckduckgo.com/?q=",
                _ => "https://www.google.com/search?q="
            };

            bool isDark = this.ActualTheme != ElementTheme.Light;
            string bgGradient = isDark 
                ? "linear-gradient(-45deg, #121212, #1E1E1E, #181818, #222222)" 
                : "linear-gradient(-45deg, #FFFFFF, #F8F8F8, #F0F0F0, #FAFAFA)";
            string fg = isDark ? "#FFFFFF" : "#000000";
            string inputBg = isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.03)";
            string border = isDark ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.1)";
            string glow = isDark ? "rgba(237, 143, 3, 0.15)" : "rgba(237, 143, 3, 0.1)";

            string shortcutsHtml = "";
            if (PinnedTabs.Count == 0)
            {
                shortcutsHtml = $"<div style='color: #888; font-size: 14px; opacity: 0.7;'>Right-click a tab and select 'Pin' to add it here</div>";
            }
            else
            {
                int delay = 1;
                foreach (var tab in PinnedTabs)
                {
                    if (string.IsNullOrEmpty(tab.Url) || tab.Url == "nuggi://newtab") continue;
                    
                    string domain = tab.Title;
                    try { domain = new Uri(tab.Url).Host.Replace("www.", ""); } catch { }
                    string letter = domain.Length > 0 ? domain.Substring(0, 1).ToUpper() : "N";
                    
                    shortcutsHtml += $@"
                <a href='{tab.Url}' class='shortcut' style='animation-delay: 0.{delay}s'>
                    <img src='https://www.google.com/s2/favicons?domain={tab.Url}&sz=64' class='shortcut-icon' onerror=""this.style.display='none'; this.nextElementSibling.style.display='flex';"" />
                    <div class='shortcut-icon fallback-icon' style='display:none;'>{letter}</div>
                    <span>{domain}</span>
                </a>";
                    delay++;
                }
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>New Tab</title>
    <style>
        @keyframes fadeUp {{ from {{ opacity: 0; transform: translateY(30px); }} to {{ opacity: 1; transform: translateY(0); }} }}
        @keyframes shimmer {{ 0% {{ background-position: 0% 50%; }} 50% {{ background-position: 100% 50%; }} 100% {{ background-position: 0% 50%; }} }}
        @keyframes bgBreathe {{ 0% {{ background-position: 0% 50%; }} 50% {{ background-position: 100% 50%; }} 100% {{ background-position: 0% 50%; }} }}
        @keyframes pulseGlow {{ 0% {{ transform: translate(-50%, -50%) scale(0.8); opacity: 0.5; }} 100% {{ transform: translate(-50%, -50%) scale(1.2); opacity: 1; }} }}
        @keyframes borderPulse {{ 0% {{ box-shadow: 0 0 0 0 rgba(237, 143, 3, 0.4); }} 70% {{ box-shadow: 0 0 0 15px rgba(237, 143, 3, 0); }} 100% {{ box-shadow: 0 0 0 0 rgba(237, 143, 3, 0); }} }}
        @keyframes bounce {{ 0%, 100% {{ transform: translateY(0) scale(1); }} 50% {{ transform: translateY(-10px) scale(1.05); }} }}

        body {{ background: {bgGradient}; background-size: 400% 400%; color: {fg}; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; animation: bgBreathe 15s ease infinite; overflow: hidden; }}
        
        .ambient-glow {{ position: absolute; width: 800px; height: 800px; background: radial-gradient(circle, {glow} 0%, rgba(0,0,0,0) 70%); top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: -1; animation: pulseGlow 6s ease-in-out infinite alternate; pointer-events: none; }}
        
        .search-container {{ text-align: center; width: 100%; max-width: 600px; z-index: 1; animation: fadeUp 0.8s cubic-bezier(0.2, 0.8, 0.2, 1); }}
        
        .logo {{ font-size: 64px; font-weight: 800; margin-bottom: 35px; background: linear-gradient(90deg, #FFB75E, #ED8F03, #FFB75E); background-size: 200% auto; animation: shimmer 3s linear infinite; -webkit-background-clip: text; -webkit-text-fill-color: transparent; filter: drop-shadow(0px 8px 16px rgba(237, 143, 3, 0.4)); transition: all 0.3s ease; }}
        .logo:hover {{ animation: shimmer 3s linear infinite, bounce 1.5s cubic-bezier(0.28, 0.84, 0.42, 1) infinite; }}
        
        input[type='text'] {{ width: 100%; padding: 18px 28px; font-size: 16px; border-radius: 32px; border: 1px solid {border}; background-color: {inputBg}; backdrop-filter: blur(10px); color: {fg}; outline: none; box-shadow: 0 4px 24px rgba(0,0,0,0.05); transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); }}
        input[type='text']:hover {{ box-shadow: 0 8px 32px rgba(0,0,0,0.1); transform: translateY(-2px); border-color: rgba(237, 143, 3, 0.5); }}
        input[type='text']:focus {{ border-color: #ED8F03; background-color: {inputBg}; transform: translateY(-4px) scale(1.03); animation: borderPulse 2s infinite; }}
        
        .shortcuts {{ display: flex; justify-content: center; gap: 32px; margin-top: 56px; }}
        .shortcut {{ display: flex; flex-direction: column; align-items: center; text-decoration: none; color: #888; transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); animation: fadeUp 0.8s cubic-bezier(0.2, 0.8, 0.2, 1) both; }}
        .shortcut:nth-child(1) {{ animation-delay: 0.1s; }}
        .shortcut:nth-child(2) {{ animation-delay: 0.2s; }}
        .shortcut:nth-child(3) {{ animation-delay: 0.3s; }}
        .shortcut:hover {{ color: {fg}; transform: translateY(-10px) scale(1.1); }}
        .shortcut:active {{ transform: translateY(2px) scale(0.9); }}
        
        .shortcut-icon {{ width: 64px; height: 64px; background-color: {inputBg}; backdrop-filter: blur(10px); border: 1px solid {border}; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin-bottom: 12px; font-size: 28px; transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); box-shadow: 0 4px 16px rgba(0,0,0,0.05); object-fit: contain; padding: 12px; box-sizing: border-box; }}
        .shortcut:hover .shortcut-icon {{ background-color: #ED8F03; border-color: #ED8F03; color: white; box-shadow: 0 12px 32px rgba(237, 143, 3, 0.4); transform: rotate(8deg) scale(1.1); filter: brightness(1.2); }}
    </style>
</head>
<body>
    <div class='ambient-glow'></div>
    <div class='search-container'>
        <div class='logo'>Nuggi</div>
        <form onsubmit='search(event)'>
            <input type='text' id='searchInput' placeholder='Search the web' autofocus autocomplete='off' />
        </form>
        <div class='shortcuts'>
            {shortcutsHtml}
        </div>
    </div>
    <script>
        function search(e) {{
            e.preventDefault();
            const q = document.getElementById('searchInput').value;
            if (q) window.location.href = '{searchAction}' + encodeURIComponent(q);
        }}
    </script>
</body>
</html>";
        }
    }
}
