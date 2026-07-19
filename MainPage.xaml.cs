using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
        public Grid ContentContainer { get; set; } = null!;
        public WebView2 PrimaryWebView { get; set; } = null!;
        public WebView2? SecondaryWebView { get; set; } = null;
        public WebView2 ActiveWebView => SecondaryWebView ?? PrimaryWebView;
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

    public class DownloadItem : INotifyPropertyChanged
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        
        private double _progress;
        public double Progress 
        { 
            get => _progress; 
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } 
        }

        private string _status = "Downloading...";
        public string Status 
        { 
            get => _status; 
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(ProgressVisibility)); } 
        }
        
        public string StatusText => $"{Status} {(_progress > 0 && Status == "Downloading..." ? $"{Math.Round(_progress)}%" : "")}";
        public Visibility ProgressVisibility => Status == "Downloading..." ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class MainPage : Page
    {
        public ObservableCollection<BrowserTab> Tabs { get; } = new();
        public ObservableCollection<BrowserTab> PinnedTabs { get; } = new();
        public ObservableCollection<DownloadItem> Downloads { get; } = new();
        private BrowserTab? _activeTab;
        
        // Spaces
        private List<string> _spaces = new() { "Personal" };
        private string _currentSpace = "Personal";
        private Dictionary<string, List<(string title, string url)>> _spaceTabs = new();

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

            UpdateAccentColors();
            LoadSpaces();

            // Load saved notes
            var savedNotes = Windows.Storage.ApplicationData.Current.LocalSettings.Values["QuickNotes"] as string ?? "";
            NotesTextBox.Text = savedNotes;

            CreateNewTab();
        }

        private bool _notesOpen = false;

        private void QuickNotes_Click(object sender, RoutedEventArgs e)
        {
            _notesOpen = !_notesOpen;

            // Make visible before animating in
            NotesPanel.Visibility = Visibility.Visible;

            // Enable translation on the composition visual
            ElementCompositionPreview.SetIsTranslationEnabled(NotesPanel, true);
            var visual = ElementCompositionPreview.GetElementVisual(NotesPanel);
            var compositor = visual.Compositor;

            // Slide animation
            var slideAnim = compositor.CreateScalarKeyFrameAnimation();
            var easing = _notesOpen
                ? compositor.CreateCubicBezierEasingFunction(new Vector2(0.0f, 0.0f), new Vector2(0.15f, 1.0f))
                : compositor.CreateCubicBezierEasingFunction(new Vector2(0.85f, 0.0f), new Vector2(1.0f, 1.0f));
            slideAnim.InsertKeyFrame(0.0f, _notesOpen ? 300f : 0f, easing);
            slideAnim.InsertKeyFrame(1.0f, _notesOpen ? 0f : 300f, easing);
            slideAnim.Duration = TimeSpan.FromMilliseconds(180);

            // Opacity animation
            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(1.0f, _notesOpen ? 1f : 0f);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(150);

            visual.StartAnimation("Translation.X", slideAnim);
            visual.StartAnimation("Opacity", opacityAnim);

            // Collapse after close animation finishes
            if (!_notesOpen)
            {
                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, a) => NotesPanel.Visibility = Visibility.Collapsed;
                visual.StartAnimation("Translation.X", slideAnim);
                batch.End();
            }
            else
            {
                NotesTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void Notes_TextChanged(object sender, TextChangedEventArgs e)
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["QuickNotes"] = NotesTextBox.Text;
        }

        private void LoadSpaces()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["Spaces"] is string spacesJson)
            {
                try { _spaces = System.Text.Json.JsonSerializer.Deserialize<List<string>>(spacesJson) ?? new() { "Personal" }; } catch { }
            }
            if (_spaces.Count == 0) _spaces.Add("Personal");
            _currentSpace = localSettings.Values["CurrentSpace"] as string ?? _spaces[0];
            
            SpaceComboBox.ItemsSource = _spaces;
            SpaceComboBox.SelectedItem = _currentSpace;
        }

        private void SaveSpaces()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["Spaces"] = System.Text.Json.JsonSerializer.Serialize(_spaces);
            localSettings.Values["CurrentSpace"] = _currentSpace;
        }

        private void SpaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpaceComboBox.SelectedItem is string selected && selected != _currentSpace)
            {
                // Save current space's tabs
                var currentTabData = new List<(string title, string url)>();
                foreach (var tab in Tabs) currentTabData.Add((tab.Title, tab.Url));
                _spaceTabs[_currentSpace] = currentTabData;
                
                // Clear current tabs
                foreach (var tab in Tabs.ToList()) 
                { 
                    WebViewContainer.Children.Remove(tab.ContentContainer); 
                    tab.PrimaryWebView.Close(); 
                    tab.SecondaryWebView?.Close(); 
                }
                Tabs.Clear();
                _activeTab = null;
                
                _currentSpace = selected;
                SaveSpaces();
                
                // Restore new space's tabs
                if (_spaceTabs.ContainsKey(_currentSpace) && _spaceTabs[_currentSpace].Count > 0)
                {
                    foreach (var (title, url) in _spaceTabs[_currentSpace])
                    {
                        CreateNewTab(string.IsNullOrEmpty(url) ? "nuggi://newtab" : url);
                    }
                }
                else
                {
                    CreateNewTab();
                }
            }
        }

        private async void AddSpace_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new TextBox { PlaceholderText = "Space name", Width = 300 };
            var dialog = new ContentDialog
            {
                Title = "New Space",
                Content = inputBox,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
            {
                _spaces.Add(inputBox.Text.Trim());
                SaveSpaces();
                SpaceComboBox.ItemsSource = null;
                SpaceComboBox.ItemsSource = _spaces;
                SpaceComboBox.SelectedItem = inputBox.Text.Trim();
            }
        }

        private void SetupWebViewEvents(WebView2 webView, BrowserTab tab)
        {
            webView.NavigationStarting += (s, e) =>
            {
                if (e.Uri != "about:blank" && !e.Uri.StartsWith("data:")) tab.Url = e.Uri;
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

                if (!string.IsNullOrWhiteSpace(webView.CoreWebView2.DocumentTitle)) tab.Title = webView.CoreWebView2.DocumentTitle;

                if (tab.Url != "nuggi://newtab" && !string.IsNullOrEmpty(tab.Url))
                {
                    try { tab.Favicon = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={new Uri(tab.Url).Host}&sz=32")); } catch { }
                    _ = HistoryManager.AddEntryAsync(tab.Title, tab.Url);
                }
            };
            
            webView.PointerPressed += (s, e) => { if (tab.SecondaryWebView != null) tab.PrimaryWebView = webView; };
        }

        private async void CreateNewTab(string url = "nuggi://newtab")
        {
            var webView = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            var container = new Grid { Visibility = Visibility.Collapsed };
            container.Children.Add(webView);

            var tab = new BrowserTab { Title = "New Tab", Url = url, PrimaryWebView = webView, ContentContainer = container };
            SetupWebViewEvents(webView, tab);

            Tabs.Add(tab);
            WebViewContainer.Children.Add(container);
            TabsListView.SelectedItem = tab;

            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

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
            else if (url == "nuggi://history")
            {
                await HistoryManager.LoadAsync();
                webView.NavigateToString(GetHistoryHtml());
                tab.Url = "nuggi://history";
                tab.Title = "History";
            }
            else
            {
                webView.CoreWebView2.Navigate(url);
            }
        }

        private void SwitchToTab(BrowserTab tab)
        {
            if (_activeTab != null) _activeTab.ContentContainer.Visibility = Visibility.Collapsed;
            _activeTab = tab;
            _activeTab.ContentContainer.Visibility = Visibility.Visible;
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
            WebViewContainer.Children.Remove(tab.ContentContainer);
            tab.PrimaryWebView.Close();
            tab.SecondaryWebView?.Close();
            
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

        private async void SplitTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BrowserTab tab && tab.SecondaryWebView == null)
            {
                var newWeb = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                tab.SecondaryWebView = newWeb;

                tab.ContentContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                tab.ContentContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Pixel) });
                tab.ContentContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(tab.PrimaryWebView, 0);
                
                var border = new Border { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(50, 150, 150, 150)) };
                Grid.SetColumn(border, 1);
                
                Grid.SetColumn(newWeb, 2);

                tab.ContentContainer.Children.Add(border);
                tab.ContentContainer.Children.Add(newWeb);

                SetupWebViewEvents(newWeb, tab);
                await newWeb.EnsureCoreWebView2Async();
                newWeb.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                
                try { newWeb.CoreWebView2.Profile.PreferredColorScheme = tab.PrimaryWebView.CoreWebView2.Profile.PreferredColorScheme; } catch { }
                newWeb.NavigateToString(GetNewTabHtml());
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
            CreateNewTab("nuggi://history");
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab("nuggi://history");
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
            _activeTab?.ActiveWebView.Reload();
        }

        private void NavigateTo(string url)
        {
            if (_activeTab != null)
            {
                _activeTab.ActiveWebView.CoreWebView2.Navigate(url);
            }
        }

        private async void NavigateActiveTab(string text)
        {
            if (_activeTab == null) return;
            
            if (text == "nuggi://newtab" || string.IsNullOrWhiteSpace(text))
            {
                await _activeTab.ActiveWebView.EnsureCoreWebView2Async();
                _activeTab.ActiveWebView.NavigateToString(GetNewTabHtml());
                _activeTab.Url = "";
                UrlTextBox.Text = "";
                return;
            }

            if (text.StartsWith("http://") || text.StartsWith("https://") || text.Contains("."))
            {
                string dest = text.StartsWith("http") ? text : "https://" + text;
                try { _activeTab.ActiveWebView.Source = new Uri(dest); } catch (UriFormatException) { }
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
        { if (_activeTab?.ActiveWebView.CanGoBack == true) _activeTab.ActiveWebView.GoBack(); }

        private void Forward_Click(object sender, RoutedEventArgs e)
        { if (_activeTab?.ActiveWebView.CanGoForward == true) _activeTab.ActiveWebView.GoForward(); }

        private void Refresh_Click(object sender, RoutedEventArgs e)
            => _activeTab?.ActiveWebView.Reload();
            
        private void CoreWebView2_DownloadStarting(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2DownloadStartingEventArgs args)
        {
            var op = args.DownloadOperation;
            var item = new DownloadItem { FileName = System.IO.Path.GetFileName(op.ResultFilePath), FilePath = op.ResultFilePath };
            
            DispatcherQueue.TryEnqueue(() => 
            { 
                Downloads.Add(item); 
                DownloadsButton.Flyout?.ShowAt(DownloadsButton);
            });

            op.BytesReceivedChanged += (s, e) =>
            {
                if (op.TotalBytesToReceive > 0) 
                    DispatcherQueue.TryEnqueue(() => item.Progress = (double)op.BytesReceived / op.TotalBytesToReceive * 100);
            };
            
            op.StateChanged += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => 
                {
                    if (op.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed) { item.Status = "Completed"; item.Progress = 100; }
                    else if (op.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted) item.Status = "Failed";
                });
            };
        }

        private async void OpenDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is DownloadItem item && item.Status == "Completed")
            {
                try 
                {
                    var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(item.FilePath));
                    var file = await folder.GetFileAsync(item.FileName);
                    await Windows.System.Launcher.LaunchFileAsync(file);
                } 
                catch { }
            }
        }

        private void UpdateAccentColors()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string accentName = localSettings.Values["AccentColor"] as string ?? "Nuggi Gold";
            Windows.UI.Color color = Windows.UI.Color.FromArgb(255, 237, 143, 3); // #ED8F03
            
            if (accentName == "Ocean Blue") color = Windows.UI.Color.FromArgb(255, 47, 128, 237);
            else if (accentName == "Forest Green") color = Windows.UI.Color.FromArgb(255, 40, 199, 111);
            else if (accentName == "Amethyst Purple") color = Windows.UI.Color.FromArgb(255, 110, 72, 170);
            else if (accentName == "Crimson Red") color = Windows.UI.Color.FromArgb(255, 255, 75, 43);

            var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            MainProgressBar.Foreground = brush;
            UrlTextBox.SelectionHighlightColor = brush;
        }
            
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
                    try { tab.PrimaryWebView.CoreWebView2.Profile.PreferredColorScheme = wvTheme; } catch { }
                    try { if (tab.SecondaryWebView != null) tab.SecondaryWebView.CoreWebView2.Profile.PreferredColorScheme = wvTheme; } catch { }
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
            
            // 4. Accent Color Setting
            var accentStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            accentStack.Children.Add(new FontIcon { Glyph = "\uE790", VerticalAlignment = VerticalAlignment.Center });
            var accentCombo = new ComboBox
            {
                Header = "Accent Color",
                Width = 200,
                ItemsSource = new[] { "Nuggi Gold", "Ocean Blue", "Forest Green", "Amethyst Purple", "Crimson Red" },
                SelectedItem = localSettings.Values["AccentColor"] as string ?? "Nuggi Gold"
            };
            accentCombo.SelectionChanged += (s, args) =>
            {
                localSettings.Values["AccentColor"] = accentCombo.SelectedItem as string;
                UpdateAccentColors();
                RefreshNewTabs();
            };
            accentStack.Children.Add(accentCombo);
            stack.Children.Add(accentStack);
            
            // 5. Clear Tabs Action
            var clearStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            clearStack.Children.Add(new FontIcon { Glyph = "\uE74D", VerticalAlignment = VerticalAlignment.Center });
            var clearBtn = new Button { Content = "Close all tabs", Width = 200 };
            clearBtn.Click += (s, args) => 
            {
                var unpinned = Tabs.Where(t => !t.IsPinned).ToList();
                foreach (var t in unpinned) 
                { 
                    Tabs.Remove(t); 
                    WebViewContainer.Children.Remove(t.ContentContainer); 
                    t.PrimaryWebView.Close(); 
                    t.SecondaryWebView?.Close();
                }
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
                if (tab.Url == "") tab.PrimaryWebView.NavigateToString(GetNewTabHtml());
                if (tab.SecondaryWebView != null && string.IsNullOrEmpty(tab.SecondaryWebView.Source?.ToString()))
                    tab.SecondaryWebView.NavigateToString(GetNewTabHtml());
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

            string accentName = localSettings.Values["AccentColor"] as string ?? "Nuggi Gold";
            string colorMid = "#ED8F03";
            string textGradient = "linear-gradient(90deg, #FFB75E, #ED8F03, #FFB75E)";
            if (accentName == "Ocean Blue") { colorMid = "#2F80ED"; textGradient = "linear-gradient(90deg, #56CCF2, #2F80ED, #56CCF2)"; }
            else if (accentName == "Forest Green") { colorMid = "#28C76F"; textGradient = "linear-gradient(90deg, #81FBB8, #28C76F, #81FBB8)"; }
            else if (accentName == "Amethyst Purple") { colorMid = "#9D50BB"; textGradient = "linear-gradient(90deg, #9D50BB, #6E48AA, #9D50BB)"; }
            else if (accentName == "Crimson Red") { colorMid = "#FF4B2B"; textGradient = "linear-gradient(90deg, #FF416C, #FF4B2B, #FF416C)"; }

            string glow = isDark ? colorMid + "26" : colorMid + "1A"; // 15% and 10% opacity in hex
            string borderGlow = colorMid + "66"; // 40% opacity in hex

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
        @keyframes borderPulse {{ 0% {{ box-shadow: 0 0 0 0 {borderGlow}; }} 70% {{ box-shadow: 0 0 0 15px {colorMid}00; }} 100% {{ box-shadow: 0 0 0 0 {colorMid}00; }} }}
        @keyframes bounce {{ 0%, 100% {{ transform: translateY(0) scale(1); }} 50% {{ transform: translateY(-10px) scale(1.05); }} }}

        body {{ background: {bgGradient}; background-size: 400% 400%; color: {fg}; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; animation: bgBreathe 15s ease infinite; overflow: hidden; }}
        
        .ambient-glow {{ position: absolute; width: 800px; height: 800px; background: radial-gradient(circle, {glow} 0%, rgba(0,0,0,0) 70%); top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: -1; animation: pulseGlow 6s ease-in-out infinite alternate; pointer-events: none; }}
        
        .search-container {{ text-align: center; width: 100%; max-width: 600px; z-index: 1; animation: fadeUp 0.8s cubic-bezier(0.2, 0.8, 0.2, 1); }}
        
        .logo {{ font-size: 64px; font-weight: 800; margin-bottom: 35px; background: {textGradient}; background-size: 200% auto; animation: shimmer 3s linear infinite; -webkit-background-clip: text; -webkit-text-fill-color: transparent; filter: drop-shadow(0px 8px 16px {borderGlow}); transition: all 0.3s ease; }}
        .logo:hover {{ animation: shimmer 3s linear infinite, bounce 1.5s cubic-bezier(0.28, 0.84, 0.42, 1) infinite; }}
        
        input[type='text'] {{ width: 100%; padding: 18px 28px; font-size: 16px; border-radius: 32px; border: 1px solid {border}; background-color: {inputBg}; backdrop-filter: blur(10px); color: {fg}; outline: none; box-shadow: 0 4px 24px rgba(0,0,0,0.05); transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); }}
        input[type='text']:hover {{ box-shadow: 0 8px 32px rgba(0,0,0,0.1); transform: translateY(-2px); border-color: {colorMid}80; }}
        input[type='text']:focus {{ border-color: {colorMid}; background-color: {inputBg}; transform: translateY(-4px) scale(1.03); animation: borderPulse 2s infinite; }}
        
        .shortcuts {{ display: flex; justify-content: center; gap: 32px; margin-top: 56px; }}
        .shortcut {{ display: flex; flex-direction: column; align-items: center; text-decoration: none; color: #888; transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); animation: fadeUp 0.8s cubic-bezier(0.2, 0.8, 0.2, 1) both; }}
        .shortcut:nth-child(1) {{ animation-delay: 0.1s; }}
        .shortcut:nth-child(2) {{ animation-delay: 0.2s; }}
        .shortcut:nth-child(3) {{ animation-delay: 0.3s; }}
        .shortcut:hover {{ color: {fg}; transform: translateY(-10px) scale(1.1); }}
        .shortcut:active {{ transform: translateY(2px) scale(0.9); }}
        
        .shortcut-icon {{ width: 64px; height: 64px; background-color: {inputBg}; backdrop-filter: blur(10px); border: 1px solid {border}; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin-bottom: 12px; font-size: 28px; transition: all 0.4s cubic-bezier(0.2, 0.8, 0.2, 1); box-shadow: 0 4px 16px rgba(0,0,0,0.05); object-fit: contain; padding: 12px; box-sizing: border-box; }}
        .shortcut:hover .shortcut-icon {{ background-color: {colorMid}; border-color: {colorMid}; color: white; box-shadow: 0 12px 32px {borderGlow}; transform: rotate(8deg) scale(1.1); filter: brightness(1.2); }}
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

        private string GetHistoryHtml()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            bool isDark = this.ActualTheme != ElementTheme.Light;
            string bg = isDark ? "#1A1A1A" : "#FAFAFA";
            string fg = isDark ? "#FFFFFF" : "#111111";
            string cardBg = isDark ? "rgba(255,255,255,0.04)" : "rgba(0,0,0,0.02)";
            string border = isDark ? "rgba(255,255,255,0.08)" : "rgba(0,0,0,0.08)";
            string muted = isDark ? "#888" : "#666";

            string accentName = localSettings.Values["AccentColor"] as string ?? "Nuggi Gold";
            string accent = "#ED8F03";
            if (accentName == "Ocean Blue") accent = "#2F80ED";
            else if (accentName == "Forest Green") accent = "#28C76F";
            else if (accentName == "Amethyst Purple") accent = "#9D50BB";
            else if (accentName == "Crimson Red") accent = "#FF4B2B";

            var history = HistoryManager.GetHistory();
            var entriesHtml = new System.Text.StringBuilder();
            string lastDate = "";
            int animDelay = 0;

            foreach (var entry in history)
            {
                string dateLabel = entry.Timestamp.Date == DateTime.Today ? "Today"
                    : entry.Timestamp.Date == DateTime.Today.AddDays(-1) ? "Yesterday"
                    : entry.Timestamp.ToString("MMMM d, yyyy");

                if (dateLabel != lastDate)
                {
                    lastDate = dateLabel;
                    entriesHtml.Append($"<div class='date-header' style='animation-delay:{animDelay * 30}ms'>{dateLabel}</div>");
                    animDelay++;
                }

                string domain = "";
                try { domain = new Uri(entry.Url).Host; } catch { domain = entry.Url; }
                string safeTitle = entry.Title.Replace("'", "&#39;").Replace("\"", "&quot;");
                string safeUrl = entry.Url.Replace("'", "&#39;").Replace("\"", "&quot;");

                entriesHtml.Append($@"
                <a href='{safeUrl}' class='entry' style='animation-delay:{animDelay * 30}ms'>
                    <img src='https://www.google.com/s2/favicons?domain={domain}&sz=32' class='fav' onerror=""this.style.display='none'""/>
                    <div class='info'>
                        <div class='title'>{safeTitle}</div>
                        <div class='url'>{domain}</div>
                    </div>
                    <div class='time'>{entry.Timestamp:HH:mm}</div>
                </a>");
                animDelay++;
            }

            if (history.Count == 0)
            {
                entriesHtml.Append("<div style='text-align:center;padding:80px 0;color:#888;font-size:16px;'>No history yet. Start browsing!</div>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>History</title>
    <style>
        @keyframes fadeIn {{ from {{ opacity: 0; transform: translateY(12px); }} to {{ opacity: 1; transform: translateY(0); }} }}
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ background: {bg}; color: {fg}; font-family: 'Segoe UI', sans-serif; padding: 48px 20%; min-height: 100vh; }}
        
        h1 {{ font-size: 32px; font-weight: 700; margin-bottom: 8px; animation: fadeIn 0.5s ease both; }}
        .subtitle {{ color: {muted}; font-size: 14px; margin-bottom: 32px; animation: fadeIn 0.5s ease 0.1s both; }}
        
        .search {{ width: 100%; padding: 14px 20px; font-size: 15px; border-radius: 12px; border: 1px solid {border}; background: {cardBg}; color: {fg}; outline: none; margin-bottom: 32px; transition: all 0.3s ease; animation: fadeIn 0.5s ease 0.15s both; }}
        .search:focus {{ border-color: {accent}; box-shadow: 0 0 0 3px {accent}33; }}
        
        .date-header {{ font-size: 13px; font-weight: 600; color: {accent}; text-transform: uppercase; letter-spacing: 1px; padding: 16px 0 8px 4px; border-bottom: 1px solid {border}; margin-bottom: 4px; animation: fadeIn 0.4s ease both; }}
        
        .entry {{ display: flex; align-items: center; gap: 14px; padding: 12px 16px; border-radius: 12px; text-decoration: none; color: {fg}; transition: all 0.25s ease; animation: fadeIn 0.4s ease both; cursor: pointer; }}
        .entry:hover {{ background: {cardBg}; transform: translateX(6px); }}
        
        .fav {{ width: 24px; height: 24px; border-radius: 6px; flex-shrink: 0; }}
        .info {{ flex: 1; min-width: 0; }}
        .title {{ font-size: 14px; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
        .url {{ font-size: 12px; color: {muted}; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
        .time {{ font-size: 12px; color: {muted}; flex-shrink: 0; }}
        
        .clear-btn {{ position: fixed; bottom: 32px; right: 20%; padding: 12px 24px; background: {accent}; color: white; border: none; border-radius: 12px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.3s ease; box-shadow: 0 4px 16px {accent}44; }}
        .clear-btn:hover {{ transform: translateY(-2px); box-shadow: 0 8px 24px {accent}66; }}
    </style>
</head>
<body>
    <h1>History</h1>
    <div class='subtitle'>{history.Count} pages visited</div>
    <input type='text' class='search' placeholder='Search history...' oninput='filter(this.value)' />
    <div id='entries'>
        {entriesHtml}
    </div>
    <button class='clear-btn' onclick='clearAll()'>Clear All History</button>
    <script>
        function filter(q) {{
            const entries = document.querySelectorAll('.entry');
            const headers = document.querySelectorAll('.date-header');
            q = q.toLowerCase();
            entries.forEach(e => {{
                const match = e.textContent.toLowerCase().includes(q);
                e.style.display = match ? 'flex' : 'none';
            }});
        }}
        function clearAll() {{
            if (confirm('Clear all browsing history?')) {{
                document.getElementById('entries').innerHTML = ""<div style='text-align:center;padding:80px 0;color:#888;font-size:16px;'>History cleared!</div>"";
                document.querySelector('.clear-btn').style.display = 'none';
            }}
        }}
    </script>
</body>
</html>";
        }
    }
}
