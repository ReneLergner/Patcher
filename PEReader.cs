// Author: Sergey Akopov

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace Patcher
{
    public class PEReader
    {
        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DOS_HEADER
        {
            public UInt16 e_magic;
            public UInt16 e_cblp;
            public UInt16 e_cp;
            public UInt16 e_crlc;
            public UInt16 e_cparhdr;
            public UInt16 e_minalloc;
            public UInt16 e_maxalloc;
            public UInt16 e_ss;
            public UInt16 e_sp;
            public UInt16 e_csum;
            public UInt16 e_ip;
            public UInt16 e_cs;
            public UInt16 e_lfarlc;
            public UInt16 e_ovno;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public UInt16[] e_res1;
            public UInt16 e_oemid;
            public UInt16 e_oeminfo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public UInt16[] e_res2;
            public UInt32 e_lfanew;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_NT_HEADERS
        {
            public UInt32 Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER32 OptionalHeader32;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader64;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER
        {
            public UInt16 Machine;
            public UInt16 NumberOfSections;
            public UInt32 TimeDateStamp;
            public UInt32 PointerToSymbolTable;
            public UInt32 NumberOfSymbols;
            public UInt16 SizeOfOptionalHeader;
            public UInt16 Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER32
        {
            public UInt16 Magic;
            public Byte MajorLinkerVersion;
            public Byte MinorLinkerVersion;
            public UInt32 SizeOfCode;
            public UInt32 SizeOfInitializedData;
            public UInt32 SizeOfUninitializedData;
            public UInt32 AddressOfEntryPoint;
            public UInt32 BaseOfCode;
            public UInt32 BaseOfData;
            public UInt32 ImageBase;
            public UInt32 SectionAlignment;
            public UInt32 FileAlignment;
            public UInt16 MajorOperatingSystemVersion;
            public UInt16 MinorOperatingSystemVersion;
            public UInt16 MajorImageVersion;
            public UInt16 MinorImageVersion;
            public UInt16 MajorSubsystemVersion;
            public UInt16 MinorSubsystemVersion;
            public UInt32 Win32VersionValue;
            public UInt32 SizeOfImage;
            public UInt32 SizeOfHeaders;
            public UInt32 CheckSum;
            public UInt16 Subsystem;
            public UInt16 DllCharacteristics;
            public UInt32 SizeOfStackReserve;
            public UInt32 SizeOfStackCommit;
            public UInt32 SizeOfHeapReserve;
            public UInt32 SizeOfHeapCommit;
            public UInt32 LoaderFlags;
            public UInt32 NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER64
        {
            public UInt16 Magic;
            public Byte MajorLinkerVersion;
            public Byte MinorLinkerVersion;
            public UInt32 SizeOfCode;
            public UInt32 SizeOfInitializedData;
            public UInt32 SizeOfUninitializedData;
            public UInt32 AddressOfEntryPoint;
            public UInt32 BaseOfCode;
            public UInt64 ImageBase;
            public UInt32 SectionAlignment;
            public UInt32 FileAlignment;
            public UInt16 MajorOperatingSystemVersion;
            public UInt16 MinorOperatingSystemVersion;
            public UInt16 MajorImageVersion;
            public UInt16 MinorImageVersion;
            public UInt16 MajorSubsystemVersion;
            public UInt16 MinorSubsystemVersion;
            public UInt32 Win32VersionValue;
            public UInt32 SizeOfImage;
            public UInt32 SizeOfHeaders;
            public UInt32 CheckSum;
            public UInt16 Subsystem;
            public UInt16 DllCharacteristics;
            public UInt64 SizeOfStackReserve;
            public UInt64 SizeOfStackCommit;
            public UInt64 SizeOfHeapReserve;
            public UInt64 SizeOfHeapCommit;
            public UInt32 LoaderFlags;
            public UInt32 NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY
        {
            public UInt32 VirtualAddress;
            public UInt32 Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Name;
            public Misc Misc;
            public UInt32 VirtualAddress;
            public UInt32 SizeOfRawData;
            public UInt32 PointerToRawData;
            public UInt32 PointerToRelocations;
            public UInt32 PointerToLinenumbers;
            public UInt16 NumberOfRelocations;
            public UInt16 NumberOfLinenumbers;
            public UInt32 Characteristics;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Misc
        {
            [FieldOffset(0)]
            public UInt32 PhysicalAddress;
            [FieldOffset(0)]
            public UInt32 VirtualSize;
        }

        #endregion

        #region Fields

        private readonly IMAGE_DOS_HEADER _dosHeader;
        private IMAGE_NT_HEADERS _ntHeaders;
        private readonly IList<IMAGE_SECTION_HEADER> _sectionHeaders = new List<IMAGE_SECTION_HEADER>();

        #endregion

        public PEReader(BinaryReader reader)
        {
            // Reset reader position, just in case
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            // Read MS-DOS header section
            _dosHeader = MarshalBytesTo<IMAGE_DOS_HEADER>(reader);

            // MS-DOS magic number should read 'MZ'
            if (_dosHeader.e_magic != 0x5a4d)
            {
                throw new InvalidOperationException("File is not a portable executable.");
            }

            // Skip MS-DOS stub and seek reader to NT Headers
            reader.BaseStream.Seek(_dosHeader.e_lfanew, SeekOrigin.Begin);

            // Read NT Headers
            _ntHeaders.Signature = MarshalBytesTo<UInt32>(reader);

            // Make sure we have 'PE' in the pe signature
            if (_ntHeaders.Signature != 0x4550)
            {
                throw new InvalidOperationException("Invalid portable executable signature in NT header.");
            }

            _ntHeaders.FileHeader = MarshalBytesTo<IMAGE_FILE_HEADER>(reader);

            // Read optional headers
            if (Is32bitAssembly())
            {
                Load32bitOptionalHeaders(reader);
            }
            else
            {
                Load64bitOptionalHeaders(reader);
            }

            // Read section data
            foreach (IMAGE_SECTION_HEADER header in _sectionHeaders)
            {
                // Skip to beginning of a section
                reader.BaseStream.Seek(header.PointerToRawData, SeekOrigin.Begin);

                // Read section data... and do something with it
                byte[] sectiondata = reader.ReadBytes((int)header.SizeOfRawData);
            }
        }

        public IMAGE_DOS_HEADER GetDOSHeader()
        {
            return _dosHeader;
        }

        public UInt32 GetPESignature()
        {
            return _ntHeaders.Signature;
        }

        public IMAGE_FILE_HEADER GetFileHeader()
        {
            return _ntHeaders.FileHeader;
        }

        public IMAGE_OPTIONAL_HEADER32 GetOptionalHeaders32()
        {
            return _ntHeaders.OptionalHeader32;
        }

        public IMAGE_OPTIONAL_HEADER64 GetOptionalHeaders64()
        {
            return _ntHeaders.OptionalHeader64;
        }

        public IList<IMAGE_SECTION_HEADER> GetSectionHeaders()
        {
            return _sectionHeaders;
        }

        public bool Is32bitAssembly()
        {
            return ((_ntHeaders.FileHeader.Characteristics & 0x0100) == 0x0100);
        }

        private void Load64bitOptionalHeaders(BinaryReader reader)
        {
            _ntHeaders.OptionalHeader64 = MarshalBytesTo<IMAGE_OPTIONAL_HEADER64>(reader);

            // Should have 10 data directories
            if (_ntHeaders.OptionalHeader64.NumberOfRvaAndSizes != 0x10)
            {
                throw new InvalidOperationException("Invalid number of data directories in NT header");
            }

            // Heathcliff74
            for (int i = 0; i < _ntHeaders.FileHeader.NumberOfSections; i++)
            {
                _sectionHeaders.Add(MarshalBytesTo<IMAGE_SECTION_HEADER>(reader));
            }
        }

        private void Load32bitOptionalHeaders(BinaryReader reader)
        {
            _ntHeaders.OptionalHeader32 = MarshalBytesTo<IMAGE_OPTIONAL_HEADER32>(reader);

            // Should have 10 data directories
            if (_ntHeaders.OptionalHeader32.NumberOfRvaAndSizes != 0x10)
            {
                throw new InvalidOperationException("Invalid number of data directories in NT header");
            }

            // Heathcliff74
            for (int i = 0; i < _ntHeaders.FileHeader.NumberOfSections; i++)
            {
                _sectionHeaders.Add(MarshalBytesTo<IMAGE_SECTION_HEADER>(reader));
            }
        }

        // Heathcliff74
        public UInt32 ConvertVirtualToRaw(UInt32 VirtualOffset)
        {
            IMAGE_SECTION_HEADER? SectionHeaderSelection = _sectionHeaders.Where(h => (((GetOptionalHeaders32().ImageBase + h.VirtualAddress) <= VirtualOffset) && ((GetOptionalHeaders32().ImageBase + h.VirtualAddress + h.SizeOfRawData) > VirtualOffset))).FirstOrDefault();
            if (SectionHeaderSelection == null)
                throw new ArgumentOutOfRangeException();

            IMAGE_SECTION_HEADER SectionHeader = (IMAGE_SECTION_HEADER)SectionHeaderSelection;

            if (string.IsNullOrEmpty(SectionHeader.Name) || (SectionHeader.SizeOfRawData == 0))
                throw new ArgumentOutOfRangeException();

            return SectionHeader.PointerToRawData + (VirtualOffset - SectionHeader.VirtualAddress - GetOptionalHeaders32().ImageBase);
        }

        private static T MarshalBytesTo<T>(BinaryReader reader)
        {
            // Unmanaged data
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

            // Create a pointer to the unmanaged data pinned in memory to be accessed by unmanaged code
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            // Use our previously created pointer to unmanaged data and marshal to the specified type
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            // Deallocate pointer
            handle.Free();

            return theStructure;
        }
    }
}
