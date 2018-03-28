using System;
using System.Collections.Generic;
using System.Text;

using More;

#if WindowsCE
using ArrayCopier = System.MissingInCEArrayCopier;
#else
using ArrayCopier = System.Array;
#endif

namespace More.Net
{
    public static class Xdr
    {
        public static UInt32 UpToNearestMod4(UInt32 value)
        {
            UInt32 mod4 = value & 0x3;
            if (mod4 == 0) return value;
            return value + 4 - mod4;
        }
    }
    /*
    public struct XdrBoolean : ISerializer
    {
        Boolean value;
        public XdrBoolean(Boolean value)                  { this.value = value; }
        public UInt32 FixedSerializationLength()           { return 4;           }
        public UInt32 SerializationLength()                { return 4;           }
        public UInt32 Serialize(Byte[] array, UInt32 offset)
        {
            array[offset    ] = 0;
            array[offset + 1] = 0;
            array[offset + 2] = 0;
            array[offset + 3] = (value) ? (Byte)1 : (Byte)0;
            return offset + 4;
        }
        public UInt32 Deserialize(Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            this.value = (array[offset + 3] == 0) ? false : true;
            return offset + 4;
        }
        public String DataString()                         { return value.ToString();          }
        public void DataString(StringBuilder builder)      { builder.Append(value.ToString()); }
        public String DataSmallString()                    { return value.ToString();          }
        public void DataSmallString(StringBuilder builder) { builder.Append(value.ToString()); }
    }
    public struct XdrEnum<EnumType> : ISerializer
    {
        Enum value;
        public XdrEnum(Enum value)                        { this.value = value; }
        public UInt32 FixedSerializationLength()           { return 4;           }
        public UInt32 SerializationLength()                { return 4;           }
        public UInt32 Serialize(Byte[] array, UInt32 offset)
        {
            Int32 valueAsInt32 = Convert.ToInt32(value);
            array[offset    ] = (Byte)(valueAsInt32 >> 24);
            array[offset + 1] = (Byte)(valueAsInt32 >> 16);
            array[offset + 2] = (Byte)(valueAsInt32 >> 8);
            array[offset + 3] = (Byte)(valueAsInt32);
            return offset + 4;
        }
        public UInt32 Deserialize(Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            Int32 valueAsInt32 = (
                (Int32)(0xFF000000 & (array[offset] << 24)) |
                       (0x00FF0000 & (array[offset + 1] << 16)) |
                       (0x0000FF00 & (array[offset + 2] << 8)) |
                       (0x000000FF & (array[offset + 3])));
            this.value = (Enum)Enum.ToObject(typeof(EnumType), valueAsInt32);

            return offset + 4;
        }
        public String DataString()                         { return value.ToString();          }
        public void DataString(StringBuilder builder)      { builder.Append(value.ToString()); }
        public String DataSmallString()                    { return value.ToString();          }
        public void DataSmallString(StringBuilder builder) { builder.Append(value.ToString()); }
    }
    */
    public class XdrBooleanReflector : ClassFieldReflector
    {
        public XdrBooleanReflector(Type typeThatContainsThisField, String fieldName)
            : base(typeThatContainsThisField, fieldName, typeof(Boolean))
        {
        }
        public override UInt32 FixedSerializationLength()
        {
            return 4;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            return 4;
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Boolean value = (Boolean)fieldInfo.GetValue(instance);
            array[offset]     = 0;
            array[offset + 1] = 0;
            array[offset + 2] = 0;
            array[offset + 3] = (value) ? (Byte)1 : (Byte)0;
            return offset + 4;

        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            fieldInfo.SetValue(instance, (array[offset + 3] == 0) ? false : true);
            return offset + 4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(String.Format("{0}:{1}", fieldInfo.Name, (Boolean)fieldInfo.GetValue(instance)));
        }
    }
    public class XdrEnumReflector : ClassFieldReflector
    {
        //Type enumType;
        public XdrEnumReflector(Type typeThatContainsThisField, String fieldName, Type enumType)
            : base(typeThatContainsThisField, fieldName, enumType)
        {
            //this.enumType = enumType;
        }
        public override UInt32 FixedSerializationLength()
        {
            return 4;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            return 4;
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Enum valueAsEnum  = (Enum)fieldInfo.GetValue(instance);
            Int32 value = Convert.ToInt32(valueAsEnum);
            array[offset    ] = (Byte)(value >> 24);
            array[offset + 1] = (Byte)(value >> 16);
            array[offset + 2] = (Byte)(value >>  8);
            array[offset + 3] = (Byte)(value      );
            return offset + 4;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            Int32 value = (Int32)(
                (Int32)(0xFF000000 & (array[offset    ] << 24)) |
                       (0x00FF0000 & (array[offset + 1] << 16)) |
                       (0x0000FF00 & (array[offset + 2] <<  8)) |
                       (0x000000FF & (array[offset + 3])));
            fieldInfo.SetValue(instance, value);
            return offset + 4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(String.Format("{0}:{1}", fieldInfo.Name, fieldInfo.GetValue(instance)));
        }
    }
    public class XdrOpaqueFixedLengthReflector : ClassFieldReflector
    {
        public readonly UInt32 dataLength;
        public readonly UInt32 dataLengthNearestContainingMod4;

