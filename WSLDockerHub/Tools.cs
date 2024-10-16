﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using JsonEasyNavigation;

namespace WSLDockerHub
{
    public class WSLDockerHubTools
    {
        public enum ImageInfoType
        {
            Tag,
            Digest
        }

        public struct ImageInfo
        {
            public string image;
            public string tagNameOrDigest;
            public string originalString;
            public ImageInfoType type;
        }


        public struct FsLayer
        {
            public string manifestDigest;
            public string digest;
            public string os;
            public string architecture;
            public string variant;
            public string size;
        }

        public struct FsLayerFilter
        {
            public string os;
            public string architecture;
            public string variant;
        }

        public static ImageInfo ParseImageInfo(string str)
        {
            string pattern = @"^(?<ImageName>.+?)(?<Type>[\@|\:])(?<TagNameOrDigest>.+)$";
            Regex rgx = new Regex(pattern);
            Match match = rgx.Match(str);
            if (match.Success)
            {
                return new ImageInfo
                {
                    image = match.Groups["ImageName"].Value,
                    tagNameOrDigest = match.Groups["TagNameOrDigest"].Value,
                    originalString = str,
                    type = match.Groups["Type"].Value == "@" ? ImageInfoType.Digest : ImageInfoType.Tag
                };
            }
            else
            {
                throw new Exception("Incorrect image info format.");
            }
        }

        public static string CreateBlobName(ImageInfo imageInfo, FsLayer fsLayer, bool appendDigest)
        {
            var sb = new StringBuilder();
            sb.Append(imageInfo.image);
            if (!string.IsNullOrEmpty(fsLayer.os))
            {
                sb.Append("-" + fsLayer.os);
            }
            if (!string.IsNullOrEmpty(fsLayer.architecture))
            {
                sb.Append("-" + fsLayer.architecture);
            }
            if (!string.IsNullOrEmpty(fsLayer.variant))
            {
                sb.Append("-" + fsLayer.variant);
            }
            if (appendDigest)
            {
                sb.Append("-" + Regex.Replace(fsLayer.digest, ".+?\\:", "").Substring(0, 16));
            }
            return sb.ToString();
        }

        public static async Task<string> GetImageToken(string imageName)
        {
            string url = $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/{imageName}:pull";

            HttpClientHandler clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            var client = new HttpClient(clientHandler);
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            var jDoc = JsonDocument.Parse(responseBody);
            var jNav = jDoc.ToNavigation();
            var token = jNav["token"].GetStringOrDefault();

            return token;
        }

        public static async Task<string> GetManifest(string imageName, string tagNameOrManifestDigest, string token)
        {
            string url = $"https://registry-1.docker.io/v2/library/{imageName}/manifests/{tagNameOrManifestDigest}";

            HttpClientHandler clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            var client = new HttpClient(clientHandler);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.oci.image.index.v1+json");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.oci.image.manifest.v1+json");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.docker.distribution.manifest.list.v2+json");
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            //Console.WriteLine(responseBody);

            return responseBody;
        }

