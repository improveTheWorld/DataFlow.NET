namespace iCode.Data
{
    public static class Read
    {
        public static IEnumerable<string /*line*/> text(StreamReader file, bool autoClose = true)
        {
            while (!file.EndOfStream)
            {
                yield return file.ReadLine();
            }

            if (autoClose) file.Close();
        }
        public static IEnumerable<string /*line*/> text(string path, bool autoClose = true)
        {
            return text(new StreamReader(path), autoClose);
        }

        public static IEnumerable<T /*scv_struct*/> csv<T>(string path, string separator = ";", bool autoClose = true) where T : struct
                                                        => Read.text(path, autoClose).csv<T>(separator);
    }
}
    