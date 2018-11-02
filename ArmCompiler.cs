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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Patcher
{
    public enum CodeType
    {
        ARM,
        Thumb,
        Thumb2
    }

    public static class ArmCompiler
    {
        public static Int32 LastErrorCode;
        public static string Output;

        public static byte[] Compile(string PathToVisualStudio, UInt32 Origin, CodeType CodeType, string ArmCodeFragment)
        {
            if (PathToVisualStudio.Length == 0)
            {
                Output = "ARM SDK is missing.";
                return null;
            }

            string AssemblyFilePath = Path.GetTempFileName();
            string ObjectFilePathTmp = Path.GetTempFileName();
            string ObjectFilePath = ObjectFilePathTmp.Replace(".tmp", ".obj");
            File.Move(ObjectFilePathTmp, ObjectFilePath);

            string CodeTypeDirective = null;
            switch (CodeType)
            {
                case Patcher.CodeType.ARM:
                    CodeTypeDirective = "CODE32";
                    break;
                case Patcher.CodeType.Thumb:
                    CodeTypeDirective = "CODE16";
                    break;
                case Patcher.CodeType.Thumb2:
                    CodeTypeDirective = "THUMB";
                    break;
            }

            string FullAssemblyCode =
                " AREA ARM_AREA, CODE, READONLY" + Environment.NewLine +
                " " + CodeTypeDirective + Environment.NewLine;

            UInt32 Padding;
            string ProcessedAssembly = ProcessArmCodeFragment(ArmCodeFragment, Origin, out Padding);

            if (Padding > 0)
                FullAssemblyCode += " SPACE " + Padding.ToString() + Environment.NewLine;

            FullAssemblyCode +=
                "start" + Environment.NewLine +
                ProcessedAssembly;

            FullAssemblyCode += " end" + Environment.NewLine;

            File.WriteAllText(AssemblyFilePath, FullAssemblyCode);

            ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(PathToVisualStudio, @"VC\bin\x86_arm\armasm.exe"));
            psi.EnvironmentVariables["PATH"] += ";" + Path.Combine(PathToVisualStudio, @"Common7\IDE\");
            psi.EnvironmentVariables["PATH"] += ";" + Path.Combine(PathToVisualStudio, @"Common7\Tools\");
            psi.EnvironmentVariables["PATH"] += ";" + Path.Combine(PathToVisualStudio, @"VC\bin\");
            psi.EnvironmentVariables["PATH"] += ";" + Path.Combine(PathToVisualStudio, @"VC\bin\x86_arm\");
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            psi.Arguments = "-g \"" + AssemblyFilePath + "\" \"" + ObjectFilePath + "\"";
            Process ArmAsmProcess = Process.Start(psi);
            ArmAsmProcess.WaitForExit();
            LastErrorCode = ArmAsmProcess.ExitCode;
            if (ArmAsmProcess.ExitCode != 0)
            {
                using (StreamReader reader = ArmAsmProcess.StandardOutput)
                {
                    Output = reader.ReadToEnd();

                    string[] Lines = Output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    Output = "";

                    foreach (string Line in Lines)
                    {
                        string Out = Line.Trim();
                        if (Out.Length == 0) continue;
                        if (Out.StartsWith("Microsoft (R) ARM Macro Assembler")) continue;
                        if (Out.StartsWith("Copyright (C) Microsoft Corporation")) continue;
                        if (Out.StartsWith(AssemblyFilePath))
                        {
                            Out = Out.Substring(AssemblyFilePath.Length);
                            int P = Out.IndexOf(':');
                            Out = Out.Substring(P + 1).Trim();
                        }
                        Output += Out + Environment.NewLine;
                    }
                }
            }

            byte[] Result = null;
            
            if (LastErrorCode == 0)
                Result = COFF.ObjectFileParser.ParseObjectFile(ObjectFilePath).SectionHeaders.Where(h => h.Name == "ARM_AREA").First().RawData;

            File.Delete(AssemblyFilePath);
            File.Delete(ObjectFilePath);

            if ((Result != null) && (Padding > 0))
            {
                byte[] RemovedPadding = new byte[Result.Length - Padding];
                Buffer.BlockCopy(Result, (int)Padding, RemovedPadding, 0, RemovedPadding.Length);
                Result = RemovedPadding;
            }

            return Result;
        }

        private static string ProcessArmCodeFragment(string ArmCodeFragment, UInt32 Origin, out UInt32 Padding)
        {
            string[] BranchOpcodes = new string[] { "B", "BEQ", "BNE", "BCS", "BHS", "BCC", "BLO", "BMI", "BPL", "BVS", "BVC", "BHI", "BLS", "BGE", "BLT", "BGT", "BLE", "BAL"};

            StringBuilder Result = new StringBuilder(1000);
            Padding = 0;

            string[] Lines = ArmCodeFragment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string Line in Lines)
            {
                string Code = Line;
                if (Line.IndexOf(':') >= 0)
                {
                    string Label = Line.Substring(0, Line.IndexOf(':'));
                    Label = Label.Trim();
                    if (Label.Length > 0)
                        Result.AppendLine(Label);
                    Code = Line.Substring(Line.IndexOf(':') + 1);
                }

                int EquPos = Code.IndexOf("EQU", StringComparison.OrdinalIgnoreCase);
                if (((EquPos) > 0) && (EquPos > 0) && (EquPos < (Code.Length - 3)))
                {
                    if ((new char[] { '\t', ' ' }.Contains(Line[EquPos - 1])) &&
                        (new char[] { '\t', ' ' }.Contains(Line[EquPos + 3])))
                    {
                        Result.AppendLine(Line.Trim());
                        continue;
                    }
                }

                Code = Code.Trim();

                bool IsAbsoluteAddress = false;

                int OpcodeLength = Code.IndexOfAny(new char [] { '\t', ' ', '.' });
                if (OpcodeLength > 0)
                {
                    string Opcode = Code.Substring(0, OpcodeLength).ToUpper();

                    string PossibleAddress = null;

                    if (Opcode == "LDR")
                    {
                        PossibleAddress = Code.Substring(Code.IndexOf(',') + 1).Trim();
                    }
                    else if (BranchOpcodes.Contains(Opcode))
                    {
                        PossibleAddress = Code.Substring(Code.IndexOfAny(new char[] { '\t', ' ' }) + 1).Trim();
                    }

                    if (PossibleAddress != null)
                    {
                        UInt32 ParsedValue;
                        if (PossibleAddress.StartsWith("0x"))
                            PossibleAddress = PossibleAddress.Substring(2);
                        IsAbsoluteAddress = UInt32.TryParse(PossibleAddress, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out ParsedValue);
                        if (IsAbsoluteAddress && (ParsedValue < Origin))
                            Padding = Math.Max(Padding, Origin - ParsedValue);
                    }
                }

                Result.Append(" ");
                Result.Append(Code);
                if (IsAbsoluteAddress)
                {
                    Result.Append(" + start - 0x");
                    Result.Append(Origin.ToString("X8"));
                }
                Result.Append(Environment.NewLine);
            }

            return Result.ToString();
        }
    }
}
