using System;
using System.IO;
using System.Threading;

namespace VsTeXCommentsExtension
{
    public class ExtensionSettings
    {
        private const int FileFormatVersion = 1;

        public static ExtensionSettings Instance { get; } = new ExtensionSettings();

        private readonly Mutex mutex = new Mutex(false, $"{nameof(VsTeXCommentsExtension)}.{nameof(ExtensionSettings)}.Mutex");

        private readonly string settingsDirectory;
        private readonly string settingsPath;

        private double customZoomScale = 1;
        public double CustomZoomScale
        {
            get { return customZoomScale; }
            set
            {
                if (customZoomScale != value)
                {
                    customZoomScale = value;
                    Save();
                    CustomZoomChanged?.Invoke(value);
                }
            }
        }

        public event ZoomChangedHandler CustomZoomChanged;

        private ExtensionSettings()
        {
            settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                nameof(VsTeXCommentsExtension));

            settingsPath = Path.Combine(settingsDirectory, nameof(ExtensionSettings));

            Load();
        }

        private void Save()
        {
            try
            {
                mutex.WaitOne();

                if (!Directory.Exists(settingsDirectory)) Directory.CreateDirectory(settingsDirectory);

                try
                {
                    using (var fs = File.Open(settingsPath, FileMode.Create))
                    using (var writer = new BinaryWriter(fs))
                    {
                        writer.Write(FileFormatVersion);
                        writer.Write(customZoomScale);
                    }
                }
                catch
                {
                    return;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private void Load()
        {
            try
            {
                mutex.WaitOne();

                if (!File.Exists(settingsPath)) return;

                try
                {
                    using (var fs = File.Open(settingsPath, FileMode.Open))
                    using (var reader = new BinaryReader(fs))
                    {
                        if (reader.ReadInt32() != FileFormatVersion) return;

                        customZoomScale = reader.ReadDouble();
                    }
                }
                catch
                {
                    return;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public delegate void ZoomChangedHandler(double zoomScale);
    }
}