        public XdrOpaqueFixedLengthReflector(Type typeThatContainsThisField, String fieldName, UInt32 dataLength)
            : base(typeThatContainsThisField, fieldName, typeof(Byte[]))
        {
            this.dataLength = dataLength;
            this.dataLengthNearestContainingMod4 = Xdr.UpToNearestMod4(dataLength);
        }
        public override UInt32 FixedSerializationLength()
        {
            return dataLengthNearestContainingMod4;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            return dataLengthNearestContainingMod4;
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            if (dataLength <= 0) return offset;

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                Array.Clear(array, (Int32)offset, (Int32)dataLength);
            }
            else
            {
                Byte[] valueAsArray = (Byte[])valueAsObject;

                if (valueAsArray.Length != dataLength)
                    throw new InvalidOperationException(String.Format("The XdrOpaqueFixedLength length is {0}, but your the byte array for field '{1}' length is {2}",
                        dataLength, fieldInfo.Name, valueAsArray.Length));

                ArrayCopier.Copy(valueAsArray, 0, array, offset, dataLength);
            }

            // Add Padding
            for (UInt32 i = dataLength; i < dataLengthNearestContainingMod4; i++)
            {
                array[i] = 0;
            }

            return offset + dataLengthNearestContainingMod4;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            Byte[] data = new Byte[dataLength];
            ArrayCopier.Copy(array, offset, data, 0, dataLength);

            fieldInfo.SetValue(instance, data);

            return offset + dataLengthNearestContainingMod4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            String dataString;