        public static async Task<FsLayer[]> GetImageFsLayers(string imageName, string tagNameOrManifestDigest, string token, FsLayerFilter? filter = null)
        {
            string manifestRes = await GetManifest(imageName, tagNameOrManifestDigest, token);
            var manifestJDoc = JsonDocument.Parse(manifestRes);
            var manifestNav = manifestJDoc.ToNavigation();
            var fsLayers = new List<FsLayer>();
            var mediaType = manifestNav["mediaType"].GetStringOrDefault().ToLower();
            switch (mediaType)
            {
                case "application/vnd.oci.image.manifest.v1+json":
                case "application/vnd.docker.distribution.manifest.v2+json":
                    {
                        var layers = manifestNav["layers"].Values;
                        foreach (var layer in layers)
                        {
                            try
                            {
                                var layerMediaType = layer["mediaType"].GetStringOrDefault();
                                if (string.Equals(layerMediaType, "application/vnd.docker.image.rootfs.diff.tar.gzip", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(layerMediaType, "application/vnd.oci.image.layer.v1.tar+gzip", StringComparison.OrdinalIgnoreCase))
                                {
                                    var layerDigest = layer["digest"].GetStringOrDefault();
                                    var layerSize = layer["size"].GetStringOrDefault();

                                    fsLayers.Add(new FsLayer
                                    {
                                        manifestDigest = tagNameOrManifestDigest,
                                        digest = layerDigest,
                                        size = layerSize
                                    });
                                }

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Read layers manifest failed: " + e.Message);
                            }
                        }
                    }
                    break;
                case "application/vnd.oci.image.index.v1+json":
                case "application/vnd.docker.distribution.manifest.list.v2+json":
                    {
                        var manifests = manifestNav["manifests"].Values;
                        var emptyFsLayers = new List<FsLayer>();
                        foreach (var manifest in manifests)
                        {
                            try
                            {
                                var digest = manifest["digest"].GetStringOrDefault();
                                // 可能不具备这些属性
                                var os = manifest["platform"]["os"].GetStringOrDefault();
                                var architecture = manifest["platform"]["architecture"].GetStringOrDefault();
                                var variant = manifest["platform"]["variant"].GetStringOrDefault();
                                emptyFsLayers.Add(new FsLayer
                                {
                                    manifestDigest = digest,
                                    os = os,
                                    architecture = architecture,
                                    variant = variant
                                });
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Read tag manifest failed: " + e.Message);
                            }
                        }
                        if (filter.HasValue)
                        {
                            var selected = SelectFsLayers(emptyFsLayers.ToArray(), filter.Value);
                            emptyFsLayers = new List<FsLayer>(selected);
                        }
                        foreach (var emptyFsLayer in emptyFsLayers)
                        {
                            var fsLayersByManifest = await GetImageFsLayers(imageName, emptyFsLayer.manifestDigest, token);
                            foreach (var fsLayer in fsLayersByManifest)
                            {
                                fsLayers.Add(new FsLayer
                                {
                                    // from emptyFsLayer
                                    os = emptyFsLayer.os,
                                    architecture = emptyFsLayer.architecture,
                                    variant = emptyFsLayer.variant,
                                    // from fsLayer
                                    manifestDigest = fsLayer.manifestDigest,
                                    digest = fsLayer.digest,
                                    size = fsLayer.size
                                });
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
            return fsLayers.ToArray();
        }

        public static async Task<Stream> GetImageBlob(string imageName, string digest, string token)
        {
            string url = $"https://registry-1.docker.io/v2/library/{imageName}/blobs/{digest}";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            var stream = await client.GetStreamAsync(url);

            return stream;
        }


        public static FsLayer[] SelectFsLayers(FsLayer[] fsLayers, FsLayerFilter? filter = null)
        {
            if (filter.HasValue)
            {
                var selectedFsLayers = fsLayers.Where((fsLayer) =>
                {
                    if (string.IsNullOrEmpty(filter.Value.os) == false && string.Equals(fsLayer.os, filter.Value.os) == false)
                    {
                        return false;
                    }
                    if (string.IsNullOrEmpty(filter.Value.architecture) == false && string.Equals(fsLayer.architecture, filter.Value.architecture) == false)
                    {
                        return false;
                    }
                    if (string.IsNullOrEmpty(filter.Value.variant) == false && string.Equals(fsLayer.architecture, filter.Value.variant) == false)
                    {
                        return false;
                    }
                    return true;
                }).ToArray();
                return selectedFsLayers;
            }
            else
            {
                return fsLayers;
            }
        }

        public static async Task<FsLayer> GetFsLayer(string imageName, string tagNameOrManifestDigest, FsLayerFilter? filter = null)
        {
            var token = await GetImageToken(imageName);
            var fsLayers = await GetImageFsLayers(imageName, tagNameOrManifestDigest, token);
            var selectedFsLayers = SelectFsLayers(fsLayers, filter);
            if (selectedFsLayers.Length > 0)
            {
                return selectedFsLayers[0];
            }
            else
            {
                throw new Exception("Images not found.");
            }
        }

        public static async Task<Stream> GetFsLayerBlobStream(string imageName, string tagNameOrManifestDigest, FsLayerFilter? filter = null)
        {
            var token = await GetImageToken(imageName);
            var fsLayer = await GetFsLayer(imageName, tagNameOrManifestDigest, filter);
            return await GetFsLayerBlobStream(imageName, fsLayer.digest, token);
        }

        public static async Task<Stream> GetFsLayerBlobStream(string imageName, string fsLayerDigest, string token = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                token = await GetImageToken(imageName);
            }
            var blobStream = GetImageBlob(imageName, fsLayerDigest, token).Result;
            return blobStream;
        }
    }
}
