using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace System.Web.Script.Serialization {
    // Preserve the existing field-based on-disk format while modernizing the runtime.
    sealed class JavaScriptSerializer {
        public int MaxJsonLength {get;set;} = Int32.MaxValue;
        static readonly JsonSerializerOptions Options=CreateOptions();
        static JsonSerializerOptions CreateOptions(){var options=new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true};options.Converters.Add(new UntypedConverter());return options;}
        public T Deserialize<T>(string value){if(value.Length>MaxJsonLength)throw new ArgumentException("JSON exceeds configured limit");try{return JsonSerializer.Deserialize<T>(value,Options);}catch(JsonException error){throw new ArgumentException("Invalid JSON",error);}}
        public object DeserializeObject(string value){return Deserialize<object>(value);}
        public string Serialize(object value){return JsonSerializer.Serialize(value,Options);}
        sealed class UntypedConverter : JsonConverter<object> {
            public override object Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options){using(var document=JsonDocument.ParseValue(ref reader))return ConvertElement(document.RootElement);}
            static object ConvertElement(JsonElement value){switch(value.ValueKind){
                case JsonValueKind.Object:var dict=new Dictionary<string,object>();foreach(var property in value.EnumerateObject())dict[property.Name]=ConvertElement(property.Value);return dict;
                case JsonValueKind.Array:var list=new List<object>();foreach(var item in value.EnumerateArray())list.Add(ConvertElement(item));return list.ToArray();
                case JsonValueKind.String:return value.GetString();case JsonValueKind.True:return true;case JsonValueKind.False:return false;
                case JsonValueKind.Number:int integer;if(value.TryGetInt32(out integer))return integer;long large;if(value.TryGetInt64(out large))return large;return value.GetDouble();default:return null;
            }}
            public override void Write(Utf8JsonWriter writer,object value,JsonSerializerOptions options){if(value==null){writer.WriteNullValue();return;}if(value.GetType()==typeof(object)){writer.WriteStartObject();writer.WriteEndObject();return;}JsonSerializer.Serialize(writer,value,value.GetType(),options);}
        }
    }
}
