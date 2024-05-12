using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SoulsFormats;

namespace ParamStructGenerator
{
    public class CppParamCodeGen : IParamCodeGen
    {
        public string FileExtension => ".hpp";

        public string GenParamCode(PARAM param, string name, bool writeComments)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(
                $@"/* This file was automatically generated from regulation data. */
#ifndef _PARAM_{name}_H
#define _PARAM_{name}_H
#pragma once
#include ""defs/{param.ParamType}.h""
"
            );
            if (writeComments)
                sb.AppendLine($@"// Type: {param.ParamType}");
            sb.AppendLine(
                $@"struct {name} : {param.ParamType} {{
    static constexpr const char* param_type = ""{param.ParamType}"";
    static constexpr const char* param_name = ""{name}"";
    static constexpr const wchar_t* param_namew = L""{name}"";
}};
"
            );
            if (param.DetectedSize != -1)
                sb.AppendLine(
                    $"static_assert(sizeof({name}) == {param.DetectedSize}, \"{name} paramdef size does not match detected size\");"
                );

            sb.AppendLine("#endif");
            return sb.ToString();
        }

        public string GenParamdefCode(PARAMDEF def, bool writeComments, long detectedSize)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(
                $@"/* This file was automatically generated from XML paramdefs. */
#pragma once
"
            );
            if (writeComments)
            {
                sb.AppendLine(
                    $@"// Data Version: {def.DataVersion}
// Is Big Endian: {(def.BigEndian ? "True" : "False")}
// Is Unicode: {(def.Unicode ? "True" : "False")}
// Format Version: {def.FormatVersion}"
                );
            }

            sb.AppendLine("namespace from {");
            sb.AppendLine("namespace paramdef {");
            sb.AppendLine("");
            sb.AppendLine($"struct {def.ParamType} {{");

            foreach (PARAMDEF.Field field in def.Fields)
            {
                if (
                    ((field.DisplayName ?? "") != "" || (field.Description ?? "") != "")
                    && !Regex.Match(field.InternalName, "^unk[0-9A-Z]?").Success
                    && !Regex.Match(field.InternalName, "^pad[0-9]?").Success
                    && !Regex.Match(field.InternalName, "^reserve").Success
                    && !Regex.Match(field.InternalName, "^Reserve").Success
                )
                {
                    {
                        sb.AppendLine("\t/**");
                        if ((field.DisplayName ?? "") != "")
                            sb.AppendLine($"\t * @brief {field.DisplayName}");
                        if (
                            (field.Description ?? "") != ""
                            && field.Description != field.DisplayName
                        )
                        {
                            sb.AppendLine($"\t * ");
                            sb.AppendLine($"\t * {field.Description}");
                        }
                        sb.AppendLine("\t*/");
                    }
                }

                string fieldTypeName = ParamdefUtils.FieldTypeToStdInt(field.DisplayType);
                if (
                    (
                        field.DisplayType == PARAMDEF.DefType.u8
                        || field.DisplayType == PARAMDEF.DefType.dummy8
                        || field.DisplayType == PARAMDEF.DefType.s8
                        || field.DisplayType == PARAMDEF.DefType.u16
                    )
                    && (!ParamUtil.IsArrayType(field.DisplayType) || field.ArrayLength == 0)
                    && (field.BitSize < 2)
                    && (
                        field.InternalType.Contains("BOOL")
                        || field.InternalType.Contains("YESNO")
                        || field.InternalType.Contains("ON_OFF")
                        || field.InternalType == "DefaultKeyAssignPrioritySuppression"
                        || Regex.Match(field.InternalName, "^is").Success
                        || Regex.Match(field.InternalName, "^was").Success
                        || Regex.Match(field.InternalName, "^disable").Success
                        || Regex.Match(field.InternalName, "^invisibleFlag[0-9]").Success
                        || Regex.Match(field.InternalName, "^cumulateReset").Success
                        || Regex.Match(field.InternalName, "^enableLuck").Success
                        || Regex.Match(field.InternalName, "^modelDispMask[0-9]").Success
                        || Regex.Match(field.InternalName, "^slot[0-9]").Success
                        || Regex.Match(field.InternalName, "^b[A-Z]").Success
                    )
                )
                {
                    fieldTypeName = "bool";
                }

                bool isZeroSize = false;

                StringBuilder fieldBuilder = new StringBuilder();
                fieldBuilder.Append($"\t{fieldTypeName} {field.InternalName}");

                if (ParamUtil.IsBitType(field.DisplayType) && field.BitSize > 0)
                    fieldBuilder.Append($": {field.BitSize}");
                else if (field.BitSize != -1)
                    isZeroSize = true;
                else if (ParamUtil.IsArrayType(field.DisplayType) && field.ArrayLength > 0)
                    fieldBuilder.Append($"[{field.ArrayLength}]");
                else if (field.ArrayLength <= 0)
                    isZeroSize = true;

                if (
                    field.Default != null
                    && !((ParamUtil.IsArrayType(field.DisplayType) && field.ArrayLength > 0))
                )
                {
                    if (fieldTypeName == "bool")
                    {
                        if (Convert.ToSingle(field.Default) != 0)
                            fieldBuilder.Append(" { true }");
                        else
                            fieldBuilder.Append(" { false }");
                    }
                    else if (fieldTypeName == "float")
                    {
                        string defaultStr = $"{field.Default}";
                        if (defaultStr.Contains("."))
                            fieldBuilder.Append($" {{ {defaultStr}f }}");
                        else
                            fieldBuilder.Append($" {{ {defaultStr}.0f }}");
                    }
                    else
                    {
                        fieldBuilder.Append($" {{ {field.Default} }}");
                    }
                }

                // Comment out the field if it has zero size
                if (isZeroSize)
                    sb.Append("\t// ");
                sb.AppendLine($"{fieldBuilder};");
                sb.AppendLine();
            }

            sb.AppendLine("};");
            sb.AppendLine("");
            sb.AppendLine("}; // namespace paramdef");
            sb.AppendLine("}; // namespace from");

            if (detectedSize > 0)
            {
                sb.AppendLine("");
                sb.AppendLine(
                    $"static_assert(sizeof(from::paramdef::{def.ParamType}) == {detectedSize}, \"{def.ParamType} paramdef size does not match detected size\");"
                );
            }

            return sb.ToString();
        }

        public string GenCommonHeader(string name, List<string> includeList)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#pragma once");
            sb.AppendLine();
            foreach (var header in includeList)
                sb.AppendLine($"#include <param/paramdef/{header}.hpp>");
            return sb.ToString();
        }
    }
}
