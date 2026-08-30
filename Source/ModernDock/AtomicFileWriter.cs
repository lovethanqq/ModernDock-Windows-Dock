using System;
using System.IO;
using System.Text;

namespace MyCustomDock
{
    internal static class AtomicFileWriter
    {
        public static void WriteAllText(string path, string content, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A destination path is required.", "path");
            if (encoding == null) throw new ArgumentNullException("encoding");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory)) throw new IOException("The destination directory is not available.");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(content ?? string.Empty);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                // Only the uniquely named temporary file is ever removed here.
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
