using System;
using System.Text.Json;

namespace appMpower.Serializers
{
   public static class WorldSerializer
   {
      public static void Serialize(Utf8JsonWriter utf8JsonWriter, Int16 id, Int16 randomNumber)
      {
         utf8JsonWriter.WriteStartObject();
         utf8JsonWriter.WriteNumber("id", id);
         utf8JsonWriter.WriteNumber("randomNumber", randomNumber);
         utf8JsonWriter.WriteEndObject();
      }
   }
}