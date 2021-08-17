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
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace inkblaster {

    public enum EditMode {
        Inking, Selection, MoveSelection
    }

    public sealed partial class InkPage : Page {
        StorageFile sourceFile;
        DispatcherTimer autosaveTimer;
        EditMode currentMode = EditMode.Inking;

        Polyline selectionLasso;
        Rect selectionBoundingBox = Rect.Empty;
        Rectangle selectionBoundingRect;

        BasicCustomPen[] customPens;

        public InkPage() {
            this.InitializeComponent();
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerHovered += UnprocessedInput_PointerHovered;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            
            autosaveTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 250) };
            autosaveTimer.Tick += autosaveTimer_Tick;
            autosaveTimer.Start();

            customPens = new BasicCustomPen[3];
            for (int i = 0; i < customPens.Length; ++i)
                customPens[i] = new BasicCustomPen();
        }

        private async void loadFile() {
            using (IRandomAccessStream stream = await sourceFile.OpenAsync(Windows.Storage.FileAccessMode.Read)) {
                using (var inputStream = stream.GetInputStreamAt(0)) {
                    await inkCanvas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
                }
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
            using (IRandomAccessStream stream = await sourceFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite)) {
                using (var outputStream = stream.GetOutputStreamAt(0)) {
                    await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                    await outputStream.FlushAsync();
                }
            }
        }

        private void clearSelection() {
            foreach(var stroke in inkCanvas.InkPresenter.StrokeContainer.GetStrokes()) {
                stroke.Selected = false;
            }
            if(selectionCanvas.Children.Any()) {
                selectionCanvas.Children.Clear();
            }
            selectionLasso = null;
            selectionBoundingBox = Rect.Empty;
            selectionBoundingRect = null;
            selectionMenu.Visibility = Visibility.Collapsed;
            currentMode = EditMode.Inking;
            inkToolbar.ActiveTool = inkToolbar.GetToolButton(InkToolbarTool.BallpointPen);
        }

        private bool canvasUnsaved = false;

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args) {
            canvasUnsaved = true;
            if (currentMode == EditMode.Selection)
                clearSelection();
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args) {
            canvasUnsaved = true;
            if (currentMode == EditMode.Selection)
                clearSelection();
        }

        private async void autosaveTimer_Tick(object sender, object e) {
            if (canvasUnsaved) {
                saveFile(false);
                canvasUnsaved = false;
            }

            var ls = ApplicationData.Current.LocalSettings;
            for(int i = 0; i < customPens.Length; ++i) {
                var b = inkToolbar.Children[i] as InkToolbarCustomPenButton;
                ls.Values["brush#" + i] = b.SelectedBrushIndex;
            }
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args) {
            if(currentMode == EditMode.Selection) {
                if (selectionBoundingBox.IsEmpty) {
                    selectionLasso = new Polyline() {
                        Stroke = new SolidColorBrush(Colors.Orange),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection() { 5, 2 }
                    };
                    selectionLasso.Points.Add(args.CurrentPoint.RawPosition);
                    selectionCanvas.Children.Add(selectionLasso);
                } else {
                    clearSelection();
                }
            }
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args) {
            if (currentMode == EditMode.Selection) {
                selectionLasso.Points.Add(args.CurrentPoint.RawPosition);
            } else if(currentMode == EditMode.MoveSelection) {
                Canvas.SetLeft(selectionBoundingRect, args.CurrentPoint.RawPosition.X);
                Canvas.SetTop(selectionBoundingRect, args.CurrentPoint.RawPosition.Y);
            } else if(currentMode == EditMode.Inking && args.CurrentPoint.Properties.IsBarrelButtonPressed == true) {
                menu.Visibility = Visibility.Collapsed;
                currentMode = EditMode.Selection;
                inkToolbar.ActiveTool = inkToolbarLassoTool;
                selectionLasso = new Polyline() {
                    Stroke = new SolidColorBrush(Colors.Orange),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 5, 2 }
                };
                selectionLasso.Points.Add(args.CurrentPoint.RawPosition);
                selectionCanvas.Children.Add(selectionLasso);
            }
        }

        private void updateSelectionMenuDisplay() {
            if (selectionBoundingBox.Top < this.ActualHeight / 2) {
                Canvas.SetTop(selectionMenu, selectionBoundingBox.Bottom + 16);
            } else {
                Canvas.SetTop(selectionMenu, selectionBoundingBox.Top - 16 - selectionMenu.ActualHeight);
            }
            Canvas.SetLeft(selectionMenu, selectionBoundingBox.Left + selectionBoundingBox.Width / 2 - selectionMenu.ActualWidth / 2);
        }

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args) {
            if (currentMode == EditMode.Selection) {
                selectionLasso.Points.Add(args.CurrentPoint.RawPosition);

                selectionBoundingBox = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(selectionLasso.Points);
                if (!selectionBoundingBox.IsEmpty && selectionBoundingBox.Height != 0 && selectionBoundingBox.Width != 0) {
                    selectionCanvas.Children.Clear();
                    selectionBoundingRect = new Rectangle() {
                        Stroke = new SolidColorBrush(Colors.DarkOrange),
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection() { 4, 2 },
                        Width = selectionBoundingBox.Width + 16,
                        Height = selectionBoundingBox.Height + 16
                    };
                    Canvas.SetLeft(selectionBoundingRect, selectionBoundingBox.Left - 8);
                    Canvas.SetTop(selectionBoundingRect, selectionBoundingBox.Top - 8);
                    selectionCanvas.Children.Add(selectionBoundingRect);

                    updateSelectionMenuDisplay();
                    selectionMenu.Visibility = Visibility.Visible;
                }
            } else if(currentMode == EditMode.MoveSelection) {
                currentMode = EditMode.Selection;
                selectionMenu.Visibility = Visibility.Visible;
                selectionBoundingBox =
                    inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(Canvas.GetLeft(selectionBoundingRect) + 8 - selectionBoundingBox.Left,
                        Canvas.GetTop(selectionBoundingRect) + 8 - selectionBoundingBox.Top));
                updateSelectionMenuDisplay();
            }
        }

        private void UnprocessedInput_PointerHovered(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args) {
            if(args.CurrentPoint.Properties.IsBarrelButtonPressed && currentMode == EditMode.Inking) {
                Canvas.SetLeft(menu, args.CurrentPoint.RawPosition.X - 16.0f);
                Canvas.SetTop(menu, args.CurrentPoint.RawPosition.Y - 16.0f);
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

        private void activateSelectionMode(object sender, RoutedEventArgs e) {
            var button = (InkToolbarCustomToolButton)sender;
            if (button.IsChecked.GetValueOrDefault()) {
                menu.Visibility = Visibility.Collapsed;
                currentMode = EditMode.Selection;
            } else {
                currentMode = EditMode.Inking;
            }
        }

        private void paste(object sender, RoutedEventArgs e) {
            if(inkCanvas.InkPresenter.StrokeContainer.CanPasteFromClipboard()) {
                inkCanvas.InkPresenter.StrokeContainer.PasteFromClipboard(new Point(Canvas.GetLeft(menu), Canvas.GetTop(menu)));
            }
        }

        private void moveSelection(object sender, RoutedEventArgs e) {
            selectionMenu.Visibility = Visibility.Collapsed;
            currentMode = EditMode.MoveSelection;
        }

        private void copySelection(object sender, RoutedEventArgs e) {
            inkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
            clearSelection();
        }

        private void cutSelection(object sender, RoutedEventArgs e) {
            inkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            clearSelection();
        }

        private void deleteSelection(object sender, RoutedEventArgs e) {
            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            clearSelection();
        }

        private void InkToolbar_Loaded(object sender, RoutedEventArgs e) {
            var ls = ApplicationData.Current.LocalSettings;
            for (int i = 0; i < customPens.Length; ++i) {
                var b = inkToolbar.Children[i] as InkToolbarCustomPenButton;
                b.CustomPen = customPens[i];
                b.SelectedBrushIndex = (ls.Values["brush#" + i] as int?).GetValueOrDefault();
                inkToolbar.ActiveTool = b;
            }
            inkToolbar.ActiveTool = inkToolbar.Children[0] as InkToolbarToolButton;
        }

        public Symbol LassoIcon = (Symbol)0xEF20;
        public Symbol MoveIcon = (Symbol)0xE759;

        private void topLevelCanvas_SizeChanged(object sender, SizeChangedEventArgs e) {
            inkCanvas.Width = this.ActualWidth;
            inkScroller.Height = this.ActualHeight;
        }

        private void toggleFullscreen(object sender, RoutedEventArgs e) {
            var cv = ApplicationView.GetForCurrentView();
            if(cv.IsFullScreenMode) {
                cv.ExitFullScreenMode();
                fullScreenToggle.Content = "\xE740";
            } else {
                if (cv.TryEnterFullScreenMode()) {
                    fullScreenToggle.Content = "\xE73F";
                }
            }
        }
    }
}
