using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace TrOCR.Helper
{
    /// <summary>
    /// Pix2Text 离线识别帮助类（调用本地 Python pix2text）
    /// 支持文字识别 + 数学公式识别（LaTeX输出）
    /// </summary>
    public sealed class Pix2TextHelper
    {
        private static readonly object _lock = new object();
        private static bool _pythonChecked = false;
        private static string _pythonPath = null;

        private static string GetPythonPath()
        {
            if (_pythonChecked) return _pythonPath;
            lock (_lock)
            {
                if (_pythonChecked) return _pythonPath;
                _pythonChecked = true;

                // 可能的 Python 路径
                string[] candidates = {
                    @"C:\Users\HP\AppData\Local\Programs\Python\Python312\python.exe",
                    @"C:\Users\HP\AppData\Local\Microsoft\WindowsApps\python.exe",
                };
                foreach (var c in candidates)
                {
                    if (File.Exists(c))
                    {
                        _pythonPath = c;
                        break;
                    }
                }
            }
            return _pythonPath;
        }

        /// <summary>
        /// 通用文本识别（自动检测文本和公式，公式会输出 LaTeX）
        /// </summary>
        public static string RecognizeText(Image image)
        {
            return RunPix2Text(image, "auto");
        }

        /// <summary>
        /// 公式专用识别（强制公式模式，输出 LaTeX 格式）
        /// </summary>
        public static string RecognizeFormula(Image image)
        {
            return RunPix2Text(image, "formula");
        }

        private static string RunPix2Text(Image image, string mode)
        {
            try
            {
                string python = GetPythonPath();
                if (string.IsNullOrEmpty(python))
                    return "***未找到 Python，请安装 Python 3.12***";

                // 保存图片到临时文件
                string tempImg = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                try
                {
                    image.Save(tempImg, System.Drawing.Imaging.ImageFormat.Png);

                    // 调用 Python 脚本
                    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pix2Text_data", "run_p2t.py");

                    if (!File.Exists(script))
                        return "***Pix2Text 脚本不存在***";

                    string args = $"\"{script}\" \"{tempImg}\"";
                    if (mode == "formula")
                        args += " --mode formula";

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = python,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };

                    // 设置环境变量
                    psi.EnvironmentVariables["P2T_HOME"] = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pix2Text_data");
                    psi.EnvironmentVariables["HF_HUB_DISABLE_SSL_VERIFICATION"] = "1";

                    using (Process proc = Process.Start(psi))
                    {
                        string stdout = proc.StandardOutput.ReadToEnd();
                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(60000);

                        // 直接使用 stdout 作为结果（Python 脚本直接输出识别文本）
                        if (!string.IsNullOrWhiteSpace(stdout))
                        {
                            // 过滤 tqdm 进度条和 HuggingFace 警告
                            StringBuilder sb = new StringBuilder();
                            foreach (string line in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string trimmed = line.Trim();
                                if (trimmed.Contains("%|") && trimmed.Contains("|%")) continue;
                                if (trimmed.StartsWith("Using a slow")) continue;
                                if (trimmed.StartsWith("0:")) continue;  // YOLO detection output
                                if (trimmed.StartsWith("Speed:")) continue;
                                if (trimmed.StartsWith("WARNING")) continue;
                                if (trimmed.StartsWith("Loading")) continue;
                                if (trimmed.StartsWith("0it")) continue;
                                sb.AppendLine(trimmed);
                            }
                            string text = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }

                        if (!string.IsNullOrWhiteSpace(stderr))
                            return $"***Pix2Text 识别失败: {stderr.Substring(0, Math.Min(200, stderr.Length))}***";
                    }
                    return "***该区域未发现文本***";
                }
                finally
                {
                    if (File.Exists(tempImg)) File.Delete(tempImg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Pix2TextHelper] 错误: {ex}");
                return $"***Pix2Text识别失败: {ex.Message}***";
            }
        }
    }
}