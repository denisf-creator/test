using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace roshade2
{
    // Базовый класс для реализации INotifyPropertyChanged (основа MVVM)
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Универсальный класс для привязки команд к кнопкам в XAML
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    // Модель для пресета (используется на 3-м шаге)
    public class PresetItem : ObservableObject
    {
        private bool _isSelected;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    // Главная ViewModel нашего окна установки
    public class MainViewModel : ObservableObject
    {
        private int _currentStep = 1;
        private string _selectedKeybind = "F8";
        private double _installProgress = 0;
        private string _installStatusText = "Ready to install";
        private bool _isInstalling = false;
        private bool _isInstallCompleted = false;

        // Текущий этап установки (от 1 до 4)
        public int CurrentStep
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(); }
        }

        // Выбранный бинд клавиши (Шаг 2: "F8" или "End")
        public string SelectedKeybind
        {
            get => _selectedKeybind;
            set
            {
                _selectedKeybind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsF8Selected));
                OnPropertyChanged(nameof(IsEndSelected));
            }
        }

        // Удобные булевы свойства для привязки кнопок F8 и End без конвертеров
        public bool IsF8Selected
        {
            get => SelectedKeybind == "F8";
            set { if (value) SelectedKeybind = "F8"; }
        }

        public bool IsEndSelected
        {
            get => SelectedKeybind == "End";
            set { if (value) SelectedKeybind = "End"; }
        }

        // Коллекции пресетов для Шага 3
        public ObservableCollection<PresetItem> GlossyPresets { get; } = new();
        public ObservableCollection<PresetItem> RoShadePresets { get; } = new();

        // Свойства для Шага 4 (Загрузка)
        public double InstallProgress
        {
            get => _installProgress;
            set { _installProgress = value; OnPropertyChanged(); }
        }

        public string InstallStatusText
        {
            get => _installStatusText;
            set { _installStatusText = value; OnPropertyChanged(); }
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set { _isInstalling = value; OnPropertyChanged(); }
        }

        public bool IsInstallCompleted
        {
            get => _isInstallCompleted;
            set { _isInstallCompleted = value; OnPropertyChanged(); }
        }

        // Команды навигации и действий
        public ICommand GoToStepCommand { get; }
        public ICommand StartInstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand FinishCommand { get; }

        public MainViewModel()
        {
            // 1 строка: Glossy
            GlossyPresets.Add(new PresetItem { Name = "Glossy", Category = "Glossy", IsSelected = true });
            GlossyPresets.Add(new PresetItem { Name = "Very Glossy", Category = "Glossy", IsSelected = false });

            // 2 строка: RoShade
            RoShadePresets.Add(new PresetItem { Name = "RoShade Low", Category = "RoShade", IsSelected = false });
            RoShadePresets.Add(new PresetItem { Name = "RoShade Medium", Category = "RoShade", IsSelected = true });
            RoShadePresets.Add(new PresetItem { Name = "RoShade High", Category = "RoShade", IsSelected = true });

            GoToStepCommand = new RelayCommand(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int step))
                {
                    CurrentStep = step;
                }
            });

            StartInstallCommand = new RelayCommand(async _ => await RunInstallationAsync(), _ => !IsInstalling);

            UninstallCommand = new RelayCommand(_ =>
                MessageBox.Show("RoShade has been uninstalled successfully.", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Information));

            FinishCommand = new RelayCommand(_ => Application.Current.Shutdown());
        }

        // Асинхронная имитация установки на 4-м шаге
        private async Task RunInstallationAsync()
        {
            IsInstalling = true;
            IsInstallCompleted = false;
            InstallProgress = 0;

            string[] stages = {
                "Locating Roblox directory...",
                "Downloading ReShade binaries...",
                $"Configuring keybind ({SelectedKeybind})...",
                "Applying selected presets...",
                "Finalizing installation..."
            };

            for (int i = 0; i <= 100; i += 2)
            {
                InstallProgress = i;
                int stageIndex = Math.Min(i / 20, stages.Length - 1);
                InstallStatusText = $"{stages[stageIndex]} ({i}%)";
                await Task.Delay(50);
            }

            InstallStatusText = "Installation Complete!";
            IsInstalling = false;
            IsInstallCompleted = true;
        }
    }
}