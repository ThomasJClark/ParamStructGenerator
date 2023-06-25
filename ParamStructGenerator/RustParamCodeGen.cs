using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using System.Security.Permissions;
using static SoulsFormats.PARAMDEF;

namespace ParamStructGenerator {
    public class RustParamCodeGen : IParamCodeGen {
        public string FileExtension => ".rs";
        public bool MultiFile = true;
        public bool ParamTypedefs = false;
        public string GenParamCode(PARAM param, string name, bool writeComments) {
            StringBuilder sb = new StringBuilder();

            if (writeComments && MultiFile) sb.AppendLine("/* This file was automatically generated from regulation data. */");
            if (MultiFile) sb.AppendLine($@"use crate::param::traits::*;");
            sb.AppendLine();
            if (MultiFile) sb.AppendLine($@"include!(""defs/{param.ParamType}{FileExtension}"");");
            sb.AppendLine();
            if (writeComments) sb.AppendLine($@"/// Type: {param.ParamType}");
            sb.AppendLine();
            sb.AppendLine($"pub type {name} = ParamStruct<{param.ParamType}, \"{name}\">;");
            sb.AppendLine($"impl ParamType for {param.ParamType} {{");
            sb.AppendLine($"\tconst NAME: &'static str = \"{param.ParamType}\";");
            sb.AppendLine($"\tconst VERSION: u16 = {param.ParamdefDataVersion};");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#[cfg(test)]");
            sb.AppendLine("mod tests {");
            sb.AppendLine("\tuse std::mem::size_of;");
            sb.AppendLine($"\tuse crate::param::{name}::{name};");
            sb.AppendLine();
            sb.AppendLine("\t#[test]");
            sb.AppendLine("\tfn size_check() {");
            sb.AppendLine($"\t\tassert_eq!(size_of::<{name}>(), {param.DetectedSize})");
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public string GenParamdefCode(PARAMDEF def, bool writeComments) {
            StringBuilder sb = new StringBuilder();

            if (writeComments && MultiFile) sb.AppendLine("/* This file was automatically generated from XML paramdefs. */");

            if (writeComments) {
                sb.AppendLine($@"/// Data Version: {def.DataVersion}
/// Is Big Endian: {(def.BigEndian ? "True" : "False")}
/// Is Unicode: {(def.Unicode ? "True" : "False")}
/// Format Version: {def.FormatVersion}");
            }
            
            sb.AppendLine($"#[repr(C)]");
            sb.AppendLine($"pub struct {def.ParamType} {{");
            
            StringBuilder bitfieldBuilder = new StringBuilder();
            int bitfieldCounter = 1;

            for (int i = 0; i < def.Fields.Count; i++) {
                Field field = def.Fields[i];
                if (writeComments) sb.AppendLine();

                if (writeComments) {
                    if ((field.DisplayName ?? "") != "")
                        sb.AppendLine($"\t/// NAME: {field.DisplayName}");
                    if ((field.Description ?? "") != "")
                        sb.AppendLine($"\t/// DESC: {field.Description.Replace("\r\n", "")}");
                }

                string fieldName = ParamdefUtils.FieldTypeToRust(field.DisplayType, ParamTypedefs);
                bool isZeroSize = false;

                StringBuilder fieldBuilder = new StringBuilder();
                fieldBuilder.Append($"\t");

                if (ParamUtil.IsBitType(field.DisplayType) && field.BitSize > 0) {
                    string bitfieldName = $"Bitfield{bitfieldCounter}";
                    fieldBuilder.Append($"pub Bitfield{bitfieldCounter}:{fieldName}");
                    int bitOffset = field.BitSize;
                    DefType bitType = field.DisplayType == DefType.dummy8 ? DefType.u8 : field.DisplayType;
                    int bitLimit = ParamUtil.GetBitLimit(bitType);
                    bitfieldBuilder.Append(GetBitField(field,bitfieldName, bitOffset, writeComments));


                    for (; i < def.Fields.Count - 1; i++)
                    {
                        Field nextField = def.Fields[i + 1];
                        DefType nextType = nextField.DisplayType;
                        if (!ParamUtil.IsBitType(nextType) || nextField.BitSize == -1 || bitOffset + nextField.BitSize > bitLimit
                            || (nextType == DefType.dummy8 ? DefType.u8 : nextType) != bitType)
                            break;
                        bitOffset += nextField.BitSize;
                        bitfieldBuilder.Append(GetBitField(nextField,bitfieldName, bitOffset, writeComments));
                    }

                    bitfieldCounter++;
                }
                else if (field.BitSize != -1) {
                    fieldBuilder.Append($"pub {field.InternalName}:{fieldName}");
                    isZeroSize = true;
                }
                else if (ParamUtil.IsArrayType(field.DisplayType) && field.ArrayLength > 0)
                    fieldBuilder.Append($"pub {field.InternalName}:[{fieldName};{field.ArrayLength}]");
                else if (field.ArrayLength <= 0) {
                    fieldBuilder.Append($"pub {field.InternalName}:{fieldName}");
                    isZeroSize = true;
                }
                else {
                    fieldBuilder.Append($"pub {field.InternalName}:{fieldName}");
                }

                // Comment out the field if it has zero size
                if (isZeroSize) sb.Append("\t// ");
                sb.AppendLine($"{fieldBuilder},");
            }
            sb.AppendLine("}\n");

            if (bitfieldBuilder.Length > 0) {
                sb.AppendLine($"impl {def.ParamType} {{");
                sb.AppendLine($"{bitfieldBuilder}");
                sb.AppendLine("}");
            }
            
            return sb.ToString();
        }
        private string GetBitField(Field field, string bitfieldName, int bitOffset, bool writeComments) {
            string returnValue = string.Empty;
            if (field.BitSize != 1) {
                int size = ParamUtil.GetValueSize(field.DisplayType);
                string fieldType = ParamdefUtils.FieldTypeToRust(field.DisplayType);

                if (writeComments) returnValue += $"\t/// {field.Description?.Replace("\n", "")}\n";
                if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
                returnValue +=
                    $"\tpub fn get_{field.InternalName}(&self) -> {fieldType} {{\n" +
                    $"\t\t&self.{bitfieldName} & ({size} << {bitOffset - 1})\n" +
                    "\t}\n" +
                    "\n";
                if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
                returnValue +=
                    $"\tpub fn set_{field.InternalName}(&mut self, state: {fieldType}) {{\n" +
                      "\t\tif state != 0 {\n" +
                    $"\t\t\tlet val = (state << {bitOffset - 1}) & ({size} << {bitOffset - 1});\n" +
                    $"\t\t\tself.{bitfieldName} |= val\n" +
                      "\t\t} else {\n" +
                    $"\t\t\tself.{bitfieldName} &= !(state << {bitOffset - 1})\n" +
                      "\t\t}\n" +
                      "\t}";

                return returnValue;
            }
            
            if (writeComments) returnValue += $"\t/// {field.Description?.Replace("\n", "")}\n";
            if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
            returnValue +=
                $"\tpub fn get_{field.InternalName}(&self) -> bool {{\n" +
                $"\t\t&self.{bitfieldName} & (1 << {bitOffset - 1}) != 0\n" +
                "\t}\n" +
                "\n";
            if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
            returnValue +=
                $"\tpub fn set_{field.InternalName}(&mut self, state: bool) {{\n" +
                  "\t\tif state {\n" +
                $"\t\t\tself.{bitfieldName} |= (1 << {bitOffset - 1})\n" +
                  "\t\t} else {\n" +
                $"\t\t\tself.{bitfieldName} &= !(1 << {bitOffset - 1})\n" +
                  "\t\t}\n" +
                  "\t}\n";
                
                return returnValue;

        }

        public string GenCommonHeader(string name, List<string> includeList) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"/* This file was automatically generated. */");
            sb.AppendLine();
            
            foreach (var header in includeList) {
                sb.AppendLine($"include!(\"{header}.h\")");
            }

            return sb.ToString();
        }
        public string GenTraitHeader() {
            return "use std::ops::Deref;\n\n" +
                "pub trait ParamType {\n" +
                "\tconst NAME: &'static str;\n" +
                "\n" +
                "\t// So you can query the type constant from an `impl ParamType`\n" +
                "\tfn param_type_name(&self) -> &'static str {\n" +
                "\t\tSelf::NAME\n" +
                "\t}\n" +
                "\tconst VERSION: u16;\n" +
                "\tfn version(&self) -> u16 {\n" +
                "\t\tSelf::VERSION\n" +
                "\t}\n" +
                "\t// etc...\n" +
                "}\n" +
                "\n" +
                "// Make a single generic wrapper for named params \n" +
                "pub struct ParamStruct<T: ParamType, const N: &'static str> {\n" +
                "\tdata: T\n" +
                "}\n" +
                "\n" +
                "// Add a Deref implementation so ParamStruct<T, N> derefs to T\n" +
                "impl<T: ParamType, const N: &'static str> Deref for ParamStruct<T, N> {\n" +
                "\ttype Target = T;\n" +
                "\n" +
                "\tfn deref(&self) -> &Self::Target {\n" +
                "\t\t&self.data\n" +
                "\t}\n" +
                "}\n" +
                "\n" +
                "pub trait Param {\n" +
                "\tconst NAME: &'static str;\n" +
                "\ttype ParamType: ParamType;\n" +
                "\n" +
                "\tfn name(&self) -> &'static str {\n" +
                "\t\tSelf::NAME\n" +
                "\t}\n" +
                "\tfn param_type_name(&self) -> &'static str where Self: ParamType {\n" +
                "\t\t<Self as ParamType>::NAME\n" +
                "\t}\n" +
                "}\n" +
                "\n" +
                "impl<T: ParamType, const N: &'static str> Param for ParamStruct<T,N> {\n" +
                "\tconst NAME: &'static str = N;\n" +
                "\ttype ParamType = T;\n" +
                "}";
        }

        public string GenModHeader(List<string> includeList) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($@"/* This file was automatically generated. */
pub mod traits;
");
            foreach (var header in includeList)
            {
                sb.AppendLine($"pub mod {header};");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
