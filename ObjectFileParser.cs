// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

// This code is based on Get-ObjDump from Matthew Graeber (@mattifestation)

using System;
using System.IO;
using System.Text;
using System.Linq;

namespace COFF
{
    public enum Machine : ushort
    {
        UNKNOWN = 0,
        I386 = 0x014C,  // Intel 386.
        R3000 = 0x0162,  // MIPS little-endian =0x160 big-endian
        R4000 = 0x0166,  // MIPS little-endian
        R10000 = 0x0168,  // MIPS little-endian
        WCEMIPSV2 = 0x0169,  // MIPS little-endian WCE v2
        ALPHA = 0x0184,  // Alpha_AXP
        SH3 = 0x01A2,  // SH3 little-endian
        SH3DSP = 0x01A3,
        SH3E = 0x01A4,  // SH3E little-endian
        SH4 = 0x01A6,  // SH4 little-endian
        SH5 = 0x01A8,  // SH5
        ARM = 0x01C0,  // ARM Little-Endian
        THUMB = 0x01C2,
        ARMV7 = 0x01C4,  // ARM Thumb-2 Little-Endian
        AM33 = 0x01D3,
        POWERPC = 0x01F0,  // IBM PowerPC Little-Endian
        POWERPCFP = 0x01F1,
        IA64 = 0x0200,  // Intel 64
        MIPS16 = 0x0266,  // MIPS
        ALPHA64 = 0x0284,  // ALPHA64
        MIPSFPU = 0x0366,  // MIPS
        MIPSFPU16 = 0x0466,  // MIPS
        AXP64 = ALPHA64,
        TRICORE = 0x0520,  // Infineon
        CEF = 0x0CEF,
        EBC = 0x0EBC,  // EFI public byte Code
        AMD64 = 0x8664,  // AMD64 (K8)
        M32R = 0x9041,  // M32R little-endian
        ARM64 = 0xAA64,  // ARMv8 in 64-bit mode
        CEE = 0xC0EE
    }

    [Flags]
    public enum CoffHeaderCharacteristics : ushort
    {
        RELOCS_STRIPPED = 0x0001,  // Relocation info stripped from file.
        EXECUTABLE_IMAGE = 0x0002,  // File is executable  (i.e. no unresolved external references).
        LINE_NUMS_STRIPPED = 0x0004,  // Line nunbers stripped from file.
        LOCAL_SYMS_STRIPPED = 0x0008,  // Local symbols stripped from file.
        AGGRESIVE_WS_TRIM = 0x0010,  // Agressively trim working set
        LARGE_ADDRESS_AWARE = 0x0020,  // App can handle >2gb addresses
        REVERSED_LO = 0x0080,  // public bytes of machine public ushort are reversed.
        BIT32_MACHINE = 0x0100,  // 32 bit public ushort machine.
        DEBUG_STRIPPED = 0x0200,  // Debugging info stripped from file in .DBG file
        REMOVABLE_RUN_FROM_SWAP = 0x0400,  // If Image is on removable media =copy and run from the swap file.
        NET_RUN_FROM_SWAP = 0x0800,  // If Image is on Net =copy and run from the swap file.
        SYSTEM = 0x1000,  // System File.
        DLL = 0x2000,  // File is a DLL.
        UP_SYSTEM_ONLY = 0x4000,  // File should only be run on a UP machine
        REVERSED_HI = 0x8000   // public bytes of machine public ushort are reversed.
    }

    public class HEADER
    {
        public Machine Machine;
        public ushort NumberOfSections;
        public DateTime TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public CoffHeaderCharacteristics Characteristics;

        public HEADER(BinaryReader br)
        {
            this.Machine = (Machine)br.ReadUInt16();
            this.NumberOfSections = br.ReadUInt16();
            this.TimeDateStamp = (new DateTime(1970, 1, 1, 0, 0, 0)).AddSeconds(br.ReadUInt32());
            this.PointerToSymbolTable = br.ReadUInt32();
            this.NumberOfSymbols = br.ReadUInt32();
            this.SizeOfOptionalHeader = br.ReadUInt16();
            this.Characteristics = (CoffHeaderCharacteristics)br.ReadUInt16();
        }
    }

