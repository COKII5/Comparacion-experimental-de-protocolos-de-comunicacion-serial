using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class MeasurementStorage
    {
        private const string FolderName = "Measurements";
        private const string TimestampFormat = "yyyyMMdd_HHmmss";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public string Warning { get; private set; } = "";

        public static string TimestampedName(string prefix, string protocolTag, string extension)
        {
            return $"{prefix}_{protocolTag}_{DateTime.Now.ToString(TimestampFormat, Invariant)}.{extension}";
        }

#if UNITY_EDITOR
        public string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", FolderName));
#else
        public string OutputFolder => Path.Combine(Application.persistentDataPath, FolderName);
#endif

        public bool Exists(string fileName)
        {
            return File.Exists(Path.Combine(OutputFolder, fileName));
        }

        public bool TryCreateOutputFolder()
        {
            try
            {
                Directory.CreateDirectory(OutputFolder);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Warning = $"Could not create {OutputFolder}: {exception.Message}";
                return false;
            }
        }

        public bool TryWrite(string fileName, bool append, Action<TextWriter> writeContent)
        {
            if (!TryCreateOutputFolder())
            {
                return false;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(OutputFolder, fileName), append, Utf8NoBom))
                {
                    writeContent(writer);
                }
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Warning = $"Could not save {fileName} (is it open in another program?): {exception.Message}";
                return false;
            }
        }

        public void ClearWarning()
        {
            Warning = "";
        }

        public string AppendWarning(string text)
        {
            return string.IsNullOrEmpty(Warning) ? text : text + "\n" + Warning;
        }
    }
}
