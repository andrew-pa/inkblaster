using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace inkblaster {
    public sealed partial class InkPage : Page {
        StorageFile sourceFile;
        DispatcherTimer autosaveTimer;

        public InkPage() {
            this.InitializeComponent();
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerHovered += UnprocessedInput_PointerHovered;
            inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            autosaveTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 500) };
            autosaveTimer.Tick += autosaveTimer_Tick;
            autosaveTimer.Start();
        }

        private async void loadFile() {
            IRandomAccessStream stream = await sourceFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
            using (var inputStream = stream.GetInputStreamAt(0)) {
                await inkCanvas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
            }
            inkCanvas.Height = inkCanvas.InkPresenter.StrokeContainer.BoundingRect.Height + 1024;
        }

        private async void saveFile(bool openPicker = true) {
            if(sourceFile == null) {
                if (!openPicker) return;
                Windows.Storage.Pickers.FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation =
                    Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add(
                    "GIF with embedded ISF",
                    new List<string>() { ".gif" });
                savePicker.DefaultFileExtension = ".gif";

                Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null) {
                    StorageApplicationPermissions.MostRecentlyUsedList.Add(file);
                    sourceFile = file;
                } else {
                    return;
                }
            }
            IRandomAccessStream stream = await sourceFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            using (var outputStream = stream.GetOutputStreamAt(0)) {
                await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                await outputStream.FlushAsync();
            }
        }

        private bool canvasUnsaved = false;

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args) {
            canvasUnsaved = true;
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args) {
            canvasUnsaved = true;
        }

        private async void autosaveTimer_Tick(object sender, object e) {
            if (canvasUnsaved) {
                saveFile(false);
                canvasUnsaved = false;
            }
        }

        private void UnprocessedInput_PointerHovered(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args) {
            if(args.CurrentPoint.Properties.IsBarrelButtonPressed) {
                menu.Translation = new Vector3((float)args.CurrentPoint.RawPosition.X - 32.0f, (float)args.CurrentPoint.RawPosition.Y - 32.0f, 0.0f);
                menu.Visibility = Visibility.Visible;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var f = e.Parameter as StorageFile;
            if(f != null) {
                StorageApplicationPermissions.MostRecentlyUsedList.Add(f);
                sourceFile = f;
                loadFile();
            }
            base.OnNavigatedTo(e);
        }

        private void closeMenu(object sender, RoutedEventArgs e) {
            menu.Visibility = Visibility.Collapsed;
        }

        private void extendCanvas(object sender, RoutedEventArgs e) {
            inkCanvas.Height += 1024;
        }

        private void saveFileButton(object sender, RoutedEventArgs e) {
            saveFile();
        }

        private void returnToMain(object sender, RoutedEventArgs e) {
            saveFile();
            Frame rootFrame = Window.Current.Content as Frame;
            rootFrame.Navigate(typeof(MainPage), null);
        }
    }
}