    [Flags]
    public enum SectionHeaderCharacteristics : uint
    {
        TYPE_NO_PAD = 0x00000008,  // Reserved.
        CNT_CODE = 0x00000020,  // Section contains code.
        CNT_INITIALIZED_DATA = 0x00000040,  // Section contains initialized data.
        CNT_UNINITIALIZED_DATA = 0x00000080,  // Section contains uninitialized data.
        LNK_INFO = 0x00000200,  // Section contains comments or some other type of information.
        LNK_REMOVE = 0x00000800,  // Section contents will not become part of image.
        LNK_COMDAT = 0x00001000,  // Section contents comdat.
        NO_DEFER_SPEC_EXC = 0x00004000,  // Reset speculative exceptions handling bits in the TLB entries for this section.
        GPREL = 0x00008000,  // Section content can be accessed relative to GP
        MEM_FARDATA = 0x00008000,
        MEM_PURGEABLE = 0x00020000,
        MEM_16BIT = 0x00020000,
        MEM_LOCKED = 0x00040000,
        MEM_PRELOAD = 0x00080000,
        ALIGN_1BYTES = 0x00100000,
        ALIGN_2BYTES = 0x00200000,
        ALIGN_4BYTES = 0x00300000,
        ALIGN_8BYTES = 0x00400000,
        ALIGN_16BYTES = 0x00500000,  // Default alignment if no others are specified.
        ALIGN_32BYTES = 0x00600000,
        ALIGN_64BYTES = 0x00700000,
        ALIGN_128BYTES = 0x00800000,
        ALIGN_256BYTES = 0x00900000,
        ALIGN_512BYTES = 0x00A00000,
        ALIGN_1024BYTES = 0x00B00000,
        ALIGN_2048BYTES = 0x00C00000,
        ALIGN_4096BYTES = 0x00D00000,
        ALIGN_8192BYTES = 0x00E00000,
        ALIGN_MASK = 0x00F00000,
        LNK_NRELOC_OVFL = 0x01000000,  // Section contains extended relocations.
        MEM_DISCARDABLE = 0x02000000,  // Section can be discarded.
        MEM_NOT_CACHED = 0x04000000,  // Section is not cachable.
        MEM_NOT_PAGED = 0x08000000,  // Section is not pageable.
        MEM_SHARED = 0x10000000,  // Section is shareable.
        MEM_EXECUTE = 0x20000000,  // Section is executable.
        MEM_READ = 0x40000000,  // Section is readable.
        MEM_WRITE = 0x80000000   // Section is writeable.
    }

    public enum AMD64RelocationType : ushort
    {
        ABSOLUTE,
        ADDR64,
        ADDR32,
        ADDR32NB,
        REL32,
        REL32_1,
        REL32_2,
        REL32_3,
        REL32_4,
        REL32_5,
        SECTION,
        SECREL,
        SECREL7,
        TOKEN,
        SREL32,
        PAIR,
        SSPAN32
    }

    public enum ARMRelocationType : ushort
    {
        ABSOLUTE,
        ADDR32,
        ADDR32NB,
        BRANCH24,
        BRANCH11,
        TOKEN,
        BLX24 = 0x08,
        BLX11 = 0x09,
        SECTION = 0x0E,
        SECREL = 0x0F,
        MOV32A = 0x10,
        MOV32T = 0x11,
        BRANCH20T = 0x12,
        BRANCH24T = 0x14,
        BLX23T = 0x15
    }

    public enum ARMv8RelocationType : ushort
    {
        ABSOLUTE,
        ADDR32,
        ADDR32NB,
        BRANCH26,
        PAGEBASE_REL21,
        REL21,
        PAGEOFFSET_12A,
        PAGEOFFSET_12L,
        SECREL,
        SECREL_LOW12A,
        SECREL_HIGH12A,
        SECREL_LOW12L,
        TOKEN,
        SECTION,
        ADDR64
    }

    public enum X86RelocationType : ushort
    {
        ABSOLUTE,
        DIR16,
        DIR32 = 0x06,
        DIR32NB = 0x07,
        SEG12 = 0x09,
        SECTION = 0x0A,
        SECREL = 0x0B,
        TOKEN = 0x0C,
        SECREL7 = 0x0D,
        REL32 = 0x14
    }

