using System.ComponentModel;
using System.Drawing;

namespace HaRepacker.Models
{
    public class NotifyPointF : INotifyPropertyChanged
    {
        private float _x;
        private float _y;

        public float X
        {
            get => _x;
            set { if (_x != value) { _x = value; OnPropertyChanged(nameof(X)); } }
        }

        public float Y
        {
            get => _y;
            set { if (_y != value) { _y = value; OnPropertyChanged(nameof(Y)); } }
        }

        public NotifyPointF(float x, float y) { _x = x; _y = y; }
        public NotifyPointF(PointF f) { _x = f.X; _y = f.Y; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static implicit operator PointF(NotifyPointF p) => new PointF(p.X, p.Y);
        public static implicit operator NotifyPointF(PointF p) => new NotifyPointF(p.X, p.Y);
    }
}
