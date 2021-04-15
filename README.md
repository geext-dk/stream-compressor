# StreamCompressor
Parallel implementation of a file compressor/decompressor using gzip.

## Requirements
.NET 5 is required to run these projects

## Usage
dotnet run compress/decompress <INPUT_PATH> <OUTPUT_PATH>

## Compression
A file is divided to blocks of a fixed size (1MB), and these blocks are compressed independently. Then these compressed blocks are written to the
destination file in the original order.

Actually, compress files only by a small amount. I guess a greater size of blocks is requried for the algorithm to work effectively.

## Decompression
A gzip file is itself can be a concatenation of multiple gzip files. Actually result of the `compress` command also produces a single file consisting
of multiple gzip files.

When decompressing, all these gzip files are searched by their headers (magic numbers and some fixed bytes like algorithm, reserved flags etc)
and decompressed simultaneously, and then written to the output file in the original order.
