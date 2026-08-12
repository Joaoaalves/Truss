namespace Truss.Cli
{
    internal static class SourceEditor
    {
        /// <summary>
        /// Inserts a block above a scaffold marker, keeping the marker in place
        /// so later installs keep landing in the same region, in install order.
        /// Returns false when the file has no marker, so callers can fall back
        /// to the literal anchors of scaffolds that predate the markers.
        /// </summary>
        public static bool InsertAtMarker(string filePath, string marker, string block)
        {
            if (!File.Exists(filePath))
                return false;

            var content = File.ReadAllText(filePath);

            if (content.Contains(block.Trim()))
                return true;

            var index = content.IndexOf(marker, StringComparison.Ordinal);

            if (index < 0)
                return false;

            var lineStart = content.LastIndexOf('\n', index - 1 >= 0 ? index - 1 : 0) + 1;

            File.WriteAllText(filePath, content.Insert(lineStart, block + Environment.NewLine + Environment.NewLine));
            return true;
        }

        public static bool InsertBefore(string filePath, string anchor, string block)
        {
            return Insert(filePath, anchor, block, before: true);
        }

        public static bool InsertAfter(string filePath, string anchor, string block)
        {
            return Insert(filePath, anchor, block, before: false);
        }

        private static bool Insert(string filePath, string anchor, string block, bool before)
        {
            if (!File.Exists(filePath))
                return false;

            var content = File.ReadAllText(filePath);

            if (content.Contains(block.Trim()))
                return true;

            var index = content.IndexOf(anchor, StringComparison.Ordinal);

            if (index < 0)
                return false;

            var insertion = before
                ? block + Environment.NewLine + Environment.NewLine
                : string.Empty;

            var updated = before
                ? content.Insert(index, insertion)
                : content.Insert(index + anchor.Length, Environment.NewLine + Environment.NewLine + block);

            File.WriteAllText(filePath, updated);
            return true;
        }
    }
}
