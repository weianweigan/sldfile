using System;
using System.Collections.Generic;
using System.Text;
using SldFile.Binary;
using SldFile.Ole;

namespace SldFile;

/// <summary>Parses known data after either container has been decoded to named streams.</summary>
internal sealed class SldStreamRegistry
{
    private static readonly SummaryStreamReader _summaryReader = new();
    private static readonly DocumentSummaryStreamReader _documentSummaryReader = new();
    private static readonly SolidWorksInformationStreamReader _solidWorksInfoReader = new();
    private static readonly ModelStampsStreamReader _modelStampsReader = new();
    private static readonly Header2StreamReader _header2Reader = new();
    private static readonly ConfigPropertiesStreamReader _configPropertiesReader = new();
    private static readonly ZlbStreamReader _zlbReader = new();
    private static readonly ResolvedFeaturesStreamReader _resolvedFeaturesReader = new();

    public void LoadFromStreams(Dictionary<string, byte[]> streams, SwFile model)
    {
        model.RawStreams = streams;
        // Helper: lookup top-level stream
        byte[]? S(string name) => streams.TryGetValue(name, out var v) ? v : null;
        // Helper: lookup nested stream by path
        byte[]? N(string path) => streams.TryGetValue(path, out var v) ? v : null;
        // Helper: process nested stream data if present
        void LoadN(string path, Action<byte[]> onData) { if (N(path) is byte[] d) onData(d); }

        LoadN("\u0005SummaryInformation", data => _summaryReader.ParseBytes(data, model.Summary));
        LoadN("\u0005DocumentSummaryInformation", data => _documentSummaryReader.ParseBytes(data, model.DocumentSummary));
        LoadN("ISolidWorksInformation", data => _solidWorksInfoReader.ParseBytes(data, model.SolidWorksInformation));

        // ── Fixed-name binary streams ─────────────────────────────────
        if (S("Header2") is byte[] h2) _header2Reader.Parse(h2, model.Header2);
        if (S("ModelStamps") is byte[] ms) _modelStampsReader.Parse(ms, model.ModelStamps);

        // ── Nested trivial streams ────────────────────────────────────
        LoadN("_DL_VERSION_5000/DLUpdateStamp",
            data => { var m = DlUpdateStampData.Parse(data); model.DlUpdateStamp.TypeFlag = m.TypeFlag; model.DlUpdateStamp.ZeroPad = m.ZeroPad; model.DlUpdateStamp.RawData = data; });
        LoadN("_DL_VERSION_19000/DLUpdateStamp",
            data => { var m = DlUpdateStampData.Parse(data); model.DlUpdateStamp.TypeFlag = m.TypeFlag; model.DlUpdateStamp.ZeroPad = m.ZeroPad; model.DlUpdateStamp.RawData = data; });
        LoadN("ThirdPtyStore/VisualStates_DisplayPane",
            data => { var m = VisualStatesDisplayPaneData.Parse(data); model.VisualStatesDisplayPane.DisplayValue = m.DisplayValue; model.VisualStatesDisplayPane.ZeroPad = m.ZeroPad; model.VisualStatesDisplayPane.RawData = data; });
        LoadN("ThirdPty/CMMotionLoadMapU",
            data => { var m = CmMotionLoadMapUData.Parse(data); model.CmMotionLoadMapU.Version = m.Version; model.CmMotionLoadMapU.FieldCount = m.FieldCount; model.CmMotionLoadMapU.GuidOrId = m.GuidOrId; model.CmMotionLoadMapU.RefValue = m.RefValue; model.CmMotionLoadMapU.StringLen = m.StringLen; model.CmMotionLoadMapU.Name = m.Name; model.CmMotionLoadMapU.RawData = data; });
        LoadN("SheetPreviews/SheetNames",
            data => { var m = SheetNamesData.Parse(data); model.SheetNames.NamesBuilder = m.NamesBuilder; model.SheetNames.RawData = data; model.SheetNames.Freeze(); });

        // ── MFC streams in nested storages ────────────────────────────
        LoadN("Contents/DisplayLists", data => DisplayListsStreamHandler.Parse(data, model.DisplayLists));
        LoadN("ThirdPtyStore/VisualStates", data => model.VisualStatesRawData = data);
        LoadN("Contents/CMgrHdr2", data => CmgrHdr2StreamHandler.Parse(data, model.CmgrHdr2));
        LoadN("_MO_VERSION_5000/Biography", data => BiographyStreamHandler.Parse(data, model.Biography));
        LoadN("_MO_VERSION_19000/Biography", data => BiographyStreamHandler.Parse(data, model.Biography));
        LoadN("_MO_VERSION_5000/History", data => HistoryStreamHandler.Parse(data, model.History));
        LoadN("_MO_VERSION_19000/History", data => HistoryStreamHandler.Parse(data, model.History));
        LoadN("Contents/Config-0-LWDATA", data => LwDataStreamHandler.Parse(data, model.LwData));
        LoadN("Contents/CMgr", data => CmgrStreamHandler.Parse(data, model.Cmgr));
        LoadN("Contents/Config-0-MatesList", data => MatesListStreamHandler.Parse(data, model.MatesList));
        LoadN("Contents/Definition", data => DefinitionStreamHandler.Parse(data, model.Definition));
        LoadN("Contents/Config-0", data => Config0StreamHandler.Parse(data, model.Config0));

        // ── Content streams ───────────────────────────────────────────
        if (S("Preview") is byte[] p) model.PreviewImage = p;
        if (S("PreviewPNG") is byte[] png) model.PreviewPng = png;
        if (N("Contents/Config-0-GhostPartition") is byte[] gp) model.GhostPartition = gp;
        if (N("Contents/Config-0-Partition") is byte[] part) model.Partition = part;

        // ── MATERIALTREE ──────────────────────────────────────────────
        if (N("swXmlContents/MATERIALTREE") is byte[] mtData && mtData.Length > 2)
            model.MaterialTreeXml = Encoding.Unicode.GetString(mtData, 2, mtData.Length - 2);

        // ── COMPINSTANCETREE ──────────────────────────────────────────
        if (N("swXmlContents/COMPINSTANCETREE") is byte[] citData && citData.Length > 0)
        {
            int citStart = Array.IndexOf(citData, (byte)'<');
            if (citStart >= 0)
                model.CompInstanceTreeXml = Encoding.UTF8.GetString(citData, citStart, citData.Length - citStart);
        }

        // ── Dynamic/discovered streams ────────────────────────────────
        LoadDynamicFromDict(streams, model);

        SynchronizeProperties(model);
    }

