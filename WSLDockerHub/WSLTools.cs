using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WSLDockerHub
{
    public class WSLTools
    {
        public static bool CheckCLI()
        {
            var process = new Process();
            process.StartInfo.FileName = "wsl";
            process.StartInfo.Arguments = "--version";
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();

            //Console.WriteLine(output);

            process.WaitForExit();

            //Console.WriteLine("Code: " + process.ExitCode);
            return process.ExitCode == 0;
        }

        public static bool Import(string distName, string installDir, Stream inputStream)
        {
            if (Directory.Exists(installDir) == false)
            {
                Directory.CreateDirectory(installDir);
            }
            var process = new Process();
            process.StartInfo.FileName = "wsl";
            process.StartInfo.WorkingDirectory = installDir;
            process.StartInfo.Arguments = $"--import \"{distName}\" . -";
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            var stdoutStream = Console.OpenStandardOutput();
            var stderrStream = Console.OpenStandardError();
            
            process.StandardOutput.BaseStream.CopyToAsync(stdoutStream);
            process.StandardError.BaseStream.CopyToAsync(stderrStream);
            inputStream.CopyTo(process.StandardInput.BaseStream);
            process.StandardInput.Close();

            process.WaitForExit();

            stdoutStream.Close();
            stderrStream.Close();

            //Console.WriteLine("Code: " + process.ExitCode);
            return process.ExitCode == 0;
        }

        public static bool Export(string distName, Stream outputStream)
        {
            var process = new Process();
            process.StartInfo.FileName = "wsl";
            process.StartInfo.Arguments = $"--export \"{distName}\" -";
            process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            
            var stderrStream = Console.OpenStandardError();

            process.StandardOutput.BaseStream.CopyToAsync(outputStream);
            process.StandardError.BaseStream.CopyToAsync(stderrStream);

            process.WaitForExit();
            
            stderrStream.Close();

            //Console.WriteLine("Code: " + process.ExitCode);
            return process.ExitCode == 0;
        }

        //public static bool Unregister(string distName)
        //{
        //    var process = new Process();
        //    process.StartInfo.FileName = "wsl";
        //    process.StartInfo.Arguments = $"--unregister \"{distName}\"";
        //    process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("UTF-16LE");
        //    process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("UTF-16LE");
        //    process.StartInfo.CreateNoWindow = true;
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.StartInfo.RedirectStandardError = true;
        //    process.Start();

        //    var stdoutStream = Console.OpenStandardOutput();
        //    var stderrStream = Console.OpenStandardError();

        //    process.StandardOutput.BaseStream.CopyToAsync(stdoutStream);
        //    process.StandardError.BaseStream.CopyToAsync(stderrStream);

        //    process.WaitForExit();

        //    stdoutStream.Close();
        //    stderrStream.Close();

        //    //Console.WriteLine("Code: " + process.ExitCode);
        //    return process.ExitCode == 0;
        //}

        public static bool Command(string args)
        {
            var process = new Process();
            process.StartInfo.FileName = "wsl";
            process.StartInfo.Arguments = args;
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("UTF-16LE");
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            var stdoutStream = Console.OpenStandardOutput();
            var stderrStream = Console.OpenStandardError();

            process.StandardOutput.BaseStream.CopyToAsync(stdoutStream);
            process.StandardError.BaseStream.CopyToAsync(stderrStream);

            process.WaitForExit();

            stdoutStream.Close();
            stderrStream.Close();

            //Console.WriteLine("Code: " + process.ExitCode);
            return process.ExitCode == 0;
        }
    }
}
