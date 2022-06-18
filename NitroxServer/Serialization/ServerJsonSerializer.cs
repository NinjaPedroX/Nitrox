﻿using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NitroxModel.DataStructures.JsonConverter;
using NitroxModel.Platforms.OS.Shared;
using NitroxServer.Serialization.World;

namespace NitroxServer.Serialization
{
    public class ServerJsonSerializer : IServerSerializer
    {
        private readonly JsonSerializer serializer;

        public ServerJsonSerializer()
        {
            serializer = new JsonSerializer();

            serializer.Error += delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
            {
                string saveDir = null;
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith(WorldManager.SavesFolderDir, StringComparison.OrdinalIgnoreCase) && Directory.Exists(arg))
                    {
                        saveDir = arg;
                        break;
                    }
                }
                using StreamWriter errorFile = new(Path.Combine(saveDir, WorldManager.ErrorLogName), append: true);
                errorFile.WriteLineAsync($"Json serialization error: {e.ErrorContext.Error}");

            };

            serializer.TypeNameHandling = TypeNameHandling.Auto;
            serializer.ContractResolver = new AttributeContractResolver();
            serializer.Converters.Add(new NitroxIdConverter());
            serializer.Converters.Add(new TechTypeConverter());
            serializer.Converters.Add(new VersionConverter());
            serializer.Converters.Add(new KeyValuePairConverter());
            serializer.Converters.Add(new StringEnumConverter());
        }

        public string FileEnding => ".json";

        public void Serialize(Stream stream, object o)
        {
            stream.Position = 0;
            using JsonTextWriter writer = new(new StreamWriter(stream));
            serializer.Serialize(writer, o);
        }

        public void Serialize(string filePath, object o)
        {
            string tmpPath = Path.ChangeExtension(filePath, ".tmp");
            using (StreamWriter stream = File.CreateText(tmpPath))
            {
                serializer.Serialize(stream, o);
            }
            FileSystem.Instance.ReplaceFile(tmpPath, filePath);
        }

        public T Deserialize<T>(Stream stream)
        {
            stream.Position = 0;
            using JsonTextReader reader = new(new StreamReader(stream));
            return (T)serializer.Deserialize(reader, typeof(T));
        }

        public T Deserialize<T>(string filePath)
        {
            using StreamReader reader = File.OpenText(filePath);
            return (T)serializer.Deserialize(reader, typeof(T));
        }
    }
}
