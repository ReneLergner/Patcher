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

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using WPinternals;

namespace Patcher
{
    public static class MainPatcher
    {
        // TargetFilePath is relative to the root of the PatchDefinition
        // OutputFilePath can be null
        public static void AddPatch(string InputFilePath, string OutputFilePath, string PatchDefinitionName, string TargetVersionDescription, string TargetFilePath, string PathToVisualStudioWithWP8SDK, UInt32 VirtualOffset, CodeType CodeType, string ArmCodeFragment, string PatchDefintionsXmlPath)
        {
            SHA1Managed SHA = new SHA1Managed();

            // Compile ARM code
            byte[] CompiledCode = null;
            if (VirtualOffset != 0)
                CompiledCode = ArmCompiler.Compile(PathToVisualStudioWithWP8SDK, VirtualOffset, CodeType, ArmCodeFragment);

            // Read original binary
            byte[] Binary = File.ReadAllBytes(InputFilePath);

            // Backup original checksum
            UInt32 ChecksumOffset = GetChecksumOffset(Binary);
            UInt32 OriginalChecksum = ByteOperations.ReadUInt32(Binary, ChecksumOffset);

            // Determine Raw Offset (PEReader)
            MemoryStream BinaryStream = new MemoryStream(Binary);
            BinaryReader BinaryReader = new BinaryReader(BinaryStream);
            PEReader PEReader = new PEReader(BinaryReader);
            UInt32 RawOffset = 0;
            if (VirtualOffset != 0)
                RawOffset = PEReader.ConvertVirtualToRaw(VirtualOffset);

            // Add or replace patch
            string PatchDefintionsXml = File.ReadAllText(PatchDefintionsXmlPath);
            PatchEngine PatchEngine = new PatchEngine(PatchDefintionsXml);
            PatchDefinition PatchDefinition = PatchEngine.PatchDefinitions.Where(d => (string.Compare(d.Name, PatchDefinitionName, true) == 0)).FirstOrDefault();
            if (PatchDefinition == null)
            {
                PatchDefinition = new PatchDefinition();
                PatchDefinition.Name = PatchDefinitionName;
                PatchEngine.PatchDefinitions.Add(PatchDefinition);
            }
            TargetVersion TargetVersion = PatchDefinition.TargetVersions.Where(v => (string.Compare(v.Description, TargetVersionDescription, true) == 0)).FirstOrDefault();
            if (TargetVersion == null)
            {
                TargetVersion = new TargetVersion();
                TargetVersion.Description = TargetVersionDescription;
                PatchDefinition.TargetVersions.Add(TargetVersion);
            }
            TargetFile TargetFile = TargetVersion.TargetFiles.Where(f => ((f.Path != null) && (string.Compare(f.Path.TrimStart(new char[] { '\\' }), TargetFilePath.TrimStart(new char[] { '\\' }), true) == 0))).FirstOrDefault();
            if (TargetFile == null)
            {
                TargetFile = new TargetFile();
                TargetVersion.TargetFiles.Add(TargetFile);
            }
            TargetFile.Path = TargetFilePath;
            TargetFile.HashOriginal = SHA.ComputeHash(Binary);
            Patch Patch;
            if (VirtualOffset != 0)
            {
                Patch = TargetFile.Patches.Where(p => p.Address == RawOffset).FirstOrDefault();
                if (Patch == null)
                {
                    Patch = new Patch();
                    Patch.Address = RawOffset;
                    TargetFile.Patches.Add(Patch);
                }
                Patch.OriginalBytes = new byte[CompiledCode.Length];
                Buffer.BlockCopy(Binary, (int)RawOffset, Patch.OriginalBytes, 0, CompiledCode.Length);
                Patch.PatchedBytes = CompiledCode;
            }

            // Apply all patches
            foreach (Patch CurrentPatch in TargetFile.Patches)
            {
                Buffer.BlockCopy(CurrentPatch.PatchedBytes, 0, Binary, (int)CurrentPatch.Address, CurrentPatch.PatchedBytes.Length);
            }

            // Calculate checksum
            // This also modifies the binary
            // Original checksum is already backed up
            UInt32 Checksum = CalculateChecksum(Binary);

            // Add or replace checksum patch
            Patch = TargetFile.Patches.Where(p => p.Address == ChecksumOffset).FirstOrDefault();
            if (Patch == null)
            {
                Patch = new Patch();
                Patch.Address = ChecksumOffset;
                TargetFile.Patches.Add(Patch);
            }
            Patch.OriginalBytes = new byte[4];
            ByteOperations.WriteUInt32(Patch.OriginalBytes, 0, OriginalChecksum);
            Patch.PatchedBytes = new byte[4];
            ByteOperations.WriteUInt32(Patch.PatchedBytes, 0, Checksum);

            // Calculate hash for patched target file
            TargetFile.HashPatched = SHA.ComputeHash(Binary);

            // Write patched file
            if (OutputFilePath != null)
                File.WriteAllBytes(OutputFilePath, Binary);

            // Write PatchDefintions
            PatchEngine.WriteDefinitions(PatchDefintionsXmlPath);
        }

        private static UInt32 CalculateChecksum(byte[] PEFile)
        {
            UInt32 Checksum = 0;
            UInt32 Hi;

            // Clear file checksum
            ByteOperations.WriteUInt32(PEFile, GetChecksumOffset(PEFile), 0);

            for (UInt32 i = 0; i < ((UInt32)PEFile.Length & 0xfffffffe); i += 2)
            {
                Checksum += ByteOperations.ReadUInt16(PEFile, i);
                Hi = Checksum >> 16;
                if (Hi != 0)
                {
                    Checksum = Hi + (Checksum & 0xFFFF);
                }
            }
            if ((PEFile.Length % 2) != 0)
            {
                Checksum += (UInt32)ByteOperations.ReadUInt8(PEFile, (UInt32)PEFile.Length - 1);
                Hi = Checksum >> 16;
                if (Hi != 0)
                {
                    Checksum = Hi + (Checksum & 0xFFFF);
                }
            }
            Checksum += (UInt32)PEFile.Length;

            // Write file checksum
            ByteOperations.WriteUInt32(PEFile, GetChecksumOffset(PEFile), Checksum);

            return Checksum;
        }

        private static UInt32 GetChecksumOffset(byte[] PEFile)
        {
            return ByteOperations.ReadUInt32(PEFile, 0x3C) + +0x58;
        }
    }
}
