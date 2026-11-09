using System;
using System.Collections.Generic;
using System.Text;

namespace SldFile.Mfc;

internal sealed class CmgrConfigEntry
{
    public uint ConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint? ParentConfigId { get; set; }
}

/// <summary>
/// Unified model for Contents/CMgrHdr2 binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes dmConfigMgrHeader_c and dmConfigHeader_c records.
/// </summary>
internal sealed class CmgrHdr2Model : IMfcSerializable
{
    public const string OuterClassName = "dmConfigMgrHeader_c";
    public const string ConfigClassName = "dmConfigHeader_c";
    public const uint ExpectedMagic = 0x8BA49ED8;

    public ushort ConfigCount { get; set; } = 1;
    public ushort TagCount { get; set; } = 1;
    public uint MagicMarker { get; set; } = ExpectedMagic;
    public uint TypeFlag { get; set; } = 0x8F; // 0x8F = Part, 0x75 = Assembly
    public ushort FileTypeCode { get; set; } = 0x0044; // 0x0044 = Part, 0x0004 = Assembly
    public uint Timestamp { get; set; }

    public bool IsPart => TypeFlag == 0x8F;
    public bool IsAssembly => TypeFlag == 0x75;

    public byte[]? RawData { get; set; }
    public List<CmgrConfigEntry> Configurations { get; } = new();

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            // Outer object: dmConfigMgrHeader_c
            ushort outerTag = 0;
            ar.WriteClassTag(OuterClassName, ref outerTag, schema: 1);

