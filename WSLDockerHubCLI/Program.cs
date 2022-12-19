using System.Diagnostics;
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
                switch (args[0].ToLower())
                {
                    case "download":
                    case "d":
                        DownloadCommand(subArgs);
                        break;
                    case "install":
                    case "i":
                        InstallCommand(subArgs);
                        break;
                    case "help":
                    case "h":
                        MainHelpCommand();
                        break;
                    default:
                        break;
                }
            }
            else
            {
                MainHelpCommand();
            }
        }

        static void MainHelpCommand()
        {
            Console.WriteLine(Resources.MainHelpText);
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