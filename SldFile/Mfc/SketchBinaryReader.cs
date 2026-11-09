using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;


namespace SldFile.Mfc;

/// <summary>
/// Bounded source index -> feature ranges -> common records -> point-table references -> semantic projections.
/// Decodes sketches directly into strongly-typed IMfcSerializable SketchFeatureModel objects.
/// </summary>
internal static class SketchBinaryReader
{
    internal static IReadOnlyList<SketchFeatureModel> Read(byte[] source, byte[]? keywords, string stream, int configurationIndex, LengthUnit? lengthUnit = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var catalog = ReadCatalog(keywords);
        var tags = IndexTags(source);
        var headers = new SortedDictionary<int, Header>();
        // Explicit definitions establish class identity, independently of the localized display name.
        foreach (var tag in tags.Where(t => IsFeatureClass(t.ClassName)))
        {
            if (TryHeader(source, tag.DataStart, out var header))
            {
                header.ClassName = tag.ClassName;
                headers[tag.DataStart] = header;
            }
        }
        // Shared-class instances have no class-name string. Join their base header against the XML id/name pair.
        for (int i = 0; i <= source.Length - 6; i++)
        {
            if (source[i + 1] != 128 || source[i + 2] != 255 || source[i + 3] != 254 || source[i + 4] != 255) continue;
            if (!headers.ContainsKey(i) && TryHeader(source, i, out var h) && h.Id.HasValue &&
                catalog.Any(c => c.Id == h.Id && c.Name == h.Name) &&
                !headers.Values.Any(ex => ex.Id == h.Id && ex.Name == h.Name))
            {
                headers[i] = h;
            }
        }
        var ordered = headers.Values.OrderBy(h => h.Start).ToArray();
        foreach (var c in catalog)
            c.IsOrigin = c.Name == "Origin" || ordered.Any(h => h.Id == c.Id && h.Name == c.Name && h.ClassName.StartsWith("moOriginProfileFeature_", StringComparison.Ordinal));
        var sketches = new List<SketchFeatureModel>();
        for (int n = 0; n < ordered.Length; n++)
        {
            var h = ordered[n];
            if (h.ClassName.StartsWith("moOriginProfileFeature", StringComparison.Ordinal) || h.Name == "Origin") continue;
            bool profile = IsProfileClass(h.ClassName);
            var item = catalog.FirstOrDefault(c => c.IsSketch && c.Id == h.Id && c.Name == h.Name);
            if (!profile && item == null) continue;
            // Origin is explicitly typed in XML even when its class definition was omitted by a damaged stream.
            if (!profile && item!.IsOrigin) continue;
            int end = n + 1 < ordered.Length ? ordered[n + 1].Start : source.Length;
            sketches.Add(ReadSketch(source, h, end, stream, configurationIndex, tags, item, lengthUnit));
        }
        return sketches.AsReadOnly();
    }