    public class RelocationEntry
    {
        public uint VirtualAddress;
        public uint SymbolTableIndex;
        public Enum Type;
        public string Name;

        public RelocationEntry(BinaryReader br)
        {
            this.VirtualAddress = br.ReadUInt32();
            this.SymbolTableIndex = br.ReadUInt32();
            // Default to X86RelocationType. This will be changed once the processor type is determined
            this.Type = (X86RelocationType)br.ReadUInt16();
        }
    }

    public class SECTION_HEADER
    {
        public string Name;
        public uint PhysicalAddress;
        public uint VirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public SectionHeaderCharacteristics Characteristics;
        public Byte[] RawData;
        public RelocationEntry[] Relocations;

        public SECTION_HEADER(BinaryReader br)
        {
            this.Name = Encoding.UTF8.GetString(br.ReadBytes(8)).Split((Char)0)[0];
            this.PhysicalAddress = br.ReadUInt32();
            this.VirtualSize = this.PhysicalAddress;
            this.VirtualAddress = br.ReadUInt32();
            this.SizeOfRawData = br.ReadUInt32();
            this.PointerToRawData = br.ReadUInt32();
            this.PointerToRelocations = br.ReadUInt32();
            this.PointerToLinenumbers = br.ReadUInt32();
            this.NumberOfRelocations = br.ReadUInt16();
            this.NumberOfLinenumbers = br.ReadUInt16();
            this.Characteristics = (SectionHeaderCharacteristics)br.ReadUInt32();
        }
    }

    public enum SectionNumber : short
    {
        UNDEFINED,
        ABSOLUTE = -1,
        DEBUG = -2
    }

    [Flags]
    public enum TypeClass : short
    {
        TYPE_NULL,
        TYPE_VOID,
        TYPE_CHAR,
        TYPE_SHORT,
        TYPE_INT,
        TYPE_LONG,
        TYPE_FLOAT,
        TYPE_DOUBLE,
        TYPE_STRUCT,
        TYPE_UNION,
        TYPE_ENUM,
        TYPE_MOE,
        TYPE_BYTE,
        TYPE_WORD,
        TYPE_UINT,
        TYPE_DWORD,
        DTYPE_POINTER = 0x100,
        DTYPE_FUNCTION = 0x200,
        DTYPE_ARRAY = 0x300,
        DTYPE_NULL = 0x400 // Technically, this is defined as 0 in the MSB
    }

    public enum StorageClass : byte
    {
        NULL,
        AUTOMATIC,
        EXTERNAL,
        STATIC,
        REGISTER,
        EXTERNAL_DEF,
        LABEL,
        UNDEFINED_LABEL,
        MEMBER_OF_STRUCT,
        ARGUMENT,
        STRUCT_TAG,
        MEMBER_OF_UNION,
        UNION_TAG,
        TYPE_DEFINITION,
        ENUM_TAG,
        MEMBER_OF_ENUM,
        REGISTER_PARAM,
        BIT_FIELD,
        BLOCK = 0x64,
        FUNCTION = 0x65,
        END_OF_STRUCT = 0x66,
        FILE = 0x67,
        SECTION = 0x68,
        WEAK_EXTERNAL = 0x69,
        CLR_TOKEN = 0x6B,
        END_OF_FUNCTION = 0xFF
    }

    public class SYMBOL_TABLE
    {
        public string Name;
        public uint Value;
        public SectionNumber SectionNumber;
        public TypeClass Type;
        public StorageClass StorageClass;
        public byte NumberOfAuxSymbols;
        public Object AuxSymbols;
        private Byte[] NameArray;

