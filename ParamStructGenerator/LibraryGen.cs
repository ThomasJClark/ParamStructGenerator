using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SoulsFormats;

namespace ParamStructGenerator
{
    public class LibraryGen
    {
        public IParamCodeGen CodeGen { get; set; }

        public void GenerateCode(string regulationPath, string paramdefFolder, string outputFolder)
        {
            var detectedSizeByParamType = new Dictionary<string, long>();

            BND4 archive = SFUtil.DecryptERRegulation(regulationPath);
            foreach (var file in from f in archive.Files where f.Name.EndsWith(".param") select f)
            {
                PARAM param = PARAM.Read(file.Bytes);
                if (!detectedSizeByParamType.ContainsKey(param.ParamType))
                    detectedSizeByParamType.Add(param.ParamType, param.DetectedSize);
            }

            var paramdefIncludes = new List<string>();
            string outFile;

            foreach (string file in Directory.GetFiles(paramdefFolder, "*.xml"))
            {
                PARAMDEF def = PARAMDEF.XmlDeserialize(file);
                ParamdefUtils.MakeInternalNamesUnique(def, ParamdefUtils.UniqueNameMethod.Counter);

                paramdefIncludes.Add(def.ParamType);

                long detectedSize = -1;
                detectedSizeByParamType.TryGetValue(def.ParamType, out detectedSize);

                Console.Write($"Generating paramdef {def.ParamType}... ");
                WriteAllTextAndCreateDirs(
                    Path.Combine(outputFolder, $@"paramdef\{def.ParamType}{CodeGen.FileExtension}"),
                    CodeGen.GenParamdefCode(def, false, detectedSize)
                );
                Console.WriteLine("done");
            }
            WriteAllTextAndCreateDirs(
                Path.Combine(outputFolder, $@"detail\paramdef{CodeGen.FileExtension}"),
                CodeGen.GenCommonHeader("defs", paramdefIncludes)
            );
        }

        public void WriteAllTextAndCreateDirs(string path, string text)
        {
            new FileInfo(path).Directory.Create();
            File.WriteAllText(path, text);
        }
    }
}