            if (Configurations.Count > 0)
            {
                // Multi-config record path
                uint count = (uint)Configurations.Count;
                ar.Count(ref count);

                ushort configTag = 0;
                foreach (var cfg in Configurations)
                {
                    ar.WriteClassTag(ConfigClassName, ref configTag, schema: 1);

                    uint f1 = 0;
                    ar.U32(ref f1);

                    string? name = cfg.Name;
                    ar.CString(ref name);

                    uint cid = cfg.ConfigId;
                    ar.U32(ref cid);

                    if (ar.DocumentVersion >= MfcVersionGates.CfgVerUnk1)
                    {
                        uint f4 = 0;
                        ar.U32(ref f4);
                    }

                    if (ar.DocumentVersion >= MfcVersionGates.CfgVerParent)
                    {
                        string? empty = string.Empty;
                        ar.CString(ref empty);

                        uint pid = cfg.ParentConfigId ?? 0xFFFFFFFFu;
                        ar.U32(ref pid);
                    }
                }
            }
            else
            {
                // Standard 101-byte Contents/CMgrHdr2 stream layout
                ushort cfgCount = ConfigCount;
                ar.U16(ref cfgCount);

                ushort configTag = 0;
                ar.WriteClassTag(ConfigClassName, ref configTag, schema: 1);

                ushort tagCount = TagCount;
                ar.U16(ref tagCount);

                ushort pad = 0;
                ar.U16(ref pad);

                // Magic marker as 2-character UTF-16 WChar
                byte[] markerBytes = BitConverter.GetBytes(MagicMarker);
                string? markerStr = Encoding.Unicode.GetString(markerBytes);
                ar.WChar(ref markerStr);

                uint zero = 0;
                ar.U32(ref zero);

                uint tf = TypeFlag;
                ar.U32(ref tf);

                string? markerStr2 = markerStr;
                ar.WChar(ref markerStr2);

                uint unk1 = 0xFFFFFFFF;
                ar.U32(ref unk1);

                uint unk2 = 0;
                ar.U32(ref unk2);

                string? empty1 = string.Empty;
                ar.WChar(ref empty1);

                string? empty2 = string.Empty;
                ar.WChar(ref empty2);

                ushort markerTag = 0x0180;
                ar.U16(ref markerTag);

                ushort ftc = FileTypeCode;
                ar.U16(ref ftc);

                uint ts = Timestamp;
                ar.U32(ref ts);
            }
        }
        else
        {
            RawData = ar.ToArray();
            Configurations.Clear();
            if (ar.Remaining < 2) return;

            var kind = ar.ReadClassTag(out string outerName, out ushort outerTag, out _);
            if (kind == MfcTagKind.Invalid || kind == MfcTagKind.Null) return;

            // Check if count is a CArchive count or u16
            int posBeforeCount = ar.Position;
            if (ar.Remaining < 2) return;
            ushort u16Count = 0;
            ar.U16(ref u16Count);
            ConfigCount = u16Count;

            var cfgKind = ar.ReadClassTag(out string cfgName, out _, out _);
            if (cfgKind == MfcTagKind.Invalid) return;

            // Peek at next 4 bytes to check if this is the standard header (tagCount + pad + WChar)
            // or the synthetic config record (DWORD f1 + CString)
            if (ar.Remaining >= 8)
            {
                int peekPos = ar.Position;
                ushort tagCount = 0;
                ar.U16(ref tagCount);
                TagCount = tagCount;

                ushort pad = 0;
                ar.U16(ref pad);

                // Check for WChar marker prefix (0xFF 0xFE 0xFF)
                if (ar.Remaining >= 4)
                {
                    string? markerStr = null;
                    ar.WChar(ref markerStr);

                    if (markerStr?.Length >= 2)
                    {
                        // Standard CMgrHdr2 header structure
                        MagicMarker = BitConverter.ToUInt32(Encoding.Unicode.GetBytes(markerStr), 0);

                        uint zero = 0;
                        ar.U32(ref zero);

                        uint tf = 0;
                        ar.U32(ref tf);
                        TypeFlag = tf;

                        string? markerStr2 = null;
                        ar.WChar(ref markerStr2);

                        if (ar.Remaining >= 4)
                        {
                            uint unk1 = 0;
                            ar.U32(ref unk1);
                        }

                        if (ar.Remaining >= 4)
                        {
                            uint unk2 = 0;
                            ar.U32(ref unk2);
                        }

                        if (ar.Remaining >= 4)
                        {
                            string? empty1 = null;
                            ar.WChar(ref empty1);
                        }

                        if (ar.Remaining >= 4)
                        {
                            string? empty2 = null;
                            ar.WChar(ref empty2);
                        }

                        if (ar.Remaining >= 2)
                        {
                            ushort markerTag = 0;
                            ar.U16(ref markerTag);
                        }

                        if (ar.Remaining >= 2)
                        {
                            ushort ftc = 0;
                            ar.U16(ref ftc);
                            FileTypeCode = ftc;
                        }

                        if (ar.Remaining >= 4)
                        {
                            uint ts = 0;
                            ar.U32(ref ts);
                            Timestamp = ts;
                        }

                        return;
                    }
                }

                // If not standard header, rollback to parse configuration entry
                ar.Position = peekPos;
            }

            // Fallback: parse as CmgrConfigEntry collection (synthetic / extended format)
            ar.Position = posBeforeCount;
            uint count = 0;
            ar.Count(ref count);
            if (count == 0 || count > 10_000) return;

            for (uint i = 0; i < count; i++)
            {
                var entryKind = ar.ReadClassTag(out string _, out _, out _);
                if (entryKind == MfcTagKind.Invalid) break;

                if (ar.Remaining < 4) break;
                uint f1 = 0;
                ar.U32(ref f1);

                string? name = null;
                ar.CString(ref name);
                if (name == null) break;

                if (ar.Remaining < 4) break;
                uint configId = 0;
                ar.U32(ref configId);

                var entry = new CmgrConfigEntry
                {
                    ConfigId = configId,
                    Name = name
                };

                if (ar.DocumentVersion >= MfcVersionGates.CfgVerUnk1 && ar.Remaining >= 4)
                {
                    uint f4 = 0;
                    ar.U32(ref f4);
                }

                if (ar.DocumentVersion >= MfcVersionGates.CfgVerParent && ar.Remaining >= 4)
                {
                    string? pName = null;
                    ar.CString(ref pName);
                    if (ar.Remaining >= 4)
                    {
                        uint pid = 0;
                        ar.U32(ref pid);
                        if (pid != 0xFFFFFFFFu)
                        {
                            entry.ParentConfigId = pid;
                        }
                    }
                }

                Configurations.Add(entry);
            }
        }
    }
}
