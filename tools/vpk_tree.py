"""Doc cay file trong VPK v2 (Source 2) va tim file localization."""
import struct
import sys

VPK_MAGIC = 0x55AA1234


def parse_vpk_dir(path):
    data = open(path, 'rb').read()

    magic, version, tree_size = struct.unpack('<III', data[:12])
    if magic != VPK_MAGIC:
        raise ValueError(f'khong phai VPK v2: magic={magic:#x}')

    pos = 12
    tree_end = 12 + tree_size
    entries = {}

    def read_str(p):
        end = data.index(b'\x00', p)
        return data[p:end].decode('utf-8', 'replace'), end + 1

    def walk(p, prefix):
        while p < tree_end:
            if data[p:p + 4] == b'\xff\xff\xff\xff':
                return p + 4
            filename, p = read_str(p)
            if not filename:
                return p
            archive, offset, length = struct.unpack('<III', data[p:p + 12])
            p += 12
            full = prefix + filename
            if length == 0xFFFFFFFF:
                p = walk(p, full + '/')
            else:
                entries[full] = (archive, offset, length)
        return p

    walk(pos, '')
    return entries, data


def main():
    path = sys.argv[1]
    entries, _ = parse_vpk_dir(path)
    print(f'=== {path} ===')
    print(f'tong file: {len(entries)}')

    needle = sys.argv[2] if len(sys.argv) > 2 else 'localization'
    hits = sorted(k for k in entries if needle.lower() in k.lower())

    print(f'\nkhop "{needle}": {len(hits)}')
    for k in hits[:40]:
        print(f'  {k}')


if __name__ == '__main__':
    main()
