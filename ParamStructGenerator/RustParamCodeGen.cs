using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using System.Security.Permissions;
using static SoulsFormats.PARAMDEF;
using static ParamStructGenerator.ParamdefUtils;

namespace ParamStructGenerator {
    public class RustParamCodeGen : IParamCodeGen {
        public string FileExtension => ".rs";
        public bool MultiFile = true;
        public bool ParamTypedefs = false;
        public string GenParamCode(PARAM param, string name, bool writeComments) {
            StringBuilder sb = new StringBuilder();

            if (writeComments && MultiFile) sb.AppendLine("/* This file was automatically generated from regulation data. */");
            sb.AppendLine("#![allow(non_snake_case)]");

            if (MultiFile) sb.AppendLine($@"use crate::param::traits::*;");
            sb.AppendLine();
            if (MultiFile) sb.AppendLine($@"include!(""defs/{param.ParamType}{FileExtension}"");");
            sb.AppendLine();
            if (writeComments) sb.AppendLine($@"/// Type: {param.ParamType}");
            sb.AppendLine();
            sb.AppendLine($"pub type {name} = ParamStruct<{param.ParamType}>;");
            sb.AppendLine($"impl Param for ParamStruct<{param.ParamType}> {{");
            sb.AppendLine($"\tconst NAME: &'static str = \"{name}\";");
            sb.AppendLine($"\tconst TYPE_NAME: &'static str = \"{param.ParamType}\";");
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
                string sanitizedName = SanitizeFieldName(field.InternalName);

                if (ParamUtil.IsBitType(field.DisplayType) && field.BitSize > 0) {
                    string bitfieldName = $"Bitfield{bitfieldCounter}";
                    fieldBuilder.Append($"pub Bitfield{bitfieldCounter}:{fieldName}");
                    int bitOffset = field.BitSize;
                    DefType bitType = field.DisplayType == DefType.dummy8 ? DefType.u8 : field.DisplayType;
                    int bitLimit = ParamUtil.GetBitLimit(bitType);
                    bitfieldBuilder.Append(GetBitField(field,bitfieldName, bitOffset - 1, writeComments));


                    for (; i < def.Fields.Count - 1; i++)
                    {
                        Field nextField = def.Fields[i + 1];
                        DefType nextType = nextField.DisplayType;
                        if (!ParamUtil.IsBitType(nextType) || nextField.BitSize == -1 || bitOffset + nextField.BitSize > bitLimit
                            || (nextType == DefType.dummy8 ? DefType.u8 : nextType) != bitType)
                            break;
                        bitfieldBuilder.Append(GetBitField(nextField,bitfieldName, bitOffset, writeComments));
                        bitOffset += nextField.BitSize;
                    }

                    bitfieldCounter++;
                }
                else if (field.BitSize != -1) {
                    fieldBuilder.Append($"pub {sanitizedName}:{fieldName}");
                    isZeroSize = true;
                }
                else if (ParamUtil.IsArrayType(field.DisplayType) && field.ArrayLength > 0)
                    fieldBuilder.Append($"pub {sanitizedName}:[{fieldName};{field.ArrayLength}]");
                else if (field.ArrayLength <= 0) {
                    fieldBuilder.Append($"pub {sanitizedName}:{fieldName}");
                    isZeroSize = true;
                }
                else {
                    fieldBuilder.Append($"pub {sanitizedName}:{fieldName}");
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
        private string SanitizeFieldName(string fieldInternalName) {
            // can add more filters later.
            if (fieldInternalName == "type") return "r#type";

            return fieldInternalName;
        }
        private string GetBitField(Field field, string bitfieldName, int bitOffset, bool writeComments) {
            string returnValue = string.Empty;
            int size = ParamUtil.GetValueSize(field.DisplayType);
            if (field.BitSize != 1) {
                string fieldType = ParamdefUtils.FieldTypeToRust(field.DisplayType);
                int maxVal = (1 << field.BitSize) - 1;

                if (writeComments) returnValue += $"\t/// {field.Description?.Replace("\n", "")}\n";
                if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
                returnValue +=
                    $"\tpub fn get_{field.InternalName}(&self) -> {fieldType} {{\n" +
                    $"\t\t&self.{bitfieldName} & 0x{TruncateConst(maxVal << bitOffset, size):X}\n" +
                    "\t}\n" +
                    "\n";
                if (writeComments) returnValue += $"\t/// {bitfieldName} MAX: {maxVal}\n";
                returnValue +=
                    $"\tpub fn set_{field.InternalName}(&mut self, state: {fieldType}) {{\n" +
                      "\t\tif state != 0 {\n" +
                    $"\t\t\tlet val = (state << {bitOffset}) & 0x{TruncateConst(maxVal << bitOffset, size):X};\n" +
                    $"\t\t\tlet newVal = &self.{bitfieldName} & 0x{TruncateConst(~(maxVal << bitOffset), size):X} | val;\n" +
                    $"\t\t\tself.{bitfieldName} = newVal\n" +
                      "\t\t} else {\n" +
                    $"\t\t\tself.{bitfieldName} &= 0x{TruncateConst(~(maxVal << bitOffset), size):X}\n" +
                      "\t\t}\n" +
                      "\t}";

                return returnValue;
            }
            
            if (writeComments) returnValue += $"\t/// {field.Description?.Replace("\n", "")}\n";
            if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
            returnValue +=
                $"\tpub fn get_{field.InternalName}(&self) -> bool {{\n" +
                $"\t\t&self.{bitfieldName} & 0x{TruncateConst(1 << bitOffset, size):X} != 0\n" +
                "\t}\n" +
                "\n";
            if (writeComments) returnValue += $"\t/// {bitfieldName}\n";
            returnValue +=
                $"\tpub fn set_{field.InternalName}(&mut self, state: bool) {{\n" +
                  "\t\tif state {\n" +
                $"\t\t\tself.{bitfieldName} |= 0x{TruncateConst(1 << bitOffset, size):X}\n" +
                  "\t\t} else {\n" +
                $"\t\t\tself.{bitfieldName} &= 0x{TruncateConst(~(1 << bitOffset), size):X}\n" +
                  "\t\t}\n" +
                  "\t}\n";
                
                return returnValue;

        }

        public string GenCommonHeader(string name, List<string> includeList) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"/* This file was automatically generated. */");
            sb.AppendLine();
            
            foreach (var header in includeList) {
                sb.AppendLine($"include!(\"{header}.rs\")");
            }

            return sb.ToString();
        }
        public string GenTraitHeader() {
            return "use std::ops::{Deref, DerefMut};\n\n" +
                "// Make a single generic wrapper for named params \n" +
                "pub struct ParamStruct<T> {\n" +
                "\tdata: T\n" +
                "}\n" +
                "\n" +
                "// Add a Deref implementation so ParamStruct<T> derefs to T\n" +
                "impl<T> Deref for ParamStruct<T> {\n" +
                "\ttype Target = T;\n" +
                "\n" +
                "\tfn deref(&self) -> &Self::Target {\n" +
                "\t\t&self.data\n" +
                "\t}\n" +
                "}\n" +
                "impl<T> DerefMut for ParamStruct<T> {" +
                "\tfn deref_mut(&mut self) -> &mut Self::Target {" +
                "\t\t&mut self.data" +
                "\t}" +
                "}" +
                "\n" +
                "pub trait Param {\n" +
                "\tconst NAME: &'static str;\n" +
                "\tconst TYPE_NAME: &'static str;\n" +
                "\tconst VERSION: u16;\n" +
                "\n" +
                "\tfn name() -> &'static str {\n" +
                "\t\tSelf::NAME\n" +
                "}\n" +
                "\t// So you can query the type constant from an `impl Param`\n" +
                "\tfn param_type_name() -> &'static str {\n" +
                "\t\tSelf::TYPE_NAME\n" +
                "\t}\n" +
                "\tfn version() -> u16 {\n" +
                "\t\tSelf::VERSION\n" +
                "\t}\n" +
                "\t// etc...\n" +
                "\t}\n" +
                "\n";
        }

        public string GenModHeader(List<string> includeList) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($@"/* This file was automatically generated. */
pub mod traits;
");
            foreach (var header in includeList)
            {
                sb.AppendLine($"#[cfg(feature = \"{header}\")]");
                sb.AppendLine($"pub mod {header};");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