        public SYMBOL_TABLE(BinaryReader br)
        {
            this.NameArray = br.ReadBytes(8);

            if (this.NameArray[0] == 0 && this.NameArray[1] == 0 && this.NameArray[2] == 0 && this.NameArray[3] == 0)
            {
                // Per specification, if the high DWORD is 0, then then low DWORD is an index into the string table
                this.Name = "/" + BitConverter.ToInt32(NameArray, 4).ToString();
            }
            else
            {
                this.Name = Encoding.UTF8.GetString(NameArray).Trim(((char)0));
            }

            this.Value = br.ReadUInt32();
            this.SectionNumber = (SectionNumber)br.ReadInt16();
            this.Type = (TypeClass)br.ReadInt16();
            if ((((int)this.Type) & 0xff00) == 0) { this.Type = (TypeClass)Enum.Parse(typeof(TypeClass), ((int)this.Type | 0x400).ToString()); }
            this.StorageClass = (StorageClass)br.ReadByte();
            this.NumberOfAuxSymbols = br.ReadByte();
        }
    }

    public class SECTION_DEFINITION
    {
        public uint Length;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint CheckSum;
        public ushort Number;
        public byte Selection;

        public SECTION_DEFINITION(BinaryReader br)
        {
            this.Length = br.ReadUInt32();
            this.NumberOfRelocations = br.ReadUInt16();
            this.NumberOfLinenumbers = br.ReadUInt16();
            this.CheckSum = br.ReadUInt32();
            this.Number = br.ReadUInt16();
            this.Selection = br.ReadByte();
            br.ReadBytes(3);
        }
    }

    public class ParsedObjectFile
    {
        public HEADER CoffHeader;
        public SECTION_HEADER[] SectionHeaders;
        public SYMBOL_TABLE[] SymbolTable;
    }

