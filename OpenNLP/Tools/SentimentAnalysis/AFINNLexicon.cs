using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenNLP.Tools.SentimentAnalysis
{
    public class AFINNLexicon
    {
        /// <summary>
        /// The path to the lexicon.
        /// </summary>
        public string mPathToLexicon;

        /// <summary>
        /// A dictionary representing the word to polarity lexicon.
        /// </summary>
        public SortedDictionary<string, int> mWordToPolarityLexicon;

        /// <summary>
        /// 
        /// </summary>
        public AFINNLexicon(string pathToLexicon)
        {
            mWordToPolarityLexicon = new SortedDictionary<string, int>();
            mPathToLexicon = pathToLexicon;
            ReadLexicon(mPathToLexicon);
        }

        /// <summary>
        /// Reads the lexicon from the disk and fills the dictionary.
        /// </summary>
        /// <param name="pathToLexicon"></param>
        public void ReadLexicon(string pathToLexicon)
        {
            foreach (var line in File.ReadLines(pathToLexicon))
            {
                var wordToPolarity = line.Split('\t');
                mWordToPolarityLexicon[wordToPolarity[0]] = int.Parse(wordToPolarity[1]);
            }
        }

        /// <summary>
        /// Writes the dictionary in the lexicon format.
        /// </summary>
        /// <param name="pathToLexicon"></param>
        public void WriteLexicon(string pathToLexicon)
        {
            string contentToWrite = string.Empty;

            foreach (var wordToPolarity in mWordToPolarityLexicon)
                contentToWrite += wordToPolarity.Key + "\t" + wordToPolarity.Value + Environment.NewLine;

            System.IO.File.WriteAllText(mPathToLexicon, contentToWrite);
        }

        /// <summary>
        /// Matches the words in sentence with the lexicon and return a score.
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public int SentimentallyTagSentence(string[] sentence)
        {
            if (sentence.Contains("hacking"))
            {
                int l = 0;
                l++;
            }

            int sentencePolarity = 0;
            foreach (var word in sentence)
            {
                string wordToLower = word.ToLower();
                foreach (var wordToPolarity in mWordToPolarityLexicon)
                {
                    if (wordToLower == wordToPolarity.Key.ToLower())
                        sentencePolarity += wordToPolarity.Value;
                }
            }
            return sentencePolarity;
        }
    }
}
