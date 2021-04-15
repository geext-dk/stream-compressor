namespace StreamCompressor.Gzip
{
    public enum CheckGZipHeaderResult
    {
        Ok,
        EmptyStream,
        NotAGzipFile
    }
}