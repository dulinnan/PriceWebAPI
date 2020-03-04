using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PriceWebAPI
{
    public static class PriceCheck
    {
        [FunctionName("PriceCheck")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            //string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string verifyCode = data.verifyCode;
            string result = GetValueFromMT.GetValue();
            return AuthorizeKey(verifyCode) != false
                ? (ActionResult)new OkObjectResult(result)
                : new UnauthorizedResult();
        }
        /// <summary>
        /// This method is running for get the SHA256
        /// </summary>
        /// <param name="message">Encoding message</param>
        /// <param name="key">Encoding secure</param>
        /// <returns>sha256</returns>
        static string ComputeHMACSHA256(string message, string key)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = encoding.GetBytes(key);
            HMACSHA256 hmacsha256 = new HMACSHA256(keyByte);
            byte[] messageBytes = encoding.GetBytes(message);
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            string sha256 = ByteToString(hashmessage);
            return sha256;
        }
        public static bool AuthorizeKey(string verifyCode)
        {
            string key = GetEnvironmentVariable("Key");
            string message = GetEnvironmentVariable("Message");
            bool isAuthorized = false;
            return verifyCode.ToUpper() == ComputeHMACSHA256(message, key).ToUpper() ? isAuthorized = true : isAuthorized;
        }
        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public static string ByteToString(byte[] buff)
        {
            string sbinary = "";
            for (int i = 0; i < buff.Length; i++)
            {
                sbinary += buff[i].ToString("X2"); // hex format
            }
            return (sbinary);
        }
    }
    public static class GetValueFromMT
    {
        public static dynamic GetValue()
        {
            string sXml = GetWebContext();
            string json = GetJson(sXml);
            return json;
        }
        /// <summary>
        /// This function is running for getting the price from Fxcm.
        /// </summary>
        /// <returns>string XML</returns>
        public static string GetWebContext()
        {
            string pageContext = "";
            try
            {
                WebClient MyWebClient = new WebClient();
                Byte[] pageData = MyWebClient.DownloadData("https://rates.fxcm.com/RatesXML2");
                pageContext = Encoding.Default.GetString(pageData);

            }
            catch (Exception ex)
            {
                pageContext = ex.Message.ToString();
            }
            return pageContext;
        }
        /// <summary>
        /// This function is running for convert the XML to JSON
        /// </summary>
        /// <param name="webContext"></param>
        /// <returns>string JSON</returns>
        public static string GetJson(string webContext)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(webContext);
            var jObject = Newtonsoft.Json.JsonConvert.SerializeXmlNode(doc);
            JObject jobj = JObject.Parse(jObject);
            string json = Fun(jobj);
            if (!String.IsNullOrEmpty(json)) json = "{" + json + "}";
            return json;
        }
        /// <summary>
        /// This method is running for updating the JSON format through track the JSON string
        /// </summary>
        /// <param name="obj">JSON object</param>
        /// <returns>JSON as we expected</returns>
        public static string Fun(JObject obj)
        {
            string result = null;

            foreach (var item in obj)
            {
                if (typeof(JObject) == item.Value.GetType())
                {
                    JObject child = (JObject)item.Value;
                    string tmp = Fun(child);
                    result += tmp;
                    string target = "@version=1.0,@encoding=UTF-8,";
                    if (result == target)
                    {
                        result = "";
                    }
                }
                else if (typeof(JArray) == item.Value.GetType())
                {
                    JArray _jarray = (JArray)item.Value;
                    foreach (var jitem in _jarray)
                    {
                        JObject jchild = (JObject)jitem;
                        string value = jchild.First.ToString();
                        value = value.Substring(9);
                        string tmp = value + ":" + jchild + ",";
                        result += tmp;

                    }
                }
            }

            return result;
        }
    }
}