    private static void LoadDynamicFromDict(Dictionary<string, byte[]> streams, SwFile model)
    {
        foreach (var kvp in streams)
        {
            var name = kvp.Key;
            var data = kvp.Value;

            // Config properties
            if (name.StartsWith("Config-") && name.EndsWith("-Properties"))
            {
                var confProps = new ConfigProperties(name) { RawData = data };
                _configPropertiesReader.Read(data, confProps);
                model.ConfigurationProperties[name] = confProps;
            }
            // ZLB streams
            else if (name.EndsWith("__ZLB"))
            {
                var zlbData = new ZlbData();
                _zlbReader.Read(data, zlbData);
                model.ZlbContents[name] = zlbData;

                // Parse known binary MFC ZLB streams
                if (!zlbData.IsXml && zlbData.RawData != null && zlbData.RawData.Length > 0)
                {
                    ParseZlbMfcStream(name, zlbData.RawData, model);
                }
                else if (name.EndsWith("COMPINSTANCETREE__ZLB"))
                {
                    model.CompInstanceTreeXml = zlbData.XmlContent;
                }
            }
            // ResolvedFeatures
            else if (name.EndsWith("-ResolvedFeatures"))
            {
                _resolvedFeaturesReader.Parse(data, model.ResolvedFeatures);
            }
        }
    }

    // ── Dynamic stream discovery (CFB) ───────────────────────────────

    private static void ParseZlbMfcStream(string streamPath, byte[] data, SwFile model)
    {
        if (streamPath.EndsWith("Contents/DisplayLists__ZLB"))
        {
            DisplayListsStreamHandler.Parse(data, model.DisplayLists);
        }
        else if (streamPath.EndsWith("Contents/User Units Table__ZLB"))
        {
            UserUnitsTableStreamHandler.Parse(data, model.UserUnitsTable);
        }
        else if (streamPath.EndsWith("Contents/View Orientation Data__ZLB"))
        {
            ViewOrientationStreamHandler.Parse(data, model.ViewOrientation);
        }
    }

    private static void SynchronizeProperties(SwFile model)
    {
        model.Summary.Author = model.SolidWorksInformation.Author;
        model.Summary.Keywords = model.SolidWorksInformation.Keywords;
        model.Summary.Comments = model.SolidWorksInformation.Comments;
        model.Summary.Title = model.SolidWorksInformation.Title;
        model.Summary.Subject = model.SolidWorksInformation.Subject;
        model.Summary.LastSavedBy = model.SolidWorksInformation.LastSavedBy;

        if (!string.IsNullOrEmpty(model.SolidWorksInformation.CreatedDate) &&
            DateTime.TryParse(model.SolidWorksInformation.CreatedDate, out var createdDate))
        {
            model.Summary.Created = createdDate;
        }

        if (!string.IsNullOrEmpty(model.SolidWorksInformation.LastSavedDate) &&
            DateTime.TryParse(model.SolidWorksInformation.LastSavedDate, out var savedDate))
        {
            model.Summary.LastSaved = savedDate;
        }
    }
}
