using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Storage;

namespace inkblaster {
    public static class ColorUtils {
        public static uint ToUint(this Color c) {
            return (uint)(((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B) & 0xffffffffL);
        }

        public static Color ToColor(this uint value) {
            return Color.FromArgb((byte)((value >> 24) & 0xFF),
                       (byte)((value >> 16) & 0xFF),
                       (byte)((value >> 8) & 0xFF),
                       (byte)(value & 0xFF));
        }
    }

    public class BasicCustomPen : InkToolbarCustomPen, INotifyPropertyChanged {
        public Color color = Colors.White;

        public Brush Brush { get { return new SolidColorBrush(color); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override InkDrawingAttributes CreateInkDrawingAttributesCore(Brush brush, double strokeWidth) {
            var cbrush = brush as SolidColorBrush;

            if (cbrush != null) {
                color = cbrush.Color;
                OnPropertyChanged("Brush");
            }

            return new InkDrawingAttributes() {
                PenTip = PenTipShape.Circle,
                Size = new Windows.Foundation.Size(strokeWidth, strokeWidth),
                Color = color
            };
        }
    }
}