namespace Truss.Cli
{
    internal static class SourceEditor
    {
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