            Byte[] valueAsArray = (Byte[])fieldInfo.GetValue(instance);
            if (valueAsArray == null)
            {
                builder.Append(String.Format("[{0} bytes at value 0]", dataLength));
            }
            else
            {
                dataString = BitConverter.ToString(valueAsArray);
                builder.Append(String.Format("{0}:{1}", fieldInfo.Name, dataString));
            }
        }
    }
    public class XdrOpaqueVarLengthReflector2 : ClassFieldReflector
    {
        public readonly UInt32 maxLength;

        public XdrOpaqueVarLengthReflector2(Type typeThatContainsThisField, String fieldName, UInt32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(Slice<Byte>))
        {
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Slice<Byte> segment = (Slice<Byte>)fieldInfo.GetValue(instance);
            return 4 + Xdr.UpToNearestMod4(segment.length);
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Slice<Byte> segment = (Slice<Byte>)fieldInfo.GetValue(instance);

            array[offset    ] = (Byte)(segment.length >> 24);
            array[offset + 1] = (Byte)(segment.length >> 16);
            array[offset + 2] = (Byte)(segment.length >>  8);
            array[offset + 3] = (Byte)(segment.length      );
            offset += 4;

            ArrayCopier.Copy(segment.array, segment.offset, array, offset, segment.length);

            UInt32 valueAsArrayMod4Length = Xdr.UpToNearestMod4(segment.length);
            for (UInt32 i = segment.length; i < valueAsArrayMod4Length; i++)
            {
                array[offset + i] = 0;
            }

            return offset + valueAsArrayMod4Length;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            UInt32 length = array.BigEndianReadUInt32(offset);
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, new Slice<Byte>(null, 0, 0));
                return offset;
            }

            fieldInfo.SetValue(instance, new Slice<Byte>(array, offset, length));

            UInt32 lengthMod4 = Xdr.UpToNearestMod4(length);

            return offset + lengthMod4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            Slice<Byte> segment = (Slice<Byte>)fieldInfo.GetValue(instance);
            builder.Append(fieldInfo.Name);
            builder.Append(":[");
            for (int i = 0; i < segment.length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(segment.array[segment.offset + i]);
            }
            builder.Append(']');
        }
    }





    //
    // TODO: Check For Max Length
    //
    public class XdrOpaqueVarLengthReflector : ClassFieldReflector
    {
        public readonly UInt32 maxLength;

        public XdrOpaqueVarLengthReflector(Type typeThatContainsThisField, String fieldName, UInt32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(Byte[]))
        {
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null) return 4;
            return 4 + Xdr.UpToNearestMod4((UInt32)((Byte[])valueAsObject).Length);
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                array[offset    ] = 0;
                array[offset + 1] = 0;
                array[offset + 2] = 0;
                array[offset + 3] = 0;
                return offset + 4;
            }

            Byte[] valueAsArray = (Byte[])valueAsObject;

            array[offset    ] = (Byte)(valueAsArray.Length >> 24);
            array[offset + 1] = (Byte)(valueAsArray.Length >> 16);
            array[offset + 2] = (Byte)(valueAsArray.Length >>  8);
            array[offset + 3] = (Byte)(valueAsArray.Length      );
            offset += 4;

            ArrayCopier.Copy(valueAsArray, 0, array, offset, valueAsArray.Length);

            UInt32 valueAsArrayMod4Length = Xdr.UpToNearestMod4((UInt32)valueAsArray.Length);
            for (UInt32 i = (UInt32)valueAsArray.Length; i < valueAsArrayMod4Length; i++)
            {
                array[offset + i] = 0;
            }

            return offset + valueAsArrayMod4Length;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            UInt32 length = array.BigEndianReadUInt32(offset);
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, null);
                return offset;
            }

            Byte[] data = new Byte[length];
            ArrayCopier.Copy(array, offset, data, 0, length);

            fieldInfo.SetValue(instance, data);

            UInt32 lengthMod4 = Xdr.UpToNearestMod4(length);

            return offset + lengthMod4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(fieldInfo.Name);
            builder.Append(':');

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                builder.Append("null");
            }
            else
            {
                Byte[] valueAsArray = (Byte[])valueAsObject;
                builder.Append('[');
                for (int i = 0; i < valueAsArray.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(valueAsArray[i].ToString());
                }
                builder.Append(']');
            }
        }
    }

    public class XdrOpaqueVarLengthReflector<SerializationType> : ClassFieldReflector where SerializationType : ISerializer, new()
    {
        public readonly Int32 maxLength;

        public XdrOpaqueVarLengthReflector(Type typeThatContainsThisField, String fieldName, Int32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(SerializationType))
        {
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null) return 4;

            SerializationType value = (SerializationType)valueAsObject;
            return 4 + Xdr.UpToNearestMod4(value.SerializationLength());
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                array[offset    ] = 0;
                array[offset + 1] = 0;
                array[offset + 2] = 0;
                array[offset + 3] = 0;
                return offset + 4;
            }

            SerializationType value = (SerializationType)valueAsObject;

            UInt32 offsetForSize = offset;
            offset = value.Serialize(array, offset + 4);

            UInt32 valueSize = offset - offsetForSize - 4;
            array[offsetForSize    ] = (Byte)(valueSize >> 24);
            array[offsetForSize + 1] = (Byte)(valueSize >> 16);
            array[offsetForSize + 2] = (Byte)(valueSize >>  8);
            array[offsetForSize + 3] = (Byte)(valueSize      );

            UInt32 valueAsArrayMod4Length = Xdr.UpToNearestMod4(valueSize);
            for (UInt32 i = valueSize; i < valueAsArrayMod4Length; i++)
            {
                array[offset++] = 0;
            }

            return offset;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            UInt32 length = array.BigEndianReadUInt32(offset);
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, null);
                return offset;
            }

            SerializationType value = new SerializationType();
            offset = value.Deserialize(array, offset, offset + length);

            fieldInfo.SetValue(instance, value);

            UInt32 lengthMod4 = Xdr.UpToNearestMod4(length);

            return offset + lengthMod4 - length;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(fieldInfo.Name);
            builder.Append(':');

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                builder.Append("<null>");
            }
            else
            {
                SerializationType value = (SerializationType)valueAsObject;
                value.DataString(builder);
            }
        }
        public override void DataSmallString(Object instance, StringBuilder builder)
        {
            builder.Append(fieldInfo.Name);
            builder.Append(':');

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                builder.Append("<null>");
            }
            else
            {
                SerializationType value = (SerializationType)valueAsObject;
                value.DataSmallString(builder);
            }
        }
    }

    //
    // TODO: Check For Max Length
    //
    public class XdrStringReflector : ClassFieldReflector
    {
        public readonly UInt32 maxLength;

        public XdrStringReflector(Type typeThatContainsThisField, String fieldName, UInt32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(String))
        {
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null) return 0;
            return 4 + Xdr.UpToNearestMod4((UInt32)((String)valueAsObject).Length);
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                array[offset    ] = 0;
                array[offset + 1] = 0;
                array[offset + 2] = 0;
                array[offset + 3] = 0;
                return offset + 4;
            }

            String valueAsString = (String)valueAsObject;

            array[offset    ] = (Byte)(valueAsString.Length >> 24);
            array[offset + 1] = (Byte)(valueAsString.Length >> 16);
            array[offset + 2] = (Byte)(valueAsString.Length >>  8);
            array[offset + 3] = (Byte)(valueAsString.Length      );
            offset += 4;

            for (int i = 0; i < valueAsString.Length; i++)
            {
                array[offset++] = (Byte)valueAsString[i];
            }

            UInt32 valueAsArrayMod4Length = Xdr.UpToNearestMod4((UInt32)valueAsString.Length);
            for (UInt32 i = (UInt32)valueAsString.Length; i < valueAsArrayMod4Length; i++)
            {
                array[offset++] = 0;
            }

            return offset;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            UInt32 length = array.BigEndianReadUInt32(offset);
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, null);
                return offset;
            }

            String data = Encoding.UTF8.GetString(array, (Int32)offset, (Int32)length);
            fieldInfo.SetValue(instance, data);

            UInt32 lengthMod4 = Xdr.UpToNearestMod4(length);

            return offset + lengthMod4;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            String dataString;

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                dataString = "null";
            }
            else
            {
                dataString = (String)valueAsObject;
            }
            builder.Append(String.Format("{0}:\"{1}\"", fieldInfo.Name, dataString));
        }
    }


    /*
    public class XdrVarLengthArray<ElementType> : ClassFieldReflector where ElementType : ISerializer, new()
    {
        public readonly Int32 maxLength;

        public XdrVarLengthArray(Type typeThatContainsThisField, String fieldName, Int32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(ElementType[]))
        {
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null) return 4;

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            if (valueAsArray.Length <= 0) return 4;
            UInt32 elementFixedSerializationLength = valueAsArray[0].FixedSerializationLength();
            if (elementFixedSerializationLength != UInt32.MaxValue)
            {
                return 4 + (elementFixedSerializationLength * (UInt32)valueAsArray.Length);
            }

            UInt32 dataLength = 0;
            for (int i = 0; i < valueAsArray.Length; i++)
            {
                dataLength += valueAsArray[i].SerializationLength();
            }

            return 4 + dataLength;
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                array[offset    ] = 0;
                array[offset + 1] = 0;
                array[offset + 2] = 0;
                array[offset + 3] = 0;
                return offset + 4;
            }

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            array[offset    ] = (Byte)(valueAsArray.Length >> 24);
            array[offset + 1] = (Byte)(valueAsArray.Length >> 16);
            array[offset + 2] = (Byte)(valueAsArray.Length >>  8);
            array[offset + 3] = (Byte)(valueAsArray.Length      );
            offset += 4;

            for (int i = 0; i < valueAsArray.Length; i++)
            {
                offset = valueAsArray[i].Serialize(array, offset);
            }

            return offset;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            Int32 length = (Int32)(
                (Int32)(0xFF000000 & (array[offset] << 24)) |
                       (0x00FF0000 & (array[offset + 1] << 16)) |
                       (0x0000FF00 & (array[offset + 2] << 8)) |
                       (0x000000FF & (array[offset + 3])));
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, null);
                return offset;
            }

            ElementType[] data = new ElementType[length];
            
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new ElementType();
                offset = data[i].Deserialize(array, offset, maxOffset);
            }

            fieldInfo.SetValue(instance, data);

            return offset;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(fieldInfo.Name);
            builder.Append(':');

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                builder.Append("null");
                return;
            }

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            builder.Append('[');
            for (int i = 0; i < valueAsArray.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                valueAsArray[i].DataString(builder);
            }
            builder.Append(']');
        }
    }
    */

    /*
    public class XdrVarLengthArray<ElementType> : ClassFieldReflector
    {
        readonly IInstanceSerializer<T> elementSerializer;
        readonly UInt32 elementFixedSerializationLength;
        public readonly UInt32 maxLength;

        public XdrVarLengthArray(Type typeThatContainsThisField, String fieldName,
            IInstanceSerializer<T> elementSerializer,UInt32 maxLength)
            : base(typeThatContainsThisField, fieldName, typeof(ElementType[]))
        {
            this.elementSerializer = elementSerializer;
            this.maxLength = maxLength;
        }
        public override UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue;
        }
        public override UInt32 SerializationLength(Object instance)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null) return 4;

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            if (valueAsArray.Length <= 0) return 4;
            UInt32 elementFixedSerializationLength = valueAsArray[0].FixedSerializationLength();
            if (elementFixedSerializationLength != UInt32.MaxValue)
            {
                return 4 + (elementFixedSerializationLength * (UInt32)valueAsArray.Length);
            }

            UInt32 dataLength = 0;
            for (int i = 0; i < valueAsArray.Length; i++)
            {
                dataLength += valueAsArray[i].SerializationLength();
            }

            return 4 + dataLength;
        }
        public override UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                array[offset] = 0;
                array[offset + 1] = 0;
                array[offset + 2] = 0;
                array[offset + 3] = 0;
                return offset + 4;
            }

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            array[offset] = (Byte)(valueAsArray.Length >> 24);
            array[offset + 1] = (Byte)(valueAsArray.Length >> 16);
            array[offset + 2] = (Byte)(valueAsArray.Length >> 8);
            array[offset + 3] = (Byte)(valueAsArray.Length);
            offset += 4;

            for (int i = 0; i < valueAsArray.Length; i++)
            {
                offset = valueAsArray[i].Serialize(array, offset);
            }

            return offset;
        }
        public override UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 maxOffset)
        {
            Int32 length = (Int32)(
                (Int32)(0xFF000000 & (array[offset] << 24)) |
                       (0x00FF0000 & (array[offset + 1] << 16)) |
                       (0x0000FF00 & (array[offset + 2] << 8)) |
                       (0x000000FF & (array[offset + 3])));
            offset += 4;

            if (length == 0)
            {
                fieldInfo.SetValue(instance, null);
                return offset;
            }

            ElementType[] data = new ElementType[length];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new ElementType();
                offset = data[i].Deserialize(array, offset, maxOffset);
            }

            fieldInfo.SetValue(instance, data);

            return offset;
        }
        public override void DataString(Object instance, StringBuilder builder)
        {
            builder.Append(fieldInfo.Name);
            builder.Append(':');

            Object valueAsObject = fieldInfo.GetValue(instance);
            if (valueAsObject == null)
            {
                builder.Append("null");
                return;
            }

            ElementType[] valueAsArray = (ElementType[])valueAsObject;

            builder.Append('[');
            for (int i = 0; i < valueAsArray.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                valueAsArray[i].DataString(builder);
            }
            builder.Append(']');
        }
    }
    */

    public class XdrBooleanDescriminateReflector : IReflector
    {
        public readonly UInt32 fixedSerializationLength;

        readonly XdrBooleanReflector descriminate;
        readonly IReflector[] trueReflectors;
        readonly IReflector[] falseReflectors;

        public XdrBooleanDescriminateReflector(XdrBooleanReflector descriminate,
            Reflectors trueReflectors, Reflectors falseReflectors)
        {
            this.descriminate = descriminate;
            this.trueReflectors = trueReflectors.reflectors;
            this.falseReflectors = falseReflectors.reflectors;

            //
            // Determine if serialization length is fixed
            //
            if (trueReflectors.fixedSerializationLength != UInt32.MaxValue &&
                falseReflectors.fixedSerializationLength != UInt32.MaxValue &&
                trueReflectors.fixedSerializationLength == falseReflectors.fixedSerializationLength)
            {
                this.fixedSerializationLength = trueReflectors.fixedSerializationLength + 4;
            }
            else
            {
                this.fixedSerializationLength = UInt32.MaxValue;
            }           
        }
        private IReflector[] GetReflectors(Object instance)
        {
            if (instance == null)
                throw new InvalidOperationException("Cannot retreive the descriminate value because the instance of this object is null");
            Boolean descriminateValue = (Boolean)descriminate.fieldInfo.GetValue(instance);
            return descriminateValue ? trueReflectors : falseReflectors ;
        }
        public UInt32 FixedSerializationLength()
        {
            return fixedSerializationLength;
        }
        public UInt32 SerializationLength(Object instance)
        {
            if (fixedSerializationLength != UInt32.MaxValue) return fixedSerializationLength;

            UInt32 length = 4; // 4 bytes for the boolean descriminate

            IReflector[] fieldReflectors = GetReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                length += fieldReflectors[i].SerializationLength(instance);
            }

            return length;
        }
        public UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            offset = descriminate.Serialize(instance, array, offset);

            IReflector[] fieldReflectors = GetReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                offset = fieldReflectors[i].Serialize(instance, array, offset);
            }

            return offset;
        }
        public UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 offsetLimit)
        {
            offset = descriminate.Deserialize(instance, array, offset, offsetLimit);

            IReflector[] fieldReflectors = GetReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                offset = fieldReflectors[i].Deserialize(instance, array, offset, offsetLimit);
            }

            return offset;
        }
        public void DataString(Object instance, StringBuilder builder)
        {
            descriminate.DataString(instance, builder);

            IReflector[] fieldReflectors = GetReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                builder.Append(',');
                fieldReflectors[i].DataString(instance, builder);
            }
        }
        public void DataSmallString(Object instance, StringBuilder builder)
        {
            descriminate.DataSmallString(instance, builder);

            IReflector[] fieldReflectors = GetReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                builder.Append(',');
                fieldReflectors[i].DataSmallString(instance, builder);
            }
        }
    }
    public class XdrDescriminatedUnionReflector<DescriminateCSharpType> : IReflector
    {
        public class KeyAndSerializer
        {
            public readonly DescriminateCSharpType descriminateKey;
            public readonly IReflector[] fieldReflectors;

            public KeyAndSerializer(DescriminateCSharpType descriminateKey,
                IReflector[] fieldReflectors)
            {
                this.descriminateKey = descriminateKey;
                this.fieldReflectors = fieldReflectors;
            }
        }

        ClassFieldReflector descriminate;
        Dictionary<DescriminateCSharpType, IReflector[]> unionDictionary;
        IReflector[] defaultFieldReflectors;

        /*
        public XdrDescriminatedUnionReflector(FieldReflectorSerializer descriminate, params KeyAndSerializer[] fieldKeyAndSerializers)
            : this(descriminate, null, fieldKeyAndSerializers)
        {
        }
        */
        public XdrDescriminatedUnionReflector(ClassFieldReflector descriminate, IReflector[] defaultFieldReflectors,
            params KeyAndSerializer[] fieldKeyAndSerializers)
        {
            this.descriminate = descriminate;

            this.descriminate = descriminate;
            this.defaultFieldReflectors = defaultFieldReflectors;

            this.unionDictionary = new Dictionary<DescriminateCSharpType, IReflector[]>();
            for (int i = 0; i < fieldKeyAndSerializers.Length; i++)
            {
                KeyAndSerializer keyAndSerializer = fieldKeyAndSerializers[i];
                unionDictionary.Add(keyAndSerializer.descriminateKey,
                    keyAndSerializer.fieldReflectors);
            }
        }
        private IReflector[] GetFieldReflectors(Object instance)
        {
            if (instance == null)
            {
                throw new InvalidOperationException(String.Format("The descriminate value (fieldInfo.Name='{0}') is 'null'", descriminate.fieldInfo.Name));
            }

            DescriminateCSharpType descriminateValue = (DescriminateCSharpType)descriminate.fieldInfo.GetValue(instance);
            IReflector[] fieldReflectors;

            if (!unionDictionary.TryGetValue(descriminateValue, out fieldReflectors))
            {
                if (defaultFieldReflectors == null)
                {
                    throw new InvalidOperationException(String.Format("The descriminate value '{0}' was not found in the union dictionary and there's no default descriminate", descriminateValue));
                }
                return defaultFieldReflectors;
            }

            return fieldReflectors;
        }
        public UInt32 FixedSerializationLength()
        {
            return UInt32.MaxValue; // TODO: Maybe I should check for fixed length later?
        }
        public UInt32 SerializationLength(Object instance)
        {
            UInt32 length = descriminate.SerializationLength(instance);

            IReflector[] fieldReflectors = GetFieldReflectors(instance);

            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                length += fieldReflectors[i].SerializationLength(instance);
            }

            return length;
        }
        public UInt32 Serialize(Object instance, Byte[] array, UInt32 offset)
        {
            offset = descriminate.Serialize(instance, array, offset);
            
            IReflector[] fieldReflectors = GetFieldReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                offset = fieldReflectors[i].Serialize(instance, array, offset);
            }
            return offset;
        }
        public UInt32 Deserialize(Object instance, Byte[] array, UInt32 offset, UInt32 offsetLimit)
        {
            offset = descriminate.Deserialize(instance, array, offset, offsetLimit);

            IReflector[] fieldReflectors = GetFieldReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                offset = fieldReflectors[i].Deserialize(instance, array, offset, offsetLimit);
            }

            return offset;
        }
        public void DataString(Object instance, StringBuilder builder)
        {
            descriminate.DataString(instance, builder);

            IReflector[] fieldReflectors = GetFieldReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                builder.Append(',');
                fieldReflectors[i].DataString(instance, builder);
            }
        }
        public void DataSmallString(Object instance, StringBuilder builder)
        {
            descriminate.DataSmallString(instance, builder);

            IReflector[] fieldReflectors = GetFieldReflectors(instance);
            for (int i = 0; i < fieldReflectors.Length; i++)
            {
                builder.Append(',');
                fieldReflectors[i].DataSmallString(instance, builder);
            }
        }
    }
}
