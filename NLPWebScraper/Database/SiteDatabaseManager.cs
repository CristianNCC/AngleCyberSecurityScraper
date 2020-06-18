using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace NLPWebScraper
{
    public static class SiteDatabaseManager
    {
        public static List<SiteTopWordsEntry> extractionDatabase = new List<SiteTopWordsEntry>();
        public const string databasePath = "../Files/siteDatabase.json";
        public const int databaseUpdateCount = 100;

        #region Serialization/Deserialization
        public static void SerializeSiteInformation()
        {
            string output = JsonConvert.SerializeObject(extractionDatabase);
            File.WriteAllText(databasePath, output);
        }

        public static void DeserializeSiteInformation()
        {
            if (!File.Exists(databasePath) || extractionDatabase.Count > 0)
                return;

            string output = File.ReadAllText(databasePath);

            if (string.IsNullOrEmpty(output))
                return;

            extractionDatabase = JsonConvert.DeserializeObject<List<SiteTopWordsEntry>>(output);
        }
        #endregion
    }
}
