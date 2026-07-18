using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

            // Register the top nav grid as the window drag/title bar region
            if (MainWindow.Instance != null)
                MainWindow.Instance.SetTitleBar(TopNavGrid);

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
                if (e.Uri != "about:blank")
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
                if (!PinnedTabs.Contains(tab)) PinnedTabs.Add(tab);
            }
        }

        private void UnpinTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BrowserTab tab)
            {
                if (PinnedTabs.Contains(tab)) PinnedTabs.Remove(tab);
                if (!Tabs.Contains(tab)) Tabs.Add(tab);
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
            SidebarSplitView.IsPaneOpen = false;
            SidebarDismissOverlay.Visibility = Visibility.Collapsed;
        }

        private void Sidebar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(SidebarRoot).Position;
            // Add a 5px buffer because edge-exits often report a coordinate slightly inside the actual width
            if (point.X >= SidebarRoot.ActualWidth - 5 || point.X <= 5 || point.Y <= 5 || point.Y >= SidebarRoot.ActualHeight - 5)
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

        private async void NavigateActiveTab(string dest)
        {
            if (_activeTab == null) return;
            
            if (dest == "nuggi://newtab" || string.IsNullOrWhiteSpace(dest))
            {
                await _activeTab.WebView.EnsureCoreWebView2Async();
                _activeTab.WebView.NavigateToString(GetNewTabHtml());
                _activeTab.Url = "";
                UrlTextBox.Text = "";
                return;
            }

            if (!dest.StartsWith("http://") && !dest.StartsWith("https://"))
                dest = dest.Contains(".") && !dest.Contains(" ")
                    ? "https://" + dest
                    : "https://www.google.com/search?q=" + Uri.EscapeDataString(dest);
            try { _activeTab.WebView.Source = new Uri(dest); } catch (UriFormatException) { }
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
                Title = "Settings & History",
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot,
                RequestedTheme = ElementTheme.Dark
            };

            var stack = new StackPanel { Spacing = 12 };
            var clearBtn = new Button { Content = "Clear Browsing History" };
            clearBtn.Click += async (s, args) =>
            {
                await HistoryManager.ClearAsync();
                clearBtn.Content = "History Cleared!";
            };

            var histHeader = new TextBlock { Text = "Recent History", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 12, 0, 0) };
            
            var histList = new ListView { MaxHeight = 300, Padding = new Thickness(0) };
            histList.ItemsSource = HistoryManager.GetHistory();
            histList.ItemTemplate = (DataTemplate)XamlReader.Load(@"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <StackPanel Margin='0,4'>
                        <TextBlock Text='{Binding Title}' FontWeight='SemiBold' TextTrimming='CharacterEllipsis' MaxWidth='300'/>
                        <TextBlock Text='{Binding Url}' FontSize='11' Foreground='#AAAAAA' TextTrimming='CharacterEllipsis' MaxWidth='300'/>
                    </StackPanel>
                </DataTemplate>");

            stack.Children.Add(clearBtn);
            stack.Children.Add(histHeader);
            stack.Children.Add(histList);
            dialog.Content = stack;

            await dialog.ShowAsync();
        }
            
        private string GetNewTabHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>New Tab</title>
    <style>
        body { background-color: #1A1A1A; color: #FFFFFF; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .search-container { text-align: center; width: 100%; max-width: 600px; }
        .logo { font-size: 48px; font-weight: bold; margin-bottom: 30px; background: linear-gradient(90deg, #5064C8, #8C52FF); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
        input[type='text'] { width: 100%; padding: 15px 20px; font-size: 16px; border-radius: 24px; border: 1px solid #333; background-color: #2C2C2C; color: white; outline: none; }
        input[type='text']:focus { border-color: #5064C8; }
        .shortcuts { display: flex; justify-content: center; gap: 20px; margin-top: 40px; }
        .shortcut { display: flex; flex-direction: column; align-items: center; text-decoration: none; color: #AAAAAA; transition: color 0.2s; }
        .shortcut:hover { color: #FFFFFF; }
        .shortcut-icon { width: 48px; height: 48px; background-color: #2C2C2C; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin-bottom: 8px; font-size: 24px; }
    </style>
</head>
<body>
    <div class='search-container'>
        <div class='logo'>Nuggi</div>
        <form onsubmit='search(event)'>
            <input type='text' id='searchInput' placeholder='Search the web' autofocus autocomplete='off' />
        </form>
    </div>
    <div class='shortcuts'>
        <a href='https://www.youtube.com' class='shortcut'>
            <div class='shortcut-icon'>▶</div>
            <span>YouTube</span>
        </a>
        <a href='https://www.reddit.com' class='shortcut'>
            <div class='shortcut-icon'>👾</div>
            <span>Reddit</span>
        </a>
        <a href='https://www.github.com' class='shortcut'>
            <div class='shortcut-icon'>🐙</div>
            <span>GitHub</span>
        </a>
    </div>
    <script>
        function search(e) {
            e.preventDefault();
            const q = document.getElementById('searchInput').value;
            if (q) window.location.href = 'https://www.google.com/search?q=' + encodeURIComponent(q);
        }
    </script>
</body>
</html>";
        }
    }
}
