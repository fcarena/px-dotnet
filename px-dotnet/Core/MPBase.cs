using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace MercadoPago
{
    public abstract class MPBase
    {
        public static bool WITHOUT_CACHE = false;
        public static bool WITH_CACHE = true;

        public static string IdempotencyKey { get; set; }

        protected MPAPIResponse LastApiResponse { get; private set; }
        protected JObject LastKnownJson { get; private set; }

        #region Errors Definitions
        public static string DataTypeError = "Error on property #PROPERTY. The value you are trying to assign has not the correct type. ";
        public static string RangeError = "Error on property #PROPERTY. The value you are trying to assign is not in the specified range. ";
        public static string RequiredError = "Error on property #PROPERTY. There is no value for this required property. ";
        public static string RegularExpressionError = "Error on property #PROPERTY. The specified value is not valid. RegExp: #REGEXPR . ";
        #endregion

        #region Core Methods
        /// <summary>
        /// Checks if current resource needs idempotency key and set IdempotencyKey if positive.
        /// </summary>
        /// <param name="classType">ClassType.</param>        
        public static void AdmitIdempotencyKey(Type classType)
        {
            var attribute = classType.GetCustomAttributes(true);

            foreach (Attribute attr in attribute)
            {
                if (attr.GetType() == typeof(Idempotent))
                {
                    IdempotencyKey = attr.GetType().GUID.ToString();
                }
            }
        }

        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod(string methodName, bool useCache)
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            return ProcessMethod<MPBase>(classType, null, methodName, null, useCache);
        }


        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method, parameters and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="param">Parameters to use in the retrieve process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod(Type type, string methodName, string param, bool useCache)
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param0", param);

            return ProcessMethod<MPBase>(classType, null, methodName, mapParams, useCache);
        }


        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method, parameters and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="param">Parameters to use in the retrieve process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod(string methodName, string param, bool useCache)
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param0", param);

            return ProcessMethod<MPBase>(classType, null, methodName, mapParams, useCache);
        }

        /// <summary>
        /// Retrieve a MPBase resource based on a specific method and configuration.       
        /// </summary>
        /// <typeparam name="T">Object derived from MPBase abstract class.</typeparam>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="useCache">Cache configuration</param>
        /// <returns>MPBase resource.</returns>
        public MPBase ProcessMethod<T>(string methodName, bool useCache) where T : MPBase
        {
            Dictionary<string, string> mapParams = null;
            T resource = ProcessMethod<T>(this.GetType(), (T)this, methodName, mapParams, useCache);

            return (T)this;
        }

        /// <summary>
        /// Core implementation of processMethod. Retrieves a generic type. 
        /// </summary>
        /// <typeparam name="T">Generic type that will return.</typeparam>
        /// <param name="clazz">Type of Class we are using.</param>
        /// <param name="resource">Resource we will use and return in the implementation.</param>
        /// <param name="methodName">The name of the method  we are trying to call.</param>
        /// <param name="parameters">Parameters to use in the process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>Generic type object, containing information about retrieval process.</returns>
        protected static T ProcessMethod<T>(Type clazz, T resource, string methodName, Dictionary<string, string> parameters, bool useCache) where T : MPBase
        {
            if (resource == null)
            {
                try
                {
                    resource = (T)Activator.CreateInstance(clazz);
                }
                catch (Exception ex)
                {
                    throw new MPException(ex.Message);
                }
            }

            var clazzMethod = GetAnnotatedMethod(clazz, methodName);
            var restData = GetRestInformation(clazzMethod);

            HttpMethod httpMethod = (HttpMethod)restData["method"];
            string path = ParsePath(restData["path"].ToString(), parameters, resource);
            PayloadType payloadType = (PayloadType)restData["payloadType"];
            JObject payload = GeneratePayload(httpMethod, resource);
            int requestTimeout = (int)restData["requestTimeout"];
            int retries = (int)restData["retries"];

            WebHeaderCollection colHeaders = new WebHeaderCollection();

            MPAPIResponse response = CallAPI(httpMethod, path, payloadType, payload, colHeaders, useCache, requestTimeout, retries);

            if (response.StatusCode >= 200 &&
                    response.StatusCode < 300)
            {
                if (httpMethod != HttpMethod.DELETE)
                {
                    resource = (T)FillResourceWithResponseData(resource, response);
                }
                else
                {
                    //resource = cleanResource(resource);
                }
            }

            resource.LastApiResponse = response;

            return resource;
        }

        /// <summary>
        /// Transforms all attributes members of the instance in a JSON String. Only for POST and PUT methods.
        /// POST gets the full object in a JSON object.
        /// PUT gets only the differences with the last known state of the object.
        /// </summary>
        /// <returns>a JSON Object with the attributes members of the instance. Null for GET and DELETE methods</returns>
        public static JObject GeneratePayload<T>(HttpMethod httpMethod, T resource) where T : MPBase
        {
            JObject payload = null;
            if (new string[] { "POST", "PUT" }.Contains(httpMethod.ToString()))
            {
                payload = MPCoreUtils.GetJsonFromResource(resource);
            }

            return payload;
        }

        /// <summary>
        /// Fills all the attributes members of the Resource obj.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="resource"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        protected static MPBase FillResourceWithResponseData<T>(T resource, MPAPIResponse response) where T : MPBase
        {
            if (response.JsonObjectResponse != null &&
                    response.JsonObjectResponse is JObject)
            {
                JObject jsonObject = (JObject)response.JsonObjectResponse;
                T resourceObject = (T)MPCoreUtils.GetResourceFromJson<T>(resource.GetType(), jsonObject);
                resource = (T)FillResource(resourceObject, resource);
                resource.LastKnownJson = MPCoreUtils.GetJsonFromResource(resource);
            }

            return resource;
        }

        /// <summary>
        /// Fills all the attributes members of the Resource obj.
        /// </summary>
        /// <returns>MPBase object with response attributes.</returns>
        private static MPBase FillResource<T>(T sourceResource, T destinationResource) where T : MPBase
        {
            FieldInfo[] declaredFields = destinationResource.GetType().GetFields();
            foreach (FieldInfo field in declaredFields)
            {
                try
                {
                    FieldInfo originField = sourceResource.GetType().GetField(field.Name, BindingFlags.Instance | BindingFlags.NonPublic);
                    field.SetValue(destinationResource, originField.GetValue(sourceResource));

                }
                catch (Exception ex)
                {
                    throw new MPException(ex.Message);
                }
            }

            return destinationResource;
        }

        /// <summary>
        /// Calls the api and returns an MPApiResponse.
        /// </summary>
        /// <returns>A MPAPIResponse object with the results.</returns>
        public static MPAPIResponse CallAPI(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            WebHeaderCollection colHeaders,
            Boolean useCache,
            int requestTimeout,
            int retries)
        {
            string cacheKey = httpMethod.ToString() + "_" + path;            
            MPAPIResponse response = null;
          
            if (useCache)
            {
                response = MPCache.GetFromCache(cacheKey);                
                
                if(response != null)
                {
                    response.isFromCache = true;
                }
            }

            if(response == null)
            {
                response = new MPRESTClient().ExecuteRequest(
                    httpMethod,
                    path,
                    payloadType,
                    payload,
                    colHeaders,
                    requestTimeout,
                    retries);

                if (useCache)
                {
                    MPCache.AddToCache(cacheKey, response);                    
                }
                else
                {
                    MPCache.RemoveFromCache(cacheKey);
                }
            }

            return response;
        }

        /// <summary>
        /// Get the method we are searching on a specific class type.
        /// </summary>
        /// <param name="clazz">Type of class we are using.</param>
        /// <param name="methodName">Method we are trying to call.</param>
        /// <returns>Info about the method we are searching.</returns>
        private static MethodInfo GetAnnotatedMethod(Type clazz, String methodName)
        {
            foreach (MethodInfo method in clazz.GetMethods())
            {
                if (method.Name == methodName && method.GetCustomAttributes(false).Length > 0)
                {
                    return method;
                }
            }

            throw new MPException("No annotated method found");
        }

        /// <summary>
        /// Get rest information based on method info.
        /// </summary>
        /// <param name="element">MethodInfo containing information about the method we are trying to call.</param>
        /// <returns>Dictionary with custom information.</returns>
        private static Dictionary<string, object> GetRestInformation(MethodInfo element)
        {
            if (element.GetCustomAttributes(false).Length == 0)
            {
                throw new MPException("No rest method found");
            }

            Dictionary<string, object> hashAnnotation = new Dictionary<string, object>();
            foreach (Attribute annotation in element.GetCustomAttributes(false))
            {
                if (annotation is BaseEndpoint)
                {
                    if (string.IsNullOrEmpty(((BaseEndpoint)annotation).Path))
                    {
                        throw new MPException(string.Format("Path not found for {0} method", ((BaseEndpoint)annotation).HttpMethod.ToString()));
                    }
                }
                else
                {
                    throw new MPException("Not supported method found");
                }

                hashAnnotation = new Dictionary<string, object>();
                hashAnnotation.Add("method", ((BaseEndpoint)annotation).HttpMethod);
                hashAnnotation.Add("path", ((BaseEndpoint)annotation).Path);
                hashAnnotation.Add("instance", element.ReturnType.Name);
                hashAnnotation.Add("Header", element.ReturnType.GUID);
                hashAnnotation.Add("payloadType", ((BaseEndpoint)annotation).PayloadType);
                hashAnnotation.Add("requestTimeout", ((BaseEndpoint)annotation).RequestTimeout);
                hashAnnotation.Add("retries", ((BaseEndpoint)annotation).Retries);
            }

            return hashAnnotation;
        }
        #endregion

        #region Tracking Methods
        /// <summary>
        /// Get Type of a required class by it's string name.
        /// </summary>
        /// <param name="typeName">Class name.</param>
        /// <returns>Type of required class.</returns>
        public static Type GetTypeFromStack()
        {
            MethodBase methodBase = new StackTrace().GetFrame(2).GetMethod();
            var className = methodBase.DeclaringType.FullName;
            var type = Type.GetType(className);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(className);
                if (type != null)
                    return type;
            }
            return null;
        }
        #endregion

        #region Core Utilities


        /// <summary>
        /// Generates a final Path based on parameters in Dictionary and resource properties.
        /// </summary>
        /// <typeparam name="T">MPBase resource.</typeparam>
        /// <param name="path">Path we are processing.</param>
        /// <param name="mapParams">Collection of parameters that we will use to process the final path.</param>
        /// <param name="resource">Resource containing parameters values to include in the final path.</param>
        /// <returns>Processed path to call the API.</returns>
        public static string ParsePath<T>(string path, Dictionary<string, string> mapParams, T resource) where T : MPBase
        {
            StringBuilder result = new StringBuilder();
            if (path.Contains(':'))
            {
                int paramIterator = 0;
                while (path.Contains(':'))
                {
                    result.Append(path.Substring(0, path.IndexOf(':')));
                    path = path.Substring(path.IndexOf(':') + 1);
                    string param = path;
                    if (path.Contains('/'))
                    {
                        param = path.Substring(0, path.IndexOf('/'));
                    }

                    string value = string.Empty;
                    if (paramIterator <= 2 &&
                            mapParams != null &&
                            !string.IsNullOrEmpty(mapParams[string.Format("param{0}", paramIterator.ToString())]))
                    {
                        value = mapParams[string.Format("param{0}", paramIterator.ToString())];
                    }
                    else if (mapParams != null &&
                         !string.IsNullOrEmpty(mapParams[param]))
                    {
                        value = mapParams[param];
                    }
                    else
                    {
                        if (resource != null)
                        {
                            JObject json = JObject.FromObject(resource);
                            
                            //Add control to verify JSON's case properties. Must be removed after testing approved status.
                            var jValueUC = json.GetValue(param.ToUpper());
                            var jValueLC = json.GetValue(param);

                            if (jValueUC != null)
                            {
                                value = jValueUC.ToString();
                            }
                            else
                            {
                                if (jValueLC != null)
                                {
                                    value = jValueLC.ToString();
                                }                                
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new MPException("No argument supplied/found for method path");
                    }

                    result.Append(value);
                    if (path.Contains('/'))
                    {
                        path = path.Substring(path.IndexOf('/'));
                    }
                    else
                    {
                        path = string.Empty;
                    }
                }

                if (!string.IsNullOrEmpty(path))
                {
                    result.Append(path);
                }
            }
            else
            {
                result.Append(path);
            }

            result.Insert(0, MPConf.BaseUrl);

            string accessToken = null;            

            if (!string.IsNullOrEmpty(GetUserToken(resource.GetType())))
            {
                accessToken = GetUserToken(resource.GetType());
            }
            else
            {
                accessToken = MPConf.GetAccessToken();
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                result.Append(string.Format("{0}{1}", "?access_token=", accessToken));            
            }            

            return result.ToString();
        }

        /// <summary>
        /// Gets the user custom token.
        /// </summary>
        /// <param name="value">Class type to retrieve the custom UserToken Attribute.</param>
        public static string GetUserToken(Type classType)
        {            
            UserToken userTokenAttribute = null;
            var userToken = "";
            userTokenAttribute = ((UserToken)Attribute.GetCustomAttribute(classType, typeof(UserToken)));

            if (userTokenAttribute != null)
            {
                userToken = userTokenAttribute.GetUserToken();
            }            

            return userToken;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public JObject GetJsonSource()
        {
            return LastApiResponse != null ? LastApiResponse.JsonObjectResponse : null;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Vaidates an object.
        /// </summary>
        /// <returns>True, if validations are correct. False otherwise.</returns>
        public static bool Validate(object o)
        {
            Type type = o.GetType();
            PropertyInfo[] properties = type.GetProperties();
            Type attrType = typeof(ValidationAttribute);
            ValidationResult result = new ValidationResult();
            string FinalMessageError = "There are errors in the object you're trying to create. Review them to continue: ";

            foreach (var propertyInfo in properties)
            {
                object[] customAttributes = propertyInfo.GetCustomAttributes(attrType, inherit: true);

                foreach (var customAttribute in customAttributes)
                {
                    var validationAttribute = (ValidationAttribute)customAttribute;

                    bool isValid = validationAttribute.IsValid(propertyInfo.GetValue(o, BindingFlags.GetProperty, null, null, null));

                    if (!isValid)
                    {
                        switch (validationAttribute.GetType().Name)
                        {
                            case "RangeAttribute":
                                {
                                    result.Errors.Add(new ValidationError() { Message = RangeError.Replace("#PROPERTY", propertyInfo.Name) });
                                }
                                break;
                            case "RequiredAttribute":
                                {
                                    result.Errors.Add(new ValidationError() { Message = RequiredError.Replace("#PROPERTY", propertyInfo.Name) });
                                }
                                break;
                            case "RegularExpressionAttribute":
                                {
                                    result.Errors.Add(new ValidationError() { Message = RegularExpressionError.Replace("#PROPERTY", propertyInfo.Name).Replace("#REGEXPR", ((RegularExpressionAttribute)customAttribute).Pattern) });
                                }
                                break;
                            case "DataTypeAttribute":
                                {
                                    result.Errors.Add(new ValidationError() { Message = DataTypeError.Replace("#PROPERTY", propertyInfo.Name) });
                                }
                                break;
                        }
                    }
                }
            }

            if (result.Errors.Count() != 0)
            {
                foreach (ValidationError error in result.Errors)
                {
                    FinalMessageError += error.Message;
                }

                throw new Exception(FinalMessageError);
            }

            return true;
        }

        #endregion

        #region Testing helpers

        /// <summary>
        ///Class that represents the validation results. 
        /// </summary>
        public partial class ValidationResult
        {
            public List<ValidationError> Errors = new List<ValidationError>();
        }

        /// <summary>
        /// Class that represents the Error contained in the ValidationResult class
        /// </summary>
        public partial class ValidationError
        {
            public int Code { get; set; }
            public string Message { get; set; }
        }

        #endregion
    }
}
