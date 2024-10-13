using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using WSLDockerHub;
using static WSLDockerHub.WSLDockerHubTools;

namespace WSLDockerHubCLI
{
    internal class Program
    {
        enum CommandReturnCode
        {
            OK,
            Failed,
            FailedAndShowHelp
        }


        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.GetEncoding("UTF-16LE");
            if (args.Length > 0)
            {
                var subArgs = args.Skip(1).ToArray();
                var commandReturnCode = CommandReturnCode.FailedAndShowHelp;
                switch (args[0].ToLower())
                {
                    case "download":
                    case "dl":
                        commandReturnCode = DownloadCommand(subArgs);
                        break;
                    case "install":
                    case "i":
                        commandReturnCode = InstallCommand(subArgs);
                        break;
                    case "export":
                        commandReturnCode = ExportCommand(subArgs);
                        break;
                    case "remove":
                    case "rm":
                        commandReturnCode = UnregisterCommand(subArgs);
                        break;
                    case "list":
                    case "ls":
                        commandReturnCode = SimpleWSLCommand("--list --verbose");
                        break;
                    case "help":
                    case "h":
                        commandReturnCode = MainHelpCommand();
                        break;
                    default:
                        break;
                }
                if (commandReturnCode == CommandReturnCode.FailedAndShowHelp)
                {
                    Console.WriteLine();
                    MainHelpCommand();
                }
            }
            else
            {
                MainHelpCommand();
            }
        }

        static CommandReturnCode MainHelpCommand()
        {
            Console.WriteLine(Resources.MainHelpText);
            return CommandReturnCode.OK;
        }

        static FsLayer[] UniGetFsLayers(ImageInfo imageInfo, FsLayerFilter filter, out string token)
        {
            Console.Write($"Fetching token for {imageInfo.image} ... ");
            token = GetImageToken(imageInfo.image).Result;
            Console.WriteLine("Done");

            Console.Write($"{imageInfo.tagNameOrDigest}: Pulling from library/{imageInfo.image} ... ");
            var fsLayers = GetImageFsLayers(imageInfo.image, imageInfo.tagNameOrDigest, token).Result;
            Console.WriteLine("Done");

            var selectedFsLayers = SelectFsLayers(fsLayers, filter);
            Console.WriteLine($"Filtered out {selectedFsLayers.Length} total of {fsLayers.Length} image(s).");

            return selectedFsLayers;
        }

