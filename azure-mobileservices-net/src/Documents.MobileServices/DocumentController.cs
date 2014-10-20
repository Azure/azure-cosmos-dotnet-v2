using Documents.MobileServices;
using Microsoft.WindowsAzure.Mobile.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.Azure.Documents.MobileServices
{
    public abstract class DocumentController<TDocument> : ApiController where TDocument : Resource
    {
        public ApiServices Services { get; set; }

        private DocumentEntityDomainManager<TDocument> domainManager;
        

        protected DocumentEntityDomainManager<TDocument> DomainManager
        {
            get
            {
                if (this.domainManager == null)
                {
                    throw new InvalidOperationException("Domain manager not set");
                }
                return this.domainManager;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                this.domainManager = value;
            }
        }
        
        protected DocumentController()
        {
        }


        protected virtual IQueryable<TDocument> Query()
        {
            IQueryable<TDocument> result = null;
            try
            {
                result = this.DomainManager.Query();
            }
            catch (HttpResponseException exception)
            {
                Services.Log.Error(exception, base.Request, LogCategories.Controllers);
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, base.Request, LogCategories.Controllers);
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }
            return result;
        }

        protected virtual SingleResult<TDocument> Lookup(string id)
        {

            try
            {
                return this.DomainManager.Lookup(id);
            }
            catch (HttpResponseException exception)
            {
                Services.Log.Error(exception, base.Request, LogCategories.TableControllers);
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, base.Request, LogCategories.TableControllers);
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }

        }

        protected async virtual Task<Document> InsertAsync(TDocument item)
        {
            if (item == null)
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }


            try
            {
                return await this.DomainManager.InsertAsync(item);

            }
            catch (HttpResponseException exception)
            {
                Services.Log.Error(exception, base.Request, LogCategories.TableControllers);
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, base.Request, LogCategories.TableControllers);

                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }

        }

        protected async virtual Task<TDocument> ReplaceAsync(string id, TDocument item)
        {
            if (item == null || !base.ModelState.IsValid)
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            TDocument result;

            try
            {
                var flag = await this.DomainManager.ReplaceAsync(id, item);

                if (!flag)
                {
                    throw new HttpResponseException(HttpStatusCode.NotFound);
                }

                result = item;
            }
            catch (HttpResponseException exception)
            {
                Services.Log.Error(exception, base.Request, LogCategories.TableControllers);
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, base.Request, LogCategories.TableControllers);
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }
            return result;
        }

        protected virtual async Task DeleteAsync(string id)
        {
            bool flag = false;
            try
            {
                flag = await this.DomainManager.DeleteAsync(id);

            }
            catch (HttpResponseException exception)
            {
                Services.Log.Error(exception, base.Request, LogCategories.TableControllers);
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, base.Request, LogCategories.TableControllers);

                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }
            if (!flag)
            {
                Services.Log.Warn("Resource not found", base.Request);
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }


        }

    }
}
