using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace OpenLibraryISBN
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var inputFilePath = "ISBN_Input_File.txt";
            var outputFilePath = "output.csv";

            var isbnList = ReadInputFile(inputFilePath);
            var bookInfoList = await GetBookInfoAsync(isbnList);
            WriteOutputFile(outputFilePath, bookInfoList);

            Console.WriteLine("CSV file generated successfully!");
        }

        static List<string> ReadInputFile(string filePath)
        {
            var isbnList = new List<string>();
            foreach (var line in File.ReadLines(filePath))
            {
                isbnList.AddRange(line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(isbn => isbn.Trim()));
            }
            return isbnList;
        }

        static async Task<List<BookInfo>> GetBookInfoAsync(List<string> isbnList)
        {
            var cache = new Dictionary<string, BookInfo>();
            var bookInfoList = new List<BookInfo>();

            foreach (var isbn in isbnList.Distinct())
            {
                BookInfo bookInfo;

                if (cache.ContainsKey(isbn))
                {
                    bookInfo = cache[isbn];
                    bookInfo.DataRetrievalType = DataRetrievalType.Cache;
                }
                else
                {
                    bookInfo = await FetchBookInfoFromApiAsync(isbn);
                    bookInfo.DataRetrievalType = DataRetrievalType.Server;
                    cache[isbn] = bookInfo;
                }

                bookInfoList.Add(bookInfo);
            }

            return bookInfoList;
        }

        static async Task<BookInfo> FetchBookInfoFromApiAsync(string isbn)
        {
            var client = new RestClient("https://openlibrary.org");
            var request = new RestRequest($"/api/books", Method.Get);
            request.AddParameter("bibkeys", $"ISBN:{isbn}");
            request.AddParameter("format", "json");
            request.AddParameter("jscmd", "data");

            var response = await client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var bookData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(response.Content);
                var bookInfo = new BookInfo
                {
                    ISBN = isbn,
                    Title = bookData.ContainsKey($"ISBN:{isbn}") ? bookData[$"ISBN:{isbn}"].title : "",
                    Subtitle = bookData.ContainsKey($"ISBN:{isbn}") ? bookData[$"ISBN:{isbn}"].subtitle : "",
                    Authors = bookData.ContainsKey($"ISBN:{isbn}") ? string.Join(", ", ((IEnumerable<dynamic>)bookData[$"ISBN:{isbn}"].authors).Select(a => (string)a.name)) : "",
                    NumberOfPages = bookData.ContainsKey($"ISBN:{isbn}") ? (int?)bookData[$"ISBN:{isbn}"].number_of_pages : null,
                    PublishDate = bookData.ContainsKey($"ISBN:{isbn}") ? (string)bookData[$"ISBN:{isbn}"].publish_date : ""
                };

                return bookInfo;
            }

            return new BookInfo { ISBN = isbn, Title = "Error retrieving data" };
        }

        static void WriteOutputFile(string filePath, List<BookInfo> bookInfoList)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("RowNumber;DataRetrievalType;ISBN;Title;Subtitle;Authors;NumberOfPages;PublishDate");

                for (int i = 0; i < bookInfoList.Count; i++)
                {
                    var bookInfo = bookInfoList[i];
                    bookInfo.RowNumber = i + 1;

                    var row = $"{bookInfo.RowNumber};{(int)bookInfo.DataRetrievalType};{bookInfo.ISBN};{bookInfo.Title};{bookInfo.Subtitle};{bookInfo.Authors};{bookInfo.NumberOfPages};{bookInfo.PublishDate}";
                    writer.WriteLine(row);
                }
            }
        }
    }

    public class BookInfo
    {
        public int RowNumber { get; set; }
        public DataRetrievalType DataRetrievalType { get; set; }
        public string? ISBN { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? Authors { get; set; }
        public int? NumberOfPages { get; set; }
        public string? PublishDate { get; set; } 
    }

    public enum DataRetrievalType
    {
        Server = 1,
        Cache = 2
    }
}