using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

namespace ParamStructGenerator
{
    public static class ParamdefUtils
    {
        public enum UniqueNameMethod
        { 
            None,     // Internal field names are left unchanged.
            Counter,  // Append a number to fields which appear with the same internal name multiple times. Will not number unique fields.
            Offset,    // Append the field offset in bytes after the field's internal name. Also append the bit offset if necessary.
            CounterNumberAll // Like the Counter mode, but will number fields that only occur once too.
        }

        /// <summary>
        /// Modify the fields of a paramdef to ensure all internal field names are unique.
        /// </summary>
        /// <param name="def"></param>
        public static void MakeInternalNamesUnique(PARAMDEF def, UniqueNameMethod method, string numberFormat = null)
        {
            int bitOffset = 0;
            var fieldMap = new Dictionary<string, List<int>>();

            for (int i = 0; i < def.Fields.Count; i++)
            {
                PARAMDEF.Field field = def.Fields[i];

                if (!fieldMap.ContainsKey(field.InternalName))
                    fieldMap[field.InternalName] = new List<int>();

                List<int> sameNameFields = fieldMap[field.InternalName];

                switch (method)
                {
                    case UniqueNameMethod.Counter:
                        if (sameNameFields.Count == 1)
                            def.Fields[sameNameFields[0]].InternalName += $"_{0.ToString(numberFormat ?? "N0")}";
                        if (sameNameFields.Count > 0)
                            field.InternalName += $"_{sameNameFields.Count.ToString(numberFormat)}";
                        break;
                    case UniqueNameMethod.Offset:
                        field.InternalName += $"_{(bitOffset/8).ToString(numberFormat ?? "X3")}";
                        break;
                    case UniqueNameMethod.CounterNumberAll:
                        field.InternalName += $"_{sameNameFields.Count.ToString(numberFormat)}";
                        break;
                }

                sameNameFields.Add(i);
                if (field.BitSize == -1)
                    bitOffset += 8 * (ParamUtil.IsArrayType(field.DisplayType) ? ParamUtil.GetValueSize(field.DisplayType) * field.ArrayLength : ParamUtil.GetValueSize(field.DisplayType));
                else
                    bitOffset += field.BitSize;
            }
        }

        public static string FieldTypeToStdInt(PARAMDEF.DefType defType, bool typedefs = false)
        {
            if (!typedefs)
            {
                switch (defType)
                {
                    case PARAMDEF.DefType.u8:
                    case PARAMDEF.DefType.dummy8: return "unsigned char";
                    case PARAMDEF.DefType.s8: return "signed char";
                    case PARAMDEF.DefType.u16: return "unsigned short";
                    case PARAMDEF.DefType.s16: return "short";
                    case PARAMDEF.DefType.u32: return "unsigned int";
                    case PARAMDEF.DefType.s32: return "int";
                    case PARAMDEF.DefType.f32: return "float";

                    case PARAMDEF.DefType.fixstr: return "char";
                    case PARAMDEF.DefType.fixstrW: return "wchar_t";
                }
                return "unknown_type";
            }
            else
            {
                return defType.ToString();
            }
        }
        
        public static string FieldTypeToRust(PARAMDEF.DefType defType, bool typedefs = false)
        {
            if (!typedefs)
            {
                switch (defType)
                {
                    case PARAMDEF.DefType.u8:
                    case PARAMDEF.DefType.dummy8: return "u8";
                    case PARAMDEF.DefType.s8: return "i8";
                    case PARAMDEF.DefType.u16: return "u16";
                    case PARAMDEF.DefType.s16: return "i16";
                    case PARAMDEF.DefType.u32: return "u32";
                    case PARAMDEF.DefType.s32: return "i32";
                    case PARAMDEF.DefType.f32: return "f32";

                    case PARAMDEF.DefType.fixstr: return "u8";
                    case PARAMDEF.DefType.fixstrW: return "u16";
                }
                return "unknown_type";
            }
            else
            {
                return defType.ToString();
            }
        }
        
        public static int TruncateConst(int val, int size) {
            switch (size)
            {
                case 1: return val & 0xFF;
                case 2: return val & 0xFFFF;
                default: return val;
            }
        }
    }
}
