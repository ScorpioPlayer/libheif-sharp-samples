/*
 * This file is part of heif-info, an example application for libheif-sharp
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 */

using LibHeifSharp;
using Mono.Options;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace System
{
    internal static class sbext
    {
        public static void AppendFormatLine(this StringBuilder sb, string format, params object[] arg0)
        {
            sb.AppendFormat(format, arg0);
            sb.AppendLine();
        }
    }
    public class HeifInfo
    {
        public static string Execute(string file)
        {
            try
            {
                using (HeifContext context = new HeifContext(file))
                {
                    var topLevelImageIds = context.GetTopLevelImageIds();
                    StringBuilder sb = new StringBuilder();
                    foreach (var imageId in topLevelImageIds)
                    {
                        using (var imageHandle = context.GetImageHandle(imageId))
                        {
                            sb.AppendFormatLine("image: {0}x{1} {2}-bit (id={3}){4}",
                                              imageHandle.Width,
                                              imageHandle.Height,
                                              imageHandle.BitDepth,
                                              imageId,
                                              imageHandle.IsPrimaryImage ? " primary" : string.Empty);
                            WriteThumbnailImageInfo(imageHandle, sb);

                            sb.AppendFormatLine("  color profile: {0}", GetColorProfileDescription(imageHandle));
                            sb.AppendFormatLine("  alpha channel: {0}", GetAlphaChannelDescription(imageHandle));

                            WriteDepthImageInfo(imageHandle, sb);
                            WriteMetadataInfo(imageHandle, sb);

                            if (LibHeifInfo.HaveVersion(1, 16, 0))
                            {
                                WriteTransformationInfo(context, imageHandle, sb);
                                WriteRegionInfo(context, imageHandle, sb);
                                WritePropertyInfo(context, imageHandle, sb);
                            }

                            if (LibHeifInfo.HaveVersion(1, 15, 0))
                            {
                                using (var image = imageHandle.Decode(HeifColorspace.Undefined, HeifChroma.Undefined))
                                {
                                    WritePixelAspectRatio(image.PixelAspectRatio, sb);
                                    WriteContentLightLevelInfo(image.ContentLightLevel, sb);
                                    WriteMasteringDisplayColorVolumeInfo(image.MasteringDisplayColourVolume, sb);
                                }
                            }
                        }
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return null;
        }

        static void PrintVersionInfo()
        {
            Console.WriteLine("heif-info v{0} LibHeifSharp v{1} libheif v{2}",
                              GetAssemblyFileVersion(typeof(Program)),
                              GetAssemblyFileVersion(typeof(LibHeifInfo)),
                              LibHeifInfo.Version.ToString(3));

            static string GetAssemblyFileVersion(Type type)
            {
                var fileVersionAttribute = type.Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();

#pragma warning disable IDE0270 // Use coalesce expression
                if (fileVersionAttribute is null)
                {
                    throw new InvalidOperationException($"Failed to get the AssemblyFileVersion for {type.Assembly.FullName}.");
                }
#pragma warning restore IDE0270 // Use coalesce expression

                var trimmedVersion = new Version(fileVersionAttribute.Version);

                return trimmedVersion.ToString(3);
            }
        }

        static string GetAlphaChannelDescription(HeifImageHandle handle)
        {
            string description = "no";

            if (handle.HasAlphaChannel)
            {
                description = handle.IsPremultipliedAlpha ? "yes (premultiplied)" : "yes";
            }

            return description;
        }

        static string GetColorProfileDescription(HeifImageHandle handle)
        {
            string description = "none";

            HeifIccColorProfile icc = handle.IccColorProfile;
            HeifNclxColorProfile nclx = handle.NclxColorProfile;

            if (icc != null)
            {
                description = nclx != null ? "icc, nclx" : "icc";
            }
            else if (nclx != null)
            {
                description = "nclx";
            }

            return description;
        }

        static void WriteContentLightLevelInfo(HeifContentLightLevel contentLightLevel, StringBuilder sb)
        {
            if (contentLightLevel != null)
            {
                sb.AppendFormatLine("  content light level (clli):");
                sb.AppendFormatLine("    max content light level: {0}", contentLightLevel.MaxContentLightLevel);
                sb.AppendFormatLine("    max picture average light level: {0}", contentLightLevel.MaxPictureAverageLightLevel);
            }
        }

        static void WriteDepthImageInfo(HeifImageHandle handle, StringBuilder sb)
        {
            if (handle.HasDepthImage)
            {
                sb.AppendFormatLine("  depth image: yes");

                var depthIds = handle.GetDepthImageIds();

                foreach (var depthId in depthIds)
                {
                    using (HeifImageHandle depthHandle = handle.GetDepthImage(depthId))
                    {
                        sb.AppendFormatLine("    depth: {0}x{1}", handle.Width, handle.Height);
                    }

                    var depthRepresentationInfo = handle.GetDepthRepresentationInfo(depthId);

                    if (depthRepresentationInfo != null)
                    {
                        sb.AppendFormatLine("    z-near: {0}", GetNullableValue(depthRepresentationInfo.ZNear));
                        sb.AppendFormatLine("    z-far: {0}", GetNullableValue(depthRepresentationInfo.ZFar));
                        sb.AppendFormatLine("    d-min: {0}", GetNullableValue(depthRepresentationInfo.DMin));
                        sb.AppendFormatLine("    d-max: {0}", GetNullableValue(depthRepresentationInfo.DMax));
                        sb.AppendFormatLine("    representation: {0}", GetRepresentationString(depthRepresentationInfo.DepthRepresentationType));

                        if (depthRepresentationInfo.DMin.HasValue || depthRepresentationInfo.DMax.HasValue)
                        {
                            sb.AppendFormatLine("    disparity reference view: {0}", depthRepresentationInfo.DisparityReferenceView);
                        }

                        static string GetRepresentationString(HeifDepthRepresentationType type)
                        {
                            switch (type)
                            {
                                case HeifDepthRepresentationType.UniformInverseZ:
                                    return "inverse Z";
                                case HeifDepthRepresentationType.UniformDisparity:
                                    return "uniform disparity";
                                case HeifDepthRepresentationType.UniformZ:
                                    return "uniform Z";
                                case HeifDepthRepresentationType.NonuniformDisparity:
                                    return "non-uniform disparity";
                                default:
                                    return "unknown";
                            }
                        }

                        static string GetNullableValue(double? nullable)
                        {
                            return nullable.HasValue ? nullable.Value.ToString() : "undefined";
                        }
                    }
                }
            }
            else
            {
                sb.AppendFormatLine("  depth image: no");
            }
        }

        static void WriteMasteringDisplayColorVolumeInfo(HeifMasteringDisplayColourVolume data, StringBuilder sb)
        {
            if (data != null)
            {
                var decoded = data.Decode();

                sb.AppendFormatLine("  mastering display color volume:");
                sb.AppendFormatLine("    display primaries (x,y): ({0};{1}), ({2};{3}), ({4};{5})",
                                  decoded.DisplayPrimariesX[0],
                                  decoded.DisplayPrimariesY[0],
                                  decoded.DisplayPrimariesX[1],
                                  decoded.DisplayPrimariesY[1],
                                  decoded.DisplayPrimariesX[2],
                                  decoded.DisplayPrimariesY[2]);
                sb.AppendFormatLine("    white point (x,y): ({0};{1})", decoded.WhitePointX, decoded.WhitePointY);
                sb.AppendFormatLine("    max display mastering luminance: {0}", decoded.MaxDisplayMasteringLuminance);
                sb.AppendFormatLine("    min display mastering luminance: {0}", decoded.MinDisplayMasteringLuminance);
            }
        }

        static void WriteMetadataInfo(HeifImageHandle handle, StringBuilder sb)
        {
            var metadataBlockIds = handle.GetMetadataBlockIds();

            if (metadataBlockIds.Count > 0)
            {
                sb.AppendFormatLine("  metadata:");

                foreach (var metadataBlockId in metadataBlockIds)
                {
                    var metadataInfo = handle.GetMetadataBlockInfo(metadataBlockId);

                    string id = GetMetadataTypeString(metadataInfo);

                    sb.AppendFormatLine("    {0}: {1} bytes", id, metadataInfo.Size);
                }
            }
            else
            {
                sb.AppendFormatLine("  metadata: none");
            }

            static string GetMetadataTypeString(HeifMetadataBlockInfo metadataInfo)
            {
                string itemType = metadataInfo.ItemType;
                string contentType = metadataInfo.ContentType;

                if (itemType == "Exif")
                {
                    return itemType;
                }
                else if (itemType == "mime" && contentType == "application/rdf+xml")
                {
                    return "XMP";
                }
                else
                {
                    return itemType + "/" + contentType;
                }
            }
        }

        static void WritePixelAspectRatio(in HeifPixelAspectRatio pixelAspectRatio, StringBuilder sb)
        {
            if (!pixelAspectRatio.HasSquareAspectRatio)
            {
                sb.AppendFormatLine("  pixel aspect ratio: {0}", pixelAspectRatio.ToString());
            }
        }

        static void WritePropertyInfo(HeifContext context, HeifImageHandle imageHandle, StringBuilder sb)
        {
            var userDescriptions = context.GetUserDescriptionProperties(imageHandle);

            sb.AppendFormatLine("  properties:");

            foreach (var item in userDescriptions)
            {
                sb.AppendFormatLine("    user description:");
                sb.AppendFormatLine("      language: {0}", item.Language);
                sb.AppendFormatLine("      name: {0}", item.Name);
                sb.AppendFormatLine("      description: {0}", item.Description);
                sb.AppendFormatLine("      tags: {0}", item.Tags);
            }
        }

        static void WriteRegionInfo(HeifContext context, HeifImageHandle imageHandle, StringBuilder sb)
        {
            sb.AppendFormatLine("  region annotations:");

            var ids = imageHandle.GetRegionItemIds();

            foreach (var id in ids)
            {
                using (HeifRegionItem regionItem = context.GetRegionItem(id))
                {
                    var regions = regionItem.GetRegionGeometries();

                    sb.AppendFormatLine("    id={0} reference_width={1} reference_height={2} {3} regions",
                                      regionItem.Id,
                                      regionItem.ReferenceWidth,
                                      regionItem.ReferenceHeight,
                                      regions.Count);

                    foreach (var region in regions)
                    {
                        sb.AppendFormatLine("      {0}", region.ToString());
                    }

                    var userDescriptions = context.GetUserDescriptionProperties(regionItem.Id);

                    foreach (var item in userDescriptions)
                    {
                        sb.AppendFormatLine("    user description:");
                        sb.AppendFormatLine("      language: {0}", item.Language);
                        sb.AppendFormatLine("      name: {0}", item.Name);
                        sb.AppendFormatLine("      description: {0}", item.Description);
                        sb.AppendFormatLine("      tags: {0}", item.Tags);
                    }
                }
            }
        }

        static void WriteThumbnailImageInfo(HeifImageHandle handle, StringBuilder sb)
        {
            var thumbnailIds = handle.GetThumbnailImageIds();

            if (thumbnailIds.Count > 0)
            {
                sb.AppendFormatLine("  thumbnails:");

                foreach (var thumbnailId in thumbnailIds)
                {
                    using (HeifImageHandle thumbnail = handle.GetThumbnailImage(thumbnailId))
                    {
                        sb.AppendFormatLine("    thumbnail: {0}x{1} {2}-bit",
                                          thumbnail.Width,
                                          thumbnail.Height,
                                          thumbnail.BitDepth);
                    }
                }
            }
            else
            {
                sb.AppendFormatLine("  thumbnails: none");
            }
        }

        static void WriteTransformationInfo(HeifContext context, HeifImageHandle imageHandle, StringBuilder sb)
        {
            var transformations = context.GetTransformationProperties(imageHandle);

            if (transformations.Count > 0)
            {
                sb.AppendFormatLine("  transformations:");

                foreach (var item in transformations)
                {
                    sb.AppendFormatLine("    {0}", item.ToString());
                }
            }
            else
            {
                sb.AppendFormatLine("  transformations: none");
            }
        }
    }
}