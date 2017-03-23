﻿using MercadoPago;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace MercadoPagoSDK.Test
{
    [TestFixture()]
    public class MPRESTClientTest : MPRESTClient
    {

        [Test()]
        public void ExecuteRequest_GetAndDeleteNustNotHavePayload()
        {
            MPRESTClient client = new MPRESTClient();
            try
            {
                MPAPIResponse response = client.ExecuteRequest(HttpMethod.GET, "https://httpbin.org/get", PayloadType.X_WWW_FORM_URLENCODED, new JObject(), null);
            }
            catch (MPRESTException ex)
            {
                Assert.AreEqual("Payload not supported for this method.", ex.Message);
            }

            try
            {
                MPAPIResponse response = client.ExecuteRequest(HttpMethod.DELETE, "https://httpbin.org/delete", PayloadType.X_WWW_FORM_URLENCODED, new JObject(), null);
            }
            catch (MPRESTException ex)
            {
                Assert.AreEqual("Payload not supported for this method.", ex.Message);
            }
        }

        [Test()]
        public void ExecuteRequest_PostAndPutMustHavePayload()
        {
            MPRESTClient client = new MPRESTClient();
            try
            {
                MPAPIResponse response = client.ExecuteRequest(HttpMethod.POST, "https://httpbin.org/post", PayloadType.X_WWW_FORM_URLENCODED, null, null);
            }
            catch (MPRESTException ex)
            {
                Assert.AreEqual("Must include payload for this method.", ex.Message);
            }

            try
            {
                MPAPIResponse response = client.ExecuteRequest(HttpMethod.PUT, "https://httpbin.org/put", PayloadType.X_WWW_FORM_URLENCODED, null, null);
            }
            catch (MPRESTException ex)
            {
                Assert.AreEqual("Must include payload for this method.", ex.Message);
            }   
        }

        [Test()]
        public void ExecuteRequest_Get()
        {
            MPRESTClient client = new MPRESTClient();
            MPAPIResponse response = client.ExecuteRequest(HttpMethod.GET, "https://httpbin.org/get", PayloadType.X_WWW_FORM_URLENCODED, null, null);
            JObject jsonResponse = JObject.Parse(response.StringResponse.ToString());
            JProperty prop = jsonResponse.Properties().FirstOrDefault(p => p.Name.Contains("url"));
            string url = prop != null ? prop.Value.ToString() : string.Empty;

            Assert.AreEqual("https://httpbin.org/get", url);
        }

        [Test()]
        public void ExecuteRequest_Post()
        {
            MPRESTClient client = new MPRESTClient();

            var jsonObject = new JObject();
            jsonObject.Add("firstName", "Clark");
            jsonObject.Add("lastName", "Kent");
            jsonObject.Add("year", 2018);

            MPAPIResponse response = client.ExecuteRequest(HttpMethod.POST, "https://httpbin.org/post", PayloadType.X_WWW_FORM_URLENCODED, jsonObject, null);
            JObject jsonResponse = JObject.Parse(response.StringResponse.ToString());

            List<JToken> contentType = MPCoreUtils.FindTokens(jsonResponse, "Content-Type");
            Assert.AreEqual("application/x-www-form-urlencoded", contentType.First().ToString());
        }

        [Test()]
        public void ExecuteRequest_Post_Json()
        {
            MPRESTClient client = new MPRESTClient();

            var jsonObject = new JObject();
            jsonObject.Add("firstName", "Clark");
            jsonObject.Add("lastName", "Kent");
            jsonObject.Add("year", 2018);

            MPAPIResponse response = client.ExecuteRequest(HttpMethod.POST, "https://httpbin.org/post", PayloadType.JSON, jsonObject, null);
            JObject jsonResponse = JObject.Parse(response.StringResponse.ToString());

            List<JToken> lastName = MPCoreUtils.FindTokens(jsonResponse, "lastName");
            Assert.AreEqual("Kent", lastName.First().ToString());

            List<JToken> year = MPCoreUtils.FindTokens(jsonResponse, "year");
            Assert.AreEqual("2018", year.First().ToString());
        }

        [Test()]
        public void ExecuteRequest_Post_MPAPIRequestResponseParser()
        {
            MPRESTClient client = new MPRESTClient();

            var jsonObject = new JObject();
            jsonObject.Add("firstName", "Comander");
            jsonObject.Add("lastName", "Shepard");
            jsonObject.Add("year", 2126);

            MPAPIResponse response = client.ExecuteRequest(HttpMethod.POST, "https://httpbin.org/post", PayloadType.JSON, jsonObject, null);

            Assert.AreEqual(200, response.StatusCode);

            JObject jsonResponse = response.JsonObjectResponse;
            List<JToken> lastName = MPCoreUtils.FindTokens(jsonResponse, "lastName");
            Assert.AreEqual("Shepard", lastName.First().ToString());

            List<JToken> year = MPCoreUtils.FindTokens(jsonResponse, "year");
            Assert.AreEqual("2126", year.First().ToString());
        }
    }
}
