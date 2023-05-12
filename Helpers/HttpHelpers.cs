using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Youtube2Spotify.Helpers
{
    public static class HttpHelpers
    {
        public static HttpWebResponse MakePostRequest(string url, string postData, string token)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            var data = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = data.Length;
            request.Method = "POST";
            request.ContentType = "application/json;charset=utf-8";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", "Bearer " + token);
            request.Timeout = 300000;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            return (HttpWebResponse)request.GetResponse();
        }

        public static object MakeYoutubeGetCalls(string url)
        {
            string html = string.Empty;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }
            // you better know what this is beforehand
            dynamic json = JsonConvert.DeserializeObject(html);
            return json;
        }
    }
}
