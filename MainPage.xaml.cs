using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class MainPage : Page
    {
        public ObservableCollection<BrowserTab> Tabs { get; } = new();
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

            CreateNewTab("https://www.google.com");
        }

        private void CreateNewTab(string url)
        {
            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            var tab = new BrowserTab { Title = "Loading...", Url = url, WebView = webView };

            webView.NavigationStarting += (s, e) =>
            {
                tab.Url = e.Uri;
                if (tab == _activeTab) UrlTextBox.Text = e.Uri;
            };

            webView.NavigationCompleted += (s, e) =>
            {
                tab.Title = string.IsNullOrWhiteSpace(webView.CoreWebView2.DocumentTitle)
                    ? "Web Page" : webView.CoreWebView2.DocumentTitle;
            };

            webView.Source = new Uri(url);
            Tabs.Add(tab);
            WebViewContainer.Children.Add(webView);
            TabsListView.SelectedItem = tab;
        }

        private void SwitchToTab(BrowserTab tab)
        {
            if (_activeTab != null) _activeTab.WebView.Visibility = Visibility.Collapsed;
            _activeTab = tab;
            _activeTab.WebView.Visibility = Visibility.Visible;
            UrlTextBox.Text = _activeTab.Url;
            TabsListView.SelectedItem = _activeTab;
        }

        private void TabsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsListView.SelectedItem is BrowserTab tab) SwitchToTab(tab);
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
            => CreateNewTab("https://www.google.com");

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BrowserTab tab)
            {
                WebViewContainer.Children.Remove(tab.WebView);
                tab.WebView.Close();
                Tabs.Remove(tab);

                if (Tabs.Count == 0) CreateNewTab("https://www.google.com");
                else if (tab == _activeTab) SwitchToTab(Tabs[0]);
            }
        }

        private void NavigateActiveTab(string dest)
        {
            if (_activeTab == null) return;
            if (!dest.StartsWith("http://") && !dest.StartsWith("https://"))
                dest = dest.Contains(".") && !dest.Contains(" ")
                    ? "https://" + dest
                    : "https://www.google.com/search?q=" + Uri.EscapeDataString(dest);
            try { _activeTab.WebView.Source = new Uri(dest); } catch (UriFormatException) { }
        }

        private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        { if (e.Key == VirtualKey.Enter) NavigateActiveTab(UrlTextBox.Text); }

        private void Back_Click(object sender, RoutedEventArgs e)
        { if (_activeTab?.WebView.CanGoBack == true) _activeTab.WebView.GoBack(); }

        private void Forward_Click(object sender, RoutedEventArgs e)
        { if (_activeTab?.WebView.CanGoForward == true) _activeTab.WebView.GoForward(); }

        private void Refresh_Click(object sender, RoutedEventArgs e)
            => _activeTab?.WebView.Reload();
    }
}