        static CommandReturnCode DownloadCommand(string[] args)
        {
            string outputDir = "";
            bool downloadAllSelected = false;
            var filter = CreateFsLayerFilterByArgs(args);
            var imageInfo = new ImageInfo();

            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];
                switch (curArg.ToLower())
                {
                    case "--output":
                    case "-o":
                        i++;
                        if (args.Length > i)
                        {
                            outputDir = Path.Combine(args[i]);
                        }
                        break;
                    case "--all":
                    case "-a":
                        downloadAllSelected = true;
                        break;
                    default:
                        if (curArg.StartsWith("-") == false)
                        {
                            try
                            {
                                imageInfo = ParseImageInfo(curArg);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return CommandReturnCode.FailedAndShowHelp;
                            }
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(outputDir))
            {
                Console.WriteLine("Output directory required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            try
            {
                var selectedFsLayers = UniGetFsLayers(imageInfo, filter, out var token);

                if (Directory.Exists(outputDir) == false)
                {
                    Directory.CreateDirectory(outputDir);
                }

                if (selectedFsLayers.Length < 1)
                {
                    Console.WriteLine("Cannot find any images.");
                    return CommandReturnCode.Failed;
                }

                foreach (var fsLayer in selectedFsLayers)
                {
                    Console.Write($"Downloading: {fsLayer.digest} ... ");
                    var blobStream = GetImageBlob(imageInfo.image, fsLayer.digest, token).Result;
                    var fs = File.Create(Path.Combine(outputDir, $"{CreateBlobName(imageInfo, fsLayer, true)}.tar.gz"));
                    blobStream.CopyTo(fs);
                    blobStream.Close();
                    fs.Close();
                    Console.WriteLine("Done");
                    if (downloadAllSelected == false)
                    {
                        Console.WriteLine("If you want to download all matching images, please add the \"--all\" or \"-a\" parameter to download everything.");
                        break;
                    }
                }
                return CommandReturnCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unknown Error: " + ex.Message);
                return CommandReturnCode.Failed;
            }
        }

        static CommandReturnCode InstallCommand(string[] args)
        {
            if (WSLTools.CheckCLI() == false)
            {
                Console.WriteLine("WSL not installed.");
                return CommandReturnCode.Failed;
            }

            string installDir = "";
            string distName = "";
            var filter = CreateFsLayerFilterByArgs(args);
            var imageInfo = new ImageInfo();

            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];
                switch (curArg.ToLower())
                {
                    case "--name":
                    case "-n":
                        i++;
                        if (args.Length > i)
                        {
                            distName = Path.Combine(args[i]);
                        }
                        break;
                    case "--dir":
                    case "-d":
                        i++;
                        if (args.Length > i)
                        {
                            installDir = Path.Combine(args[i]);
                        }
                        break;
                    default:
                        if (curArg.StartsWith("-") == false)
                        {
                            try
                            {
                                imageInfo = ParseImageInfo(curArg);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return CommandReturnCode.FailedAndShowHelp;
                            }
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(installDir))
            {
                Console.WriteLine("Install directory required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            if (string.IsNullOrEmpty(distName))
            {
                Console.WriteLine("Custom linux distribution name required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            try
            {
                var selectedFsLayers = UniGetFsLayers(imageInfo, filter, out var token);

                if (selectedFsLayers.Length < 1)
                {
                    Console.WriteLine("Cannot find any images.");
                    return CommandReturnCode.Failed;
                }

                var fsLayer = selectedFsLayers[0];
                Console.WriteLine($"Start install from Internet: {fsLayer.digest}");
                var blobStream = GetImageBlob(imageInfo.image, fsLayer.digest, token).Result;
                var importDone = WSLTools.Import(distName, installDir, blobStream);
                blobStream.Close();
                if (importDone)
                {
                    return CommandReturnCode.OK;
                }
                else
                {
                    Console.WriteLine("Failed to install.");
                    return CommandReturnCode.Failed;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unknown Error: " + ex.Message);
                return CommandReturnCode.Failed;
            }
        }

        static CommandReturnCode ExportCommand(string[] args)
        {
            if (WSLTools.CheckCLI() == false)
            {
                Console.WriteLine("WSL not installed.");
                return CommandReturnCode.Failed;
            }

            string outputPath = "";
            string distName = "";
            bool compress = false;

            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];
                switch (curArg.ToLower())
                {
                    case "--output":
                    case "-o":
                        i++;
                        if (args.Length > i)
                        {
                            outputPath = Path.Combine(args[i]);
                        }
                        break;
                    case "--compress":
                    case "-c":
                        compress = true;
                        break;
                    default:
                        if (curArg.StartsWith("-") == false)
                        {
                            distName = curArg;
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(distName))
            {
                Console.WriteLine("Distribution name required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine("Output file path required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            bool exportDone;

            var fs = File.Create(outputPath);

            Console.WriteLine($"Start export image: {distName} => {Path.GetFileName(outputPath)}");
            if (compress)
            {
                var compressor = new GZipStream(fs, CompressionMode.Compress);
                exportDone = WSLTools.Export(distName, compressor);
                compressor.Close();
            }
            else
            {
                exportDone = WSLTools.Export(distName, fs);
            }

            fs.Close();

            if (exportDone)
            {
                return CommandReturnCode.OK;
            }
            else
            {
                File.Delete(outputPath);
                Console.WriteLine("Failed to export image.");
                return CommandReturnCode.Failed;
            }
        }

        static CommandReturnCode UnregisterCommand(string[] args)
        {
            if (WSLTools.CheckCLI() == false)
            {
                Console.WriteLine("WSL not installed.");
                return CommandReturnCode.Failed;
            }

            string distName = "";

            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];
                switch (curArg.ToLower())
                {
                    default:
                        if (curArg.StartsWith("-") == false)
                        {
                            distName = curArg;
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(distName))
            {
                Console.WriteLine("Distribution name required.");
                return CommandReturnCode.FailedAndShowHelp;
            }

            if (WSLTools.Command($"--unregister {distName}"))
            {
                return CommandReturnCode.OK;
            }
            else
            {
                return CommandReturnCode.Failed;
            }
        }

        static CommandReturnCode SimpleWSLCommand(string cmd)
        {
            if (WSLTools.CheckCLI() == false)
            {
                Console.WriteLine("WSL not installed.");
                return CommandReturnCode.Failed;
            }

            if (WSLTools.Command(cmd))
            {
                return CommandReturnCode.OK;
            }
            else
            {
                return CommandReturnCode.Failed;
            }
        }

        static FsLayerFilter CreateFsLayerFilterByArgs(string[] args)
        {
            var fsLayerFilter = new FsLayerFilter();
            for (int i = 0; i < args.Length; i++)
            {
                var curArg = args[i];
                switch (curArg.ToLower())
                {
                    case "--os":
                        i++;
                        fsLayerFilter.os = args[i];
                        break;
                    case "--arch":
                        i++;
                        fsLayerFilter.architecture = args[i];
                        break;
                    case "--variant":
                        i++;
                        fsLayerFilter.variant = args[i];
                        break;
                    default:
                        break;
                }
            }
            return fsLayerFilter;
        }
    }
}