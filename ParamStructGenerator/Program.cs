using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Xml;

namespace ParamStructGenerator
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string paramdefFolder = Path.GetFullPath(
                @"..\Erd-Tools\Documentation\Params\Defs-English"
            );
            string regulationPath =
                @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\regulation.bin";
            string outputFolder = Path.GetFullPath(@"..\libER\include\param");

            LibraryGen gen = new LibraryGen() { CodeGen = new CppParamCodeGen() };
            // CSingleHeaderGen gen = new CSingleHeaderGen() { CEMode = true };
            gen.GenerateCode(regulationPath, paramdefFolder, outputFolder);
        }
    }
}
