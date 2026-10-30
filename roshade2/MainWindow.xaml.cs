using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace roshade2
{
    public partial class MainWindow : Window
    {
        private bool _isDraggingSlider = false;
        private double _sliderRatio = 0.5; // Ползунок ровно посередине (50%)

        public MainWindow()
        {
            InitializeComponent();
            LoadCustomImages();
        }

        private void LoadCustomImages()
        {
            BeforeImage.Source = TryLoadImage("before.jpg");
            AfterImage.Source = TryLoadImage("after.jpg");

            var rawLogo = TryLoadImage("logo.png");
            if (rawLogo != null)
            {
                var processedLogo = ProcessLogoImage(rawLogo);
                MainLogo.Source = processedLogo;
                TitleBarLogo.Source = processedLogo;
            }
        }

        // Загружает картинку напрямую изнутри скомпилированного .exe файла (EmbeddedResource)
        private BitmapSource? TryLoadImage(string fileName)
        {
            // 1. Читаем вшитый ресурс прямо из памяти .exe (работает в Publish Single-File на 100%)
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }

            // 2. Запасной вариант: ищем на диске (поднимаемся до 8 папок вверх от bin/Release/net10.0/publish/win-x64)
            string? currentDir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && currentDir != null; i++)
            {
                string candidate = Path.Combine(currentDir, "Assets", fileName);
                if (File.Exists(candidate))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(candidate, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            return null;
        }

        // Удаляет белый фон ТОЛЬКО снаружи от краев картинки (Flood Fill) и обрезает пустые поля вокруг буквы R
        private BitmapSource ProcessLogoImage(BitmapSource source)
        {
            var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            formatted.CopyPixels(pixels, stride, 0);

            bool[] visited = new bool[width * height];
            Queue<(int x, int y)> queue = new Queue<(int x, int y)>();

            queue.Enqueue((0, 0));
            queue.Enqueue((width - 1, 0));
            queue.Enqueue((0, height - 1));
            queue.Enqueue((width - 1, height - 1));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x < 0 || x >= width || y < 0 || y >= height) continue;

                int idx = y * width + x;
                if (visited[idx]) continue;
                visited[idx] = true;

                int pIdx = idx * 4;
                byte b = pixels[pIdx];
                byte g = pixels[pIdx + 1];
                byte r = pixels[pIdx + 2];

                if (r > 235 && g > 235 && b > 235)
                {
                    pixels[pIdx + 3] = 0;

                    queue.Enqueue((x + 1, y));
                    queue.Enqueue((x - 1, y));
                    queue.Enqueue((x, y + 1));
                    queue.Enqueue((x, y - 1));
                }
            }

            int minX = width, minY = height, maxX = 0, maxY = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(y * width + x) * 4 + 3] > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);

            if (maxX > minX && maxY > minY)
            {
                var cropped = new CroppedBitmap(result, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
                cropped.Freeze();
                return cropped;
            }

            result.Freeze();
            return result;
        }

        // ==================== ЛОГИКА ПОЛЗУНКА ДО / ПОСЛЕ ====================

        private void Comparison_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSliderPosition(ComparisonContainer.ActualWidth * _sliderRatio);
        }

        private void Comparison_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
            ComparisonContainer.CaptureMouse();
            UpdateSliderPosition(e.GetPosition(ComparisonContainer).X);
        }

        private void Comparison_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSlider)
            {
                UpdateSliderPosition(e.GetPosition(ComparisonContainer).X);
            }
        }

        private void Comparison_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSlider)
            {
                _isDraggingSlider = false;
                ComparisonContainer.ReleaseMouseCapture();
            }
        }

        private void UpdateSliderPosition(double x)
        {
            double width = ComparisonContainer.ActualWidth;
            double height = ComparisonContainer.ActualHeight;
            if (width <= 0 || height <= 0) return;

            x = Math.Clamp(x, 0, width);
            _sliderRatio = x / width;

            BeforeImageClip.Rect = new Rect(0, 0, x, height);

            SliderHandle.Height = height;
            Canvas.SetLeft(SliderHandle, x - (SliderHandle.Width / 2));
        }

        // ==================== УПРАВЛЕНИЕ ОКНОМ ====================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}