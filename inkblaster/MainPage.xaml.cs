using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace inkblaster {
    public class RecentFile {
        public StorageFile file;
        public String path;
        public String name;
        public DateTime creationDate;

        public RecentFile(StorageFile file) {
            this.file = file;
            this.name = file.DisplayName;
            this.creationDate = file.DateCreated.LocalDateTime;
            this.path = file.Path;
        }
    }

    public sealed partial class MainPage : Page {
        private ObservableCollection<RecentFile> recentFiles;

        public MainPage() {
            this.InitializeComponent();
            recentFiles = new ObservableCollection<RecentFile>();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            recentFiles.Clear();
            foreach (var entry in StorageApplicationPermissions.MostRecentlyUsedList.Entries) {
                recentFiles.Add(new RecentFile(await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(entry.Token)));
            }
            base.OnNavigatedTo(e);
        }

        private async void newFile(object sender, RoutedEventArgs e) {
            Frame rootFrame = Window.Current.Content as Frame;
            rootFrame.Navigate(typeof(InkPage), null);
        }

        private async void openFile(object sender, RoutedEventArgs e) {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".gif");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null) {
                Frame rootFrame = Window.Current.Content as Frame;
                rootFrame.Navigate(typeof(InkPage), file);
            } else {
                // The file picker was dismissed with no file selected to save
            }
        }

        private void recentFilesList_ItemClick(object sender, ItemClickEventArgs e) {
            Frame rootFrame = Window.Current.Content as Frame;
            rootFrame.Navigate(typeof(InkPage), (e.ClickedItem as RecentFile).file);
        }
    }
}
