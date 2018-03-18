//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Threading.Tasks;
//using Microsoft.Azure.Documents;
//using Microsoft.Azure.Documents.Client;
//using Microsoft.Azure.Documents.Linq;
//using System.Collections.ObjectModel;

//namespace WeatherFunctions
//{
//    class DocumentDBRepository<T> where T : class
//    {
//        private static readonly string DatabaseId = "nww3";
//        private static readonly string CollectionId = "data";
//        private static DocumentClient client;

//        public static async Task<T> GetItemAsync(string id, string category)
//        {
//            try
//            {
//                Document document =
//                    await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id),
//                        new RequestOptions() { PartitionKey = new PartitionKey(category) });
//                return (T)(dynamic)document;
//            }
//            catch (DocumentClientException e)
//            {
//                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
//                {
//                    return null;
//                }
//                else
//                {
//                    throw;
//                }
//            }
//        }

//        public static async Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate)
//        {
//            IDocumentQuery<T> query = client.CreateDocumentQuery<T>(
//                UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId),
//                new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true })
//                .Where(predicate)
//                .AsDocumentQuery();

//            List<T> results = new List<T>();
//            while (query.HasMoreResults)
//            {
//                results.AddRange(await query.ExecuteNextAsync<T>());
//            }

//            return results;
//        }

//        public static async Task<Document> CreateItemAsync(T item)
//        {
//            return await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId), item);
//        }

//        public static async Task<Document> UpdateItemAsync(string id, T item)
//        {
//            return await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id), item);
//        }

//        public static async Task DeleteItemAsync(string id, string category)
//        {
//            await client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id), new RequestOptions() { PartitionKey = new PartitionKey(category) });
//        }

//        public static void Initialize()
//        {
//            client = new DocumentClient(new Uri("https://spmstestcosmosdb.documents.azure.com:443/"), "R7USDLOv2cU1Fhknx3aNA3lH47NJbu65enf0Hi4Of8rgRHL5BMXTMtkvyP3FzuGAPR5yiNSgLSk2sNhMEOcNbA==");
//            CreateDatabaseIfNotExistsAsync().Wait();
//            CreateCollectionIfNotExistsAsync().Wait();
//        }

//        private static async Task CreateDatabaseIfNotExistsAsync()
//        {
//            try
//            {
//                await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(DatabaseId));
//            }
//            catch (DocumentClientException e)
//            {
//                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
//                {
//                    await client.CreateDatabaseAsync(new Database { Id = DatabaseId });
//                }
//                else
//                {
//                    throw;
//                }
//            }
//        }

//        private static async Task CreateCollectionIfNotExistsAsync()
//        {
//            try
//            {
//                await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId));
//            }
//            catch (DocumentClientException e)
//            {
//                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
//                {
//                    await client.CreateDocumentCollectionAsync(
//                        UriFactory.CreateDatabaseUri(DatabaseId),
//                        new DocumentCollection
//                        {
//                            Id = CollectionId,
//                            PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() { "/UTC" } }
//                        },
//                        new RequestOptions { OfferThroughput = 1000 });
//                }
//                else
//                {
//                    throw;
//                }
//            }
//        }
//    }
//}