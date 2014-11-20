using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.WindowsAzure.Mobile.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Linq;
using System.Web.Http;
using System.Configuration;
using Microsoft.Azure.Documents.MobileServices;
using Microsoft.WindowsAzure.Mobile.Service.Tables;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Web.Http.OData;

namespace Documents.MobileServices
{
        public class DocumentDBDomainManager<TDocument> :IDomainManager<TDocument>  where TDocument : DocumentResource , new()
        {
            public HttpRequestMessage Request { get; set; }
            public ApiServices Services { get; set; }


            private string _collectionId;
            private string _databaseId;
            private Database _database;
            private DocumentCollection _collection;
            private DocumentClient _client;

            public DocumentDBDomainManager(string collectionId, string databaseId, HttpRequestMessage request, ApiServices services)
            {
                Request = request;
                Services = services;
                _collectionId = collectionId;
                _databaseId = databaseId;
            }

            public DocumentEntityDomainManager(HttpRequestMessage request, ApiServices services)
            {
                var attribute = typeof(TDocument).GetCustomAttributes(typeof(DocumentAttribute), true).FirstOrDefault() as DocumentAttribute;
                if (attribute == null)
                    throw new ArgumentException("the model class must be decorated with the Document attribute");
		
                Request = request;
                Services = services;
                _collectionId = attribute.CollectionId;
                _databaseId = attribute.DatabaseId;
            }
	
            public async Task<bool> DeleteAsync(string id)
            {
                try
                {
                    var doc = GetDocument(id);


                    if (doc == null)
                    {
                        return false;
                    }

                    await Client.DeleteDocumentAsync(doc.SelfLink);

                    return true;


                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }
            }

            public Task<TDocument> InsertAsync(TDocument data)
            {
                try
                {
                    data.CreatedAt = DateTimeOffset.UtcNow;
                    data.UpdatedAt = DateTimeOffset.UtcNow;
                    return  Client.CreateDocumentAsync(Collection.SelfLink, data)
                                   .ContinueWith<TDocument>(t=> GetDocFromResponse(t));                

                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }
            }
            private TDocument GetDocFromResponse(Task<ResourceResponse<Document>> source)
            {
                if (source.IsFaulted)
                {
                    new InvalidOperationException("Parent task is faulted.",source.Exception);
                }

                return  GetDocEntity(source.Result.Resource);                
            }

            private TDocument GetDocEntity(Document source)
            {
                if (source == null)
                {
                    new ArgumentNullException("source");
                }
                
                return JsonConvert.DeserializeObject<TDocument>(JsonConvert.SerializeObject(source));
               
            }

            public SingleResult<TDocument> Lookup(string id)
            {
                var qry = this.Query().Where(d => d.Id == id)
                            .Select<TDocument, TDocument>(d => d);

                var result = qry.ToList<TDocument>();

                return SingleResult.Create<TDocument>(result.AsQueryable());
            }

            public Task<SingleResult<TDocument>> LookupAsync(string id)
            {
                try
                {
                    return Task<SingleResult<TDocument>>.Run(()=> Lookup(id));
                    
                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }
            }

            public IQueryable<TDocument> Query()
            {
                try
                {
                    var qry = Client
                            .CreateDocumentQuery<TDocument>(Collection.DocumentsLink)
                            .ToList()
                            .AsQueryable();

                    return qry;
                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }
            }

            public Task<IEnumerable<TDocument>> QueryAsync()
            {
                throw new NotImplementedException();
            }

            public Task<TDocument> UpdateAsync(string id, Delta<TDocument> patch)
            {
                if (id == null)
                {
                    throw new ArgumentNullException("id");
                }

                if (patch == null)
                {
                    throw new ArgumentNullException("patch");
                }


                var doc = this.GetDocument(id);
                if (doc  == null)
                {
                    Services.Log.Error(string.Format( "Resource with id {0} not found", id));
                    throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
                }


                TDocument current =  this.GetDocEntity(doc);

                patch.Patch(current);
                this.VerifyUpdatedKey(id, current);
                current.UpdatedAt = DateTimeOffset.UtcNow;

                try
                {
                    return Client.ReplaceDocumentAsync(current.SelfLink, current)
                        .ContinueWith<TDocument>(t => GetDocFromResponse(t));
                }

                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }
            }

            public  Task<TDocument> ReplaceAsync(string id, TDocument data)
            {

                if (id == null)
                {
                    throw new ArgumentNullException("id");
                }
                if (data == null)
                {
                    throw new ArgumentNullException("data");
                }

                this.VerifyUpdatedKey(id, data);
                data.CreatedAt = DateTimeOffset.UtcNow;

                try
                {
                    return Client.ReplaceDocumentAsync(data.SelfLink, data)
                        .ContinueWith<TDocument>(t => GetDocFromResponse(t));
                }

                catch (Exception ex)
                {
                    Services.Log.Error(ex);
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }

            }
        

            private Document GetDocument(string id)
            {
                return Client.CreateDocumentQuery<Document>(Collection.DocumentsLink)
                            .Where(d => d.Id == id)
                            .AsEnumerable()
                            .FirstOrDefault();
            }
            private void VerifyUpdatedKey(string id, TDocument data)
            {
                if (data == null || data.Id != id)
                {
                    HttpResponseMessage badKey = this.Request.CreateErrorResponse(HttpStatusCode.BadRequest, "The keys don't match");
                    throw new HttpResponseException(badKey);
                }
            }

            #region DocumentDBClient

            private DocumentClient Client
            {
                get
                {
                    if (_client == null)
                    {
                        string endpoint = ConfigurationManager.AppSettings["DocumentDB_Endpoint"];
                        string authKey = ConfigurationManager.AppSettings["DocumentDB_AuthKey"];
                        Uri endpointUri = new Uri(endpoint);
                        _client = new DocumentClient(endpointUri, authKey);
                    }

                    return _client;
                }
            }

            private DocumentCollection Collection
            {
                get
                {
                    if (_collection == null)
                    {
                        _collection = ReadOrCreateCollection(Database.SelfLink);
                    }

                    return _collection;
                }
            }

            private Database Database
            {
                get
                {
                    if (_database == null)
                    {
                        _database = ReadOrCreateDatabase();
                    }

                    return _database;
                }
            }

            private DocumentCollection ReadOrCreateCollection(string databaseLink)
            {
                var col = Client.CreateDocumentCollectionQuery(databaseLink)
                                  .Where(c => c.Id == _collectionId)
                                  .AsEnumerable()
                                  .FirstOrDefault();

                if (col == null)
                {
                    col = Client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection { Id = _collectionId }).Result;
                }

                return col;
            }

            private Database ReadOrCreateDatabase()
            {
                var db = Client.CreateDatabaseQuery()
                                .Where(d => d.Id == _databaseId)
                                .AsEnumerable()
                                .FirstOrDefault();

                if (db == null)
                {
                    db = Client.CreateDatabaseAsync(new Database { Id = _databaseId }).Result;
                }

                return db;
            }
            #endregion       

            public Task<IEnumerable<TDocument>> QueryAsync(System.Web.Http.OData.Query.ODataQueryOptions query)
            {
                throw new NotImplementedException();
            }
        }    
}
