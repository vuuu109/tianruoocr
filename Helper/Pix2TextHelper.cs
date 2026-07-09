using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TrOCR.Helper
{
    public sealed class Pix2TextHelper
    {
        private static readonly object _lock = new object();
        private static string _pythonPath = null;

        private static string GetPythonPath()
        {
            if (_pythonPath != null) return _pythonPath;
            lock (_lock)
            {
                string[] candidates = {
                    @"C:\Users\HP\AppData\Local\Programs\Python\Python312\python.exe",
                };
                foreach (var c in candidates)
                {
                    if (File.Exists(c)) { _pythonPath = c; break; }
                }
            }
            return _pythonPath;
        }

        public static string RecognizeText(Image image)
        {
            try
            {
                string python = GetPythonPath();
                if (string.IsNullOrEmpty(python))
                    return "***鏈壘鍒?Python***";

                string tempImg = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
                try
                {
                    image.Save(tempImg, System.Drawing.Imaging.ImageFormat.Png);
                    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pix2Text_data", "run_p2t.py");
                    if (!File.Exists(script)) return "***Pix2Text 鑴氭湰涓嶅瓨鍦?**";

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = python,
                        Arguments = $"\"{script}\" \"{tempImg}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };
                    psi.EnvironmentVariables["P2T_HOME"] = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pix2Text_data");
                    psi.EnvironmentVariables["HF_HUB_DISABLE_SSL_VERIFICATION"] = "1";

                    using (Process proc = Process.Start(psi))
                    {
                        string stdout = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(60000);
                        if (!string.IsNullOrWhiteSpace(stdout))
                        {
                            string[] lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            StringBuilder result = new StringBuilder();
                            bool inResult = false;
                            foreach (var line in lines)
                            {
                                if (line.Contains("=== 璇嗗埆缁撴灉 ===")) { inResult = true; continue; }
                                if (line.Contains("=== 瀹屾垚 ===")) { break; }
                                if (inResult) result.AppendLine(line.Trim());
                            }
                            string text = result.ToString().Trim();
                            if (!string.IsNullOrEmpty(text)) return text;
                        }
                    }
                    return "***璇ュ尯鍩熸湭鍙戠幇鏂囨湰***";
                }
                finally { if (File.Exists(tempImg)) File.Delete(tempImg); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Pix2TextHelper] 閿欒: {ex}");
                return $"***Pix2Text璇嗗埆澶辫触: {ex.Message}***";
            }
        }
    }
}