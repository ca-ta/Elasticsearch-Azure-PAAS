﻿using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using ElasticsearchWorker.IndexResponse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ElasticsearchWorker.ClusterApi
{
    public class ClusterClient
    {
        protected ElasticsearchClient _clusterClient;

        public ClusterClient()
        {
            var settings = new ConnectionConfiguration();
        
            settings.ThrowOnElasticsearchServerExceptions(false);
            var timeout = Convert.ToInt32(TimeSpan.FromMinutes(30).TotalMilliseconds);
            settings.SetConnectTimeout(timeout);
            settings.SetTimeout(timeout);
            settings.ExposeRawResponse();

            _clusterClient = new ElasticsearchClient(settings);
            
        }
        public ResultWrapper<bool> IsMaster(string nodeName)
        {
            var response = _clusterClient.ClusterState<MasterNode>("master_node,nodes");

            var result = Request<bool,MasterNode>(response,(r) =>  r.Success && r.Response.nodes[r.Response.master_node].name == nodeName);

            return result;
        }

        public ResultWrapper<bool> IsClusterHealthy()
        {
            try
            {
                var response = _clusterClient.ClusterHealth<ClusterHealth>();
                var result = Request<bool, ClusterHealth>(response, (r) => response.Success && response.Response.status == "green");
                return result;
            }
            catch(WebException e)
            {
                return new ResultWrapper<bool>()
                {
                    ErrorMessage = e.Message,
                    StatusCode = (int)e.Status,
                    IsError = true
                    
                };
            }
        }

        public ResultWrapper<T> BulkAddOrUpdate<T>(string index, string type, IEnumerable<T> body, Func<T, string> getId)
        {
            var dynamicBody = new List<object>();

            foreach (var item in body)
            {
                var id = getId(item);
                dynamicBody.Add(new { 
                    index = new { 
                        _id = id 
                    } 
                });
                dynamicBody.Add(item);
            }



            var response = _clusterClient.BulkPut<T>(index, type, dynamicBody.ToArray());

            return new ResultWrapper<T>
            {
                ErrorMessage = response.ServerError == null ? null : response.ServerError.Error,
                IsError =  response.HttpStatusCode != (int)HttpStatusCode.OK,
                StatusCode = response.HttpStatusCode
            };
        } 
        public ResultWrapper<T> AddOrUpdate<T>(string index, string type, string id,T body)
        {
            var response = _clusterClient.Index<T>(index, type,id ,body);
         
            var result = Request<T, T>(response, (r) => response.Response);
            return result;
        }
        public ResultWrapper<T> GetItem<T>(string index, string type, string id)
        {
            var response = _clusterClient.GetSource<T>(index, type, id);
            
            var result = Request<T, T>(response, (r) => response.Response);
            return result;
        }

        public ResultWrapper<T> Request<T, K>(ElasticsearchResponse<K> response, Func<ElasticsearchResponse<K>, T> setResult)
        {
            var result = new ResultWrapper<T>
            {
                IsError = response.HttpStatusCode != (int)HttpStatusCode.OK,
                
                StatusCode = response.HttpStatusCode,
                Result = setResult(response)
            };

            if (response.ServerError != null)
            {
                result.ErrorMessage = response.ServerError.Error;
            }
            return result;
        }
    }
}
