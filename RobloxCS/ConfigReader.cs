﻿using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CS8618
sealed class CSharpOptions
{
    public string EntryPointName { get; set; }
    public string MainMethodName { get; set; }
    public string AssemblyName { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(EntryPointName) &&
               !string.IsNullOrEmpty(MainMethodName) &&
               !string.IsNullOrEmpty(AssemblyName);
    }
}

sealed class ConfigData
{
    public string SourceFolder { get; set; }
    public string OutputFolder { get; set; }
    public CSharpOptions CSharpOptions { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(SourceFolder) &&
               !string.IsNullOrEmpty(OutputFolder) &&
               CSharpOptions != null && CSharpOptions.IsValid();
    }
}
#pragma warning restore CS8618

namespace RobloxCS
{

    internal static class ConfigReader
    {
        private const string _fileName = "roblox-cs.yml";

        public static ConfigData Read(string inputDirectory)
        {
            var configPath = inputDirectory + "/" + _fileName;
            ConfigData? config = default;
            string ymlContent = default!;

            try
            {
                ymlContent = File.ReadAllText(configPath);
            }
            catch (Exception e)
            {
                FailToRead(e.Message);
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithAttemptingUnquotedStringTypeDeserialization()
                .WithDuplicateKeyChecking()
                .Build();

            try
            {
                config = deserializer.Deserialize<ConfigData>(ymlContent);
            }
            catch (Exception e)
            {
                FailToRead(e.ToString());
            }

            if (config == null || !config.IsValid())
            {
                FailToRead("Invalid config! Make sure it has all required fields.");
            }

            return config!;
        }

        private static void FailToRead(string message)
        {
            Logger.Error($"Failed to read {_fileName}!\n{message}");
        }
    }
}