    private static SketchFeatureModel ReadSketch(byte[] bytes, Header header, int end, string stream, int config, List<MfcTag> tags, CatalogEntry? catalog, LengthUnit? lengthUnit)
    {
        var diagnostics = new List<string>();
        diagnostics.Add("PartialSerialization: object graph, solver state and opaque payloads are not fully decoded.");
        var feature = new SketchFeatureModel
        {
            Name = header.Name,
            Id = header.Id,
            SourceOffset = header.Start,
            ConfigurationIndex = config,
            SourceStream = stream,
            MfcClassName = header.ClassName,
            Is3D = header.ClassName.StartsWith("mo3DProfileFeature", StringComparison.Ordinal) ? true : IsProfileClass(header.ClassName) ? false : (bool?)null,
            RawData = Slice(bytes, header.Start, end)
        };
        var points = new List<SketchPointModel>();
        var pointTable = new List<SketchPointModel?>();
        var markers = new List<int>();
        for (int i = header.ContentStart; i <= end - 80; i++) if (IsRecord(bytes, i, end)) markers.Add(i);
        int lastPointEnd = header.ContentStart;
        int declaredPoints = 0;
        if (markers.Count > 0 && (U16(bytes, markers[0] + 64) == 30 || U16(bytes, markers[0] + 64) == 14))
        {
            int first = markers[0];
            if (first - 14 >= header.ContentStart)
            {
                uint count = U32(bytes, first - 14);
                if (count > 0 && count <= (uint)((end - first) / 80 + 1)) declaredPoints = (int)count;
            }
            if (declaredPoints == 0)
            {
                int ptCount = 0;
                foreach (int m in markers)
                {
                    ushort enc = U16(bytes, m + 64);
                    if ((enc == 30 || enc == 14) && !(F64(bytes, m + 72) == -1 && U32(bytes, m + 68) <= 1))
                        ptCount++;
                    else
                        break;
                }
                declaredPoints = ptCount;
            }
            if (declaredPoints == 0) diagnostics.Add("UnsupportedPointGroupHeader at " + first);
        }

        foreach (int m in markers.Take(declaredPoints))
        {
            if (F64(bytes, m + 72) == -1 && U32(bytes, m + 68) <= 1) break;
            ushort encoding = U16(bytes, m + 64);
            int size = encoding == 30 ? 82 : encoding == 14 ? 90 : 0;
            if (size == 0 || !Has(m, size, end) || (U32(bytes, m + 5) != uint.MaxValue && U32(bytes, m + 9) != uint.MaxValue))
            {
                diagnostics.Add("TruncatedOrUnsupportedPointTable at " + m);
                pointTable.Add(null);
                continue;
            }
            var x = F64(bytes, m + 66); var y = F64(bytes, m + 74); var z = encoding == 14 ? F64(bytes, m + 82) : 0;
            lastPointEnd = Math.Max(lastPointEnd, m + size);
            if (!Finite(x) || !Finite(y) || !Finite(z)) { diagnostics.Add("NonFinitePoint at " + m); pointTable.Add(null); continue; }
            var point = new SketchPointModel { Id = U32(bytes, m - 4), TableIndex = pointTable.Count, X = x, Y = y, Z = z, SourceOffset = m - 4 };
            points.Add(point); pointTable.Add(point);
            if (feature.Is3D.HasValue && feature.Is3D != (encoding == 14)) diagnostics.Add("MixedCoordinateEncoding at " + m);
            feature.Is3D = encoding == 14;
        }
        if (pointTable.Count != declaredPoints)
        {
            diagnostics.Add("PointTableCountMismatch: point-table positions cannot be established; references left unresolved.");
            points.Clear(); pointTable.Clear();
        }
        var segments = new List<SketchSegmentModel>();
        foreach (int m in markers.Where(m => m >= lastPointEnd))
        {
            if (!Has(m, 88, end) || U32(bytes, m - 4) == uint.MaxValue || U32(bytes, m + 68) > 1 || F64(bytes, m + 72) != -1) continue;
            ushort first = U16(bytes, m + 64), last = U16(bytes, m + 66);
            var segment = new SketchSegmentModel
            {
                Id = U32(bytes, m - 4),
                SourceOffset = m - 4,
                Kind = SketchSegmentKind.Unknown,
                IsConstruction = U32(bytes, m + 68) == 0,
                StartPointIndex = first < pointTable.Count ? first : -1,
                EndPointIndex = last < pointTable.Count ? last : -1
            };
            SketchPointModel? Point(int index) => index >= 0 && index < pointTable.Count ? pointTable[index] : null;
            segment.StartPoint = Point(first); segment.EndPoint = Point(last);
            var bodyTag = tags.FirstOrDefault(t => t.TagOffset == m + 80);
            if (bodyTag?.ClassName == "moDimText_c")
            {
                int cursor = bodyTag.DataStart;
                if (TryWide(bytes, ref cursor, end, out var text)) { segment.Kind = SketchSegmentKind.Text; segment.Text = text; }
            }
            else if (Has(m, 96, end) && I32(bytes, m + 80) == 1 &&
                     Enumerable.Range(0, 4).All(k => U16(bytes, m + 86 + k * 2) < pointTable.Count) &&
                     Enumerable.Range(0, 4).Select(k => U16(bytes, m + 86 + k * 2)).Distinct().Count() == 4)
            {
                segment.Kind = SketchSegmentKind.Ellipse;
                segment.CenterPointIndex = U16(bytes, m + 84);
                segment.MajorPointIndex = U16(bytes, m + 86);
                segment.MinorPointIndex = U16(bytes, m + 88);
                segment.CenterPoint = Point(segment.CenterPointIndex);
                segment.MajorPoint = Point(segment.MajorPointIndex);
                segment.MinorPoint = Point(segment.MinorPointIndex);
            }
            else if (tags.Any(t => t.TagOffset >= m + 80 && t.TagOffset <= m + 100 && t.ClassName == "modifSplineList_c") ||
                     (Has(m, 92, end) && I32(bytes, m + 80) == 2))
            {
                segment.Kind = SketchSegmentKind.Spline;
                diagnostics.Add("SplineParametersNotDecoded at " + m);
            }
            else if (Has(m, 90, end) && (I32(bytes, m + 80) == 1 || I32(bytes, m + 80) == -1 || (U32(bytes, m + 80) & 0xFFFF) == 1) &&
                     U32(bytes, m + 86) == 0xfffffffe)
            {
                segment.Kind = SketchSegmentKind.Arc;
                segment.CenterPointIndex = U16(bytes, m + 84);
                segment.CenterPoint = Point(segment.CenterPointIndex);
                segment.Direction = I32(bytes, m + 80);
                segment.IsCircle = first == last;
                if (segment.CenterPoint != null && segment.StartPoint != null)
                {
                    double radius = Distance(segment.CenterPoint, segment.StartPoint);
                    if (Finite(radius)) segment.Radius = radius;
                    else diagnostics.Add("NonFiniteDerivedRadius at " + m);
                }
            }
            else if ((U32(bytes, m + 80) & 0xFFFF) == 0)
                segment.Kind = SketchSegmentKind.Line;
            if (segment.Kind == SketchSegmentKind.Unknown) diagnostics.Add("UnknownSegmentLayout at " + m);
            if (segment.Kind != SketchSegmentKind.Text && (segment.StartPoint == null || segment.EndPoint == null)) diagnostics.Add("UnresolvedPointReference at " + m);
            segments.Add(segment);
        }
        feature.Points = points;
        feature.Segments = segments;
        feature.Relations = ReadRelations(bytes, header.ContentStart, end);
        var parameters = ReadDimensions(bytes, header.ContentStart, end, tags, diagnostics);
        feature.Parameters = parameters;
        if (catalog == null) feature.Dimensions = parameters;
        else
        {
            var dimensions = new List<SketchDimensionModel>();
            foreach (var summary in catalog.Dimensions)
            {
                var parameter = parameters.FirstOrDefault(p => p.Name == summary.Name);
                if (parameter != null) dimensions.Add(parameter);
                else if (config != 0)
                    diagnostics.Add("ConfigurationDimensionSummaryUnavailable: global keyword summary omitted for " + summary.Name);
                else
                {
                    bool distance = config == 0 && feature.Relations.Count(r => r.TypeCode == 1) == catalog.Dimensions.Count &&
                        tags.Any(t => t.TagOffset >= header.ContentStart && t.TagOffset < end && t.ClassName == "sgPntPntDist") &&
                        !tags.Any(t => t.TagOffset >= header.ContentStart && t.TagOffset < end && t.ClassName.IndexOf("Angl", StringComparison.OrdinalIgnoreCase) >= 0);
                    double? scale = distance ? LengthScale(lengthUnit) : null;
                    string rawVal = summary.Value.TrimEnd('°', ' ');
                    double value;
                    double? systemValue = null;
                    bool isAngle = summary.Value.Contains("°") || summary.Name.IndexOf("Angl", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isAngle)
                    {
                        if (double.TryParse(rawVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                        {
                            systemValue = value * Math.PI / 180.0;
                        }
                    }
                    else if (scale.HasValue && double.TryParse(rawVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && Finite(value * scale.Value))
                    {
                        systemValue = value * scale.Value;
                    }
                    var dtype = isAngle ? SketchDimensionType.Angle :
                                summary.Name.IndexOf("Dia", StringComparison.OrdinalIgnoreCase) >= 0 ? SketchDimensionType.Diameter :
                                summary.Name.StartsWith("R", StringComparison.OrdinalIgnoreCase) ? SketchDimensionType.Radius :
                                SketchDimensionType.Linear;

                    dimensions.Add(new SketchDimensionModel
                    {
                        Name = summary.Name,
                        DisplayValue = summary.Value,
                        SystemValue = systemValue,
                        ValueSource = "KeywordSummary",
                        DimensionType = dtype,
                        SourceOffset = -1
                    });
                    diagnostics.Add("DimensionFromKeywordSummary: " + summary.Name);
                }
            }
            feature.Dimensions = dimensions;
        }
        if (points.Count == 0 && segments.Count == 0) diagnostics.Add("NoSupportedDirectGeometry: empty, nested or unsupported sketch payload.");
        feature.Diagnostics = diagnostics;
        BindObjectGraph(feature);
        return feature;
    }

    private static void BindObjectGraph(SketchFeatureModel feature)
    {
        foreach (var rel in feature.Relations)
        {
            rel.OwnerSketch = feature;
            foreach (var rf in rel.References)
            {
                rf.OwnerSketch = feature;
                if (rf.Index < feature.Points.Count && (rf.ClassReference == 0x80AD || rel.Kind == SketchRelationKind.Distance))
                {
                    rf.Target = feature.Points[rf.Index];
                }
                else if (rf.Index < feature.Segments.Count)
                {
                    rf.Target = feature.Segments[rf.Index];
                }
                else if (rf.Index < feature.Points.Count)
                {
                    rf.Target = feature.Points[rf.Index];
                }
            }
        }

        var distanceRelations = feature.Relations.Where(r => r.Kind == SketchRelationKind.Distance || r.Kind == SketchRelationKind.Radius || r.Kind == SketchRelationKind.Diameter).ToList();
        for (int i = 0; i < feature.Dimensions.Count; i++)
        {
            var dim = feature.Dimensions[i];
            dim.OwnerSketch = feature;
            if (i < distanceRelations.Count)
            {
                dim.Relation = distanceRelations[i];
            }
            DeriveDimensionPlacement(dim, feature, i);
        }

        foreach (var p in feature.Parameters)
        {
            p.OwnerSketch = feature;
        }
    }

    private static void DeriveDimensionPlacement(SketchDimensionModel dim, SketchFeatureModel feature, int index)
    {
        var attached = dim.GetAttachedEntities();
        SketchSegmentModel? targetSeg = null;
        SketchPointModel? p1 = null, p2 = null;

        foreach (var ent in attached)
        {
            if (ent is SketchSegmentModel seg)
            {
                targetSeg = seg;
                break;
            }
            if (ent is SketchPointModel pt)
            {
                if (p1 == null) p1 = pt;
                else if (p2 == null) p2 = pt;
            }
        }

        if (targetSeg == null && p1 == null && dim.SystemValue.HasValue && dim.SystemValue.Value > 0)
        {
            double val = dim.SystemValue.Value;
            if (dim.DimensionType == SketchDimensionType.Radius || dim.DimensionType == SketchDimensionType.Diameter)
            {
                targetSeg = feature.Segments.FirstOrDefault(s => s.Kind == SketchSegmentKind.Arc && s.Radius.HasValue &&
                    (Math.Abs(s.Radius.Value - val) < 0.002 || Math.Abs(s.Radius.Value * 2.0 - val) < 0.002));
            }
            else
            {
                targetSeg = feature.Segments.FirstOrDefault(s => s.Kind == SketchSegmentKind.Line && s.StartPoint != null && s.EndPoint != null &&
                    Math.Abs(Distance(s.StartPoint, s.EndPoint) - val) < Math.Max(val * 0.05, 0.002));
            }
        }

        if (targetSeg == null && p1 == null && feature.Segments.Count > 0)
        {
            int segIdx = index % feature.Segments.Count;
            targetSeg = feature.Segments[segIdx];
        }

        if (targetSeg != null)
        {
            if (targetSeg.Kind == SketchSegmentKind.Arc && targetSeg.CenterPoint != null && targetSeg.Radius.HasValue)
            {
                dim.CenterX ??= targetSeg.CenterPoint.X;
                dim.CenterY ??= targetSeg.CenterPoint.Y;
                dim.CenterZ ??= targetSeg.CenterPoint.Z;
                dim.Radius ??= targetSeg.Radius.Value;
                if (dim.DimensionType == SketchDimensionType.Unknown || dim.DimensionType == SketchDimensionType.Linear)
                {
                    dim.DimensionType = targetSeg.IsCircle == true ? SketchDimensionType.Diameter : SketchDimensionType.Radius;
                }

                dim.WitnessPoint1X ??= targetSeg.CenterPoint.X;
                dim.WitnessPoint1Y ??= targetSeg.CenterPoint.Y;
                dim.WitnessPoint1Z ??= targetSeg.CenterPoint.Z;

                double angle = Math.PI / 4.0 + (index % 4) * (Math.PI / 6.0);
                double r = targetSeg.Radius.Value;
                dim.WitnessPoint2X ??= targetSeg.CenterPoint.X + r * Math.Cos(angle);
                dim.WitnessPoint2Y ??= targetSeg.CenterPoint.Y + r * Math.Sin(angle);
                dim.WitnessPoint2Z ??= targetSeg.CenterPoint.Z;

                if (!dim.HasTextPosition)
                {
                    double textR = r + 0.02 + (index % 3) * 0.01;
                    dim.TextX = targetSeg.CenterPoint.X + textR * Math.Cos(angle);
                    dim.TextY = targetSeg.CenterPoint.Y + textR * Math.Sin(angle);
                    dim.TextZ = targetSeg.CenterPoint.Z;
                }
            }
            else if (targetSeg.StartPoint != null && targetSeg.EndPoint != null)
            {
                p1 = targetSeg.StartPoint;
                p2 = targetSeg.EndPoint;
            }
        }

        if (p1 != null && p2 != null)
        {
            dim.WitnessPoint1X ??= p1.X;
            dim.WitnessPoint1Y ??= p1.Y;
            dim.WitnessPoint1Z ??= p1.Z;

            dim.WitnessPoint2X ??= p2.X;
            dim.WitnessPoint2Y ??= p2.Y;
            dim.WitnessPoint2Z ??= p2.Z;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-6)
            {
                double ux = dx / len;
                double uy = dy / len;
                dim.DirectionX ??= ux;
                dim.DirectionY ??= uy;
                dim.DirectionZ ??= 0;

                double nx = -uy;
                double ny = ux;
                dim.NormalX ??= nx;
                dim.NormalY ??= ny;
                dim.NormalZ ??= 0;

                if (!dim.HasTextPosition)
                {
                    double offset = 0.018 + (index % 3) * 0.012;
                    dim.TextX = (p1.X + p2.X) / 2.0 + nx * offset;
                    dim.TextY = (p1.Y + p2.Y) / 2.0 + ny * offset;
                    dim.TextZ = 0;
                }
            }
        }
    }

    private static List<SketchRelationModel> ReadRelations(byte[] bytes, int start, int end)
    {
        var results = new List<SketchRelationModel>();
        for (int i = start; i <= end - 26; i++)
        {
            uint type = U32(bytes, i), flags = U32(bytes, i + 4);
            if (type == 0 || type > 1000) continue;
            bool standard = flags == 2 && U16(bytes, i + 8) == 0 && U16(bytes, i + 10) == 0xfffe && U16(bytes, i + 12) == 0 && IsHandle(bytes, i + 14, end);
            bool dimension = (type == 1 || type == 3) && flags == 2 && U32(bytes, i + 8) == 0 && IsHandle(bytes, i + 12, end);
            bool offset = type == 47 && (flags == 2 || flags == 65538) && U32(bytes, i + 8) == 0 && U16(bytes, i + 12) == 0;
            if (!standard && !dimension && !offset) continue;
            int cursor = i + (dimension ? 12 : 14);
            var handles = new List<SketchEntityReferenceModel>();
            while (IsHandle(bytes, cursor, end))
            {
                handles.Add(new SketchEntityReferenceModel { ClassReference = U16(bytes, cursor), Index = U16(bytes, cursor + 2), SourceOffset = cursor });
                cursor += 12;
            }
            results.Add(new SketchRelationModel { TypeCode = (int)type, SourceOffset = i, Layout = standard ? "Common" : dimension ? "Dimension" : "Offset", References = handles });
        }
        return results;
    }

    private static List<SketchDimensionModel> ReadDimensions(byte[] bytes, int start, int end, List<MfcTag> tags, List<string> diagnostics)
    {
        var results = new List<SketchDimensionModel>();

        // 1. Read moLengthParameter_c and moAngleParameter_c
        foreach (var tag in tags.Where(t => t.TagOffset >= start && t.TagOffset < end && (t.ClassName == "moLengthParameter_c" || t.ClassName == "moAngleParameter_c")))
        {
            int cursor = tag.DataStart + 2;
            if (!TryWide(bytes, ref cursor, end, out var name) || !Has(cursor, 16, end)) { diagnostics.Add("TruncatedParameter at " + tag.TagOffset); continue; }
            cursor += 16;
            if (!TryWide(bytes, ref cursor, end, out _) || !Has(cursor, 10, end)) { diagnostics.Add("TruncatedParameter at " + tag.TagOffset); continue; }
            cursor += 2; double value = F64(bytes, cursor);
            if (!Finite(value)) { diagnostics.Add("NonFiniteDimension at " + cursor); continue; }

            bool isAngle = tag.ClassName == "moAngleParameter_c";
            var param = isAngle
                ? (MoParameterModel)new MoAngleParameterModel(name, value)
                : new MoLengthParameterModel(name, value);
            param.SourceOffset = cursor;
            param.ValueSource = "BinaryParameter";

            results.Add(new SketchDimensionModel(param)
            {
                SourceOffset = cursor,
                DimensionType = isAngle ? SketchDimensionType.Angle : SketchDimensionType.Linear
            });
        }

        // 2. Scan display dimensions (moDisplay*Dim_c) for text position, direction and witness points
        var displayDimTags = tags.Where(t => t.TagOffset >= start && t.TagOffset < end &&
            t.ClassName.StartsWith("moDisplay", StringComparison.Ordinal) && t.ClassName.EndsWith("Dim_c", StringComparison.Ordinal)).ToList();

        for (int i = 0; i < displayDimTags.Count; i++)
        {
            var dt = displayDimTags[i];
            int p = dt.DataStart;
            double? textX = null, textY = null, textZ = null;
            double? dirX = null, dirY = null, dirZ = null;

            for (int scan = p; scan < Math.Min(p + 600, end - 24); scan++)
            {
                if (bytes[scan] == 0xFF && bytes[scan + 1] == 0xFE && bytes[scan + 2] == 0xFF)
                {
                    int pos = scan + 3;
                    byte cnt = bytes[pos++];
                    pos += cnt * 2;
                    if (pos + 24 <= end)
                    {
                        double x = F64(bytes, pos);
                        double y = F64(bytes, pos + 8);
                        double z = F64(bytes, pos + 16);
                        if (Finite(x) && Finite(y) && Finite(z) && Math.Abs(x) < 50.0 && Math.Abs(y) < 50.0 && Math.Abs(z) < 50.0)
                        {
                            textX = x; textY = y; textZ = z;

                            for (int vpos = pos + 24; vpos < Math.Min(pos + 120, end - 24); vpos += 8)
                            {
                                double vx = F64(bytes, vpos);
                                double vy = F64(bytes, vpos + 8);
                                double vz = F64(bytes, vpos + 16);
                                double lenSq = vx * vx + vy * vy + vz * vz;
                                if (Math.Abs(lenSq - 1.0) < 0.01)
                                {
                                    dirX = vx; dirY = vy; dirZ = vz;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            var dtype = dt.ClassName switch
            {
                "moDisplayDistanceDim_c" => SketchDimensionType.Linear,
                "moDisplayRadialDim_c" or "moDisplayTrueRadialDim_c" => SketchDimensionType.Radius,
                "moDisplayDiameterDim_c" => SketchDimensionType.Diameter,
                "moDisplayAngularDim_c" => SketchDimensionType.Angle,
                "moDisplayArcLengthDim_c" => SketchDimensionType.ArcLength,
                "moDisplayChamferDim_c" => SketchDimensionType.Chamfer,
                "moDisplayOrdDim_c" or "moDisplayAngOrdDim_c" => SketchDimensionType.Ordinate,
                _ => SketchDimensionType.Linear
            };

            var displayDim = MoDisplayDimModel.CreateFromClassName(dt.ClassName);
            displayDim.SourceOffset = dt.TagOffset;
            displayDim.TextX = textX;
            displayDim.TextY = textY;
            displayDim.TextZ = textZ;
            displayDim.DirectionX = dirX;
            displayDim.DirectionY = dirY;
            displayDim.DirectionZ = dirZ;
            if (dirY.HasValue && dirX.HasValue)
            {
                displayDim.NormalX = -dirY;
                displayDim.NormalY = dirX;
                displayDim.NormalZ = 0;
            }

            if (i < results.Count)
            {
                var target = results[i];
                target.DisplayDimension = displayDim;
                if (target.DimensionType == SketchDimensionType.Unknown || target.DimensionType == SketchDimensionType.Linear)
                    target.DimensionType = dtype;
            }
            else
            {
                var dim = new SketchDimensionModel
                {
                    Name = $"DisplayDim_{i + 1}",
                    DisplayDimension = displayDim,
                    DimensionType = dtype,
                    ValueSource = "DisplayDimension"
                };
                results.Add(dim);
            }
        }

        return results;
    }

    private static bool IsHandle(byte[] b, int p, int end) => Has(p, 12, end) && (U16(b, p) & 0x8000) != 0 && U16(b, p) != 0xffff && U32(b, p + 4) == uint.MaxValue && U32(b, p + 8) == 0;
    internal static bool IsRecord(byte[] b, int p, int end) => p >= 4 && Has(p, 80, end) && b[p] == 255 && b[p + 1] == 255 && b[p + 3] == 0 && ((b[p + 2] == 31 && b[p + 4] == 3) || (b[p + 2] == 7 && b[p + 4] == 1)) && U32(b, p + 5) == uint.MaxValue && U32(b, p + 13) == 0xbf800000;
    private static bool IsProfileClass(string name) => name.StartsWith("moProfileFeature_", StringComparison.Ordinal) || name.StartsWith("mo3DProfileFeature_", StringComparison.Ordinal);
    private static bool IsFeatureClass(string name) => IsProfileClass(name) || name.StartsWith("moOriginProfileFeature_", StringComparison.Ordinal) || name.EndsWith("_ct", StringComparison.Ordinal) || name.EndsWith("_cM", StringComparison.Ordinal);
    private static bool TryHeader(byte[] bytes, int start, out Header header)
    {
        header = new Header { Start = start }; int p = start;
        if (Has(p, 2, bytes.Length) && bytes[p + 1] == 0x80) p += 2;
        if (!TryWide(bytes, ref p, bytes.Length, out var name) || name.Length == 0 || !Has(p, 16, bytes.Length)) return false;
        int id = I32(bytes, p + 8);
        header.Name = name; header.Id = id >= 0 ? id : (int?)null; header.ContentStart = p + 16;
        return true;
    }

    internal static List<MfcTag> IndexTags(byte[] source)
    {
        var result = new List<MfcTag>();
        for (int i = 0; i <= source.Length - 6; i++)
        {
            if (!MfcPrimitives.IsMfcTag(source, i)) continue;
            int cursor = i; var tag = MfcPrimitives.ReadMfcTag(source, ref cursor);
            if (tag == null || tag.ClassName.Length == 0 || tag.DataStart > source.Length) continue;
            result.Add(tag); i = tag.DataStart - 1;
        }
        return result;
    }

    internal static bool TryWide(byte[] bytes, ref int cursor, int end, out string value)
    {
        value = string.Empty; int p = cursor;
        if (end < 0 || end > bytes.Length || !Has(p, 4, end) || bytes[p] != 255 || bytes[p + 1] != 254 || bytes[p + 2] != 255) return false;
        p += 3; uint count = bytes[p++];
        if (count == 255)
        {
            if (!Has(p, 2, end)) return false; count = U16(bytes, p); p += 2;
            if (count == 65535) { if (!Has(p, 4, end)) return false; count = U32(bytes, p); p += 4; }
        }
        if (count > (uint)((end - p) / 2)) return false;
        int length = checked((int)count * 2);
        value = Encoding.Unicode.GetString(bytes, p, length); cursor = p + length; return true;
    }

    private static List<CatalogEntry> ReadCatalog(byte[]? bytes)
    {
        var result = new List<CatalogEntry>(); if (bytes == null) return result;
        int p = Array.IndexOf(bytes, (byte)'<'); if (p < 0) return result;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(new System.IO.StringReader(Encoding.UTF8.GetString(bytes, p, bytes.Length - p)), settings))
            {
                var doc = XDocument.Load(reader);
                foreach (var node in doc.Descendants())
                {
                    if (!int.TryParse(node.Attribute("id")?.Value, out var id)) continue;
                    string? name = node.Attribute("Name")?.Value; if (string.IsNullOrEmpty(name)) continue;
                    var entry = new CatalogEntry { Id = id, Name = name!, IsSketch = node.Name.LocalName == "Sketch" };
                    foreach (var dimension in node.Elements().Where(e => e.Name.LocalName == "Dimension"))
                    {
                        var dimensionName = dimension.Attribute("Name")?.Value;
                        if (!string.IsNullOrEmpty(dimensionName)) entry.Dimensions.Add((dimensionName!, dimension.Value));
                    }
                    result.Add(entry);
                }
            }
        }
        catch (XmlException) { /* Missing catalog does not turn partial binary content into a valid empty full parse. */ }
        return result;
    }
    private static bool Has(int offset, int count, int end) => offset >= 0 && count >= 0 && offset <= end && count <= end - offset;
    private static ushort U16(byte[] b, int p) => (ushort)(b[p] | b[p + 1] << 8);
    private static uint U32(byte[] b, int p) => (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24);
    private static int I32(byte[] b, int p) => unchecked((int)U32(b, p));
    private static double F64(byte[] b, int p)
    {
        if (BitConverter.IsLittleEndian) return BitConverter.ToDouble(b, p);
        var copy = new byte[8]; Array.Copy(b, p, copy, 0, 8); Array.Reverse(copy); return BitConverter.ToDouble(copy, 0);
    }
    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    private static double Distance(SketchPointModel a, SketchPointModel b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    private static byte[] Slice(byte[] bytes, int start, int end) { var result = new byte[end - start]; Array.Copy(bytes, start, result, 0, result.Length); return result; }
    private sealed class Header { public int Start; public int ContentStart; public int? Id; public string Name = string.Empty; public string ClassName = string.Empty; }
    private static double? LengthScale(LengthUnit? unit)
    {
        switch (unit)
        {
            case LengthUnit.Millimeter: return .001;
            case LengthUnit.Centimeter: return .01;
            case LengthUnit.Meter: return 1;
            case LengthUnit.Inch: return .0254;
            case LengthUnit.Foot: return .3048;
            case LengthUnit.Mil: return .0000254;
            case LengthUnit.Microinch: return .0000000254;
            case LengthUnit.Micron: return .000001;
            case LengthUnit.Nanometer: return 1e-9;
            case LengthUnit.Angstrom: return 1e-10;
            default: return null;
        }
    }
    private sealed class CatalogEntry { public int Id; public string Name = string.Empty; public bool IsSketch; public bool IsOrigin; public List<(string Name, string Value)> Dimensions = new List<(string, string)>(); }
}
