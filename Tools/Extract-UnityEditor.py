"""Read the signed Unity NSISBI archive without executing its installer.

Format reference: https://gist.github.com/Slaynash/57b3a911e4da092e2b2664046700fe7d
Only file records under Editor are extracted; no installer commands are executed.
"""
import argparse
import concurrent.futures
import lzma
import struct
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("installer", type=Path)
parser.add_argument("destination", type=Path)
parser.add_argument("--extract", action="store_true")
args = parser.parse_args()
root = args.destination.resolve()

def decoder(properties):
    code = properties[0]
    return lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[{
        "id": lzma.FILTER_LZMA1, "dict_size": struct.unpack_from("<I", properties, 1)[0],
        "lc": code % 9, "lp": (code // 9) % 5, "pb": code // 45,
    }])

with args.installer.open("rb") as source:
    prefix = source.read(1024 * 1024)
    position = prefix.find(b"\xef\xbe\xad\xdeNullsoftInst") - 4
    if position < 0:
        raise ValueError("NSIS signature not found")
    flags, = struct.unpack_from("<I", prefix, position)
    if flags & 0x30 == 0:
        raise ValueError("Expected NSISBI")
    expected_header, = struct.unpack_from("<I", prefix, position + 20)
    source.seek(position + 36)
    encoded_size, = struct.unpack("<I", source.read(4))
    if not encoded_size & 0x80000000:
        raise ValueError("Expected compressed non-solid header")
    header_size = encoded_size & 0x7fffffff
    compressed_header = source.read(header_size)
    header = decoder(compressed_header[:5]).decompress(compressed_header[5:])
    if len(header) != expected_header:
        raise ValueError("Header length mismatch")
    data_start = position + 40 + header_size

entries, count = struct.unpack_from("<II", header, 20)
strings, _ = struct.unpack_from("<II", header, 28)
variables = ["CMDLINE", "INSTDIR", "OUTDIR", "EXEDIR", "LANGUAGE", "TEMP", "PLUGINSDIR", "EXEPATH", "EXEFILE", "HWNDPARENT", "_CLICK", "_OUTDIR"]

def text(index):
    offset = strings + index * 2
    result = []
    while offset + 2 <= len(header):
        character, = struct.unpack_from("<H", header, offset)
        offset += 2
        if character == 0:
            return "".join(result)
        if character <= 4:
            value, = struct.unpack_from("<H", header, offset)
            offset += 2
            if character == 4:
                character = value
            elif character == 3:
                number = (value & 127) | ((value >> 8 & 127) << 7)
                result.append("$" + (variables[number - 20] if 20 <= number < 32 else str(number)))
                continue
            else:
                result.append("$UNRESOLVED")
                continue
        result.append(chr(character))
    raise ValueError("String extends outside header")

files = {}
directory = ""
saved_directory = ""
for index in range(count):
    command, *parameters = struct.unpack_from("<9I", header, entries + index * 36)
    if command == 27 and parameters[0] == 31:
        saved_directory = text(parameters[1]).replace("$_OUTDIR", saved_directory).replace("$OUTDIR", directory).replace("$INSTDIR", "")
    elif command == 11 and parameters[1]:
        directory = text(parameters[0]).replace("$_OUTDIR", saved_directory).replace("$OUTDIR", directory).replace("$INSTDIR", "").strip("\\/")
    elif command == 20:
        relative = (directory + "/" + text(parameters[1])).replace("\\", "/").lstrip("/")
        if not relative.startswith("Editor/"):
            continue
        destination = (root / relative).resolve()
        if not destination.is_relative_to(root):
            raise ValueError("File outside destination")
        files[relative] = (destination, data_start + parameters[2])

print(f"Found {len(files)} Editor files; header bytes {len(header)}", flush=True)
for name in list(files)[:12]:
    print(name, flush=True)
if not args.extract:
    raise SystemExit(0)
if "Editor/Unity.exe" not in files:
    raise ValueError("Unity.exe file record not found")

def extract(record):
    destination, offset = record
    destination.parent.mkdir(parents=True, exist_ok=True)
    with args.installer.open("rb") as source, destination.open("wb") as output:
        source.seek(offset)
        encoded, = struct.unpack("<I", source.read(4))
        remaining = encoded & 0x7fffffff
        compressed = bool(encoded & 0x80000000)
        inflater = None
        if compressed:
            inflater = decoder(source.read(5))
            remaining -= 5
        while remaining:
            block = source.read(min(1024 * 1024, remaining))
            if not block:
                raise ValueError("Archive ended early")
            remaining -= len(block)
            if not compressed:
                output.write(block)
                continue
            output.write(inflater.decompress(block, max_length=8 * 1024 * 1024))
            while not inflater.needs_input and not inflater.eof:
                output.write(inflater.decompress(b"", max_length=8 * 1024 * 1024))
    return destination

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    for index, path in enumerate(pool.map(extract, files.values()), 1):
        if index % 500 == 0:
            print(f"Extracted {index}/{len(files)}", flush=True)
print("Editor extraction complete", flush=True)
