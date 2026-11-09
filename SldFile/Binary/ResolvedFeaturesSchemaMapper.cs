using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class ResolvedFeaturesSchemaMapper
{
    public static FolderHeader ParseFolderHeader(MfcElement element) => SldFile.Mfc.ResolvedFeaturesModel.ParseFolderHeader(element);
}