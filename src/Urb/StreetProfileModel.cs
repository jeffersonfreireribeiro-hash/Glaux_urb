using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    public struct StreetWidthDomain
    {
        public double Minimum, Maximum;
        public double Min => Minimum;
        public double Max => Maximum;
        public bool IsFixed => Math.Abs(Maximum - Minimum) < 1e-9;

        public StreetWidthDomain(double minimum, double maximum)
        { Minimum = minimum; Maximum = maximum; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "[{0:F3}, {1:F3}]", Minimum, Maximum);
    }

    public struct WidthConstraint
    {
        public double Min, Max;
        public bool IsFixed => Math.Abs(Max - Min) < 1e-9;

        public WidthConstraint(double min, double max)
        { Min = min; Max = max; }

        public static implicit operator StreetWidthDomain(WidthConstraint c) => new StreetWidthDomain(c.Min, c.Max);
        public static implicit operator WidthConstraint(StreetWidthDomain d) => new WidthConstraint(d.Minimum, d.Maximum);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "[{0:F3}, {1:F3}] (Fixed={2})", Min, Max, IsFixed);
    }

    public sealed class StreetProfileElement
    {
        public Guid Id;
        public string ElementType;
        public string Type { get => ElementType; set => ElementType = value; } // Legacy code alias.
        public int Direction;
        public bool Required;
        public StreetWidthDomain WidthDomain;
        public WidthConstraint WidthConstraint
        {
            get => WidthDomain;
            set => WidthDomain = value;
        }
        public bool IsFixed => WidthDomain.IsFixed;
        public double MinimumWidth { get => WidthDomain.Minimum; set => WidthDomain.Minimum = value; }
        public double MaximumWidth { get => WidthDomain.Maximum; set => WidthDomain.Maximum = value; }
        public double PreferredWidth, FittedWidth; // PreferredWidth is retained for legacy callers.
        public string Status, Reason;
        public StreetProfileElement Copy() => (StreetProfileElement)MemberwiseClone();
        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0} | Id={1} | Direction={2} | WidthDomain={3} | Preferred={4:F3} | Width={5:F3} | Required={6} | Status={7} | Reason={8}",
            Type, Id, Direction, WidthDomain, PreferredWidth, FittedWidth, Required, Status ?? "NOMINAL", Reason ?? "NONE");
    }

    public sealed class StreetProfile
    {
        public string Street, StreetType;
        public string StreetName { get => Street; set => Street = value; }
        public string Type { get => StreetType; set => StreetType = value; } // Legacy code alias.
        public List<StreetProfileElement> Elements = new List<StreetProfileElement>();
        public StreetProfile Copy() => new StreetProfile { Street = Street, StreetType = StreetType,
            Elements = Elements.Select(x => x.Copy()).ToList() };
        public override string ToString() => (StreetName ?? "Rua") + " | " + (StreetType ?? "Custom") + " | " +
            string.Join(" > ", Elements.Select(x => x.Type + (x.Direction > 0 ? " →" : x.Direction < 0 ? " ←" : "")));
    }

    public static class StreetProfileTypeValidator
    {
        public static IEnumerable<string> Validate(StreetProfile profile)
        {
            if (profile == null || profile.Elements == null) yield break;
            string streetType = (profile.StreetType ?? "").Trim();
            bool forward = profile.Elements.Any(e => e.Type == "Lane" && e.Direction > 0);
            bool backward = profile.Elements.Any(e => e.Type == "Lane" && e.Direction < 0);
            if (streetType.Equals("One Way", StringComparison.OrdinalIgnoreCase) && forward && backward)
                yield return "PROFILE_TYPE_MISMATCH: Street Type 'One Way' contains Lane elements in both directions.";
            if (streetType.Equals("Pedestrian", StringComparison.OrdinalIgnoreCase) &&
                profile.Elements.Any(e => e.Type == "Lane"))
                yield return "PROFILE_TYPE_MISMATCH: Street Type 'Pedestrian' contains a Lane element.";
        }
    }

    public sealed class StreetProfileFitResult
    {
        public bool Success;
        public string Reason;
        public double Available, MinimumRequired, MaximumDesired, Deficit, Excess, LeftWidth, RoadWidth, RightWidth;
        public StreetProfile Profile;
        public string Diagnostic => string.Format(CultureInfo.InvariantCulture,
            "{0} | AvailableWidth={1:F3} | MinimumRequired={2:F3} | MaximumDesired={3:F3} | Deficit={4:F3} | Excess={5:F3} | Reason={6}",
            Success ? "PASS" : "PROFILE_CONFLICT", Available, MinimumRequired, MaximumDesired, Deficit, Excess, Reason ?? "NONE");
    }

    public static class StreetProfileElementFitter
    {
        public static bool IsRoadDomain(string kind) => kind == "Road" || kind == "Lane" ||
            kind == "Median" || kind == "Pedestrian" || kind == "Cycle Track" ||
            kind == "Parking" || kind == "Transit" || kind == "Shoulder";

        public static StreetProfileFitResult Fit(StreetProfile source, double available)
        {
            var result = new StreetProfileFitResult { Available = available };
            if (source == null || source.Elements == null || source.Elements.Count == 0 ||
                double.IsNaN(available) || double.IsInfinity(available) || available <= 0)
                return Fail(result, "INVALID_PROFILE_OR_SECTION", 0);
            var profile = source.Copy();
            result.Profile = profile;
            int firstRoad = profile.Elements.FindIndex(x => IsRoadDomain(x.Type));
            int lastRoad = profile.Elements.FindLastIndex(x => IsRoadDomain(x.Type));
            if (firstRoad < 0) return Fail(result, "ROAD_DOMAIN_REQUIRED", 0);
            foreach (var e in profile.Elements)
            {
                // Definitions made before WidthDomain existed used PreferredWidth as
                // their upper bound. Normalize only this legacy representation.
                if (e.MaximumWidth == 0 && e.PreferredWidth > 0)
                    e.MaximumWidth = e.PreferredWidth;
                if (string.IsNullOrWhiteSpace(e.Type) || double.IsNaN(e.MaximumWidth) ||
                    double.IsInfinity(e.MaximumWidth) || double.IsNaN(e.MinimumWidth) ||
                    double.IsInfinity(e.MinimumWidth) || double.IsNaN(e.PreferredWidth) ||
                    double.IsInfinity(e.PreferredWidth) || e.PreferredWidth < 0 ||
                    e.MinimumWidth < 0 || e.MinimumWidth > e.MaximumWidth ||
                    (e.Required && e.MinimumWidth <= 0))
                    return Fail(result, "INVALID_WIDTH_DOMAIN", 0);
                e.FittedWidth = e.MaximumWidth;
                e.Status = !e.Required && e.MaximumWidth <= 1e-8 ? "SUPPRESSED" : "ACTIVE";
                e.Reason = e.Status == "SUPPRESSED" ? "ZERO_WIDTH_DOMAIN" : null;
            }
            result.MinimumRequired = profile.Elements.Sum(e => e.Required ? e.MinimumWidth : 0);
            result.MaximumDesired = profile.Elements.Sum(e => e.MaximumWidth);
            if (result.MinimumRequired > available + 1e-8)
                return Fail(result, "INSUFFICIENT_PUBLIC_WIDTH", result.MinimumRequired - available);
            if (available > result.MaximumDesired + 1e-8)
            { result.Excess = available - result.MaximumDesired; return Fail(result, "EXCESS_WIDTH", 0); }
            double deficit = Math.Max(0, result.MaximumDesired - available);
            // Shrink optional bands only to their minima while active.
            foreach (var e in profile.Elements.Where(x => !x.Required))
            {
                double take = Math.Min(deficit, e.FittedWidth - e.MinimumWidth);
                e.FittedWidth -= take; deficit -= take;
                if (take > 1e-8)
                { e.Status = e.FittedWidth <= 1e-8 ? "SUPPRESSED" : "REDUCED";
                  e.Reason = "INSUFFICIENT_WIDTH"; }
            }
            // Protect required minima; reduce road extras before sidewalk extras.
            var order = profile.Elements.Where(x => x.Required && x.Type != "Sidewalk")
                .Concat(profile.Elements.Where(x => x.Required && x.Type == "Sidewalk"));
            foreach (var e in order)
            {
                double take = Math.Min(deficit, e.FittedWidth - e.MinimumWidth);
                e.FittedWidth -= take; deficit -= take;
                if (take > 1e-8) { e.Status = "REDUCED"; e.Reason = "INSUFFICIENT_WIDTH"; }
            }
            // Optional elements may be removed entirely, but never left active below min.
            foreach (var e in profile.Elements.Where(x => !x.Required && x.FittedWidth > 1e-8))
            {
                if (deficit <= 1e-8) break;
                deficit -= e.FittedWidth; e.FittedWidth = 0;
                e.Status = "SUPPRESSED"; e.Reason = "INSUFFICIENT_WIDTH";
            }
            if (deficit > 1e-7) return Fail(result, "INSUFFICIENT_PUBLIC_WIDTH", deficit);
            double surplus = available - profile.Elements.Sum(e => e.FittedWidth);
            if (surplus > 1e-8)
            {
                // A discrete optional suppression may leave room. Refill required
                // elements only up to their maxima; never violate a domain.
                foreach (var e in profile.Elements.Where(x => x.Required && x.Type == "Sidewalk")
                    .Concat(profile.Elements.Where(x => x.Required && x.Type != "Sidewalk")))
                {
                    double add = Math.Min(surplus, e.MaximumWidth - e.FittedWidth);
                    if (add <= 1e-8) continue;
                    e.FittedWidth += add; surplus -= add;
                    e.Status = "EXPANDED"; e.Reason = "WIDTH_AFTER_OPTIONAL_SUPPRESSION";
                }
                if (surplus > 1e-7)
                { result.Excess = surplus; return Fail(result, "WIDTH_DOMAIN_GAP", 0); }
            }
            result.LeftWidth = profile.Elements.Take(firstRoad).Sum(e => e.FittedWidth);
            result.RightWidth = profile.Elements.Skip(lastRoad + 1).Sum(e => e.FittedWidth);
            result.RoadWidth = available - result.LeftWidth - result.RightWidth;
            if (result.RoadWidth <= 1e-8) return Fail(result, "ROAD_DOMAIN_COLLAPSED", 0);
            result.Success = true;
            return result;
        }

        private static StreetProfileFitResult Fail(StreetProfileFitResult r, string reason, double deficit)
        { r.Reason = reason; r.Deficit = Math.Max(0, deficit); return r; }
    }

    // =========================================================================
    // STREET PROFILE ASSIGNMENT MODEL & MATCHING SERVICE
    // =========================================================================

    public static class ProfileMatchStatus
    {
        public const string MATCHED = "MATCHED";
        public const string UNMATCHED_STREET = "UNMATCHED_STREET";
        public const string AMBIGUOUS_PROFILE_MATCH = "AMBIGUOUS_PROFILE_MATCH";
    }

    public static class StreetNameNormalizer
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string[] tokens = name.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tokens).ToUpperInvariant();
        }

        public static bool Matches(string a, string b, bool normalized = true)
        {
            if (a == null || b == null) return false;
            if (normalized)
                return string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
            return string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);
        }
    }

    public sealed class ProfiledStreet
    {
        public string StreetID { get; set; }
        public string StreetName { get; set; }
        public Curve SourceGeometry { get; set; }
        public StreetProfile StreetProfile { get; set; }
        public string MatchInfo { get; set; }
        public int FeatureIndex { get; set; }
        public string SourceId { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string TreePath { get; set; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "ProfiledStreet: '{0}' (ID={1}) | Match={2} | Profile={3} | Length={4:F2}m",
            StreetName ?? "Unnamed", StreetID ?? "None", MatchInfo ?? "NONE",
            StreetProfile != null ? (StreetProfile.StreetName ?? StreetProfile.Street) : "NONE",
            SourceGeometry != null && SourceGeometry.IsValid ? SourceGeometry.GetLength() : 0.0);
    }

    public sealed class ProfiledSection
    {
        public int StreetPathIndex, StationIndex;
        public string StreetID, StreetName, Reason;
        public bool IsRequired;
        public Point3d[] Points;
        public StreetProfile StreetProfile;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "ProfiledSection {{{0};{1}}} | StreetID={2} | StreetName={3} | Profile={4} | Required={5}",
            StreetPathIndex,StationIndex,StreetID,StreetName,
            StreetProfile?.StreetName ?? "NONE",IsRequired);
    }

    public sealed class RawGisStreetItem
    {
        public Curve Geometry;
        public string Name;
        public string SourceId;
        public int FeatureIndex;
        public Dictionary<string, object> Attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string TreePath;
    }

    public sealed class StreetProfileAssignmentResult
    {
        public List<ProfiledStreet> Profiled = new List<ProfiledStreet>();
        public List<Curve> MatchedCurves = new List<Curve>();
        public List<Curve> UnmatchedCurves = new List<Curve>();
        public List<Curve> AmbiguousCurves = new List<Curve>();
        public List<RawGisStreetItem> UnmatchedItems = new List<RawGisStreetItem>();
        public List<RawGisStreetItem> AmbiguousItems = new List<RawGisStreetItem>();
        public List<string> Diagnostics = new List<string>();
        public int TotalFeatures;
        public int MatchedCount;
        public int UnmatchedCount;
        public int AmbiguousCount;

        public string Report => string.Format(CultureInfo.InvariantCulture,
            "Street Profile Assignment Report: Total={0} | Matched={1} | Unmatched={2} | Ambiguous={3}\n{4}",
            TotalFeatures, MatchedCount, UnmatchedCount, AmbiguousCount,
            string.Join("\n", Diagnostics.Take(50)) + (Diagnostics.Count > 50 ? $"\n... (+{Diagnostics.Count - 50} mais)" : ""));
    }

    public static class StreetProfileAssignmentService
    {
        public static bool AreProfilesEquivalent(StreetProfile a, StreetProfile b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            if (!string.Equals(a.StreetType ?? "", b.StreetType ?? "", StringComparison.OrdinalIgnoreCase))
                return false;

            var elemsA = a.Elements ?? new List<StreetProfileElement>();
            var elemsB = b.Elements ?? new List<StreetProfileElement>();
            if (elemsA.Count != elemsB.Count) return false;

            for (int i = 0; i < elemsA.Count; i++)
            {
                var ea = elemsA[i];
                var eb = elemsB[i];
                if (ea == null && eb == null) continue;
                if (ea == null || eb == null) return false;

                // Equal widths do not make independently defined profiles the same
                // identity. Copies retain ElementIDs; competing definitions do not.
                if (ea.Id != eb.Id) return false;

                if (!string.Equals(ea.Type ?? "", eb.Type ?? "", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (ea.Direction != eb.Direction)
                    return false;
                if (ea.Required != eb.Required)
                    return false;
                if (Math.Abs(ea.MinimumWidth - eb.MinimumWidth) > 1e-4)
                    return false;
                if (Math.Abs(ea.MaximumWidth - eb.MaximumWidth) > 1e-4)
                    return false;
            }

            return true;
        }

        public static StreetProfileAssignmentResult Assign(
            IEnumerable<RawGisStreetItem> gisItems,
            IEnumerable<StreetProfile> profiles,
            bool normalized = true)
        {
            var result = new StreetProfileAssignmentResult();
            var profileList = (profiles ?? Enumerable.Empty<StreetProfile>()).Where(p => p != null).ToList();
            var itemList = (gisItems ?? Enumerable.Empty<RawGisStreetItem>()).Where(i => i != null).ToList();
            result.TotalFeatures = itemList.Count;

            // Check if there are profiles with identical names (ambiguous within the profile set)
            var profileGroups = new Dictionary<string, List<StreetProfile>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in profileList)
            {
                string key = normalized ? StreetNameNormalizer.Normalize(p.StreetName) : (p.StreetName ?? "").Trim();
                if (string.IsNullOrEmpty(key)) continue;
                if (!profileGroups.TryGetValue(key, out var list))
                {
                    list = new List<StreetProfile>();
                    profileGroups[key] = list;
                }
                // Evita tratar cópias idênticas/equivalentes do mesmo perfil como ambiguidade concorrente
                if (!list.Any(existing => AreProfilesEquivalent(existing, p)))
                {
                    list.Add(p);
                }
            }

            // Identifica se há um perfil padrão/coringa (*) para fallback de vias não nomeadas ou sem match direto
            var defaultProfile = profileList.FirstOrDefault(p =>
                string.Equals(p.StreetName, "*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "DEFAULT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "PADRAO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "PADRÃO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "RUA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "VIAS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.StreetName, "LOGRADOURO", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(p.StreetName));
            // Map each GIS item
            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                string streetName = item.Name ?? "";
                string matchKey = normalized ? StreetNameNormalizer.Normalize(streetName) : streetName.Trim();

                if (string.IsNullOrEmpty(matchKey))
                {
                    if (defaultProfile != null)
                    {
                        var profiledDefault = new ProfiledStreet
                        {
                            StreetID = item.SourceId ?? $"Street_{i}",
                            StreetName = string.IsNullOrWhiteSpace(streetName) ? defaultProfile.StreetName : streetName,
                            SourceGeometry = item.Geometry,
                            StreetProfile = defaultProfile,
                            MatchInfo = "DEFAULT_PROFILE_FALLBACK",
                            FeatureIndex = item.FeatureIndex,
                            SourceId = item.SourceId,
                            Attributes = item.Attributes != null ? new Dictionary<string, object>(item.Attributes, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object>(),
                            TreePath = item.TreePath
                        };
                        result.Profiled.Add(profiledDefault);
                        result.MatchedCount++;
                        if (item.Geometry != null) result.MatchedCurves.Add(item.Geometry);
                        result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                            "[{0}] ID={1} | Name='{2}' -> MATCH: Perfil padrão '{3}' atribuído por fallback (nome vazio no GIS).",
                            i, item.SourceId ?? i.ToString(), streetName, defaultProfile.StreetName));
                        continue;
                    }

                    result.UnmatchedCount++;
                    result.UnmatchedItems.Add(item);
                    if (item.Geometry != null) result.UnmatchedCurves.Add(item.Geometry);
                    result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                        "[{0}] ID={1} | Name='{2}' -> {3}: Nome de logradouro vazio.",
                        i, item.SourceId ?? i.ToString(), streetName, ProfileMatchStatus.UNMATCHED_STREET));
                    continue;
                }

                if (!profileGroups.TryGetValue(matchKey, out var candidateProfiles) || candidateProfiles.Count == 0)
                {
                    // Tenta correspondência pelo tipo de via (ex: TIPO='Rua' correspondendo a perfil 'Rua')
                    if (item.Attributes != null && item.Attributes.TryGetValue("TIPO", out var tipoVal) && tipoVal != null)
                    {
                        string tipoKey = normalized ? StreetNameNormalizer.Normalize(tipoVal.ToString()) : tipoVal.ToString().Trim();
                        if (!string.IsNullOrEmpty(tipoKey) && profileGroups.TryGetValue(tipoKey, out var tipoProfiles) && tipoProfiles.Count > 0)
                        {
                            candidateProfiles = tipoProfiles;
                        }
                    }
                }

                if (candidateProfiles == null || candidateProfiles.Count == 0)
                {
                    if (defaultProfile != null)
                    {
                        var profiledFallback = new ProfiledStreet
                        {
                            StreetID = item.SourceId ?? $"Street_{i}",
                            StreetName = streetName,
                            SourceGeometry = item.Geometry,
                            StreetProfile = defaultProfile,
                            MatchInfo = "DEFAULT_PROFILE_FALLBACK",
                            FeatureIndex = item.FeatureIndex,
                            SourceId = item.SourceId,
                            Attributes = item.Attributes != null ? new Dictionary<string, object>(item.Attributes, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object>(),
                            TreePath = item.TreePath
                        };
                        result.Profiled.Add(profiledFallback);
                        result.MatchedCount++;
                        if (item.Geometry != null) result.MatchedCurves.Add(item.Geometry);
                        result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                            "[{0}] ID={1} | Name='{2}' -> MATCH: Perfil padrão '{3}' atribuído por fallback.",
                            i, item.SourceId ?? i.ToString(), streetName, defaultProfile.StreetName));
                        continue;
                    }

                    result.UnmatchedCount++;
                    result.UnmatchedItems.Add(item);
                    if (item.Geometry != null) result.UnmatchedCurves.Add(item.Geometry);
                    result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                        "[{0}] ID={1} | Name='{2}' -> {3}: Nenhum perfil correspondente encontrado.",
                        i, item.SourceId ?? i.ToString(), streetName, ProfileMatchStatus.UNMATCHED_STREET));
                    continue;
                }

                if (candidateProfiles.Count > 1)
                {
                    result.AmbiguousCount++;
                    result.AmbiguousItems.Add(item);
                    if (item.Geometry != null) result.AmbiguousCurves.Add(item.Geometry);
                    result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                        "[{0}] ID={1} | Name='{2}' -> {3}: {4} perfis competem pelo mesmo nome ('{5}').",
                        i, item.SourceId ?? i.ToString(), streetName, ProfileMatchStatus.AMBIGUOUS_PROFILE_MATCH,
                        candidateProfiles.Count, string.Join("', '", candidateProfiles.Select(p => p.StreetName))));
                    continue;
                }

                // Exactly one profile match!
                var matchedProfile = candidateProfiles[0];
                var profiled = new ProfiledStreet
                {
                    StreetID = item.SourceId ?? $"Street_{i}",
                    StreetName = item.Name,
                    SourceGeometry = item.Geometry,
                    StreetProfile = matchedProfile,
                    MatchInfo = normalized ? "NORMALIZED_MATCH" : "EXACT_MATCH",
                    FeatureIndex = item.FeatureIndex,
                    SourceId = item.SourceId,
                    Attributes = item.Attributes != null ? new Dictionary<string, object>(item.Attributes, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object>(),
                    TreePath = item.TreePath
                };

                result.Profiled.Add(profiled);
                result.MatchedCount++;
                if (item.Geometry != null) result.MatchedCurves.Add(item.Geometry);
                result.Diagnostics.Add(string.Format(CultureInfo.InvariantCulture,
                    "[{0}] ID={1} | Name='{2}' -> {3} com Perfil='{4}' (Tipo='{5}', {6} faixas).",
                    i, profiled.StreetID, streetName, ProfileMatchStatus.MATCHED,
                    matchedProfile.StreetName, matchedProfile.StreetType, matchedProfile.Elements.Count));
            }

            return result;
        }
    }
}