    public static class ObjectFileParser
    {
        public static ParsedObjectFile ParseObjectFile(string ObjectFilePath)
        {
            ParsedObjectFile Result = null;

            // Fixed structure sizes
            int SizeofCOFFFileHeader = 20;
            int SizeofSectionHeader = 40;
            int SizeofSymbolTableEntry = 18;
            int SizeofRelocationEntry = 10;

            // Open the object file for reading
            using (FileStream FileStream = System.IO.File.OpenRead(ObjectFilePath))
            {

                long FileLength = FileStream.Length;

                if (FileLength < SizeofCOFFFileHeader)
                {
                    // You cannot parse the COFF header if the file is not big enough to contain a COFF header.
                    throw new Exception("ObjectFile is too small to store a COFF header.");
                }

                // Open a BinaryReader object for the object file
                using (BinaryReader BinaryReader = new System.IO.BinaryReader(FileStream))
                {

                    // Parse the COFF header
                    COFF.HEADER CoffHeader = new COFF.HEADER(BinaryReader);

                    if (CoffHeader.SizeOfOptionalHeader != 0)
                    {
                        // Per the PECOFF specification, an object file does not have an optional header
                        throw new Exception("Coff header indicates the existence of an optional header. An object file cannot have an optional header.");
                    }

                    if (CoffHeader.PointerToSymbolTable == 0)
                    {
                        throw new Exception("An object file is supposed to have a symbol table.");
                    }

                    if (FileLength < ((CoffHeader.NumberOfSections * SizeofSectionHeader) + SizeofCOFFFileHeader))
                    {
                        // The object file isn't big enough to store the number of sections present.
                        throw new Exception("ObjectFile is too small to store section header data.");
                    }

                    // A string collection used to store section header names. This collection is referenced while
                    // parsing the symbol table entries whose name is the same as the section header. In this case,
                    // the symbol entry will have a particular auxiliary symbol table entry.
                    System.Collections.Specialized.StringCollection SectionHeaderNames = new System.Collections.Specialized.StringCollection();

                    // Correlate the processor type to the relocation type. There are more relocation type defined
                    // in the PECOFF specification, but I don't expect those to be present. In that case, relocation
                    // entries default to X86RelocationType.
                    COFF.SECTION_HEADER[] SectionHeaders = new COFF.SECTION_HEADER[CoffHeader.NumberOfSections];

                    // Parse section headers
                    for (int i = 0; i < CoffHeader.NumberOfSections; i++)
                    {
                        SectionHeaders[i] = new COFF.SECTION_HEADER(BinaryReader);

                        // Add the section name to the string collection. This will be referenced during symbol table parsing.
                        SectionHeaderNames.Add(SectionHeaders[i].Name);

                        // Save the current filestream position. We are about to jump out of place.
                        long SavedFilePosition = FileStream.Position;

                        // Check to see if the raw data points beyond the actual file size
                        if ((SectionHeaders[i].PointerToRawData + SectionHeaders[i].SizeOfRawData) > FileLength)
                        {
                            throw new Exception("Section header's raw data exceeds the size of the object file.");
                        }
                        else
                        {
                            // Read the raw data into a byte array
                            FileStream.Seek(SectionHeaders[i].PointerToRawData, SeekOrigin.Begin);
                            SectionHeaders[i].RawData = BinaryReader.ReadBytes((int)SectionHeaders[i].SizeOfRawData);
                        }

                        // Check to see if the section has a relocation table
                        if ((SectionHeaders[i].PointerToRelocations != 0) && (SectionHeaders[i].NumberOfRelocations != 0))
                        {
                            // Check to see if the relocation entries point beyond the actual file size
                            if ((SectionHeaders[i].PointerToRelocations + (SizeofRelocationEntry * SectionHeaders[i].NumberOfRelocations)) > FileLength)
                            {
                                throw new Exception("(SectionHeaders[i].Name) section header's relocation entries exceeds the soze of the object file.");
                            }

                            FileStream.Seek(SectionHeaders[i].PointerToRelocations, SeekOrigin.Begin);

                            COFF.RelocationEntry[] Relocations = new COFF.RelocationEntry[SectionHeaders[i].NumberOfRelocations];

                            for (int j = 0; j < SectionHeaders[i].NumberOfRelocations; j++)
                            {
                                Relocations[j] = new COFF.RelocationEntry(BinaryReader);
                                // Cast the relocation as its respective type
                                switch (CoffHeader.Machine)
                                {
                                    case Machine.I386:
                                        Relocations[j].Type = (COFF.X86RelocationType)Relocations[j].Type;
                                        break;
                                    case Machine.AMD64:
                                        Relocations[j].Type = (COFF.AMD64RelocationType)Relocations[j].Type;
                                        break;
                                    case Machine.ARMV7:
                                        Relocations[j].Type = (COFF.ARMRelocationType)Relocations[j].Type;
                                        break;
                                    case Machine.ARM64:
                                        Relocations[j].Type = (COFF.ARMv8RelocationType)Relocations[j].Type;
                                        break;
                                }
                            }

                            // Add the relocation table entry to the section header
                            SectionHeaders[i].Relocations = Relocations;
                        }
    
                        // Restore the original filestream pointer
                        FileStream.Seek(SavedFilePosition, SeekOrigin.Begin);
                    }

                    // Retrieve the contents of the COFF string table
                    long SymTableSize = CoffHeader.NumberOfSymbols * SizeofSymbolTableEntry;
                    long StringTableOffset = CoffHeader.PointerToSymbolTable + SymTableSize;

                    if (StringTableOffset > FileLength)
                    {
                        throw new Exception("The string table points beyond the end of the file.");
                    }

                    FileStream.Seek(StringTableOffset, SeekOrigin.Begin);
                    UInt32 StringTableLength = BinaryReader.ReadUInt32();

                    if (StringTableLength > FileLength)
                    {
                        throw new Exception("The string table's length exceeds the length of the file.");
                    }

                    string StringTable = System.Text.Encoding.UTF8.GetString(BinaryReader.ReadBytes((int)StringTableLength));

                    COFF.SYMBOL_TABLE[] RawSymbolTable = new COFF.SYMBOL_TABLE[CoffHeader.NumberOfSymbols];

                    // Retrieve the symbol table
                    if (FileLength < StringTableOffset)
                    {
                        throw new Exception("Symbol table is larger than the file size.");
                    }

                    FileStream.Seek(CoffHeader.PointerToSymbolTable, SeekOrigin.Begin);
                    int NumberofRegularSymbols = 0;

                    /*
                        Go through each symbol table looking for auxiliary symbols to parse

                        Currently supported auxiliary symbol table entry formats:
                        1) .file
                        2) Entry names that match the name of a section header
                    */

                    for (int i = 0; i < CoffHeader.NumberOfSymbols; i++)
                    {
                        // Parse the symbol tables regardless of whether they are normal or auxiliary symbols
                        RawSymbolTable[i] = new COFF.SYMBOL_TABLE(BinaryReader);

                        if (RawSymbolTable[i].NumberOfAuxSymbols == 0)
                        {
                            // This symbol table entry has no auxiliary symbols
                            NumberofRegularSymbols++;
                        }
                        else if (RawSymbolTable[i].Name == ".file")
                        {
                            long TempPosition = FileStream.Position; // Save filestream position
                            // Retrieve the file name
                            RawSymbolTable[i].AuxSymbols = System.Text.Encoding.UTF8.GetString(BinaryReader.ReadBytes(RawSymbolTable[i].NumberOfAuxSymbols * SizeofSymbolTableEntry)).TrimEnd(((Char)0));
                            FileStream.Seek(TempPosition, SeekOrigin.Begin); // Restore filestream position
                        }
                        else if (SectionHeaderNames.Contains(RawSymbolTable[i].Name))
                        {
                            long TempPosition = FileStream.Position; // Save filestream position
                            RawSymbolTable[i].AuxSymbols = new COFF.SECTION_DEFINITION(BinaryReader);
                            FileStream.Seek(TempPosition, SeekOrigin.Begin); // Restore filestream position
                        }
                    }

                    // Create an array of symbol table entries without auxiliary table entries
                    COFF.SYMBOL_TABLE[] SymbolTable = new COFF.SYMBOL_TABLE[NumberofRegularSymbols];
                    int k = 0;

                    for (int i = 0; i < CoffHeader.NumberOfSymbols; i++)
                    {
                        SymbolTable[k] = RawSymbolTable[i]; // FYI, the first symbol table entry will never be an aux symbol
                        k++;

                        // Skip over the auxiliary symbols
                        if (RawSymbolTable[i].NumberOfAuxSymbols != 0)
                        {
                            i += RawSymbolTable[i].NumberOfAuxSymbols;
                        }
                    }

                    // Fix the section names if any of them point to the COFF string table
                    for (int i = 0; i < CoffHeader.NumberOfSections; i++)
                    {
                        if ((SectionHeaders[i].Name != null) && (SectionHeaders[i].Name.IndexOf('/') == 0))
                        {
                            string StringTableIndexString = SectionHeaders[i].Name.Substring(1);

                            int StringTableIndex;
                            if (int.TryParse(StringTableIndexString, out StringTableIndex))
                            {
                                StringTableIndex -= 4;

                                if (StringTableIndex > (StringTableLength + 4))
                                {
                                    throw new Exception("String table entry exceeds the bounds of the object file.");
                                }

                                int Length = StringTable.IndexOf(((Char)0), StringTableIndex);
                                SectionHeaders[i].Name = StringTable.Substring(StringTableIndex, Length);
                            }
                        }
                    }

                    // Fix the symbol table names
                    for (int i = 0; i < SymbolTable.Length; i++)
                    {
                        if ((SymbolTable[i].Name != null) && (SymbolTable[i].Name.IndexOf('/') == 0))
                        {
                            string StringTableIndexString = SymbolTable[i].Name.Substring(1);

                            int StringTableIndex;
                            if (int.TryParse(StringTableIndexString, out StringTableIndex))
                            {
                                StringTableIndex -= 4;
                                int Length = StringTable.IndexOf(((Char)0), StringTableIndex) - StringTableIndex;
                                SymbolTable[i].Name = StringTable.Substring(StringTableIndex, Length);
                            }
                        }
                    }

                    // Apply symbol names to the relocation entries
                    // SectionHeaders | Where-Object { _.Relocations } | % {
                    //     _.Relocations | % { _.Name = RawSymbolTable[_.SymbolTableIndex].Name }
                    // }
                    SectionHeaders.Where(h => (h.Relocations != null)).ToList().ForEach(h => h.Relocations.ToList().ForEach(r => r.Name = RawSymbolTable[r.SymbolTableIndex].Name));

                    Result = new ParsedObjectFile();
                    Result.CoffHeader = CoffHeader;
                    Result.SectionHeaders = SectionHeaders;
                    Result.SymbolTable = SymbolTable;
                }
            }

            return Result;
        }
    }
}
