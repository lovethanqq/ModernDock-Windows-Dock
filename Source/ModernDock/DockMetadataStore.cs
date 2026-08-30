using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MyCustomDock
{
    // The legacy dock_config.txt format intentionally remains seven columns.
    // This small sidecar only carries information that cannot fit that format.
    [DataContract]
    public sealed class DockMetadataEntry
    {
        [DataMember(Order = 1)] public string IdentityKey { get; set; }
        [DataMember(Order = 2)] public string Title { get; set; }
        [DataMember(Order = 3)] public string TargetPath { get; set; }
        [DataMember(Order = 4)] public string IconFile { get; set; }
        [DataMember(Order = 5)] public string ShortcutSource { get; set; }
        [DataMember(Order = 6)] public bool PathMatchAuto { get; set; }
        [DataMember(Order = 7)] public bool ProcessNameMatchAuto { get; set; }
    }

    [DataContract]
    internal sealed class DockMetadataDocument
    {
        [DataMember(Order = 1)]
        public List<DockMetadataEntry> Items { get; set; }
    }

    public static class DockMetadataStore
    {
        public static IList<DockMetadataEntry> Load(string path)
        {
            var result = new List<DockMetadataEntry>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return result;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(DockMetadataDocument));
                using (var stream = File.OpenRead(path))
                {
                    var document = serializer.ReadObject(stream) as DockMetadataDocument;
                    if (document != null && document.Items != null)
                    {
                        foreach (var entry in document.Items)
                        {
                            if (entry != null) result.Add(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("metadata.load path=" + path, ex);
            }

            return result;
        }

        public static void Apply(string path, IList<DockItem> items)
        {
            try
            {
                IList<DockMetadataEntry> entries = Load(path);
                if (items == null || entries.Count == 0) return;

                foreach (DockItem item in items)
                {
                    DockMetadataEntry entry = FindEntry(entries, item);
                    if (entry == null) continue;
                    item.ShortcutSource = entry.ShortcutSource ?? string.Empty;
                    item.AutoDerivedPathMatch = entry.PathMatchAuto;
                    item.AutoDerivedProcessNameMatch = entry.ProcessNameMatchAuto;
                }
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("metadata.apply path=" + path, ex);
            }
        }

        public static void Save(string path, IList<DockItem> items)
        {
            if (!TrySave(path, items))
            {
                throw new IOException("Dock metadata could not be saved.");
            }
        }

        public static bool TrySave(string path, IList<DockItem> items)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                var entries = new List<DockMetadataEntry>();
                if (items != null)
                {
                    foreach (DockItem item in items)
                    {
                        if (item == null || !item.IsFixed) continue;
                        if (string.IsNullOrWhiteSpace(item.ShortcutSource) &&
                            !item.AutoDerivedPathMatch && !item.AutoDerivedProcessNameMatch) continue;

                        entries.Add(new DockMetadataEntry
                        {
                            IdentityKey = ApplicationIdentityResolver.GetFixedIdentityKey(item),
                            Title = item.Title ?? string.Empty,
                            TargetPath = item.TargetPath ?? string.Empty,
                            IconFile = item.IconFile ?? string.Empty,
                            ShortcutSource = item.ShortcutSource ?? string.Empty,
                            PathMatchAuto = item.AutoDerivedPathMatch,
                            ProcessNameMatchAuto = item.AutoDerivedProcessNameMatch
                        });
                    }
                }

                if (entries.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return true;
                }

                var document = new DockMetadataDocument { Items = entries };
                var serializer = new DataContractJsonSerializer(typeof(DockMetadataDocument));
                byte[] bytes;
                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, document);
                    bytes = stream.ToArray();
                }

                string json = Encoding.UTF8.GetString(bytes);
                if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
                AtomicFileWriter.WriteAllText(path, json, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("metadata.save path=" + path, ex);
                return false;
            }
        }

        private static DockMetadataEntry FindEntry(IList<DockMetadataEntry> entries, DockItem item)
        {
            if (entries == null || item == null) return null;
            string identityKey = ApplicationIdentityResolver.GetFixedIdentityKey(item);
            foreach (DockMetadataEntry entry in entries)
            {
                if (entry != null && string.Equals(entry.IdentityKey, identityKey, StringComparison.OrdinalIgnoreCase)) return entry;
            }

            // A user may have edited the target path outside the Dock. Keep a
            // conservative fallback for the common stable title/icon case.
            foreach (DockMetadataEntry entry in entries)
            {
                if (entry == null) continue;
                if (!string.IsNullOrWhiteSpace(entry.Title) &&
                    string.Equals(entry.Title, item.Title, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.IconFile ?? string.Empty, item.IconFile ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
