using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// Reads only the schema version, so a newer schema is recognised even when its body cannot be parsed.
    /// </summary>
    [DataContract]
    internal sealed class SettingsSchemaProbe
    {
        [DataMember]
        public int SchemaVersion { get; set; }
    }

    /// <summary>
    /// JSON of the settings file: DataContractJsonSerializer with the simple dictionary format.
    /// </summary>
    internal static class SettingsJson
    {
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        /// <summary>UTF-8 without BOM, one line without indentation.</summary>
        public static byte[] Serialize(NumberingSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                CreateSerializer(typeof(NumberingSettings)).WriteObject(stream, settings);
                return stream.ToArray();
            }
        }

        /// <summary>Strips the UTF-8 BOM; serializer exceptions are passed through as they are.</summary>
        public static T Deserialize<T>(byte[] content) where T : class
        {
            int offset = HasBom(content) ? Utf8Bom.Length : 0;
            using (var stream = new MemoryStream(content, offset, content.Length - offset, false))
            {
                return CreateSerializer(typeof(T)).ReadObject(stream) as T;
            }
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type,
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }

        private static bool HasBom(byte[] content)
        {
            if (content.Length < Utf8Bom.Length)
            {
                return false;
            }

            for (int i = 0; i < Utf8Bom.Length; i++)
            {
                if (content[i] != Utf8Bom[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
