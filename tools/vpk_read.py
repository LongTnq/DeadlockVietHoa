"""Doc VPK v2 (Source 2) va liet ke / trich xuat file trong 'resource/localization'."""
import struct
import sys

VPK_HEADER = 0x55AA1234


def read_vpk_dir(path):
    """Tra ve dict: duong dan -> (archive_index, entry_offset, entry_length)."""
    data = open(path, 'rb').read()
    if len(data) < 12:
        raise ValueError('file qua nho')

    magic, version, tree_size = struct.unpack('<III', data[:12])
    if magic != VPK_HEADER:
        raise ValueError(f'khong phai VPK v2: {magic:#x}')

    pos = 12
    entries = {}

    def read_string(p):
        end = data.index(b'\x00', p)
        return data[p:end].decode('utf-8', 'replace'), end + 1

    def walk(p, prefix):
        while p < 12 + tree_size:
            if p >= len(data):
                break
            # entry cuoi -> ket thuc cay
            if data[p:p + 4] == b'\xff\xff\xff\xff':
                return p + 4
            filename, p = read_string(p)
            if not filename:
                return p
            path_full = prefix + filename
            archive_idx, entry_offset, entry_length = struct.unpack(
                '<III', data[p:p + 12])
            p += 12
            if entry_length == 0xFFFFFFFF:
                # thu muc
                p = walk(p, path_full + '/')
            else:
                entries[path_full] = (archive_idx, entry_offset, entry_length)

    walk(pos, '')
    return entries, data


def main():
    dir_path = sys.argv[1]
    entries, dir_data = read_vpk_dir(dir_path)

    print(f'=== {dir_path} ===')
    print(f'tong so file: {len(entries)}')

    target = [k for k in entries if 'vietnamese' in k.lower()]
    print(f'\nfile vietnamese: {len(target)}')

    for k in sorted(target):
        archive_idx, offset, length = entries[k]
        print(f'  {k:60s} archive={archive_idx} off={offset} len={length}')


if __name__ == '__main__':
    main()
