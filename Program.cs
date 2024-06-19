using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Directory = Lucene.Net.Store.Directory;

namespace ConsoleApp1
{
    class Program
    {
        static Dictionary<string, double> PageRankScores = new Dictionary<string, double>();
        static Dictionary<string, (double HubScore, double AuthorityScore)> HITSScores = new Dictionary<string, (double, double)>();
        static Dictionary<string, List<string>> LinkGraph = new Dictionary<string, List<string>>();

        static void Main(string[] args)
        {
            createindex();
            PerformLinkAnalysis();

            Console.WriteLine("Enter the search query:");
            string query = Console.ReadLine();
            searchindex(query);
        }

        static void createindex()
        {
            string indexFileLocation = @"E:\WIR CS-479\Assignment 5\Lucene Search\Index";
            Directory dir = FSDirectory.Open(new DirectoryInfo(indexFileLocation));
            StandardAnalyzer analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);

            IndexWriter indexWriter = new IndexWriter(dir, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED);

            string metaFilePath = @"E:\WIR CS-479\Assignment 5\Lucene Search\Index\meta.txt";

            string[] metaLines = File.ReadAllLines(metaFilePath);

            string docID = null;
            string bodyContent = null;
            string url = null;
            List<string> outgoingLinks = null;

            foreach (string line in metaLines)
            {
                if (line.StartsWith("Web Page ID:"))
                {
                    docID = ExtractValue(line);
                }
                else if (line.StartsWith("Page Body:"))
                {
                    bodyContent = ExtractValue(line);
                }
                else if (line.StartsWith("URI:"))
                {
                    url = ExtractValue(line);
                }
                else if (line.StartsWith("Outgoing Links:"))
                {
                    outgoingLinks = ExtractValue(line).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                if (!string.IsNullOrEmpty(docID) && !string.IsNullOrEmpty(bodyContent) && !string.IsNullOrEmpty(url) && outgoingLinks != null)
                {
                    Document doc = new Document();
                    doc.Add(new Field("content", bodyContent, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.YES));
                    doc.Add(new Field("docID", docID, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("url", url, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    indexWriter.AddDocument(doc);

                    LinkGraph[docID] = outgoingLinks;

                    docID = null;
                    bodyContent = null;
                    url = null;
                    outgoingLinks = null;
                }
            }

            indexWriter.Optimize();
            indexWriter.Close();

            GenerateInvertedIndexCSV(indexFileLocation);
        }

        static string ExtractValue(string line)
        {
            int colonIndex = line.IndexOf(": ");
            if (colonIndex >= 0)
            {
                return line.Substring(colonIndex + 2).Trim();
            }
            return string.Empty;
        }

        static void GenerateInvertedIndexCSV(string indexFileLocation)
        {
            Directory dir = FSDirectory.Open(new DirectoryInfo(indexFileLocation));
            IndexReader reader = IndexReader.Open(dir, true);

            Dictionary<string, (int Frequency, List<string> PostingList)> invertedIndex = new Dictionary<string, (int, List<string>)>();

            for (int i = 0; i < reader.MaxDoc; i++)
            {
                if (reader.IsDeleted(i)) continue;

                Document doc = reader.Document(i);
                string docID = doc.Get("docID");
                ITermFreqVector termFreqVector = reader.GetTermFreqVector(i, "content");

                if (termFreqVector != null)
                {
                    string[] terms = termFreqVector.GetTerms();
                    int[] freqs = termFreqVector.GetTermFrequencies();

                    for (int j = 0; j < terms.Length; j++)
                    {
                        string term = terms[j];
                        int freq = freqs[j];

                        if (freq > 0)
                        {
                            if (invertedIndex.ContainsKey(term))
                            {
                                invertedIndex[term] = (invertedIndex[term].Frequency + freq, invertedIndex[term].PostingList.Append(docID).ToList());
                            }
                            else
                            {
                                invertedIndex[term] = (freq, new List<string> { docID });
                            }
                        }
                    }
                }
            }

            foreach (var entry in invertedIndex)
            {
                string postingList = string.Join("-", entry.Value.PostingList.Distinct());
                Console.WriteLine($"Keyword: {entry.Key}, Frequency: {entry.Value.Frequency}, Posting List: {postingList}");
            }

            using (StreamWriter writer = new StreamWriter(@"E:\WIR CS-479\Assignment 5\Lucene Search\Index\InvertedIndex.csv"))
            {
                writer.WriteLine("Keyword,Frequency,PostingList");
                foreach (var entry in invertedIndex)
                {
                    if (entry.Value.Frequency > 0)
                    {
                        string postingList = string.Join("-", entry.Value.PostingList.Distinct());
                        writer.WriteLine($"{entry.Key},{entry.Value.Frequency},{postingList}");
                    }
                }
            }
        }
        static List<string> ExtractLinks(string line)
        {
            return line.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .Where(word => Uri.IsWellFormedUriString(word, UriKind.Absolute))
                       .ToList();
        }

        static void PerformLinkAnalysis()
        {
            string indexFileLocation = @"E:\WIR CS-479\Assignment 5\Lucene Search\Index";
            Directory dir = FSDirectory.Open(new DirectoryInfo(indexFileLocation));
            IndexReader reader = IndexReader.Open(dir, true);

            Dictionary<string, List<string>> linkGraph = new Dictionary<string, List<string>>();

            string metaFilePath = @"E:\WIR CS-479\Assignment 5\Lucene Search\Index\meta.txt";
            string[] metaLines = File.ReadAllLines(metaFilePath);

            string docID = null;
            List<string> outgoingLinks = new List<string>();

            foreach (string line in metaLines)
            {
                if (line.StartsWith("Web Page ID:"))
                {
                    docID = ExtractValue(line);
                }
                else if (line.StartsWith("Outgoing Links:"))
                {
                    outgoingLinks = ExtractLinks(ExtractValue(line));
                }

                if (!string.IsNullOrEmpty(docID))
                {
                    linkGraph[docID] = outgoingLinks;
                    docID = null;
                    outgoingLinks = new List<string>();
                }
            }

            // Debug: Print the link graph
            foreach (var node in linkGraph)
            {
                Console.WriteLine($"Doc ID: {node.Key}, Outgoing Links: {string.Join(", ", node.Value)}");
            }

            HITSScores = ComputeHITS(linkGraph);

            foreach (var entry in HITSScores.OrderByDescending(e => e.Value.AuthorityScore))
            {
                Console.WriteLine($"Doc ID: {entry.Key}, Authority Score: {entry.Value.AuthorityScore}, Hub Score: {entry.Value.HubScore}");
            }

            using (StreamWriter writer = new StreamWriter(@"E:\WIR CS-479\Assignment 5\Lucene Search\Index\HITSScores.csv"))
            {
                writer.WriteLine("DocID,AuthorityScore,HubScore");
                foreach (var entry in HITSScores.OrderByDescending(e => e.Value.AuthorityScore))
                {
                    writer.WriteLine($"{entry.Key},{entry.Value.AuthorityScore},{entry.Value.HubScore}");
                }
            }
        }

        static Dictionary<string, (double HubScore, double AuthorityScore)> ComputeHITS(Dictionary<string, List<string>> linkGraph, int iterations = 100)
        {
            Dictionary<string, double> hubScores = linkGraph.ToDictionary(kvp => kvp.Key, kvp => 1.0);
            Dictionary<string, double> authorityScores = linkGraph.ToDictionary(kvp => kvp.Key, kvp => 1.0);

            for (int iter = 0; iter < iterations; iter++)
            {
                var newAuthorityScores = new Dictionary<string, double>();
                var newHubScores = new Dictionary<string, double>();

                foreach (var node in linkGraph.Keys)
                {
                    newAuthorityScores[node] = linkGraph
                        .Where(kvp => kvp.Value.Contains(node))
                        .Sum(kvp => hubScores[kvp.Key]);

                    newHubScores[node] = linkGraph[node]
                        .Sum(outgoing => authorityScores.ContainsKey(outgoing) ? authorityScores[outgoing] : 0.0);
                }

                // Normalize the scores
                double norm = newAuthorityScores.Values.Sum();
                if (norm > 0)
                {
                    foreach (var key in newAuthorityScores.Keys.ToList())
                    {
                        newAuthorityScores[key] /= norm;
                    }
                }

                norm = newHubScores.Values.Sum();
                if (norm > 0)
                {
                    foreach (var key in newHubScores.Keys.ToList())
                    {
                        newHubScores[key] /= norm;
                    }
                }

                authorityScores = newAuthorityScores;
                hubScores = newHubScores;
            }

            return hubScores.ToDictionary(kvp => kvp.Key, kvp => (HubScore: kvp.Value, AuthorityScore: authorityScores[kvp.Key]));
        }


        static void searchindex(string queryText)
        {
            Directory dir = FSDirectory.Open(new DirectoryInfo(@"E:\WIR CS-479\Assignment 5\Lucene Search\Index"));

            IndexSearcher searcher = new IndexSearcher(dir, true);
            QueryParser parser = new QueryParser(Lucene.Net.Util.Version.LUCENE_30, "content", new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30));
            Query query = parser.Parse(queryText);

            TopDocs hits = searcher.Search(query, 10);

            Console.WriteLine($"Found {hits.TotalHits} document(s) that matched query '{queryText}':");

            List<(Document Doc, float Score, double AuthorityScore, double HubScore)> results = new List<(Document, float, double, double)>();

            foreach (ScoreDoc scoreDoc in hits.ScoreDocs)
            {
                Document doc = searcher.Doc(scoreDoc.Doc);
                string docID = doc.Get("docID");
                double authorityScore = HITSScores.ContainsKey(docID) ? HITSScores[docID].AuthorityScore : 0.0;
                double hubScore = HITSScores.ContainsKey(docID) ? HITSScores[docID].HubScore : 0.0;
                results.Add((doc, scoreDoc.Score, authorityScore, hubScore));
            }

            var sortedResults = results.OrderByDescending(r => r.Score * 0.4 + r.AuthorityScore * 0.3 + r.HubScore * 0.3).ToList();

            foreach (var result in sortedResults)
            {
                Document doc = result.Doc;
                string docID = doc.Get("docID");
                string contentValue = doc.Get("content");
                string url = doc.Get("url");
                Console.WriteLine($"Doc ID: {docID}");
                Console.WriteLine($"Content: {contentValue}");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"Combined Score: {result.Score * 0.4 + result.AuthorityScore * 0.3 + result.HubScore * 0.3}");
                Console.WriteLine();
            }
            Console.ReadLine();
        }
    }
}